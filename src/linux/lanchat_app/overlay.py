"""窗口内菜单、通知和接收者选择的键盘与焦点管理。"""

import tkinter as tk

from .ui_font import get_ui_font


class WindowOverlay:
    """跟随主窗口的暗色弹层，只在 Tk 线程操作，在关闭时恢复原焦点。"""

    def __init__(self, root, fallback_focus, aux_font):
        self.root = root
        self.fallback_focus = fallback_focus
        self.aux_font = aux_font
        self.panel = None
        self.previous_focus = None
        root.bind("<Escape>", self.close, add="+")
        root.bind_all("<Button-1>", self._dismiss_overlay_outside, add="+")

    def close(self, event=None):
        """关闭当前弹层并恢复焦点，供 Escape 与命令调用。"""
        if self.panel is None:
            return
        self.panel.destroy()
        self.panel = None
        focus, self.previous_focus = self.previous_focus, None
        (focus if focus is not None and focus.winfo_exists() else self.fallback_focus).focus_set()
        return "break"

    def _dismiss_overlay_outside(self, event):
        if self.panel is None:
            return
        widget = event.widget
        while widget is not None:
            if widget is self.panel:
                return
            widget = getattr(widget, "master", None)
        self.close()

    def show(self, title, actions, entry=False, initial="", body=None):
        """显示暗色窗口内弹层，选择时关闭弹层再执行对应回调。"""
        self.close()
        self.previous_focus = self.root.focus_get()
        panel = self.panel = tk.Frame(self.root, bg="#202024", padx=18, pady=12)
        panel.place(relx=0.5, rely=0.5, anchor="center")
        panel.lift()
        tk.Label(panel, text=title, bg="#202024", fg="#F2F2F6",
                 font=get_ui_font(11, "bold")).pack(anchor="w", pady=(0, 8))
        if body:
            message = tk.Text(panel, width=48, height=min(10, max(2, body.count("\n") + len(body) // 38 + 1)),
                              wrap="word", bg="#202024", fg="#8E8E93", font=self.aux_font,
                              borderwidth=0, highlightthickness=0)
            message.insert("1.0", body)
            message.configure(state="disabled")
            message.pack(fill="x")
        if entry:
            editor = tk.Entry(panel, width=32, bg="#28282C", fg="#F2F2F6", font=get_ui_font(11),
                              insertbackground="#F2F2F6", borderwidth=0, highlightthickness=0)
            editor.insert(0, initial)
            editor.pack(fill="x", ipady=5)
        choices = tk.Listbox(panel, width=40, height=min(8, len(actions)), bg="#202024", fg="#F2F2F6",
                             font=get_ui_font(11), selectbackground="#28282C", selectforeground="#F2F2F6",
                             relief="flat", highlightthickness=0, exportselection=False, activestyle="none")
        for label, _ in actions:
            choices.insert("end", label)
        choices.pack(fill="x", pady=(8, 0))
        choices.selection_set(0)

        def execute(index):
            value = editor.get() if entry else None
            self.close()
            callback = actions[index][1]
            callback(value) if entry else callback()
            return "break"

        choices.bind("<Button-1>", lambda e: execute(choices.nearest(e.y)))
        choices.bind("<Return>", lambda e: execute(choices.curselection()[0]) if choices.curselection() else "break")
        choices.bind("<space>", lambda e: execute(choices.curselection()[0]) if choices.curselection() else "break")
        def move_focus(widget):
            widget.focus_set()
            return "break"

        for sequence in ("<Tab>", "<Shift-Tab>", "<ISO_Left_Tab>"):
            choices.bind(sequence, lambda event: move_focus(editor if entry else choices))
            if entry:
                editor.bind(sequence, lambda event: move_focus(choices))
        if entry:
            editor.bind("<Return>", lambda e: execute(0))
            editor.focus_set()
        else:
            choices.focus_set()

    def notice(self, title, message):
        """在窗口内展示可关闭的通知。"""
        self.show(title, [("关闭", lambda: None)], body=message)
