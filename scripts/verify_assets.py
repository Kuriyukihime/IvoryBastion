from pathlib import Path
import struct, math, json
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
for path in (ROOT/'assets').glob('*.meshbin'):
    f=path.open('rb');fmt=f.read(5);assert fmt in [b'BAST1',b'BAST2',b'BAST3'];four=fmt==b'BAST3';textured=four or fmt==b'BAST2'
    def integer():return struct.unpack('<i',f.read(4))[0]
    def string():return f.read(f.read(1)[0]).decode()
    bones=integer()
    for _ in range(bones):string();assert all(map(math.isfinite,struct.unpack('<3f',f.read(12))));integer()
    vertices=triangles=0
    for _ in range(integer()):
        name=string();assert all(map(math.isfinite,struct.unpack('<5f',f.read(20))));nv=integer();ni=integer();assert nv>0 and ni%3==0
        for _ in range(nv):
            if four:
                data=struct.unpack('<8f4i4f',f.read(64));values=data[:8];ids=data[8:12];weights=data[12:]
                assert all(0<=b<bones for b in ids) and all(0<=w<=1.00001 for w in weights) and abs(sum(weights)-1)<.0001
            else:
                *values,b0,b1,w=struct.unpack('<8fiif' if textured else '<6fiif',f.read(44 if textured else 36));assert 0<=b0<bones and 0<=b1<bones and 0<=w<=1
            assert all(map(math.isfinite,values))
            if textured:assert all(-.00001<=u<=1.00001 for u in values[6:8]), (path.name,'UV outside atlas')
            normal=values[3:6];assert .8<sum(x*x for x in normal)<1.2, (path.name,name,'invalid vertex normal')
        ids=struct.unpack('<'+str(ni)+'i',f.read(ni*4));assert min(ids)>=0 and max(ids)<nv;vertices+=nv;triangles+=ni//3
    assert f.read()==b'';print(f'PASS {path.name}: {bones} bones, {vertices:,} vertices, {triangles:,} triangles')
for path in (ROOT/'assets').glob('*.png'):
    im=Image.open(path);assert im.width>=256 and im.height>=256;im.verify()
assert Image.open(ROOT/'package/icon.png').size==(256,256)
atlas=Image.open(ROOT/'assets/missile_bluefire.png').convert('RGBA')
assert atlas.size==(2048,1024), 'Explosion flipbook must be eight columns by four rows'
yy,xx=np.mgrid[-1:1:256j,-1:1:256j];outside=xx*xx+yy*yy>=1
for i in range(32):
    tile=np.array(atlas.crop(((i%8)*256,(i//8)*256,(i%8+1)*256,(i//8+1)*256)))
    assert tile[:,:,3].max()>200, 'Explosion frame is empty'
    assert not np.any(tile[:,:,3][outside]), 'Explosion texture exceeds the radius used by the geometry'
for name in ['smoke','ember','flash']:
    tile=np.array(Image.open(ROOT/f'assets/missile_{name}.png').convert('RGBA'))
    assert tile.shape==(256,256,4) and not np.any(tile[:,:,3][outside]), 'Particle sprite exceeds its circular support'
print('PASS explosion atlas layout, all 32 frames and particle-radius masks.')
manifest=json.loads((ROOT/'package/manifest.json').read_text(encoding='utf-8-sig'));assert manifest['name']=='IvoryBastion'
required={'bbepis-BepInExPack','RiskofThunder-RoR2BepInExPack','RiskofThunder-HookGenPatcher',
          'RiskofThunder-R2API_Core','RiskofThunder-R2API_ContentManagement','RiskofThunder-R2API_Prefab',
          'RiskofThunder-R2API_Language','RiskofThunder-R2API_RecalculateStats','RiskofThunder-R2API_Networking',
          'Rune580-Risk_Of_Options'}
dependencies=[entry.rsplit('-',1)[0] for entry in manifest['dependencies']]
assert required.issubset(dependencies), 'A required runtime or settings-menu dependency is missing'
assert len(dependencies)==len(set(dependencies)), 'Duplicate package dependency'
print('PASS icons, package identity and dependency manifest.')
