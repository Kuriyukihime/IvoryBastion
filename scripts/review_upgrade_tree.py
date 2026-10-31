"""Offline preview of the real C# Engineering layout exported by utility_checks.

Segoe UI approximates TMP metrics. This is layout proof, not a game screenshot.
"""
from pathlib import Path
import json
import re
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'dist' / 'IvoryBastion-1.0.0-review'
OUT.mkdir(parents=True, exist_ok=True)
SCALE = 2
issues = []

def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', round(size*SCALE))

def wrap(text, face, width, enabled):
    lines = []
    for row in text.split('\n'):
        if not enabled:
            lines.append(row)
            continue
        line = ''
        for word in row.split():
            candidate = f'{line} {word}'.strip()
            if face.getlength(candidate) > width and line:
                lines.append(line)
                line = word
            else:
                line = candidate
        lines.append(line)
    return lines

for page in json.loads((ROOT/'.build/engineering-layout.json').read_text()):
    im = Image.new('RGB', (1100*SCALE, 750*SCALE), '#080e18')
    draw = ImageDraw.Draw(im)
    for e in page['Elements']:
        x = (550+e['X'])*SCALE
        y = (360-e['Y'])*SCALE
        w, h = e['Width']*SCALE, e['Height']*SCALE
        bounds = (x-w/2, y-h/2, x+w/2, y+h/2)
        rgb = tuple(round(v*255) for v in e['Color'][:3])
        if e['Name'] == 'Engineering':
            draw.rectangle(bounds, fill='#101c2a', outline='#48647a', width=2)
        elif e['Kind'] == 'button':
            draw.rectangle(bounds, fill='#17364b' if e['Enabled'] else '#162431', outline='#456077', width=1)
        elif e['Kind'] == 'image':
            if e['Asset']:
                asset=ROOT/'assets'/f"{e['Asset']}.png"
                if asset.exists():
                    icon=Image.open(asset).convert('RGBA')
                    icon.thumbnail((int(w),int(h)),Image.Resampling.LANCZOS)
                    im.paste(icon,(int(x-icon.width/2),int(y-icon.height/2)),icon)
            else:
                draw.rectangle(bounds, fill=rgb)
        elif e['Kind'] == 'text':
            text = re.sub(r'<[^>]*>', '', e['Text'] or '')
            size = e['Size']
            while True:
                face=font(size)
                lines=wrap(text,face,w,e['Wrap'])
                lineheight=face.size*1.12
                fits=max((face.getlength(t) for t in lines),default=0)<=w+1 and len(lines)*lineheight<=h+2
                if fits or size<=e['MinSize']+.01:
                    break
                size=max(e['MinSize'],size-.25)
            if not fits:
                issues.append({'page':page['Page'],'full':page['Full'],'text':text,'width':e['Width'],'height':e['Height']})
            top=y-len(lines)*lineheight/2
            for i,line in enumerate(lines):
                draw.text((x-w/2,top+i*lineheight),line,font=face,fill=rgb,anchor='lt')
    # Public readout omitted from the partial; draw the same clear top context.
    draw.text((65*SCALE,44*SCALE),'ENGINEERING  /  1000 FABRICATION',font=font(22),fill='#e6f3ff')
    draw.text((65*SCALE,74*SCALE),'Specialized paths · Shared Defense · Run-long progression',font=font(12),fill='#94afc4')
    draw.text((30*SCALE,721*SCALE),'Offline production-layout preview. Font and lighting approximate the game.',font=font(10),fill='#71869a')
    name=('siege','defense','mobile')[page['Page']]
    suffix='mastered' if page['Full'] else 'fresh'
    im.resize((1100,750),Image.Resampling.LANCZOS).save(OUT/f'engineering-{name}-{suffix}.png')

(ROOT/'.build/engineering-text-layout.json').write_text(json.dumps({'approximateFontClips':issues},indent=2))
print(f'Exported six production-layout previews; {len(issues)} approximate font clips.')
if issues:
    for issue in issues:
        print(issue)
    raise SystemExit(1)
