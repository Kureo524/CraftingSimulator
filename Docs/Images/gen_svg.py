"""Génère les visuels SVG du README dans la DA « Verre d'encre » (polices du jeu intégrées en WOFF, sous-ensemble).
Usage, depuis la racine du dépôt (nécessite fontTools) :
    python3 Docs/Images/gen_svg.py Assets/_Project/UI/Fonts Docs/Images"""
import base64, io, math, random, sys
from fontTools.ttLib import TTFont
from fontTools import subset

FONTS_DIR, OUT = sys.argv[1], sys.argv[2]

# ---------------- Jetons de la DA ----------------
BG = "#0c0c12"
TEXT, TEXT2, TEXT3 = "#e8eefa", "#a3a6b6", "#8a8a9c"
ACCENT, ACCENT_TEXT, LAVENDER, SUCCESS, WARNING, DANGER = "#8aa8e0", "#a9c4f0", "#a597c0", "#9ebd85", "#e7b52f", "#e0606a"
SURFACE = "rgba(22,22,30,0.62)"
GLASS = "rgba(30,30,40,0.42)"
BORDER = "rgba(232,238,250,0.09)"
RIM = "rgba(232,238,250,0.24)"
STATUS_BG, STATUS_BORDER, STATUS_RIM = "rgba(16,20,32,0.55)", "rgba(138,168,224,0.28)", "rgba(138,168,224,0.45)"

FONT_FILES = {
    "Gothic A1:400": "GothicA1-Regular.ttf",
    "Gothic A1:700": "GothicA1-Bold.ttf",
    "IBM Plex Mono:400": "IBMPlexMono-Regular.ttf",
    "Barlow Condensed:600": "BarlowCondensed-SemiBold.ttf",
}
_metrics = {}


def font(key):
    if key not in _metrics:
        f = TTFont(f"{FONTS_DIR}/{FONT_FILES[key]}")
        _metrics[key] = (f.getBestCmap(), f["hmtx"].metrics, f["head"].unitsPerEm)
    return _metrics[key]


def width(text, key, size):
    cmap, hmtx, upm = font(key)
    w = 0
    for ch in text:
        g = cmap.get(ord(ch))
        w += hmtx[g][0] if g in hmtx else upm * 0.5
    return w * size / upm


def wrap(text, key, size, max_w):
    lines, cur = [], ""
    for word in text.split(" "):
        test = (cur + " " + word).strip()
        if width(test, key, size) <= max_w or not cur:
            cur = test
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


# ---------------- Polices intégrées ----------------
def font_faces(used_text):
    chars = set(used_text) | set(" 0123456789.,:;!?'’«»()[]/-–—+×→·°%…")
    css = []
    for key, file in FONT_FILES.items():
        family, weight = key.split(":")
        opts = subset.Options()
        opts.flavor = "woff"
        opts.layout_features = ["kern", "liga"]
        opts.name_IDs = []
        f = TTFont(f"{FONTS_DIR}/{file}")
        sub = subset.Subsetter(opts)
        sub.populate(text="".join(sorted(chars)))
        sub.subset(f)
        buf = io.BytesIO()
        f.flavor = "woff"
        f.save(buf)
        data = base64.b64encode(buf.getvalue()).decode()
        css.append(f"@font-face{{font-family:'{family}';font-weight:{weight};"
                   f"src:url(data:font/woff;base64,{data}) format('woff');}}")
    return "\n".join(css)


# ---------------- Briques ----------------
class Svg:
    def __init__(self, w, h, title):
        self.w, self.h, self.title = w, h, title
        self.parts, self.text = [], ""

    def add(self, s):
        self.parts.append(s)

    def t(self, x, y, s, size=12, key="Gothic A1:400", fill=TEXT, anchor="start", opacity=None):
        self.text += s
        family, weight = key.split(":")
        op = f' opacity="{opacity}"' if opacity is not None else ""
        self.add(f'<text x="{x:.1f}" y="{y:.1f}" font-family="{family}, sans-serif" font-weight="{weight}" '
                 f'font-size="{size}" fill="{fill}" text-anchor="{anchor}"{op}>{esc(s)}</text>')

    def glass(self, x, y, w, h, r=18, fill=SURFACE):
        # Verre : fond translucide, bordure discrète, liseré clair en haut et à gauche (lumière haut-gauche).
        self.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{BORDER}"/>')
        self.add(f'<path d="M{x + 0.5},{y + h - r} V{y + r} A{r - 0.5},{r - 0.5} 0 0 1 {x + r},{y + 0.5} H{x + w - r}" '
                 f'fill="none" stroke="{RIM}" stroke-width="1"/>')

    def status(self, x, y, w, h, r=12):
        self.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{STATUS_BG}" stroke="{STATUS_BORDER}"/>')
        self.add(f'<path d="M{x + 0.5},{y + h - r} V{y + r} A{r - 0.5},{r - 0.5} 0 0 1 {x + r},{y + 0.5} H{x + w - r}" '
                 f'fill="none" stroke="{STATUS_RIM}"/>')

    def chip(self, x, y, label, color):
        w = width(label, "Gothic A1:700", 11) + 30
        self.add(f'<rect x="{x}" y="{y}" width="{w:.1f}" height="24" rx="12" fill="rgba(232,238,250,0.06)" stroke="{BORDER}"/>')
        self.add(f'<circle cx="{x + 12}" cy="{y + 12}" r="3" fill="{color}"/>')
        self.t(x + 21, y + 16, label, 11, "Gothic A1:700", TEXT2)
        return w

    def sky(self, seed=7, density=2400, constellation=True):
        w, h = self.w, self.h
        self.add('<defs>'
                 f'<radialGradient id="lb" cx="{0.12 * w}" cy="{0.18 * h}" r="{0.42 * max(w, h)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset="0" stop-color="{ACCENT}" stop-opacity="0.31"/><stop offset="1" stop-color="{ACCENT}" stop-opacity="0"/></radialGradient>'
                 f'<radialGradient id="ll" cx="{0.88 * w}" cy="{0.12 * h}" r="{0.36 * max(w, h)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset="0" stop-color="{LAVENDER}" stop-opacity="0.25"/><stop offset="1" stop-color="{LAVENDER}" stop-opacity="0"/></radialGradient>'
                 f'<radialGradient id="lg" cx="{0.60 * w}" cy="{1.0 * h}" r="{0.42 * max(w, h)}" gradientUnits="userSpaceOnUse">'
                 f'<stop offset="0" stop-color="{SUCCESS}" stop-opacity="0.19"/><stop offset="1" stop-color="{SUCCESS}" stop-opacity="0"/></radialGradient>'
                 f'<clipPath id="frame"><rect width="{w}" height="{h}" rx="22"/></clipPath>'
                 '</defs>')
        self.add('<g clip-path="url(#frame)">')
        self.add(f'<rect width="{w}" height="{h}" fill="{BG}"/>')
        for g in ("lb", "ll", "lg"):
            self.add(f'<rect width="{w}" height="{h}" fill="url(#{g})"/>')
        rnd = random.Random(seed)
        n = int(w * h / density)
        bright = []
        for _ in range(n):
            x, y = rnd.uniform(0, w), rnd.uniform(0, h)
            r = rnd.uniform(0.3, 1.6)
            o = rnd.uniform(0.2, 0.65)
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r:.2f}" fill="{TEXT}" opacity="{o:.2f}"/>')
            if r > 1.25:
                bright.append((x, y))
        if constellation:
            bright = bright[:14]
            for i, (x1, y1) in enumerate(bright):
                near = sorted(((math.dist((x1, y1), p), p) for p in bright[i + 1:]))[:1]
                for d, (x2, y2) in near:
                    if d < 0.28 * w:
                        self.add(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{TEXT}" stroke-opacity="0.16"/>')
        self.add(f'<rect x="0.5" y="0.5" width="{w - 1}" height="{h - 1}" rx="22" fill="none" stroke="{BORDER}"/>')

    def close_sky(self):
        self.add('</g>')

    def save(self, name):
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}" '
                f'role="img" aria-label="{esc(self.title)}"><title>{esc(self.title)}</title>'
                f'<style>{font_faces(self.text)}</style>')
        open(f"{OUT}/{name}", "w", encoding="utf-8").write(head + "\n".join(self.parts) + "</svg>\n")


def iso_cube(s, cx, cy, a, color, opacity=1.0, fill_op=0.0):
    """Cube isométrique en traits fins (sommet haut au centre (cx, cy), arête a)."""
    dx, dy = a * math.cos(math.radians(30)), a * math.sin(math.radians(30))
    top = [(cx, cy), (cx + dx, cy + dy), (cx, cy + 2 * dy), (cx - dx, cy + dy)]
    pts = lambda p: " ".join(f"{x:.1f},{y:.1f}" for x, y in p)
    left = [(cx - dx, cy + dy), (cx, cy + 2 * dy), (cx, cy + 2 * dy + a), (cx - dx, cy + dy + a)]
    right = [(cx, cy + 2 * dy), (cx + dx, cy + dy), (cx + dx, cy + dy + a), (cx, cy + 2 * dy + a)]
    for face, shade in ((top, "#1a1b26"), (left, "#13141c"), (right, "#0f1017")):
        s.add(f'<polygon points="{pts(face)}" fill="{shade}"/>')
        s.add(f'<polygon points="{pts(face)}" fill="{color}" fill-opacity="{fill_op:.3f}" stroke="{color}" '
              f'stroke-opacity="{opacity:.2f}" stroke-width="1" stroke-linejoin="round"/>')


# ================= 1. Bannière =================
def banner():
    s = Svg(1280, 420, "Atelier Nucléaire — Mon atelier, mes règles")
    s.sky(seed=11)
    # Multi-bloc stylisé : boîte 3×3×3 en cubes, contrôleur et cœur teintés.
    a = 40
    dx, dy = a * math.cos(math.radians(30)), a * math.sin(math.radians(30))
    ox, oy = 1020, 62
    cells = [(x, y, z) for z in range(3) for y in range(3) for x in range(3)]
    cells.sort(key=lambda c: (c[2], c[0] + c[1]))
    for x, y, z in cells:
        cx = ox + (x - y) * dx
        cy = oy + (x + y) * dy - z * a + 2 * a
        core = (x, y, z) == (1, 1, 1)
        ctrl = (x, y, z) == (2, 1, 1)
        if core:
            continue
        color = LAVENDER if ctrl else ACCENT
        glass = z == 1 and x == 2 and y != 1 or (y == 2 and z == 1 and x == 1)
        iso_cube(s, cx, cy, a, color, opacity=0.5 if not ctrl else 0.9,
                 fill_op=0.28 if ctrl else (0.14 if glass else 0.06))
    # Pastille d'antimatière au-dessus de la structure
    s.add(f'<circle cx="{ox}" cy="{oy - 8}" r="9" fill="{LAVENDER}" fill-opacity="0.85"/>')
    s.add(f'<circle cx="{ox}" cy="{oy - 8}" r="16" fill="none" stroke="{LAVENDER}" stroke-opacity="0.35"/>')
    s.add(f'<line x1="{ox}" y1="{oy + 8}" x2="{ox}" y2="{oy + 30}" stroke="{TEXT}" stroke-opacity="0.16"/>')

    # Panneau de verre principal
    s.glass(56, 64, 700, 292)
    s.t(92, 128, "La Horde · B3 · Quête I4", 13, "Gothic A1:400", TEXT3)
    s.t(90, 190, "Atelier Nucléaire", 60, "Gothic A1:700", TEXT)
    s.t(92, 228, "Mon atelier, mes règles — une démo de crafting par procédés industriels.", 17, "Gothic A1:400", TEXT2)
    s.status(92, 258, 628, 38)
    s.t(110, 282, "[Objectif final : une pastille d'antimatière.]", 13, "IBM Plex Mono:400", ACCENT_TEXT)
    x = 92
    for label, col in (("Unity 6.3 LTS", ACCENT), ("4 règles qui interagissent", SUCCESS), ("3 objectifs", LAVENDER)):
        x += s.chip(x, 314, label, col) + 10
    s.close_sky()
    s.save("banner.svg")


# ================= 2. Règles =================
RULES = [
    ("1", "Budget d'énergie", "Chaque procédé coûte de l'énergie à chaque tick. Sous-alimentée, une machine ralentit ; le déphaseur exige 100 % ou n'avance pas.",
     "[Énergie à 60 % : la machine ralentit.]", ACCENT),
    ("2", "Sous-produits", "L'électrolyse donne hydrogène et oxygène, le réacteur vapeur et déchets. Une sortie pleine bloque la machine : tout doit être évacué ou recyclé.",
     "[Sortie pleine : évacue les produits.]", SUCCESS),
    ("3", "Chaleur et refroidissement", "Le joueur règle la combustion. 1 mB brûlé demande 10 mB d'eau, rendus en vapeur. Sans eau, ou vapeur bloquée : surchauffe, combustible perdu.",
     "[Manque d'eau : chaleur +33/tick.]", WARNING),
    ("4", "Géométrie des multi-blocs", "Chaque composant intérieur ajoute de la puissance et du coût : plus d'assemblages, plus de combustion ; plus de bobines, plus d'énergie exigée.",
     "[Structure complète : 5×5×3.]", LAVENDER),
]


def rules():
    W, pad, gap, cw, ch = 1280, 40, 20, 590, 214
    H = 2 * pad + 2 * ch + gap + 64
    s = Svg(W, H, "Les quatre règles de l'atelier")
    s.sky(seed=23, density=3600, constellation=False)
    s.t(pad, pad + 26, "Les quatre règles", 26, "Gothic A1:700", TEXT)
    s.t(W - pad, pad + 24, "Chacune nourrit la suivante : la vapeur alimente, les déchets deviennent du polonium.", 13,
        "Gothic A1:400", TEXT3, anchor="end")
    for i, (num, title, body, sysmsg, col) in enumerate(RULES):
        x = pad + (i % 2) * (cw + gap)
        y = pad + 54 + (i // 2) * (ch + gap)
        s.glass(x, y, cw, ch)
        s.t(x + 28, y + 62, num, 48, "Barlow Condensed:600", col)
        s.t(x + 74, y + 44, title, 19, "Gothic A1:700", TEXT)
        for k, line in enumerate(wrap(body, "Gothic A1:400", 14, cw - 74 - 28)):
            s.t(x + 74, y + 76 + k * 21, line, 14, "Gothic A1:400", TEXT2)
        s.status(x + 74, y + ch - 56, cw - 74 - 28, 34)
        s.t(x + 90, y + ch - 34, sysmsg, 12, "IBM Plex Mono:400", ACCENT_TEXT)
    s.close_sky()
    s.save("rules.svg")


# ================= 3. Chaîne de l'antimatière =================
def chain():
    W, H = 1280, 470
    s = Svg(W, H, "La chaîne de l'antimatière")
    s.sky(seed=5, density=3600, constellation=False)
    s.t(40, 66, "La boucle de l'antimatière", 26, "Gothic A1:700", TEXT)
    s.t(40, 92, "Le réacteur à fission est à la fois la centrale et la source de polonium : c'est l'énigme du dernier objectif.", 13,
        "Gothic A1:400", TEXT3)
    s.add(f'<defs><marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto">'
          f'<path d="M0,0 L10,5 L0,10 z" fill="{TEXT2}"/></marker></defs>')

    def node(cx, cy, label, sub=None, col=ACCENT, w=None):
        w = w or max(width(label, "Gothic A1:700", 14), width(sub or "", "IBM Plex Mono:400", 11)) + 60
        h = 58 if sub else 42
        s.glass(cx - w / 2, cy - h / 2, w, h, r=14)
        s.add(f'<circle cx="{cx - w / 2 + 16}" cy="{cy - (8 if sub else 0)}" r="3.5" fill="{col}"/>')
        s.t(cx - w / 2 + 28, cy + (-3 if sub else 5), label, 14, "Gothic A1:700", TEXT)
        if sub:
            s.t(cx - w / 2 + 28, cy + 17, sub, 11, "IBM Plex Mono:400", TEXT3)
        return w

    def edge(x1, y1, x2, y2, label=None, col=TEXT2, lx=None, ly=None, dashed=False):
        d = ' stroke-dasharray="4 5"' if dashed else ""
        s.add(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{col}" stroke-opacity="0.75" stroke-width="1.4" marker-end="url(#ar)"{d}/>')
        if label:
            # Étiquette au milieu, décalée perpendiculairement à la flèche, côté haut.
            mx, my = (x1 + x2) / 2, (y1 + y2) / 2
            L = math.hypot(x2 - x1, y2 - y1) or 1
            nx, ny = (y2 - y1) / L, -(x2 - x1) / L
            if ny > 0:
                nx, ny = -nx, -ny
            hw = width(label, "IBM Plex Mono:400", 11) / 2
            off = 12 + hw * abs(y2 - y1) / L
            s.t(mx + nx * off, my + ny * off + 4, label, 11, "IBM Plex Mono:400", ACCENT_TEXT, anchor="middle")

    # Ligne 1 : combustible → réacteur → vapeur → turbine → énergie
    y1, y2 = 190, 350
    wf = node(130, y1, "Combustible fissile", "objectif 2", SUCCESS)
    we = node(130, y2, "Eau", "pompe électrique", ACCENT)
    wr = node(430, 270, "Réacteur à fission", "[règles 3 et 4]", WARNING)
    wt = node(730, y1, "Turbine", "100 mB vapeur → 1000 J", ACCENT)
    wa = node(730, y2, "Activateur neutronique", "100 mB déchets → 10 Po", LAVENDER)
    wd = node(1060, 270, "Déphaseur supercritique", "2000 J/tick par bobine", LAVENDER)
    node(1060, 400, "Pastille d'antimatière", "objectif 3", LAVENDER)
    m = 8  # marge entre flèche et nœud
    rl, rr = 430 - wr / 2 - m, 430 + wr / 2 + m
    edge(130 + wf / 2 + m, y1 + 6, rl, 256, "brûlé")
    edge(130 + we / 2 + m, y2 - 6, rl, 286, "refroidit")
    edge(rr, 256, 730 - wt / 2 - m, y1 + 6, "vapeur")
    edge(rr, 286, 730 - wa / 2 - m, y2 - 6, "déchets")
    edge(730 + wt / 2 + m, y1 + 6, 1060 - wd / 2 - m, 256, "énergie [règle 1]")
    edge(730 + wa / 2 + m, y2 - 6, 1060 - wd / 2 - m, 286, "polonium [règle 2]")
    edge(1060, 300, 1060, 370)
    # Retour : l'énergie de la turbine alimente aussi le contrôle du réacteur
    s.add(f'<path d="M700,{y1 - 30} C620,120 480,140 450,240" fill="none" stroke="{ACCENT}" stroke-opacity="0.5" '
          f'stroke-width="1.2" stroke-dasharray="4 5" marker-end="url(#ar)"/>')
    s.t(590, 128, "contrôle du réacteur", 11, "IBM Plex Mono:400", TEXT3, anchor="middle")
    s.close_sky()
    s.save("chain.svg")


# ================= 4. Objectifs =================
def objectives():
    W, H = 1280, 300
    s = Svg(W, H, "Trois objectifs de difficulté croissante")
    s.sky(seed=31, density=3600, constellation=False)
    s.t(40, 64, "Trois objectifs, trois façons de fabriquer", 26, "Gothic A1:700", TEXT)
    items = [
        ("Lingot d'acier", "×4", "Fer + charbon au four. Broyer d'abord double le rendement.", 1, "Découvrir les règles"),
        ("Combustible fissile", "1000 mB", "Chaîne chimique du soufre et de l'uranium ; l'infuseur sert deux fois.", 2, "Ordre et sous-produits"),
        ("Pastille d'antimatière", "×1", "Trouver le régime stable du réacteur qui alimente le déphaseur.", 3, "L'énigme"),
    ]
    cw, gap = 386, 21
    for i, (title, qty, body, lvl, tag) in enumerate(items):
        x, y = 40 + i * (cw + gap), 92
        s.glass(x, y, cw, 172)
        s.t(x + 24, y + 40, title, 18, "Gothic A1:700", TEXT)
        s.t(x + cw - 24, y + 42, qty, 22, "Barlow Condensed:600", ACCENT_TEXT, anchor="end")
        for k in range(3):
            s.add(f'<circle cx="{x + 30 + k * 16}" cy="{y + 64}" r="4.5" fill="{ACCENT if k < lvl else "rgba(232,238,250,0.12)"}"/>')
        s.t(x + 84, y + 68, tag, 12, "Gothic A1:400", TEXT3)
        for k, line in enumerate(wrap(body, "Gothic A1:400", 14, cw - 48)):
            s.t(x + 24, y + 104 + k * 21, line, 14, "Gothic A1:400", TEXT2)
        if i < 2:
            s.add(f'<path d="M{x + cw + 4},{y + 86} l{gap - 8},0" stroke="{TEXT3}" stroke-width="1.2"/>')
    s.close_sky()
    s.save("objectives.svg")


banner()
rules()
chain()
objectives()
print("ok")
