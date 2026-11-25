# Logos du launcher en pixel art (grille 32x32).
#   python make_icon.py preview <out.png>   -> planche des 20 logos
#   python make_icon.py ico <n> <out.ico>   -> icône multi-tailles du logo n (1-20), intégrée à l'exe
#   python make_icon.py cs <Logos.cs>       -> les 20 logos en PNG base64 pour le choix au premier lancement
from PIL import Image, ImageDraw, ImageFont
import base64, io, sys

RED, DRED, YEL, DYEL, WHITE, BLACK = (237, 28, 36), (150, 10, 20), (255, 210, 0), (190, 140, 0), (255, 255, 255), (10, 10, 12)
CYAN, ORANGE, BLUE, PURPLE, GREEN, PINK, GREY, BROWN = (66, 220, 255), (255, 150, 30), (40, 80, 255), (200, 60, 255), (40, 220, 70), (255, 120, 190), (120, 120, 130), (150, 90, 40)

HEART = [
    "..XXX...XXX..",
    ".XXXXX.XXXXX.",
    "XXWXXXXXXXXXX",
    "XWXXXXXXXXXXX",
    "XXXXXXXXXXXXX",
    "XXXXXXXXXXXXX",
    ".XXXXXXXXXXX.",
    "..XXXXXXXXX..",
    "...XXXXXXX...",
    "....XXXXX....",
    ".....XXX.....",
    "......X......",
]
SMALL_HEART = [".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..."]
TINY_HEART = ["X.X", "XXX", ".X."]
BIG_STAR = [
    ".......Y.......", ".......Y.......", "......YYY......", "......YYY......", ".....YYYYY.....",
    "...YYYYYYYYY...", "YYYYYYYYYYYYYYY", "...YYYYYYYYY...", ".....YYYYY.....", "......YYY......",
    "......YYY......", ".......Y.......", ".......Y.......",
]
STAR = ["...Y...", "...Y...", "..YYY..", "YYYYYYY", "..YYY..", "...Y...", "...Y..."]


def darker(c, f=0.6):
    return tuple(int(v * f) for v in c[:3])


def blit(img, pat, ox, oy, colors, shade=None):
    for y, row in enumerate(pat):
        for x, ch in enumerate(row):
            if ch in colors:
                img.putpixel((ox + x, oy + y), colors[ch])
        if shade:
            xs = [x for x, ch in enumerate(row) if ch != "."]
            if xs:
                img.putpixel((ox + xs[-1], oy + y), shade)


def base(bg=BLACK, border=WHITE):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, 31, 31], radius=5, fill=bg, outline=border, width=1)
    return img, d


def heart(img, color=RED, ox=9, oy=10):
    blit(img, HEART, ox, oy, {"X": color, "W": WHITE}, darker(color))


# ---------- les 20 logos ----------
def l_play():
    img, d = base()
    d.polygon([(8, 5), (8, 26), (27, 15.5)], fill=YEL)
    d.line([(8, 26), (27, 15.5)], fill=DYEL)
    blit(img, SMALL_HEART, 10, 13, {"X": RED})
    return img


def l_save():
    img, d = base(bg=(20, 14, 40), border=YEL)
    blit(img, BIG_STAR, 8, 3, {"Y": YEL})
    blit(img, SMALL_HEART, 12, 21, {"X": RED})
    for p in [(4, 6), (26, 9), (5, 24), (27, 25), (22, 3)]:
        img.putpixel(p, WHITE)
    return img


def l_fight():
    img = Image.new("RGBA", (32, 32), BLACK + (255,))
    d = ImageDraw.Draw(img)
    d.rectangle([3, 3, 28, 28], outline=WHITE, width=2)
    heart(img)
    for x in (7, 12, 17, 22):
        d.line([(x + 1, 6), (x + 1, 8)], fill=WHITE)
    return img


def l_rewind():
    img, d = base(border=CYAN)
    d.arc([3, 3, 28, 28], start=40, end=330, fill=CYAN, width=2)
    d.polygon([(23, 4), (28, 9), (22, 10)], fill=CYAN)
    heart(img)
    return img


def l_stats():
    img, d = base()
    blit(img, ["X...", "X...", "X...", "X...", "XXXX"], 4, 4, {"X": YEL})
    blit(img, ["X...X", "X...X", ".X.X.", ".X.X.", "..X.."], 9, 4, {"X": YEL})
    blit(img, SMALL_HEART, 19, 4, {"X": RED})
    d.rectangle([4, 13, 27, 18], fill=YEL)
    d.rectangle([4, 19, 27, 19], fill=DYEL)
    blit(img, ["XXX.X.X", "X.X.X.X", "XXX.X.X", "X...X.X", "X....X."], 4, 22, {"X": WHITE})
    for x in range(14, 28, 3):
        d.rectangle([x, 23, x + 1, 26], fill=RED)
    return img


def soul(color, bg=BLACK):
    def f():
        img, d = base(bg=bg, border=color)
        heart(img, color)
        for p in [(5, 5), (26, 6), (6, 26), (25, 25)]:
            img.putpixel(p, darker(color, 0.8))
        return img
    return f


def l_inverted():
    img, d = base(bg=YEL, border=BLACK)
    heart(img, RED)
    return img


def l_broken():
    img, d = base(bg=(30, 0, 4), border=RED)
    heart(img, RED)
    for (x, y) in [(15, 10), (16, 11), (15, 12), (14, 13), (15, 14), (16, 15), (15, 16), (14, 17), (15, 18), (15, 19), (15, 20)]:
        img.putpixel((x, y), (30, 0, 4))
    return img


def l_crown():
    img, d = base(bg=(25, 10, 40), border=YEL)
    crown = ["Y..Y..Y", "YY.Y.YY", "YYYYYYY", "YRYYYRY", "YYYYYYY"]
    blit(img, crown, 12, 3, {"Y": YEL, "R": RED})
    heart(img, RED, 9, 12)
    return img


def l_floppy():
    img, d = base(bg=(20, 30, 70), border=WHITE)
    d.rectangle([6, 5, 25, 26], fill=(40, 60, 140))
    d.polygon([(22, 5), (25, 8), (25, 5)], fill=(20, 30, 70))
    d.rectangle([10, 5, 21, 11], fill=(190, 190, 200))
    d.rectangle([17, 6, 19, 10], fill=(40, 60, 140))
    d.rectangle([9, 15, 22, 26], fill=WHITE)
    blit(img, SMALL_HEART, 12, 17, {"X": RED})
    return img


def l_pad():
    img, d = base()
    d.rounded_rectangle([3, 10, 28, 23], radius=5, fill=(70, 70, 85))
    d.rectangle([7, 15, 12, 17], fill=WHITE)
    d.rectangle([9, 13, 10, 19], fill=WHITE)
    img.putpixel((22, 14), YEL); img.putpixel((24, 16), RED); img.putpixel((20, 16), CYAN); img.putpixel((22, 18), GREEN)
    blit(img, TINY_HEART, 14, 4, {"X": RED})
    return img


def l_potion():
    img, d = base(bg=(10, 25, 20), border=GREEN)
    d.rectangle([13, 3, 18, 6], fill=BROWN)
    d.rectangle([14, 7, 17, 10], fill=(200, 230, 255))
    d.ellipse([7, 10, 24, 28], fill=(200, 230, 255))
    d.ellipse([9, 15, 22, 26], fill=RED)
    d.rectangle([14, 17, 17, 24], fill=WHITE)
    d.rectangle([12, 19, 19, 22], fill=WHITE)
    return img


def l_rocket():
    img, d = base(bg=(8, 8, 30), border=ORANGE)
    d.polygon([(16, 3), (21, 10), (21, 21), (11, 21), (11, 10)], fill=WHITE)
    d.polygon([(11, 16), (7, 23), (11, 21)], fill=RED)
    d.polygon([(21, 16), (25, 23), (21, 21)], fill=RED)
    blit(img, TINY_HEART, 15, 11, {"X": RED})
    d.polygon([(12, 22), (20, 22), (16, 29)], fill=ORANGE)
    d.polygon([(14, 22), (18, 22), (16, 26)], fill=YEL)
    for p in [(5, 6), (26, 5), (4, 15), (27, 14)]:
        img.putpixel(p, WHITE)
    return img


def l_key():
    img, d = base(border=YEL)
    blit(img, HEART, 3, 4, {"X": YEL, "W": WHITE}, DYEL)
    d.rectangle([13, 14, 28, 16], fill=YEL)
    d.rectangle([23, 17, 24, 21], fill=YEL)
    d.rectangle([27, 17, 28, 20], fill=YEL)
    return img


def l_flower():
    img, d = base(bg=(10, 20, 10), border=GREEN)
    d.rectangle([15, 18, 16, 28], fill=GREEN)
    d.polygon([(16, 24), (22, 20), (20, 25)], fill=GREEN)
    for (cx, cy) in [(16, 6), (22, 10), (20, 17), (12, 17), (10, 10)]:
        d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=YEL)
    d.ellipse([12, 8, 20, 16], fill=RED)
    img.putpixel((14, 10), WHITE)
    return img


def l_paw():
    img, d = base(bg=(30, 20, 12), border=WHITE)
    d.ellipse([9, 15, 23, 27], fill=WHITE)
    for (x, y) in [(6, 9), (11, 5), (17, 5), (22, 9)]:
        d.ellipse([x, y, x + 4, y + 5], fill=WHITE)
    blit(img, SMALL_HEART, 13, 18, {"X": RED})
    return img


def l_lightning():
    img, d = base(bg=(20, 20, 0), border=YEL)
    heart(img, RED)
    d.polygon([(18, 4), (11, 17), (16, 17), (13, 28), (22, 13), (17, 13), (20, 4)], fill=YEL, outline=DYEL)
    return img


LOGOS = [
    ("Play", l_play), ("Save", l_save), ("Combat", l_fight), ("Rembobiner", l_rewind), ("Fiche LV", l_stats),
    ("Âme cyan", soul(CYAN, (0, 18, 24))), ("Âme orange", soul(ORANGE, (28, 12, 0))), ("Âme bleue", soul(BLUE, (4, 8, 30))),
    ("Âme violette", soul(PURPLE, (20, 4, 28))), ("Âme verte", soul(GREEN, (2, 22, 6))), ("Âme rose", soul(PINK, (30, 8, 20))),
    ("Inversé", l_inverted), ("Brisé", l_broken), ("Couronne", l_crown), ("Disquette", l_floppy),
    ("Manette", l_pad), ("Potion", l_potion), ("Fusée", l_rocket), ("Clé", l_key), ("Éclair", l_lightning),
]


def preview(out):
    tile, pad, cols = 110, 20, 5
    rows = (len(LOGOS) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tile + pad) + pad, rows * (tile + 50) + pad), (32, 32, 36))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("segoeuib.ttf", 15)
    except OSError:
        font = ImageFont.load_default()
    for i, (name, f) in enumerate(LOGOS):
        x, y = pad + (i % cols) * (tile + pad), pad + (i // cols) * (tile + 50)
        g = f().resize((tile, tile), Image.NEAREST)
        sheet.paste(g, (x, y), g)
        d.text((x, y + tile + 6), "%d. %s" % (i + 1, name), fill=WHITE, font=font)
    sheet.save(out)


def ico(n, out):
    g = LOGOS[n - 1][1]()
    sizes = [256, 128, 64, 48, 32, 24, 16]
    frames = [g.resize((s, s), Image.NEAREST if s >= 32 else Image.LANCZOS) for s in sizes]
    frames[0].save(out, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[1:])


def cs(out):
    lines = ["// Généré par make_icon.py : 20 logos 32x32 en PNG base64.", "static class Logos {",
             "  public static readonly string[] Names = { %s };" % ", ".join('"%s"' % n for n, _ in LOGOS),
             "  public static readonly string[] Png = {"]
    for _, f in LOGOS:
        buf = io.BytesIO()
        f().save(buf, format="PNG")
        lines.append('    "%s",' % base64.b64encode(buf.getvalue()).decode())
    lines += ["  };", "}"]
    open(out, "w", encoding="utf-8-sig").write("\n".join(lines) + "\n")


if __name__ == "__main__":
    {"preview": lambda: preview(sys.argv[2]), "ico": lambda: ico(int(sys.argv[2]), sys.argv[3]), "cs": lambda: cs(sys.argv[2])}[sys.argv[1]]()
