"""Offline study using the actual siege mesh and exported runtime shield geometry."""
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from preview_shield import ROOT, VERSION, model_panel, shield_layer


def main():
    inner = json.loads((ROOT / '.build/shield-geometry.json').read_text())
    outer = json.loads((ROOT / '.build/aegis-hex-geometry.json').read_text())
    width, height = 2100, 1000
    canvas = Image.new('RGB', (width, height), (12, 19, 31))
    half = outer['outerRadius']*1.12
    base = model_panel('siege', (1360,1360), half)
    for panel in range(3):
        layers = []
        if panel != 2:
            layers.append((np.array(inner['normals'])*outer['innerRadius'], np.array(inner['normals']), np.array(inner['triangles']).reshape(-1, 3), np.array(inner['calm'])))
        if panel != 0:
            layers.append((np.array(outer['vertices'])*outer['outerRadius'], np.array(outer['normals']), np.array(outer['triangles']).reshape(-1, 3), np.array(outer['colors'])))
        image = base.copy()
        for vertices, normals, triangles, colors in layers:
            image = shield_layer(image,vertices,normals,triangles,colors,half)
        canvas.paste(image.resize((680,680),Image.Resampling.LANCZOS),(10+panel*700,155))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 27)
    small = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 20)
    draw.text((30, 24), 'IVORY BASTION 0.8 / SIEGE SHIELD + AEGIS FORTIFICATION', font=font, fill=(224, 236, 255))
    draw.text((30, 66), 'Current textured BAST3 model + actual runtime shield geometry and vertex colors. Offline neutral-pose study.', font=small, fill=(132, 160, 195))
    for i, label in enumerate(('Innate shield / blue', 'Shield + Aegis / blue and yellow', 'Shield broken / Aegis remains')):
        draw.text((75+i*700, 875), label, font=small, fill=(220, 224, 237))
    draw.text((30, 950),'Separate spherical layers; no model outline or coating. Unity lighting, bloom, depth sorting and motion are not reproduced.',font=small,fill=(132,160,195))
    target = ROOT / f'dist/IvoryBastion-{VERSION}-siege-shield-preview.png'
    canvas.save(target)
    print(target)


if __name__ == '__main__': main()
