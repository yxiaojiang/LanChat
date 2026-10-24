#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generate high-fidelity, multi-resolution app icons for LanChat:
  - app.ico (16, 24, 32, 48, 64, 128, 256)
  - app.png (256x256)
  - lanchat.svg (Vector SVG for Linux)
"""

import os
from PIL import Image, ImageDraw, ImageFilter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

def create_icon_image(size: int) -> Image.Image:
    # 4x supersampling for ultra-crisp antialiasing
    scale = 4 if size <= 64 else 2
    s = size * scale

    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    pad = s * 0.05
    radius = s * 0.22

    # 1. Base Squircle with vertical gradient
    bg_mask = Image.new("L", (s, s), 0)
    bg_mask_draw = ImageDraw.Draw(bg_mask)
    bg_mask_draw.rounded_rectangle([pad, pad, s - pad, s - pad], radius=radius, fill=255)

    gradient = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gradient)
    for y in range(int(pad), int(s - pad) + 1):
        ratio = (y - pad) / max(1, (s - 2 * pad))
        # Top #0fd674 -> Bottom #069c4e
        r = int(15 * (1 - ratio) + 6 * ratio)
        g = int(214 * (1 - ratio) + 156 * ratio)
        b = int(116 * (1 - ratio) + 78 * ratio)
        g_draw.line([(pad, y), (s - pad, y)], fill=(r, g, b, 255))

    canvas.paste(gradient, (0, 0), bg_mask)

    # 2. Secondary Speech Bubble (Right, mint #dcfce7)
    scx = s * 0.60
    scy = s * 0.56
    sbw = s * 0.38
    sbh = s * 0.32

    sub_bubble = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sub_draw = ImageDraw.Draw(sub_bubble)
    sub_draw.ellipse([scx - sbw/2, scy - sbh/2, scx + sbw/2, scy + sbh/2], fill=(220, 252, 231, 245))
    # Tail pointing down-right
    sub_tail = [
        (scx + sbw * 0.15, scy + sbh * 0.35),
        (scx + sbw * 0.48, scy + sbh * 0.60),
        (scx + sbw * 0.30, scy + sbh * 0.22)
    ]
    sub_draw.polygon(sub_tail, fill=(220, 252, 231, 245))

    # 3. Primary Speech Bubble (Left, pure white #ffffff) with subtle shadow
    mcx = s * 0.42
    mcy = s * 0.44
    mbw = s * 0.48
    mbh = s * 0.40

    main_bubble = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    main_draw = ImageDraw.Draw(main_bubble)
    main_draw.ellipse([mcx - mbw/2, mcy - mbh/2, mcx + mbw/2, mcy + mbh/2], fill=(255, 255, 255, 255))
    # Tail pointing down-left
    main_tail = [
        (mcx - mbw * 0.26, mcy + mbh * 0.34),
        (mcx - mbw * 0.52, mcy + mbh * 0.62),
        (mcx - mbw * 0.10, mcy + mbh * 0.42)
    ]
    main_draw.polygon(main_tail, fill=(255, 255, 255, 255))

    # Subtle drop shadow for main bubble
    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sh_draw = ImageDraw.Draw(shadow)
    sh_draw.ellipse([mcx - mbw/2, mcy - mbh/2 + s*0.015, mcx + mbw/2, mcy + mbh/2 + s*0.015], fill=(0, 60, 25, 45))
    sh_draw.polygon([(p[0], p[1] + s*0.015) for p in main_tail], fill=(0, 60, 25, 45))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=s * 0.015))

    # Composite layers
    canvas = Image.alpha_composite(canvas, sub_bubble)
    canvas = Image.alpha_composite(canvas, shadow)
    canvas = Image.alpha_composite(canvas, main_bubble)

    # Downsample to target size with high-quality Lanczos filter
    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)
    return final_img

def generate_svg(svg_path: str):
    svg_content = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <defs>
    <linearGradient id="bgGrad" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="#0fd674"/>
      <stop offset="100%" stop-color="#069c4e"/>
    </linearGradient>
    <filter id="bubbleShadow" x="-10%" y="-10%" width="130%" height="130%">
      <feDropShadow dx="0" dy="4" stdDeviation="4" flood-color="#003c19" flood-opacity="0.25"/>
    </filter>
  </defs>
  <!-- Squircle Base -->
  <rect x="14" y="14" width="228" height="228" rx="54" fill="url(#bgGrad)"/>
  
  <!-- Sub Bubble (Right) -->
  <path d="M 154 116 C 182 116 204 130 204 148 C 204 166 182 180 154 180 C 145 180 137 178 130 174 L 110 184 L 116 166 C 108 160 104 154 104 148 C 104 130 126 116 154 116 Z" fill="#dcfce7" opacity="0.95"/>
  
  <!-- Main Bubble (Left) with Drop Shadow -->
  <path d="M 108 84 C 69 84 38 102 38 126 C 38 140 47 153 62 161 L 52 184 L 78 174 C 87 178 97 180 108 180 C 147 180 178 162 178 138 C 178 108 147 84 108 84 Z" fill="#ffffff" filter="url(#bubbleShadow)"/>
</svg>
"""
    with open(svg_path, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"[SUCCESS] Generated: {svg_path}")

def main():
    sizes = [256, 128, 64, 48, 32, 24, 16]
    images = [create_icon_image(sz) for sz in sizes]

    # Save 256 PNG
    png_path = os.path.join(SCRIPT_DIR, "app.png")
    images[0].save(png_path, "PNG")
    print(f"[SUCCESS] Generated: {png_path}")

    # Save ICO with all resolutions
    ico_path = os.path.join(SCRIPT_DIR, "app.ico")
    images[0].save(
        ico_path,
        format="ICO",
        sizes=[(sz, sz) for sz in sizes]
    )
    print(f"[SUCCESS] Generated: {ico_path} with {len(sizes)} resolutions: {sizes}")

    # Save SVG
    svg_path = os.path.join(SCRIPT_DIR, "lanchat.svg")
    generate_svg(svg_path)

if __name__ == "__main__":
    main()
