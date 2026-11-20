"""选择 Tk 桌面字体与中文回退。"""

import sys
from tkinter import font as tkfont


def get_ui_font(size=10, weight="normal"):
    """自适应跨平台字体：在 Linux 提供完善的中文字体回退链，彻底杜绝生硬粗糙点阵字"""
    if sys.platform == "win32":
        return ("Microsoft YaHei UI", size, weight)
    candidates = [
        "WenQuanYi Micro Hei",
        "Noto Sans CJK SC",
        "Source Han Sans CN",
        "Droid Sans Fallback",
        "DejaVu Sans",
        "sans-serif"
    ]
    try:
        avail = set(tkfont.families())
        for cand in candidates:
            if cand in avail:
                return (cand, size, weight)
    except Exception:
        pass
    return ("sans-serif", size, weight)
