"""Package a local visual review index; it performs no install or publication."""
from pathlib import Path
import html
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
VERSION = json.loads((ROOT / 'package/manifest.json').read_text())['version_number']
OUT = ROOT / f'dist/IvoryBastion-{VERSION}-review'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for required in [OUT / 'REVIEW.txt', OUT / 'audio-mix.wav',
                     ROOT / f'dist/IvoryBastion-{VERSION}.zip']:
        if not required.is_file():
            raise FileNotFoundError(required)
    hud = ROOT / f'dist/IvoryBastion-{VERSION}-hud-edges.png'
    if hud.exists():
        shutil.copy2(hud, OUT / 'hud-edges.png')
    cards = [
        ('Seraph armor and materials', 'models/mobile-front.png',
         'Actual exported meshes, identical camera and lighting. Layered helmet, shoulders and gauntlets; distinct ceramic and metal response.'),
        ('Armor close-up', 'models/mobile-armor-detail.png',
         'The approved body proportions and rig anchors are retained.'),
        ('Fortress and missile doors', 'models/siege-front.png',
         'Dimensional forward-facing doors, raised throne and the established compact rear launchers.'),
        ('Missile bank details', 'models/siege-battery-detail.png',
         'Inset armor, mechanical frames, hinge hardware and restrained blue indicators.'),
        ('Rear gameplay view', 'models/mobile-gameplay-distance.png',
         'Same-scale distant comparison; this view shows how much detail survives outside a close-up.'),
        ('Slanted HUD edge quality', 'hud-edges.png',
         'Shared runtime triangles at three scales and angles. Edge coverage smooths stairs while preserving the slant, colors and sharp interiors.'),
        ('Movement comparison', 'animation/motion-before-after.png',
         'Compiled choreography on the same mesh isolates motion changes from model changes.'),
        ('Flight and Blue Shift', 'animation/flight-before-after.gif',
         'Acceleration, braking and the gather/release sequence. Offline compiled-pose study.'),
        ('Cannon recoil and cooling', 'animation/lance-before-after.gif',
         'Fast breech response, controlled recovery and outward-opening thermal fins.'),
        ('Individual battery recoil', 'animation/battery-before-after.gif',
         'Each bank reacts to its own launches.'),
        ('Energy surfaces', 'animation/energy-surfaces-before-after.png',
         'Actual falloff and ribbon geometry; Unity bloom and scene lighting are not reproduced.'),
        ('Missile blast readability', 'animation/explosions-before-after.png',
         'Actual textures and particle formulas. Blast footprints remain full size; overlapping aftermath is less opaque.'),
    ]
    body = []
    for title, path, detail in cards:
        if not (OUT / path).is_file():
            raise FileNotFoundError(OUT / path)
        body.append(f'<section><h2>{html.escape(title)}</h2><p>{html.escape(detail)}</p>'
                    f'<a href="{path}"><img src="{path}" loading="lazy" alt="{html.escape(title)}"></a></section>')
    page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Ivory Bastion — local review</title><style>
:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#0a1018;color:#e8f0f9;font:16px/1.55 system-ui,sans-serif}
main{max-width:1280px;margin:auto;padding:36px 22px}h1{font-size:clamp(28px,4vw,46px);margin:0 0 10px}h2{margin:0;font-size:23px}
p{color:#acbfd1;margin:8px 0 20px}a{color:#7cc9ff}nav{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0 38px}nav a{padding:11px 18px;border:1px solid #375974;border-radius:6px;text-decoration:none;background:#112334}
section{padding:24px 0 34px;border-top:1px solid #263b4d}img{display:block;width:100%;height:auto;border:1px solid #243547;border-radius:6px}audio{width:min(600px,100%)}small{color:#8da6be}
</style><main><small>LOCAL REVIEW • NOT PUBLISHED</small><h1>Ivory Bastion ''' + VERSION + '''</h1>
<p>A visual and reliability pass over the published 0.32.0 baseline. Open any comparison at full resolution. Model renders approximate the game's materials; they are actual exported geometry, not concept art or game captures.</p>
<nav><a href="../IvoryBastion-''' + VERSION + '''.zip">Review mod ZIP</a><a href="REVIEW.txt">Changes and validation</a><a href="audio-mix.wav">Audio mix WAV</a></nav>
<section><h2>Audio mix</h2><p>Original voice and sound recordings through the production mixer: weapon traffic, dialogue, engine and a shield warning.</p><audio controls preload="none" src="audio-mix.wav"></audio></section>
''' + ''.join(body) + '''<section><h2>Validation scope</h2><p>Offline production-code checks, installed-game method compatibility, asset and pose checks, and visual inspection. Live game, multiplayer transport, driver playback and GPU behavior still require your review in game.</p></section></main></html>'''
    receipt = ROOT / f'dist/IvoryBastion-{VERSION}-upload.json'
    if receipt.is_file() and json.loads(receipt.read_text())['version'] == VERSION:
        page = page.replace('LOCAL REVIEW • NOT PUBLISHED', 'APPROVED • PUBLISHED RELEASE')
        page = page.replace('Ivory Bastion — local review', 'Ivory Bastion — release comparisons')
        page = page.replace('Review mod ZIP', 'Release mod ZIP')
    (OUT / 'index.html').write_text(page, encoding='utf-8')
    print(OUT / 'index.html')


if __name__ == '__main__':
    main()
