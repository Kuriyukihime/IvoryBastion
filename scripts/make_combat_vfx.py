"""Render an original explosion volume: blue-hot combustion gases cooling into soot.

This is a CPU volume render, not a photograph or a game capture. Rebuilding only
these textures leaves the approved voice, audio, models and missile trails alone.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, map_coordinates

ROOT = Path(__file__).resolve().parents[1]
SIZE, VOLUME, FRAMES = 256, 128, 32
COLS, ROWS = 8, 4


def noise_volume(seed):
    rng = np.random.default_rng(seed)
    result = np.zeros((VOLUME,) * 3, np.float32)
    for blur, weight in [(13, .55), (6, .40), (2.7, .24), (1.1, .10)]:
        n = gaussian_filter(rng.standard_normal(result.shape, dtype=np.float32), blur, mode='wrap')
        result += n * (weight / n.std())
    return result


def render_frames():
    grid = np.linspace(-1, 1, VOLUME, dtype=np.float32)
    z, y, x = np.meshgrid(grid, -grid, grid, indexing='ij')
    radius = np.sqrt(x*x + y*y + z*z)
    noise = noise_volume(7221)
    detail = noise_volume(8211)
    # Intersecting lobes form one billowing volume rather than separate flame tongues.
    lobes = [(0, 0, 0, .66)]
    for i in range(19):
        h = 1 - 2 * (i + .5) / 19
        a = i * 2.399963
        r = math.sqrt(1 - h*h)
        lobes.append((r*math.cos(a)*.59, h*.59, r*math.sin(a)*.59, .32 + .045*math.sin(i*3.7)))
    frames = []
    for frame in range(FRAMES):
        t = frame / (FRAMES - 1)
        coords = [((z+1)*.5*(VOLUME-1)+t*9) % VOLUME,
                  ((y+1)*.5*(VOLUME-1)-t*14) % VOLUME,
                  ((x+1)*.5*(VOLUME-1)+t*5) % VOLUME]
        turbulence = map_coordinates(noise, coords, order=1, mode='wrap')
        fine = map_coordinates(detail, [c*1.7 for c in coords], order=1, mode='wrap')
        xx = x + .035*turbulence
        yy = y + .027*fine
        zz = z + .03*turbulence
        field = np.full_like(x, -2)
        for i, (cx, cy, cz, r) in enumerate(lobes):
            turn = .12*t
            lx = cx*math.cos(turn) - cz*math.sin(turn)
            lz = cx*math.sin(turn) + cz*math.cos(turn)
            wave = .018*math.sin(i*2.1 + t*4)
            sphere = r + wave - np.sqrt((xx-lx)**2+(yy-cy)**2+(zz-lz)**2)
            field = np.maximum(field, sphere)
        field += .037*turbulence + .018*fine
        # The sphere is also the gameplay radius boundary. Never draw gas outside it.
        density = np.clip(field/.09, 0, 1) * np.clip((.995-radius)/.035, 0, 1)
        density *= np.clip(.75 + .3*turbulence, .2, 1.2)
        dz, dy, dx = np.gradient(gaussian_filter(density, 1.1))
        norm = np.sqrt(dx*dx + dy*dy + dz*dz) + 1e-4
        light = np.clip((-dx*.35 + dy*.7 - dz*.62)/norm, -.65, 1)
        smoke_shade = .08 + .13*(light*.5+.5) + .035*np.clip(fine, -1, 1)
        smoke = np.stack([smoke_shade*.67, smoke_shade*.82, smoke_shade*1.10], -1)
        # Seams stay hot while exposed cauliflower lobes char from the outside.
        cooling = max(0, t-.10) * 1.28
        heat = np.clip(.77 - cooling + .22*turbulence + .15*fine + .23*(.65-radius), 0, 1)
        heat *= np.clip(1.25-t*.62, 0, 1)
        knots = [0, .18, .36, .52, .70, .86, 1]
        palette = np.array([[.006,.012,.04], [.008,.03,.24], [.012,.13,.68],
                            [.025,.39,1], [.14,.68,1], [.53,.88,1], [.94,.985,1]])
        hot = np.stack([np.interp(heat, knots, palette[:,c]) for c in range(3)], -1).astype(np.float32)
        fire = np.clip((heat-.20)*2.3, 0, 1)[...,None]
        colour = smoke*(1-fire) + hot*fire
        opacity = 1-np.exp(-density*(2/VOLUME)*23)
        transmission = np.concatenate([np.ones_like(opacity[:1]), np.cumprod(1-opacity[:-1], axis=0)], axis=0)
        weights = opacity*transmission
        alpha = weights.sum(axis=0)
        rgb = (colour*weights[...,None]).sum(axis=0) / np.maximum(alpha[...,None], 1e-5)
        rgba = np.uint8(np.clip(np.dstack([rgb, alpha]), 0, 1)*255)
        image = Image.fromarray(rgba).resize((SIZE,SIZE), Image.Resampling.LANCZOS)
        # Lanczos reconstruction must not put even faint pixels outside the sphere.
        yy2,xx2=np.mgrid[-1:1:complex(SIZE),-1:1:complex(SIZE)]
        clipped=np.array(image);clipped[xx2*xx2+yy2*yy2>=1,3]=0
        image=Image.fromarray(clipped)
        frames.append(image)
        print(f'Rendered explosion volume {frame+1}/{FRAMES}', flush=True)
    return frames


def main():
    frames = render_frames()
    atlas = Image.new('RGBA', (SIZE*COLS,SIZE*ROWS))
    for i, frame in enumerate(frames):
        atlas.paste(frame, ((i%COLS)*SIZE,(i//COLS)*SIZE))
    atlas.save(ROOT/'assets/missile_bluefire.png')
    frames[-1].save(ROOT/'assets/missile_smoke.png')
    y,x = np.mgrid[-1:1:complex(SIZE), -1:1:complex(SIZE)]
    radius = np.sqrt(x*x+y*y)
    ember_alpha = np.exp(-((x/.16)**2+(y/.6)**2)*2)*np.clip(1-radius,0,1)
    white = np.ones_like(x)
    Image.fromarray(np.uint8(np.dstack([white,white,white,ember_alpha])*255)).save(ROOT/'assets/missile_ember.png')
    # A filled flash, not a shock ring or an ion disc.
    flash_alpha = np.clip(1-radius,0,1)**2
    Image.fromarray(np.uint8(np.dstack([white,white,white,flash_alpha])*255)).save(ROOT/'assets/missile_flash.png')
    sheet = Image.new('RGB',(SIZE*4,SIZE*2+68),(17,21,28))
    draw = ImageDraw.Draw(sheet)
    draw.text((16,12),'BLUE COMBUSTION DETONATION / CPU-RENDERED VOLUME / NOT AN IN-GAME CAPTURE',fill=(219,233,249))
    for j,i in enumerate([0,3,6,10,15,20,25,31]):
        at=((j%4)*SIZE,68+(j//4)*SIZE)
        sheet.paste(frames[i],at,frames[i])
        draw.text((at[0]+12,at[1]+8),f'Frame {i+1:02d}',fill=(200,215,233))
    sheet.save(ROOT/'dist/IvoryBastion-0.1.34-blue-detonation-textures.png')
    frames[0].save(ROOT/'.build/blue-detonation-volume.webp',save_all=True,append_images=frames[1:],duration=50,loop=0,lossless=True)
    print('Saved 32-frame volume atlas, smoke, spark and flash textures.')


if __name__ == '__main__':
    main()
