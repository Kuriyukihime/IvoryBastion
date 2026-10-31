"""Offline layout study: runtime module sizes, resource palette and shipped art.

Native UI is represented schematically. This does not launch Unity or claim an
in-game capture. The detailed sheet checks typography at native module scale.
"""
from pathlib import Path
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DATA = json.loads((ROOT / '.build/hud-modules.json').read_text(encoding='utf-8'))
VERSION = json.loads((ROOT / 'package/manifest.json').read_text(encoding='utf-8'))['version_number']
S = 3
WHITE = (237, 246, 255, 255)
BLUE = (59, 157, 255, 255)
QUIET = (143, 173, 201, 255)
READY = (140, 224, 255, 255)
GOLD = (255, 200, 74, 255)
ISSUES = []


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('segoeuib.ttf' if bold else 'segoeui.ttf'), round(size * S))


def text(im, value, x, y, width, height, size, color=WHITE, bold=False, align='left'):
    d = ImageDraw.Draw(im)
    f = font(size, bold)
    if d.textlength(value, font=f) > width * S:
        f = font(size - 1, bold)
    if d.textlength(value, font=f) > width * S + 1:
        ISSUES.append({'text': value, 'width': width})
    px = x - width / 2 if align == 'left' else x + width / 2 if align == 'right' else x
    anchor = {'left': 'lm', 'right': 'rm', 'center': 'mm'}[align]
    d.text((round(px * S), round(y * S)), value, font=f, fill=color, anchor=anchor)


def rect(im, box, color):
    ImageDraw.Draw(im).rectangle(tuple(round(v * S) for v in box), fill=color)


def art(im, name, x, y, w, h):
    asset = Image.open(ROOT / f'assets/{name}.png').convert('RGBA')
    asset.thumbnail((round(w * S), round(h * S)), Image.Resampling.LANCZOS)
    im.alpha_composite(asset, (round(x * S - asset.width / 2), round(y * S - asset.height / 2)))


def hexagon(im, x, y, radius, tint, stroke=1):
    points = [(round((x + math.cos(i * math.pi / 3) * radius) * S),
               round((y + math.sin(i * math.pi / 3) * radius) * S)) for i in range(6)]
    ImageDraw.Draw(im).line(points + [points[0]], fill=tint, width=max(1, round(stroke * S)))


def meter(im, kind, x, y, w, h, fraction, cells=1):
    tint = tuple(DATA['colors'][kind])
    low = tuple(round(c * .75) for c in tint[:3]) + (255,)
    high = tuple(round(c + (255 - c) * .18) for c in tint[:3]) + (255,)
    filled = w * fraction
    cells = min(64, max(1, cells))
    cell = w / cells
    gap = min(.8, cell * .15) if cells > 1 else 0
    for i in range(cells):
        left, right = x + i * cell, x + min(filled, (i + 1) * cell - gap)
        if right <= left:
            continue
        rect(im, (left, y, right, y + h), low)
        rect(im, (left, y, right, y + .65), high)
    if filled:
        cap = tuple(round(c + (255 - c) * .4) for c in tint[:3]) + (255,)
        rect(im, (x + max(0, filled - .8), y, x + filled, y + h), cap)


def chrome(w, h, aegis=False):
    im = Image.new('RGBA', (round(w * S), round(h * S)))
    d = ImageDraw.Draw(im)
    points = [(0, h), (w, h), (w, 0), (0, 0)]
    d.polygon([(round(x * S), round(y * S)) for x, y in points], fill=(7, 13, 22, 247),
              outline=(157, 191, 222, 210), width=S)
    accent = GOLD if aegis else BLUE
    rect(im, (3, 1, 95, 4), WHITE)
    rect(im, (100, 1, w - 14, 3), accent)
    for x in range(3):
        for y in range(3):
            hexagon(im, 24 + x * 13, h - (24 + y * 15 + (x % 2) * 7.5), 8,
                    (23, 41, 63, 150), .7)
    hexagon(im, w - 13, 13, 4.5, (220, 242, 255, 190), .85)
    rect(im, (w - 7, h - 42, w - 6, h - 18), accent)
    return im


def combat(siege=False, aegis=False, exhausted=False):
    w, h = DATA['combatWidth'], DATA['combatHeight']
    im = chrome(w, h, aegis)
    def label(value, x, y, width, height, size, color=WHITE, bold=False):
        text(im, value, w / 2 + x, h / 2 - y, width, height, size, color, bold)
    label('SIEGE / MISSILE BATTERY' if siege else 'FLIGHT / THRUSTER RESERVE', 0, 51, 304, 18, 12.5)
    art(im, 'hud_siege' if siege else 'hud_mobile', w / 2 - 124, h / 2 - 6, 60, 64)
    label('32 / 40' if siege else '00%' if exhausted else '86%', 36, 26, 232, 29, 25, tuple(DATA['colors']['missiles' if siege else 'fuel']))
    rect(im, (w / 2 + 36 - 116, h / 2 - 10, w / 2 + 36 + 116, h / 2 - 2), (20, 43, 69, 255))
    if not exhausted:
        meter(im, 'missiles' if siege else 'fuel', w / 2 + 36 - 115.25, h / 2 - 9.25, 230.5, 6.5, .8 if siege else .86, 40 if siege else 1)
    label('RELOAD 2/s  /  HEAVY IN 1' if siege and aegis else 'RELOAD 1/s  /  HEAVY IN 1' if siege
          else 'RELEASE SECONDARY TO REARM' if exhausted else 'MISSILE STOCKPILE 30/40', 36, -10, 232, 16, 10.5, QUIET)
    rect(im, (w / 2 + 36 - 116, h / 2 + 23.5, w / 2 + 36 + 116, h / 2 + 28.5), (20, 43, 69, 255))
    meter(im, 'heavy' if siege else 'missiles', w / 2 + 36 - 115.25, h / 2 + 24.25, 230.5, 3.5, .875 if siege else .75, 8 if siege else 40)
    rect(im, (16, h / 2 + 36.5, w - 16, h / 2 + 37.5), (55, 89, 119, 100))
    label('LANCE LOCKED  /  AEGIS 5.8s' if aegis else 'LANCE RECHARGE 4.2s' if siege
          else 'THRUSTERS EXHAUSTED' if exhausted else 'FLIGHT SYSTEMS READY', 0, -49, 304, 18, 10.5, GOLD if aegis else READY)
    return im


def factory(siege=False, paused=False, complete=False, available=False):
    w, h = DATA['factoryWidth'], DATA['factoryHeight']
    im = chrome(w, h)
    def label(value, x, y, width, height, size, color=WHITE, align='left'):
        text(im, value, w / 2 + x, h / 2 - y, width, height, size, color, align=align)
    art(im, 'factory', w / 2 - 85, h / 2 - 29, 21, 21)
    label('FABRICATION', 21, 29, 160, 18, 11)
    label('182 FAB' if available else '82 FAB', -35, 6, 124, 27, 22)
    label('PAUSED' if paused else '14–35/min' if siege else 'STANDBY', 67, 6, 62, 24, 10, QUIET, 'right')
    label('ALL SYSTEMS UPGRADED' if complete else 'UPGRADE AVAILABLE' if available else 'NEXT UPGRADE  140 FAB',
          0, -12, 196, 16, 10, READY if available else QUIET)
    rect(im, (14, h / 2 + 21.5, w - 14, h / 2 + 26.5), (20, 43, 69, 255))
    meter(im, 'fabrication', 14.75, h / 2 + 22.25, 194.5, 3.5, 1 if complete or available else .6)
    label('[V] ENGINEERING', 0, -36, 196, 16, 10.5, READY)
    return im


def paste(im, module, x, y):
    im.alpha_composite(module, (round(x * S), round(y * S)))


scene = Image.new('RGBA', (1280 * S, 720 * S), (15, 24, 34, 255))
text(scene, 'IVORY BASTION / MODULAR HUD', 640, 25, 1100, 32, 19, WHITE, True, 'center')
text(scene, 'Offline layout study with schematic native UI; not an in-game screenshot', 640, 52, 1100, 20, 11, QUIET, align='center')
# Native currencies and ally list remain unobstructed.
for index, (value, color) in enumerate([('$ 260', GOLD), ('37', READY), ('3', (198, 123, 248, 255))]):
    text(scene, value, 135, 88 + index * 25, 160, 22, 17, color)
combat_scale = 416 / DATA['combatWidth']
factory_scale = combat_scale * DATA['factoryScale']
factory_width = DATA['factoryWidth'] * factory_scale
factory_height = DATA['factoryHeight'] * factory_scale
factory_x = 840 - DATA['moduleGap'] * combat_scale - factory_width
factory_y = 552 - factory_height
factory_view = factory(True)
factory_view = factory_view.resize((round(factory_width * S), round(factory_height * S)), Image.Resampling.LANCZOS)
paste(scene, factory_view, factory_x, factory_y)
text(scene, 'PRODUCTION / ENGINEERING', factory_x + factory_width / 2, factory_y - 20, factory_width + 24, 20, 10, QUIET, align='center')
text(scene, 'Bulwark Sentinel', 166, 185, 224, 20, 13, WHITE)
rect(scene, (55, 201, 260, 209), (116, 185, 65, 255))
combat_view = combat(True, True)
combat_view = combat_view.resize((round(416 * S), round(DATA['combatHeight'] * combat_scale * S)), Image.Resampling.LANCZOS)
paste(scene, combat_view, 840, 552 - DATA['combatHeight'] * combat_scale)
text(scene, 'COMBAT / RESOURCES', 1048, 365, 416, 20, 10, QUIET, align='center')
for i, name in enumerate(['laser', 'protocol', 'deploy', 'saturation']):
    art(scene, name, 878 + i * 82, 632, 77, 77)
    text(scene, ['M1', 'M2', 'SHIFT', 'R'][i], 878 + i * 82, 685, 84, 18, 12, QUIET, align='center')
rect(scene, (1210, 609, 1256, 655), (8, 14, 22, 255))
text(scene, 'Q', 1233, 685, 46, 18, 12, QUIET, align='center')
# Schematic native health/XP with existing separate defense gauges.
text(scene, 'Lv. 18', 90, 580, 76, 20, 13, WHITE)
rect(scene, (140, 577, 432, 586), (130, 195, 204, 255))
text(scene, 'SERAPH 340 / 500', 155, 605, 206, 18, 10, WHITE)
text(scene, 'AEGIS 320', 367, 605, 130, 18, 10, GOLD, align='right')
rect(scene, (52, 617, 432, 622), GOLD)
rect(scene, (52, 625, 432, 634), WHITE)
for i in range(40):
    hexagon(scene, 55 + i * 9.5, 629.5, 4.2, (35, 112, 221, 230), .7)
rect(scene, (52, 641, 432, 676), (105, 175, 50, 255))
text(scene, '1000 / 1000', 242, 659, 340, 30, 16, WHITE, True, 'center')
text(scene, 'DEFENSE / HEALTH', 242, 698, 380, 18, 10, QUIET, align='center')
text(scene, '+', 640, 360, 50, 50, 20, (150, 176, 198, 140), align='center')

detail = Image.new('RGBA', (1030 * S, 440 * S), (15, 24, 34, 255))
text(detail, 'RESOURCE MODULES / NATIVE UI SCALE', 515, 26, 970, 30, 18, WHITE, True, 'center')
for x, module, caption in [(28, combat(False), 'FLIGHT'), (380, combat(True, True), 'SIEGE / AEGIS WINDOW')]:
    paste(detail, module, x, 92)
    text(detail, caption, x + 168, 70, 336, 18, 11, QUIET, align='center')
paste(detail, factory(True, available=True), 760, 92)
text(detail, 'FABRICATION', 872, 70, 224, 18, 11, QUIET, align='center')
paste(detail, combat(False, exhausted=True), 28, 277)
paste(detail, factory(True, paused=True), 400, 277)
paste(detail, factory(True, complete=True), 760, 277)
text(detail, 'EXHAUSTED', 196, 255, 336, 18, 11, QUIET, align='center')
text(detail, 'PAUSED', 512, 255, 224, 18, 11, QUIET, align='center')
text(detail, 'UPGRADES COMPLETE', 872, 255, 224, 18, 11, QUIET, align='center')
text(detail, 'Shipped artwork and resource colors; approximate font metrics. Native HUD shown schematically.', 515, 426, 970, 18, 10, QUIET, align='center')

for suffix, im in [('modular-hud', scene), ('hud-modules', detail)]:
    out = ROOT / f'dist/IvoryBastion-{VERSION}-{suffix}.png'
    rendered = im.convert('RGB').resize((im.width // S, im.height // S), Image.Resampling.LANCZOS)
    try:
        rendered.save(out)
    except OSError:
        # Windows previewers can hold the preceding review image open.
        if not out.exists():
            raise
        out = out.with_stem(out.stem + '-updated')
        rendered.save(out)
    print(out)
(ROOT / '.build/modular-hud-layout-review.json').write_text(json.dumps(ISSUES, indent=2), encoding='utf-8')
if ISSUES:
    raise RuntimeError(ISSUES)
