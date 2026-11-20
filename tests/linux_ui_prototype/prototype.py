"""对比 Canvas 与嵌入 GDI+ 资源的 Text，测量 2000 条合成消息。"""

import argparse
import json
import os
from pathlib import Path
import platform
import sys
import time
import tkinter as tk
from tkinter import filedialog, font

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src/linux"))
from lanchat_app.overlay import WindowOverlay
from lanchat_app.ui_font import get_ui_font


BG, FG, MUTED = "#121212", "#F2F2F6", "#8E8E93"
MESSAGES = tuple(f"{index + 1:04d}  "
                 + ("已连接内网节点" if index % 11 == 0 else "文件：报告.pdf · 1.2 MB" if index % 5 == 0
                    else "局域网会话 · 测试选择复制与滚动")
                 for index in range(2000))


def rounded(canvas, x, y, w, h, r, color):
    """以 Canvas 几何路径画圆角，对照 GDI+ 透明边缘。"""
    points = (x, y + r, x, y, x + r, y, x + w - r, y, x + w, y, x + w, y + r,
              x + w, y + h - r, x + w, y + h, x + w - r, y + h, x + r, y + h, x, y + h, x, y + h - r)
    canvas.create_polygon(points, fill=color, outline="", smooth=True, splinesteps=24)


class Prototype:
    """两种视图共享样例和输入控件，随窗口启动，关闭窗口时结束测量。"""

    def __init__(self, root, assets, scale, accent):
        self.root, self.assets, self.scale, self.accent = root, assets, scale, accent
        root.title("LanChat Tk 原型 · Ctrl+1 Canvas / Ctrl+2 Text")
        root.configure(bg=BG)
        root.geometry("1040x720")
        self.message_font = font.Font(font=get_ui_font(11))
        self.samples = tk.Canvas(root, bg=BG, highlightthickness=0)
        self.samples.pack(fill="x")
        self.editor = tk.Text(root, height=3, bg="#202024", fg=FG, insertbackground=FG, font=self.message_font,
                              relief="flat", borderwidth=0, padx=12, pady=8)
        self.editor.pack(side="bottom", fill="x", padx=16, pady=10)
        self.editor.bind("<Return>", self._enter)
        self.editor.bind("<Control-Return>", self._newline)
        aux = (self.message_font.actual("family"), -round(root.winfo_fpixels("9.5p")))
        self.status = tk.Label(root, bg=BG, fg=MUTED, font=aux,
                               text="Ctrl+O 选择文件 · Ctrl+M 暗色选人 · Enter 记录提交")
        self.status.pack(side="bottom")
        self.overlay = WindowOverlay(root, self.editor, aux)
        self.flow = None
        self.images = {}
        self._draw_samples()
        root.bind("<Control-Key-1>", lambda event: self.switch("canvas"))
        root.bind("<Control-Key-2>", lambda event: self.switch("text"))
        root.bind("<Control-o>", lambda event: filedialog.askopenfilename(parent=root))
        root.bind("<Control-m>", lambda event: self.overlay.show("选择接收者", [
            ("节点甲 · 192.168.40.2", lambda: self.status.configure(text="已选择节点甲")),
            ("节点乙 · 192.168.40.3", lambda: self.status.configure(text="已选择节点乙")),
        ]))
        self.switch("canvas")
        self.editor.focus_set()

    def _draw_samples(self):
        self.samples.delete("all")
        self.images = {}
        directory = self.assets / str(self.scale) / self.accent
        for path in directory.glob("*.png"):
            try:
                self.images[path.stem] = tk.PhotoImage(file=str(path))
            except tk.TclError:
                continue
        specs = (("attach", 36, 36, 18, "#28282C"), ("send", 44, 44, 22, "#" + self.accent),
                 ("pill", 180, 64, 32, "#202024"), ("thumb", 4, 48, 2, "#383840"),
                 ("thumb-hover", 5, 48, 2.5, "#70707C"), ("bubble", 160, 38, 14, "#" + self.accent))
        k = self.scale / 100
        self.samples.configure(height=round(148 * k))
        x = 104 * k
        self.samples.create_text(12, 32 * k, anchor="w", text="Canvas", fill=MUTED, font=self.message_font)
        label = "GDI+ PNG" if self.images else "资源回退"
        self.samples.create_text(12, 106 * k, anchor="w", text=label, fill=MUTED, font=self.message_font)
        for name, width, height, radius, color in specs:
            w, h = width * k, height * k
            rounded(self.samples, x, 3 * k, w, h, radius * k, color)
            if name in ("attach", "send"):
                center, middle = x + w / 2, 3 * k + h / 2
                self.samples.create_line(center, middle - 7 * k, center, middle + 7 * k,
                                         width=1.8 * k, fill=FG, capstyle="round")
                points = ((center - 7 * k, middle, center + 7 * k, middle) if name == "attach" else
                          (center - 6 * k, middle - k, center, middle - 7 * k, center + 6 * k, middle - k))
                self.samples.create_line(*points, width=1.8 * k, fill=FG, capstyle="round", joinstyle="round")
            image = self.images.get(name)
            if image:
                self.samples.create_image(x, 75 * k, anchor="nw", image=image)
            else:
                rounded(self.samples, x, 75 * k, w, h, radius * k, color)
            x += w + 12 * k

    def _enter(self, _event):
        value = self.editor.get("1.0", "end-1c")
        print(json.dumps({"input": value, "event": "enter"}, ensure_ascii=False), flush=True)
        self.status.configure(text=f"已提交 {len(value)} 字符")
        self.editor.delete("1.0", "end")
        return "break"

    def _newline(self, _event):
        self.editor.insert("insert", "\n")
        return "break"

    def switch(self, mode):
        """重建消息视图，恢复首屏滚动比例并返回耗时毫秒。"""
        started = time.perf_counter()
        anchor = self.flow.yview()[0] if self.flow is not None else 0
        if self.flow is not None:
            self.flow.destroy()
        self.flow = self._canvas() if mode == "canvas" else self._text()
        self.flow.pack(fill="both", expand=True, padx=16)
        self.root.update_idletasks()
        self.flow.yview_moveto(anchor)
        self.root.update_idletasks()
        return round((time.perf_counter() - started) * 1000, 2)

    def _canvas(self):
        flow = tk.Canvas(self.root, bg=BG, highlightthickness=0)
        height = round(38 * self.scale / 100)
        for index, message in enumerate(MESSAGES):
            y = index * height + 5
            if index % 11:
                rounded(flow, 6, y, 372, height - 6, 12, "#202024")
            flow.create_text(14, y + (height - 6) / 2, text=message, anchor="w",
                             fill=MUTED if index % 11 == 0 else FG, font=self.message_font, tags=("message",))
        flow.configure(scrollregion=(0, 0, 400, len(MESSAGES) * height))

        def select(event, extend=False):
            items = flow.find_withtag("current")
            if items and "message" in flow.gettags(items[0]):
                item = items[0]
                flow.focus_set()
                point = f"@{flow.canvasx(event.x):.0f},{flow.canvasy(event.y):.0f}"
                flow.select_to(item, point) if extend else flow.select_from(item, point)

        def copy(_event):
            item = flow.select_item()
            if item:
                start, end = flow.index(item, "sel.first"), flow.index(item, "sel.last")
                flow.clipboard_clear()
                flow.clipboard_append(flow.itemcget(item, "text")[start:end + 1])
            return "break"

        flow.bind("<Button-1>", select)
        flow.bind("<B1-Motion>", lambda event: select(event, True))
        flow.bind("<Control-c>", copy)
        flow.bind("<MouseWheel>", lambda event: flow.yview_scroll(-int(event.delta / 120), "units"))
        flow.bind("<Button-4>", lambda event: flow.yview_scroll(-1, "units"))
        flow.bind("<Button-5>", lambda event: flow.yview_scroll(1, "units"))
        return flow

    def _text(self):
        flow = tk.Text(self.root, bg=BG, fg=FG, font=self.message_font, wrap="word", relief="flat", borderwidth=0,
                       padx=8, pady=4, spacing1=6, spacing3=6)
        flow.tag_configure("bubble", background="#202024")
        flow.tag_configure("system", foreground=MUTED)
        corner = self.images.get("corner")
        for index, message in enumerate(MESSAGES):
            if corner and index % 11 and index % 5 == 0:
                flow.image_create("end", image=corner, padx=4)
            flow.insert("end", message + "\n", "bubble" if index % 11 else "system")
        flow.configure(state="disabled")
        return flow

    def benchmark(self):
        """返回视图切换、滚动和缩放重建耗时及滚动比例偏差。"""
        measurements = {}
        initial_scale = self.scale
        for mode in ("canvas", "text"):
            switched = self.switch(mode)
            started = time.perf_counter()
            for index in range(1, 21):
                self.flow.yview_moveto(index / 24)
                self.root.update_idletasks()
            scroll = round((time.perf_counter() - started) * 1000, 2)
            anchor = self.flow.yview()[0]
            self.scale = 125 if initial_scale != 125 else 150
            self._draw_samples()
            redraw = self.switch(mode)
            delta = round(abs(anchor - self.flow.yview()[0]), 5)
            self.scale = initial_scale
            self._draw_samples()
            measurements[mode] = {"switch_ms": switched, "20_scrolls_ms": scroll, "redraw_ms": redraw,
                                  "anchor_delta": delta}
        return measurements


def main():
    """启动手动验证窗口，或输出一轮测量和当前 Tk 环境的 JSON。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assets", type=Path)
    parser.add_argument("--scale", type=int, choices=(100, 125, 150, 200), default=100)
    parser.add_argument("--accent", choices=("8E64DC", "5C9CFF"), default="8E64DC")
    parser.add_argument("--benchmark", action="store_true")
    args = parser.parse_args()
    root = tk.Tk()
    proto = Prototype(root, args.assets, args.scale, args.accent)
    report = {"os": platform.platform(), "session_type": os.environ.get("XDG_SESSION_TYPE"),
              "display": os.environ.get("DISPLAY"), "wayland_display": os.environ.get("WAYLAND_DISPLAY"),
              "tk_backend": root.tk.call("tk", "windowingsystem"), "tk_patchlevel": root.tk.call("info", "patchlevel"),
              "tk_scaling": root.tk.call("tk", "scaling"), "asset_scale": args.scale, "accent": args.accent,
              "loaded_assets": len(proto.images), "asset_bytes": sum(p.stat().st_size for p in args.assets.rglob("*.png"))}
    if args.benchmark:
        report["messages"] = len(MESSAGES)
        report["milliseconds"] = proto.benchmark()
    print(json.dumps(report, ensure_ascii=False, indent=2), flush=True)
    if args.benchmark:
        root.destroy()
    else:
        root.mainloop()


if __name__ == "__main__":
    main()
