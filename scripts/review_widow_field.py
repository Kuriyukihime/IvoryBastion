"""Render a placement diagram from the actual C# upgrade/landing rules."""
from pathlib import Path
import json
import math
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'dist/IvoryBastion-1.0.0-review'

def generate():
    data = ROOT / '.build/widow-field-layout.json'
    subprocess.run(['dotnet', str(ROOT / 'scripts/utility_checks/bin/Release/net10.0/Checks.dll'),
                    '--mine-field-layout', str(data)], check=True)
    fields = json.loads(data.read_text(encoding='utf-8'))
    scale = 2
    image = Image.new('RGB', (1600 * scale, 980 * scale), '#09111c')
    draw = ImageDraw.Draw(image)
    font = lambda n: ImageFont.truetype('C:/Windows/Fonts/seguisb.ttf', n * scale)
    draw.text((40*scale, 24*scale), 'WIDOW ARRAY  /  THREE-RING DEPLOYMENT', font=font(29), fill='#e4f4ff')
    for panel, field in enumerate(fields):
        center = ((400 + panel * 800) * scale, 490 * scale)
        unit = 12 * scale
        xy = lambda x, z: (center[0] + x * unit, center[1] - z * unit)
        circle = lambda x, z, r: (x-r, z-r, x+r, z+r)
        draw.text(((40 + panel*800)*scale, 94*scale),
                  f"{field['Name'].upper()}  /  {field['Count']} MINES", font=font(23), fill='#93cfff')
        radii = [field['Radius'] * i / 3 for i in (1, 2, 3)]
        draw.text(((40 + panel*800)*scale, 130*scale),
                  'Rings: ' + ' / '.join(f'{r:g}m' for r in radii), font=font(19), fill='#9ab8ce')
        for p in field['Points']:
            x, z = xy(p['X'], p['Z'])
            draw.ellipse(circle(x, z, field['Blast'] * unit), outline='#20364b', width=scale)
        for radius in radii:
            draw.ellipse(circle(*center, radius*unit), outline='#47738f', width=scale*2)
        for p in field['Points']:
            x, z = xy(p['X'], p['Z'])
            for leg in range(8):
                a = leg * math.tau / 8
                draw.line((x+math.cos(a)*5*scale,z+math.sin(a)*5*scale,
                           x+math.cos(a)*11*scale,z+math.sin(a)*11*scale), fill='#7da9c9', width=scale*2)
            draw.ellipse(circle(x,z,5*scale), fill='#e4f4ff', outline='#43aaff', width=scale)
        draw.rounded_rectangle((center[0]-16*scale,center[1]-21*scale,
                                center[0]+16*scale,center[1]+21*scale), radius=4*scale,
                               fill='#172e42', outline='#e4f4ff', width=scale*2)
    draw.text((40*scale, 864*scale), '5 normal / 8 elite / 12 boss attached marks  |  48 active mines per owner', font=font(23), fill='#d7edff')
    draw.text((40*scale, 904*scale), 'Faint circles show 8m blast coverage. Mines must attach and receive an actual weapon hit.', font=font(20), fill='#9ab8ce')
    draw.text((40*scale, 938*scale), 'Flat-ground diagram from production rules; terrain can move landing points. Mine blasts never trigger other mines.', font=font(17), fill='#718fa7')
    OUT.mkdir(parents=True, exist_ok=True)
    target = OUT / 'widow-array-blast-layout.png'
    image.resize((1600,980), Image.Resampling.LANCZOS).save(target)
    print('Rendered production three-ring mine layout:', target)
    return target

if __name__ == '__main__':
    generate()
