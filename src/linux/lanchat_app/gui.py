"""Tk 会话列表与聊天流，网络事件在窗口主线程消费。"""

import json
from concurrent.futures import ThreadPoolExecutor
import os
import pkgutil
import queue
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, font as tkfont
import urllib.request
import uuid
import webbrowser

from . import APP_VERSION, GITHUB_REPO
from .history import BROADCAST, local_timestamp
from .sessions import SessionState
from .network import LanChatService, _usable_ipv4, format_size
from .overlay import WindowOverlay
from .ui_font import get_ui_font
from .storage import application_directory, log_message


class WeChatGui:
    """在 Tk 主线程管理会话与消息显示，网络回调通过队列投递，窗口关闭时释放服务。"""

    def __init__(self, service: LanChatService):
        self.service = service
        self.root = tk.Tk()
        self.root.title("LanChat")
        self.root.geometry("1020x680")
        self.root.minsize(840, 560)
        self.root.configure(bg="#121212")

        # 跨线程安全通信事件队列 (彻底根治 Linux/X11 下 Tcl/Tk 跨线程死锁与按钮假死)
        self.event_queue = queue.Queue()
        self._chat_font = tkfont.Font(font=get_ui_font(10))

        self._load_icon()

        self.current_conv_key = BROADCAST
        self.sessions = SessionState()
        self._aux_font = tkfont.Font(font=get_ui_font(10))
        # Tk 只接受整数点数；负字号按当前 DPI 转换为最接近 9.5pt 的像素值。
        self._aux_font.configure(size=-max(1, round(self.root.winfo_fpixels("9.5p"))))

        # 颜色规范：100% 对标 GrokBot UI 设计体系 (纯黑深邃 + 魅紫 #8E64DC)
        self.C_BG = "#121212"
        self.C_SIDEBAR = "#1C1C20"
        self.C_SESSIONS = "#161618"
        self.C_CHAT = "#121212"
        self.C_BORDER = "#28282C"
        self.C_PURPLE = "#8E64DC"
        self.C_PURPLE_HOVER = "#A078E6"
        self.C_BUBBLE_OTHER = "#1E1E21"
        self.C_BUBBLE_SELF = "#8E64DC"
        self.C_TEXT_WHITE = "#F2F2F6"
        self.C_TEXT_MUTED = "#8E8E93"
        self.C_SESSION_SELECT = "#28282C"
        self.C_SESSION_HOVER = "#222226"

        self._build_ui()
        self.overlay = WindowOverlay(self.root, self.txt_input, self._aux_font)
        self._wire_callbacks()

        # 启动主线程安全事件泵循环 (每 25ms 消费事件，永不死锁)
        self.root.after(25, self._poll_event_queue)
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

    def _load_icon(self):
        try:
            # zipapp 从归档直接读取图标，源码运行时回退到外置同名资源。
            data = pkgutil.get_data(__package__, "app.png")
            if data:
                self._icon_img = tk.PhotoImage(data=data)
                self.root.iconphoto(True, self._icon_img)
                return
        except (OSError, tk.TclError):
            pass
        for path in (application_directory() / "app.png", "/usr/share/icons/hicolor/256x256/apps/lanchat.png"):
            try:
                self._icon_img = tk.PhotoImage(file=str(path))
                self.root.iconphoto(True, self._icon_img)
                return
            except tk.TclError:
                pass

    def _build_ui(self):
        # 1. 左侧边栏 (导航与头像，Grok Level1 #1C1C20)
        self.sidebar = tk.Frame(self.root, bg=self.C_SIDEBAR, width=64)
        self.sidebar.pack(side="left", fill="y")
        self.sidebar.pack_propagate(False)

        self.avatar_canvas = tk.Canvas(self.sidebar, width=44, height=44, bg=self.C_SIDEBAR, highlightthickness=0)
        self.avatar_canvas.pack(pady=(16, 16))
        self._draw_avatar()

        self.btn_nav_chat = tk.Button(
            self.sidebar, text="会话", font=get_ui_font(10, "bold"),
            bg=self.C_SESSION_SELECT, fg=self.C_PURPLE, activebackground="#333336",
            activeforeground=self.C_PURPLE_HOVER, relief="flat", bd=0, padx=6, pady=6, cursor="hand2",
            command=lambda: self._select_session(BROADCAST)
        )
        self.btn_nav_chat.pack(pady=4, fill="x", padx=8)

        self.btn_nav_refresh = tk.Button(
            self.sidebar, text="刷新", font=get_ui_font(10),
            bg=self.C_SIDEBAR, fg=self.C_TEXT_WHITE, activebackground="#28282C",
            activeforeground=self.C_TEXT_WHITE, relief="flat", bd=0, padx=6, pady=6, cursor="hand2",
            command=self._on_refresh
        )
        self.btn_nav_refresh.pack(pady=4, fill="x", padx=8)

        self.btn_nav_add = tk.Button(
            self.sidebar, text="连接", font=get_ui_font(10),
            bg=self.C_SIDEBAR, fg=self.C_TEXT_WHITE, activebackground="#28282C",
            activeforeground=self.C_TEXT_WHITE, relief="flat", bd=0, padx=6, pady=6, cursor="hand2",
            command=self._on_add_ip
        )
        self.btn_nav_add.pack(pady=4, fill="x", padx=8)

        self.btn_settings = tk.Button(
            self.sidebar, text="设置", font=get_ui_font(10),
            bg=self.C_SIDEBAR, fg=self.C_TEXT_MUTED, activebackground="#28282C",
            activeforeground=self.C_TEXT_WHITE, relief="flat", bd=0, padx=6, pady=6, cursor="hand2",
            command=self._show_settings_menu
        )
        self.btn_settings.pack(side="bottom", pady=16, fill="x", padx=8)

        # 2. 中间会话列表栏 (Grok Level0 紧密列表 #161618)
        self.sessions_panel = tk.Frame(self.root, bg=self.C_SESSIONS, width=250)
        self.sessions_panel.pack(side="left", fill="y")
        self.sessions_panel.pack_propagate(False)

        search_frame = tk.Frame(self.sessions_panel, bg=self.C_SESSIONS, height=52)
        search_frame.pack(fill="x", padx=10, pady=(12, 6))

        search_box = tk.Frame(search_frame, bg="#222226", bd=0)
        search_box.pack(fill="x")

        lbl_lens = tk.Label(search_box, text="🔍", bg="#222226", fg=self.C_TEXT_MUTED, font=get_ui_font(8))
        lbl_lens.pack(side="left", padx=(8, 4))

        self.entry_search = tk.Entry(
            search_box, bg="#222226", fg=self.C_TEXT_WHITE, insertbackground="#ffffff",
            relief="flat", font=get_ui_font(9), bd=0
        )
        self.entry_search.pack(side="left", fill="x", expand=True, ipady=5, padx=(0, 4))
        self.entry_search.bind("<KeyRelease>", lambda e: self._on_search_changed())
        self.entry_search.bind("<Escape>", lambda e: self._on_search_clear())

        # 搜索框清除小叉按钮 (×)：输入内容时浮现
        self.btn_search_clear = tk.Button(
            search_box, text="✕", font=get_ui_font(8, "bold"),
            bg="#222226", fg=self.C_TEXT_MUTED, activebackground="#2E2E34",
            activeforeground="#FFFFFF", relief="flat", bd=0, padx=6, pady=0,
            cursor="hand2", command=self._on_search_clear
        )

        sep1 = tk.Frame(self.sessions_panel, bg=self.C_BORDER, height=1)
        sep1.pack(fill="x", pady=(6, 0))

        self.sessions_container = tk.Frame(self.sessions_panel, bg=self.C_SESSIONS)
        self.sessions_container.pack(fill="both", expand=True)

        # 3. 右侧主聊天区 (Grok Level0 #121212)
        self.chat_panel = tk.Frame(self.root, bg=self.C_CHAT)
        self.chat_panel.pack(side="right", fill="both", expand=True)

        # 顶部标头
        self.header_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=56)
        self.header_frame.pack(fill="x")
        self.header_frame.pack_propagate(False)

        header_info = tk.Frame(self.header_frame, bg=self.C_CHAT)
        header_info.pack(side="left", padx=18, pady=8)

        self.lbl_chat_title = tk.Label(
            header_info, text="局域网全员群聊", font=get_ui_font(12, "bold"),
            bg=self.C_CHAT, fg=self.C_TEXT_WHITE
        )
        self.lbl_chat_title.pack(anchor="w")

        self.lbl_chat_sub = tk.Label(
            header_info, text="局域网广播模式 (所有人可见)", font=get_ui_font(8),
            bg=self.C_CHAT, fg=self.C_TEXT_MUTED
        )
        self.lbl_chat_sub.pack(anchor="w")

        header_actions = tk.Frame(self.header_frame, bg=self.C_CHAT)
        header_actions.pack(side="right", padx=16, pady=12)

        for text, cmd in [
            ("刷新", self._on_refresh),
            ("加节点", self._on_add_ip),
            ("接收目录", self._open_save_folder),
            ("清屏", self._clear_current_chat)
        ]:
            b = tk.Button(
                header_actions, text=text, font=get_ui_font(8),
                bg="#1E1E22", fg=self.C_TEXT_WHITE, activebackground="#2A2A30",
                activeforeground="#ffffff", relief="flat", bd=0, padx=10, pady=4,
                cursor="hand2", command=cmd
            )
            b.pack(side="left", padx=3)

        sep2 = tk.Frame(self.chat_panel, bg=self.C_BORDER, height=1)
        sep2.pack(fill="x")

        # 消息流视图
        msg_container = tk.Frame(self.chat_panel, bg=self.C_CHAT)
        msg_container.pack(fill="both", expand=True)

        self.txt_history = tk.Text(
            msg_container, bg=self.C_CHAT, fg=self.C_TEXT_WHITE,
            relief="flat", bd=0, font=get_ui_font(10),
            padx=20, pady=12, state="disabled", wrap="word",
            selectbackground=self.C_PURPLE, selectforeground="#ffffff"
        )
        history_scroll = tk.Scrollbar(msg_container, orient="vertical", command=self.txt_history.yview)
        self.txt_history.configure(yscrollcommand=history_scroll.set)

        history_scroll.pack(side="right", fill="y")
        self.txt_history.pack(side="left", fill="both", expand=True)

        # 气泡标签配置 (对标 GrokBot 魅紫与深灰卡片)
        self.txt_history.tag_configure("sys_msg", justify="center", foreground=self.C_TEXT_MUTED, font=self._aux_font, spacing1=6, spacing3=6)
        self.txt_history.tag_configure("send_status", justify="right", foreground=self.C_TEXT_MUTED, font=self._aux_font, rmargin=16)
        self.txt_history.tag_configure("send_error", justify="right", foreground="#FC4455", font=self._aux_font, rmargin=16)
        self.txt_history.tag_configure("file_action", foreground=self.C_PURPLE, font=self._aux_font, underline=True)
        self.txt_history.tag_configure("center_time", justify="center", foreground="#666668", font=get_ui_font(8), spacing1=6, spacing3=4)
        self.txt_history.tag_configure("other_nick", justify="left", foreground=self.C_TEXT_MUTED, font=get_ui_font(8), lmargin1=16, lmargin2=16, spacing1=6, spacing3=2)
        self.txt_history.tag_configure("self_nick", justify="right", foreground=self.C_TEXT_MUTED, font=get_ui_font(8), rmargin=16, spacing1=6, spacing3=2)

        self._last_hist_width = 0
        def _on_hist_configure(event):
            if abs(event.width - self._last_hist_width) > 25:
                self._last_hist_width = event.width
                self._redraw_chat_history()
        self.txt_history.bind("<Configure>", _on_hist_configure)

        # 输入工具栏
        sep3 = tk.Frame(self.chat_panel, bg=self.C_BORDER, height=1)
        sep3.pack(fill="x")

        tool_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=36)
        tool_frame.pack(fill="x", padx=16, pady=4)

        btn_send_file = tk.Button(
            tool_frame, text="发送文件", font=get_ui_font(8),
            bg="#1E1E22", fg=self.C_TEXT_WHITE, activebackground="#2A2A30", activeforeground="#ffffff",
            relief="flat", bd=0, padx=10, pady=3, cursor="hand2", command=self._on_send_file
        )
        btn_send_file.pack(side="left", padx=4)

        self.btn_auto_accept = tk.Button(
            tool_frame, text="自动接收: 开", font=get_ui_font(8),
            bg="#1E1E22", fg=self.C_PURPLE, activebackground="#2A2A30", activeforeground=self.C_PURPLE_HOVER,
            relief="flat", bd=0, padx=10, pady=3, cursor="hand2", command=self._toggle_auto_accept
        )
        self._update_auto_accept_button()
        self.btn_auto_accept.pack(side="left", padx=4)

        btn_folder = tk.Button(
            tool_frame, text="接收目录", font=get_ui_font(8),
            bg="#1E1E22", fg=self.C_TEXT_WHITE, activebackground="#2A2A30", activeforeground="#ffffff",
            relief="flat", bd=0, padx=10, pady=3, cursor="hand2", command=self._open_save_folder
        )
        btn_folder.pack(side="left", padx=4)

        btn_clear = tk.Button(
            tool_frame, text="清屏", font=get_ui_font(8),
            bg="#1E1E22", fg=self.C_TEXT_WHITE, activebackground="#2A2A30", activeforeground="#ffffff",
            relief="flat", bd=0, padx=10, pady=3, cursor="hand2", command=self._clear_current_chat
        )
        btn_clear.pack(side="left", padx=4)

        # 多行输入框
        input_container = tk.Frame(self.chat_panel, bg=self.C_CHAT)
        input_container.pack(fill="x", padx=16, pady=(0, 4))

        self.txt_input = tk.Text(
            input_container, bg="#18181A", fg=self.C_TEXT_WHITE, insertbackground="#ffffff",
            font=get_ui_font(10), height=4, relief="flat", bd=0,
            padx=8, pady=8, wrap="word"
        )
        self.txt_input.pack(fill="x")
        self.txt_input.bind("<Return>", self._on_enter_pressed)
        self.txt_input.bind("<Control-Return>", lambda e: self.txt_input.insert("insert", "\n"))
        self.txt_input.bind("<KeyRelease>", self._update_send_btn_state)
        self.txt_input.bind("<BackSpace>", self._update_send_btn_state)

        # 底部发送面板
        bottom_frame = tk.Frame(self.chat_panel, bg=self.C_CHAT, height=38)
        bottom_frame.pack(fill="x", padx=18, pady=(0, 12))

        self.lbl_tip = tk.Label(bottom_frame, text="给 局域网全员群聊 发消息 (Enter 发送，Ctrl+Enter 换行)", font=get_ui_font(8), bg=self.C_CHAT, fg=self.C_TEXT_MUTED)
        self.lbl_tip.pack(side="left")

        self.btn_send = tk.Button(
            bottom_frame, text="发送 (S)", font=get_ui_font(9, "bold"),
            bg="#2C2C32", fg="#73737A", activebackground="#2C2C32",
            activeforeground="#73737A", relief="flat", bd=0, padx=22, pady=5,
            cursor="arrow", state="disabled", command=self._send_message
        )
        self.btn_send.pack(side="right")

        # 初始化渲染
        self._render_sessions()
        self._update_send_btn_state()
        self._append_system_message("欢迎使用 LanChat 局域网即时通讯客户端 (Linux GrokBot 暗黑版)！")
        self._append_system_message(f"本机 IP: {self.service.primary_ip}:{self.service.tcp_port} | 昵称: {self.service.nickname}")

    def _on_search_changed(self):
        kw = self.entry_search.get().strip()
        if kw:
            if not self.btn_search_clear.winfo_ismapped():
                self.btn_search_clear.pack(side="right", padx=(0, 6))
        else:
            if self.btn_search_clear.winfo_ismapped():
                self.btn_search_clear.pack_forget()
        self._render_sessions()

    def _on_search_clear(self):
        self.entry_search.delete(0, "end")
        self.btn_search_clear.pack_forget()
        self._render_sessions()
        self.entry_search.focus_set()

    def _update_send_btn_state(self, event=None):
        try:
            text = self.txt_input.get("1.0", "end-1c").strip()
            if text:
                self.btn_send.config(
                    bg=self.C_PURPLE, fg="#ffffff",
                    activebackground=self.C_PURPLE_HOVER, activeforeground="#ffffff",
                    cursor="hand2", state="normal"
                )
            else:
                self.btn_send.config(
                    bg="#2C2C32", fg="#73737A",
                    activebackground="#2C2C32", activeforeground="#73737A",
                    cursor="arrow", state="disabled"
                )
        except Exception:
            pass

    def _draw_avatar(self):
        self.avatar_canvas.delete("all")
        self.avatar_canvas.create_oval(2, 2, 42, 42, fill=self.C_PURPLE, outline="")
        initial = (self.service.nickname[0] if self.service.nickname else "L").upper()
        self.avatar_canvas.create_text(22, 22, text=initial, fill="#ffffff", font=get_ui_font(13, "bold"))

    def _wire_callbacks(self):
        # 将网络工作线程事件完全投递至线程安全队列，彻底杜绝跨线程直接调用 Tkinter
        self.service.on_peer_joined = lambda p: self.event_queue.put(("peer_joined", p))
        self.service.on_peer_updated = lambda p: self.event_queue.put(("peer_updated", p))
        self.service.on_peer_left = lambda pid: self.event_queue.put(("peer_left", pid))
        self.service.on_message_received = lambda *data: self.event_queue.put(("msg_recv", data))
        self.service.on_file_received = lambda *data: self.event_queue.put(("file_recv", data))
        self.service.on_file_progress = lambda *data: self.event_queue.put(("file_prog", data))
        self.service.on_file_result = lambda *data: self.event_queue.put(("operation_result", data))

    def _poll_event_queue(self):
        """主线程安全事件循环分发器：每 25ms 消费事件队列并刷新 UI"""
        try:
            while True:
                event_type, data = self.event_queue.get_nowait()
                if event_type == "peer_joined":
                    self._handle_peer_joined(data)
                elif event_type == "peer_updated":
                    self._handle_peer_updated(data)
                elif event_type == "peer_left":
                    self._handle_peer_left(data)
                elif event_type == "msg_recv":
                    self._handle_msg_recv(*data)
                elif event_type == "file_recv":
                    self._handle_file_recv(*data)
                elif event_type == "file_prog":
                    self._handle_file_prog(*data)
                elif event_type == "operation_result":
                    self._finish_operation(*data)
                elif event_type == "show_update":
                    self._show_update_dialog(*data)
                elif event_type == "info_box":
                    title, msg = data
                    self.overlay.notice(title, msg)
                elif event_type == "warning_box":
                    title, msg = data
                    self.overlay.notice(title, msg)
        except queue.Empty:
            pass
        except Exception as ex:
            log_message("ERROR", f"事件队列消费异常: {ex}")
        finally:
            if self.root:
                try:
                    self.root.after(25, self._poll_event_queue)
                except Exception:
                    pass

    def _render_sessions(self):
        for widget in self.sessions_container.winfo_children():
            widget.destroy()

        keyword = self.entry_search.get().strip().lower()

        # 1. 广播群聊项 (置顶深邃背景与徽章)
        if not keyword or "群聊" in keyword or "broadcast" in keyword:
            is_cur = (self.current_conv_key == BROADCAST)
            bg = self.C_SESSION_SELECT if is_cur else "#1A1724"
            f_bc = tk.Frame(self.sessions_container, bg=bg, height=56, cursor="hand2")
            f_bc.pack(fill="x", padx=6, pady=2)
            f_bc.pack_propagate(False)

            top_bc = tk.Frame(f_bc, bg=bg)
            top_bc.pack(fill="x", padx=10, pady=(8, 0))

            lbl_t = tk.Label(top_bc, text="● 局域网全员群聊", font=get_ui_font(9, "bold"), bg=bg, fg=self.C_TEXT_WHITE)
            lbl_t.pack(side="left")

            lbl_pin = tk.Label(top_bc, text="置顶", font=get_ui_font(7), bg=bg, fg=self.C_PURPLE)
            lbl_pin.pack(side="right")

            if self.sessions.unread.get(BROADCAST, 0) > 0:
                lbl_badge_bc = tk.Label(top_bc, text=str(self.sessions.unread[BROADCAST]), font=get_ui_font(7, "bold"), bg=self.C_PURPLE, fg="#ffffff", padx=4)
                lbl_badge_bc.pack(side="right", padx=(0, 4))

            with self.service.lock:
                count = len(self.service.peers)
            lbl_sub = tk.Label(f_bc, text=f"在线成员: {count} 人", font=get_ui_font(8), bg=bg, fg=self.C_TEXT_MUTED)
            lbl_sub.pack(anchor="w", padx=10)

            for w in (f_bc, top_bc, lbl_t, lbl_pin, lbl_sub):
                w.bind("<Button-1>", lambda e: self._select_session(BROADCAST))
                w.bind("<Button-3>", lambda e: self._show_session_menu(BROADCAST))
                w.bind("<Button-2>", lambda e: self._show_session_menu(BROADCAST))

        # 2. 在线好友列表项
        with self.service.lock:
            peers_list = list(self.service.peers.values())

        for session in self.sessions.list_sessions(peers_list):
            if keyword and keyword not in session.nick.lower() and keyword not in session.address:
                continue

            bg = self.C_SESSION_SELECT if self.current_conv_key == session.key else self.C_SESSIONS
            f_p = tk.Frame(self.sessions_container, bg=bg, height=56, cursor="hand2")
            f_p.pack(fill="x", padx=6, pady=2)
            f_p.pack_propagate(False)

            top_box = tk.Frame(f_p, bg=bg)
            top_box.pack(fill="x", padx=10, pady=(6, 0))

            lbl_dot = tk.Label(top_box, text="●", font=get_ui_font(8), bg=bg,
                               fg=self.C_PURPLE if session.online else self.C_TEXT_MUTED)
            lbl_dot.pack(side="left")

            lbl_nick = tk.Label(top_box, text=session.nick, font=get_ui_font(9, "bold"), bg=bg, fg=self.C_TEXT_WHITE)
            lbl_nick.pack(side="left", padx=4)

            if self.sessions.unread.get(session.key, 0) > 0:
                lbl_badge = tk.Label(top_box, text=str(self.sessions.unread[session.key]), font=get_ui_font(7, "bold"), bg=self.C_PURPLE, fg="#ffffff", padx=4)
                lbl_badge.pack(side="right")

            lbl_msg = tk.Label(f_p, text=session.preview.split("\n")[0] or session.address, font=get_ui_font(8), bg=bg, fg=self.C_TEXT_MUTED)
            lbl_msg.pack(anchor="w", padx=10)

            pid = session.key
            for w in (f_p, top_box, lbl_dot, lbl_nick, lbl_msg):
                w.bind("<Button-1>", lambda e, k=pid: self._select_session(k))
                w.bind("<Button-3>", lambda e, k=pid: self._show_session_menu(k))
                w.bind("<Button-2>", lambda e, k=pid: self._show_session_menu(k))

    def _show_session_menu(self, key):
        self._select_session(key)
        self.overlay.show("会话操作", [("发送文件", self._on_send_file), ("清空聊天记录", self._clear_current_chat)])

    def _select_session(self, conv_key):
        if conv_key != self.current_conv_key:
            self.sessions.drafts[self.current_conv_key] = self.txt_input.get("1.0", "end-1c")
            self.txt_input.delete("1.0", "end")
            self.txt_input.insert("1.0", self.sessions.drafts.pop(conv_key, ""))
        self.current_conv_key = conv_key
        self.sessions.unread[conv_key] = 0

        if conv_key == BROADCAST:
            self.lbl_chat_title.config(text="局域网全员群聊")
            with self.service.lock:
                count = len(self.service.peers)
            self.lbl_chat_sub.config(text=f"局域网广播模式 (共 {count} 位好友在线)")
            if hasattr(self, "lbl_tip"):
                self.lbl_tip.config(text="给 局域网全员群聊 发消息 (Enter 发送, Ctrl+Enter 换行)")
        else:
            peers = self._online_peers()
            peer = self.sessions.route(conv_key, peers)
            if peer:
                self.lbl_chat_title.config(text=peer.nick)
                self.lbl_chat_sub.config(text=f"{peer.host} @ {peer.ip}:{peer.port}")
                if hasattr(self, "lbl_tip"):
                    self.lbl_tip.config(text=f"给 {peer.nick} 发消息 (Enter 发送, Ctrl+Enter 换行)")
            else:
                session = next((entry for entry in self.sessions.list_sessions(peers) if entry.key == conv_key), None)
                self.lbl_chat_title.config(text=session.nick if session else "离线会话")
                self.lbl_chat_sub.config(text="节点离线")
                self.lbl_tip.config(text="好友离线时可保留草稿")

        self._render_sessions()
        self._redraw_chat_history()
        self._update_send_btn_state()

    def _redraw_chat_history(self):
        self.txt_history.configure(state="normal")
        try:
            self.txt_history.delete("1.0", "end")
            for item in self.sessions.histories.get(self.current_conv_key, []):
                self._render_message_item(item)
        finally:
            self.txt_history.configure(state="disabled")
        self.txt_history.see("end")

    def _render_message_item(self, item):
        if item.get("is_sys", False):
            self.txt_history.insert("end", f"{item['text']}\n\n", "sys_msg")
            return

        is_self = item["is_self"]
        sender = item["sender"]
        text = item["text"]
        t_str = item["time"]
        is_file = item.get("is_file", False)

        # 计算可用宽度与自适应气泡尺寸
        total_w = self.txt_history.winfo_width()
        if total_w < 100:
            total_w = 760
        pad_horiz = 40
        available_w = max(total_w - pad_horiz, 280)

        lines = text.split("\n")
        max_line_w = max(self._chat_font.measure(line) for line in lines) if lines else 20
        if is_file:
            max_line_w += 80

        # 最大气泡宽度不超过可用宽度的 62%，最大 520px
        max_bubble_w = min(max(available_w * 0.62, 180), 520)
        bubble_w = min(max_line_w + 32, max_bubble_w)
        bubble_w = max(bubble_w, 56)

        tag_id = f"bubble_{abs(hash((text, t_str, is_self, is_file)))}"

        if is_self:
            # 自己发送：右对齐气泡，左侧自然留白
            lmargin = int(available_w - bubble_w - 16)
            rmargin = 16
            bg_color = "#28282C" if is_file else self.C_PURPLE
            fg_color = self.C_PURPLE if is_file else "#ffffff"

            self.txt_history.tag_configure(
                tag_id, justify="left", background=bg_color, foreground=fg_color,
                font=self._chat_font, lmargin1=lmargin, lmargin2=lmargin, rmargin=rmargin,
                spacing1=6, spacing3=6
            )
            self.txt_history.insert("end", f"{t_str}  我\n", "self_nick")
            prefix = "[发送文件] " if is_file else ""
            self.txt_history.insert("end", f" {prefix}{text} ", tag_id)
            self._render_file_actions(item)
            if item.get("status"):
                tag = "send_error" if item.get("failed") else "send_status"
                self.txt_history.insert("end", "\n")
                self.txt_history.insert("end", item["status"], tag)
            self.txt_history.insert("end", "\n\n")
        else:
            # 对方发送：左对齐气泡，右侧自然留白
            lmargin = 16
            rmargin = int(available_w - bubble_w - 16)
            bg_color = "#222226"
            fg_color = self.C_PURPLE if is_file else self.C_TEXT_WHITE

            self.txt_history.tag_configure(
                tag_id, justify="left", background=bg_color, foreground=fg_color,
                font=self._chat_font, lmargin1=lmargin, lmargin2=lmargin, rmargin=rmargin,
                spacing1=6, spacing3=6
            )
            self.txt_history.insert("end", f"{sender}  {t_str}\n", "other_nick")
            prefix = "[接收文件] " if is_file else ""
            self.txt_history.insert("end", f" {prefix}{text} ", tag_id)
            self._render_file_actions(item)
            self.txt_history.insert("end", "\n\n")

    def _render_file_actions(self, item):
        path = item.get("local_path")
        if not item.get("is_file") or not isinstance(path, str) or not os.path.isabs(path) or not os.path.isfile(path):
            return
        self.txt_history.insert("end", "\n")
        for action, label, target in (("open", "打开", path), ("locate", "所在目录", os.path.dirname(path))):
            tag = f"{action}_{id(item)}"
            self.txt_history.insert("end", label, ("file_action", tag))
            self.txt_history.insert("end", "  ")
            self.txt_history.tag_bind(tag, "<Button-1>", lambda event, value=target: self._open_local_path(value))

    def _append_message(self, conv_key, is_self, sender, text, is_file=False, **details):
        timestamp = local_timestamp(details.pop("ts", None))
        item = {"is_self": is_self, "sender": sender, "text": text, "timestamp": timestamp,
                "time": timestamp[11:19], "is_file": is_file, **details}
        self._append_item(conv_key, item)
        return item

    def _append_item(self, conv_key, item):
        conv_key = self.sessions.resolve(conv_key)
        self.sessions.histories.setdefault(conv_key, []).append(item)
        if not item.get("transient"):
            try:
                self.sessions.store.append(conv_key, item)
            except OSError as exc:
                self._history_error(exc)
        if conv_key == self.current_conv_key:
            self.txt_history.configure(state="normal")
            try:
                self._render_message_item(item)
            finally:
                self.txt_history.configure(state="disabled")
            self.txt_history.see("end")
        else:
            self.sessions.unread[conv_key] = self.sessions.unread.get(conv_key, 0) + 1
            self._render_sessions()

    def _append_system_message(self, text, conv_key=BROADCAST):
        self._append_item(conv_key, {"is_sys": True, "text": text, "transient": True})

    def _begin_operation(self, conv_key, text, status, **details):
        conv_key = self.sessions.resolve(conv_key)
        operation_id = uuid.uuid4().hex
        item = self._append_message(conv_key, True, self.service.nickname, text, status=status,
                                    operation_id=operation_id, sender_id=self.service.local_id, **details)
        self.sessions.pending[operation_id] = (conv_key, item)
        return operation_id

    def _finish_operation(self, operation_id, success, detail):
        operation = self.sessions.pending.pop(operation_id, None)
        if operation:
            key, item = operation
            item.update(status=detail, failed=not success, delivery_state="delivered" if success else "failed")
            if not item.get("transient"):
                try:
                    self.sessions.store.complete(key, item)
                except OSError as exc:
                    self._history_error(exc)
            if key == self.current_conv_key:
                self._redraw_chat_history()

    def _on_enter_pressed(self, event):
        self._send_message()
        return "break"

    def _send_message(self):
        text = self.txt_input.get("1.0", "end-1c").strip()
        if not text: return
        key = self.current_conv_key
        broadcast = key == BROADCAST
        peers = self._online_peers()
        targets = list(self.sessions.active_routes(peers).values()) if broadcast else [self.sessions.route(key, peers)]
        if not targets or targets[0] is None:
            self._append_system_message("当前没有在线接收者。" if broadcast else "好友已离线，草稿已保留。", key)
            return
        self.txt_input.delete("1.0", "end")
        self._update_send_btn_state()
        target = targets[0]
        operation_id = self._begin_operation(key, text, "等待送达…", remote_address=None if broadcast else target.ip,
                                             target_id="ALL" if broadcast else target.id,
                                             peer_nick="" if broadcast else target.nick)

        def deliver():
            with ThreadPoolExecutor(max_workers=min(16, len(targets))) as pool:
                delivered = sum(pool.map(lambda peer: self.service.send_text_message(peer, text, broadcast), targets))
            success = delivered == len(targets)
            detail = f"已送达 {delivered}/{len(targets)} 人" if broadcast else ("已送达" if success else "发送失败")
            self.event_queue.put(("operation_result", (operation_id, success, detail)))

        if not broadcast:
            targets[0].last_msg = text
            self._render_sessions()
        threading.Thread(target=deliver, daemon=True).start()


    def _online_peers(self):
        with self.service.lock:
            return list(self.service.peers.values())

    def _incoming_key(self, sender_id, source_ip):
        with self.service.lock:
            peer = self.service.peers.get(sender_id)
        confirmed = bool(peer and peer.ip == source_ip and peer.address_confirmed)
        return self.sessions.key_for(sender_id, source_ip, confirmed or self.service.is_local_source(source_ip))

    def _handle_msg_recv(self, sender_nick, content, is_broadcast, sender_id, ts, source_ip):
        conv_key = BROADCAST if is_broadcast else self._incoming_key(sender_id, source_ip)
        self._append_message(conv_key, False, sender_nick, content, remote_address=source_ip,
                             sender_id=sender_id, peer_nick="" if is_broadcast else sender_nick,
                             target_id="ALL" if is_broadcast else self.service.local_id, ts=ts)
        with self.service.lock:
            if sender_id in self.service.peers:
                self.service.peers[sender_id].last_msg = content
        self._render_sessions()

    def _handle_file_recv(self, sender_nick, file_name, local_path, file_size, sender_id, source_ip):
        key = self._incoming_key(sender_id, source_ip)
        text = f"{file_name} ({format_size(file_size)})"
        self._append_message(key, False, sender_nick, text, is_file=True, remote_address=source_ip,
                             sender_id=sender_id, peer_nick=sender_nick, target_id=self.service.local_id,
                             file_name=file_name, local_path=local_path, file_size=file_size)

    def _handle_file_prog(self, transfer_id, transferred, total, speed):
        operation = self.sessions.pending.get(transfer_id)
        if operation:
            key, item = operation
            pct = int((transferred * 100) / total) if total > 0 else 100
            item["file_size"] = total
            item["status"] = f"传输 {pct}% · {speed:.1f} MB/s" if transferred < total else "等待落盘确认…"
            if key == self.current_conv_key:
                self._redraw_chat_history()


    def _reconcile_peer(self, peer):
        try:
            merged = self.sessions.reconcile(peer)
        except OSError as exc:
            self._history_error(exc)
            return
        for source, target in merged:
            if self.current_conv_key == source:
                self.current_conv_key = target
                merged_draft = self.sessions.drafts.pop(target, "")
                if merged_draft:
                    current = self.txt_input.get("1.0", "end-1c")
                    if current != merged_draft:
                        self.txt_input.insert("end-1c", ("\n" if current else "") + merged_draft)
                self._select_session(target)

    def _handle_peer_joined(self, peer):
        self._reconcile_peer(peer)
        self._append_system_message(f"[+] 好友上线: {peer.nick} ({peer.ip})")
        self._render_sessions()

    def _handle_peer_updated(self, peer):
        self._reconcile_peer(peer)
        self._render_sessions()

    def _handle_peer_left(self, peer_id):
        self._append_system_message("[-] 好友已离线")
        if self.current_conv_key != BROADCAST and not self.sessions.route(self.current_conv_key, self._online_peers()):
            self.lbl_chat_sub.config(text="节点离线")
        self._render_sessions()

    def _on_send_file(self):
        key = self.current_conv_key
        online = self._online_peers()
        peer = self.sessions.route(key, online)
        peers = list(self.sessions.active_routes(online).values()) if key == BROADCAST else [peer] if peer is not None else []
        if not peers:
            self._append_system_message("没有在线接收者，无法发送文件。", key)
        elif len(peers) == 1:
            self._choose_file(key, peers[0].id)
        else:
            self.overlay.show("选择文件接收者",
                               [(f"{p.nick} · {p.ip}:{p.port}", lambda pid=p.id: self._choose_file(key, pid))
                                for p in peers])

    def _choose_file(self, key, peer_id):
        paths = filedialog.askopenfilenames(title="选择要发送的文件", parent=self.root)
        if not paths:
            return
        with self.service.lock:
            peer = self.service.peers.get(peer_id)
        if not peer:
            self._append_system_message("接收者已离线，无法发送文件。", key)
            return
        for path in paths:
            name = os.path.basename(path)
            try:
                file_size = os.path.getsize(path)
            except OSError:
                file_size = 0  # 网络工作线程仍会为该路径报告文件打开失败。
            operation = self._begin_operation(key, name, f"等待 {peer.nick} 接收…", is_file=True,
                                              file_name=name, file_size=file_size, local_path=path, remote_address=peer.ip,
                                              target_id=peer.id, peer_nick=peer.nick)
            self.service.send_file(peer, path, operation)

    def _toggle_auto_accept(self):
        self.service.set_auto_accept(not self.service.auto_accept)
        self._update_auto_accept_button()

    def _update_auto_accept_button(self):
        txt = "自动接收: 开" if self.service.auto_accept else "自动接收: 关"
        fg = self.C_PURPLE if self.service.auto_accept else self.C_TEXT_MUTED
        self.btn_auto_accept.config(text=txt, fg=fg)

    def _open_save_folder(self):
        self._open_local_path(self.service.save_dir)

    def _open_local_path(self, path):
        try:
            if sys.platform == "darwin":
                subprocess.Popen(["open", path])
            elif sys.platform == "win32":
                os.startfile(path)
            else:
                subprocess.Popen(["xdg-open", path])
        except OSError as exc:
            self.overlay.notice("无法打开", str(exc))


    def _history_error(self, exc):
        log_message("WARN", f"无法更新聊天历史：{exc}")
        self.lbl_chat_sub.config(text="本地聊天历史写入失败")

    def _clear_current_chat(self):
        key = self.current_conv_key

        def clear():
            try:
                self.sessions.store.clear(key)
            except OSError as exc:
                self._history_error(exc)
                return
            self.sessions.histories.pop(key, None)
            stale = [operation for operation, (conv, _) in self.sessions.pending.items() if conv == key]
            for operation in stale:
                self.sessions.pending.pop(operation)
            self._redraw_chat_history()
            self._render_sessions()

        self.overlay.show("清空当前会话的聊天记录？", [("取消", lambda: None), ("清空记录", clear)],
                          body="会删除当前会话的本地历史，已接收的文件仍可在接收目录中查看。")

    def _on_refresh(self):
        self.service.broadcast_presence("HELLO")
        self._append_system_message("正在向局域网广播并探测在线好友...")

    def _on_add_ip(self):
        key = self.current_conv_key
        def connect(ip):
            ip = ip.strip()
            if _usable_ipv4(ip) is None:
                self._append_system_message(f"无效的 IPv4 地址：{ip}", key)
                return
            operation = self._begin_operation(key, f"连接 {ip}", "等待对方回应…", transient=True)
            self.service.try_tcp_handshake(
                ip, callback=lambda ok, detail: self.event_queue.put(("operation_result", (operation, ok, detail))))
        self.overlay.show("输入对方的局域网 IPv4", [("连接", connect)], entry=True)

    def _show_settings_menu(self):
        self.overlay.show("设置", [
            ("修改昵称", self._on_change_nick),
            ("切换自动接收文件", self._toggle_auto_accept),
            ("打开接收文件夹", self._open_save_folder),
            ("清空当前聊天记录", self._clear_current_chat),
            ("检查新版本", self._check_for_updates),
            ("关于 LanChat", self._show_about),
            ("退出 LanChat", self._on_close),
        ])

    def _on_change_nick(self):
        def save(new_nick):
            if new_nick.strip():
                self.service.set_nickname(new_nick.strip())
                self._draw_avatar()
                self.service.broadcast_presence("HELLO")
                self._append_system_message(f"您的昵称已更改为: {self.service.nickname}")
        self.overlay.show("修改昵称", [("保存", save)], entry=True, initial=self.service.nickname)

    def _show_about(self):
        msg = (
            f"LanChat v{APP_VERSION} (Linux GrokBot 暗黑版)\n\n"
            "局域网秒级极速通讯与文件互传\n"
            "UI: 纯标准库自绘 GrokBot Dark Mode\n"
            "零外部运行库依赖\n"
            "版权所有 © 2026 ChaseFraser LanChat Team. All rights reserved.\n"
            f"项目主页: https://github.com/{GITHUB_REPO}"
        )
        self.overlay.notice("关于 LanChat", msg)

    def _check_for_updates(self, silent=False):
        def _worker():
            tag = None
            body = "包含重要稳定性更新。"
            html_url = f"https://github.com/{GITHUB_REPO}/releases"
            last_err = None

            # 1. 优先尝试 GitHub REST API
            try:
                url = f"https://api.github.com/repos/{GITHUB_REPO}/releases/latest"
                req = urllib.request.Request(url, headers={"User-Agent": f"LanChat-Linux/{APP_VERSION}"})
                with urllib.request.urlopen(req, timeout=8) as resp:
                    data = json.loads(resp.read().decode('utf-8'))
                tag = data.get("tag_name", "").lstrip("vV")
                body = data.get("body", "包含重要稳定性更新。")
                html_url = data.get("html_url", f"https://github.com/{GITHUB_REPO}/releases")
            except Exception as ex:
                last_err = ex

            # 2. 降级尝试 Web 302 重定向 (无 60次/小时 API 限制)
            if not tag:
                try:
                    web_url = f"https://github.com/{GITHUB_REPO}/releases/latest"
                    req2 = urllib.request.Request(web_url, headers={"User-Agent": "Mozilla/5.0"})
                    with urllib.request.urlopen(req2, timeout=8) as resp2:
                        final_url = resp2.geturl()
                        if "/releases/tag/" in final_url:
                            tag = final_url.split("/releases/tag/")[-1].lstrip("vV")
                            html_url = final_url
                            last_err = None
                except Exception as ex2:
                    if not last_err:
                        last_err = ex2

            if tag:
                try:
                    cur_parts = [int(x) for x in APP_VERSION.split('.') if x.isdigit()]
                    tag_parts = [int(x) for x in tag.split('.') if x.isdigit()]
                    if tag_parts > cur_parts:
                        self.event_queue.put(("show_update", (tag, body, html_url)))
                    else:
                        if not silent:
                            self.event_queue.put(("info_box", ("检查更新", f"当前已是最新版本 (v{APP_VERSION})，无需更新。")))
                    return
                except Exception:
                    pass

            if not silent and last_err:
                tip = str(last_err)
                if hasattr(last_err, "code"):
                    if last_err.code == 403:
                        tip = "访问被禁止 (403 Forbidden)。\n原因：仓库目前为私有 (Private) 仓库，或触发了 GitHub API 速率限制。\n解决：请将 GitHub 仓库设为 Public 并发布 Release。"
                    elif last_err.code == 404:
                        tip = "未找到发布版本 (404 Not Found)。\n原因：仓库尚未创建 Release 版本，或仓库为私有 (Private) 状态。\n解决：请在 GitHub 发布 Release，并将仓库设为 Public。"
                self.event_queue.put(("warning_box", ("检查更新", f"检查更新失败。\n\n{tip}")))

        threading.Thread(target=_worker, daemon=True).start()

    def _show_update_dialog(self, tag, body, html_url):
        self.overlay.show(f"发现 LanChat v{tag}",
                           [("前往 GitHub 下载", lambda: webbrowser.open(html_url)), ("关闭", lambda: None)],
                           body=f"当前版本 v{APP_VERSION}\n\n{body}")

    def _on_close(self):
        log_message("INFO", "=== LanChat GUI 安全退出 ===")
        try:
            self.service.close()
        except Exception:
            pass
        try:
            self.root.destroy()
        except Exception:
            pass

    def run(self):
        self.root.mainloop()
