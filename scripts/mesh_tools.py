"""Shared geometry and offline rendering helpers for the original Seraph models.

No downloaded models/textures or game files are used. The custom animation
driver poses a dedicated original skeleton. Requires numpy/Pillow.
"""
from pathlib import Path
import json
import math
import struct
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
BONES = []
BN = {}
ANCHORS = np.empty((0,3))
PALETTE = {}
PARTS = {}

def norm(v):
    v=np.asarray(v,dtype=float)
    return v/max(np.linalg.norm(v),1e-12)

def rigid(bone):
    return lambda p: [(BN[bone],1.)]

def chain_weight(names):
    ids=[BN[n] for n in names]
    def weights(p):
        # Project onto the chain; adjacent weights ensure smooth joint deformation.
        candidates=[]
        for a,b in zip(ids,ids[1:]):
            d=ANCHORS[b]-ANCHORS[a]
            t=float(np.clip(np.dot(p-ANCHORS[a],d)/np.dot(d,d),0,1))
            nearest=ANCHORS[a]+d*t
            candidates.append((np.linalg.norm(p-nearest),a,b,t))
        _,a,b,t=min(candidates)
        t=t*t*(3-2*t)
        return [(a,1-t),(b,t)]
    return weights



def limb_weight(names, radius=.045):
    """Rigid limb sections with blending only around the actual joints.

    Whole-segment interpolation incorrectly lets an elbow/wrist rotate the
    middle of the upper arm, producing curved, inflated sleeves in idle poses.
    """
    ids=[BN[name] for name in names]
    lengths=np.linalg.norm(np.diff(ANCHORS[ids],axis=0),axis=1)
    starts=np.r_[0.,np.cumsum(lengths)]
    def weights(p):
        candidates=[]
        for index,(a,b) in enumerate(zip(ids,ids[1:])):
            direction=ANCHORS[b]-ANCHORS[a]
            t=float(np.clip(np.dot(p-ANCHORS[a],direction)/np.dot(direction,direction),0.,1.))
            nearest=ANCHORS[a]+direction*t
            candidates.append((np.linalg.norm(p-nearest),starts[index]+t*lengths[index]))
        _,along=min(candidates)
        for joint in range(1,len(ids)):
            width=min(radius,lengths[joint-1]*.24)
            if joint<len(lengths): width=min(width,lengths[joint]*.24)
            if along<starts[joint]-width:
                return [(ids[joint-1],1.)]
            if along<=starts[joint]+width:
                t=float(np.clip((along-starts[joint]+width)/(2*width),0.,1.))
                t=t*t*(3-2*t)
                return [(ids[joint-1],1-t),(ids[joint],t)]
        return [(ids[-1],1.)]
    return weights

def add(material, vertices, faces, weights):
    part=PARTS[material]
    offset=len(part['v'])
    part['v'].extend([list(p) for p in vertices])
    part['t'].extend([[a+offset,b+offset,c+offset] for a,b,c in faces])
    part['w'].extend([weights(np.array(p)) for p in vertices])

def surface(material, grid, weights, wrap=True):
    """Outward facing rows running bottom-to-top, with radial x=cos,z=sin."""
    rows=len(grid); cols=len(grid[0]); faces=[]
    for y in range(rows-1):
        for x in range(cols if wrap else cols-1):
            a=y*cols+x; b=y*cols+(x+1)%cols
            c=a+cols; d=b+cols
            faces.extend([(a,c,b),(b,c,d)])
    add(material,np.array(grid).reshape((-1,3)),faces,weights)

def ellipsoid(material, center, radii, weights, rotation=None, sides=40, rings=24):
    center=np.array(center); radii=np.array(radii)
    rotation=np.eye(3) if rotation is None else rotation
    # Nonzero cap rings prevent zero-area triangles at duplicated sphere poles.
    grid=[]
    for j in range(rings+1):
        lat=-math.pi/2+math.pi*(.0001+(j/rings)*.9998)
        grid.append([center+rotation@(radii*np.array([math.cos(lat)*math.cos(a),math.sin(lat),math.cos(lat)*math.sin(a)])) for a in np.arange(sides)*math.tau/sides])
    surface(material,grid,weights)

def catmull(values, x):
    i=min(len(values)-2,int(x)); t=x-i
    p0=np.array(values[max(0,i-1)],dtype=float)
    p1=np.array(values[i],dtype=float); p2=np.array(values[i+1],dtype=float)
    p3=np.array(values[min(len(values)-1,i+2)],dtype=float)
    return .5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)

def tube(material, centers, radii, weights, sides=32, steps=10, front=(0,0,1)):
    centers=np.array(centers); radii=np.array(radii)
    grid=[]
    for i in range((len(centers)-1)*steps+1):
        t=i/steps
        c=catmull(centers,t); r=catmull(radii,t)
        before=catmull(centers,max(0,t-.001))
        after=catmull(centers,min(len(centers)-1,t+.001))
        axis=norm(after-before)
        u=norm(np.cross(axis,front)); v=norm(np.cross(u,axis))
        grid.append([c+u*max(.0005,r[0])*math.cos(a)+v*max(.0005,r[1])*math.sin(a) for a in np.arange(sides)*math.tau/sides])
    # Our row order runs along the axis; cross-section basis ensures outward winding.
    surface(material,grid,weights)

def curve(material, points, radius, weights, steps=10):
    tube(material,points,[(radius,radius)]*len(points),weights,sides=12,steps=steps,front=(0,1,.23))

def torus(material, center, major, minor, weights, normal=(0,0,1), squash=1.):
    normal=norm(normal); u=norm(np.cross((0,1,0),normal))
    if np.linalg.norm(u)<.1: u=norm(np.cross((1,0,0),normal))
    v=norm(np.cross(normal,u)); center=np.array(center)
    vertices=[]; faces=[]; sides=48; rings=12
    for i in range(sides):
        a=i*math.tau/sides; radial=u*math.cos(a)+v*math.sin(a)*squash
        for j in range(rings):
            b=j*math.tau/rings
            vertices.append(center+radial*(major+minor*math.cos(b))+normal*minor*math.sin(b))
    for i in range(sides):
        for j in range(rings):
            a=i*rings+j; b=((i+1)%sides)*rings+j
            c=i*rings+(j+1)%rings; d=((i+1)%sides)*rings+(j+1)%rings
            faces.extend([(a,b,c),(b,d,c)])
    add(material,vertices,faces,weights)

def armor_panel(material, outline, origin, weights, u=(1,0,0), v=(0,1,0),
                bulge=.008, curve_u=0., curve_v=0., thickness=.009, segments=None, radial_rings=None):
    """A closed, smoothly contoured plate with a narrow rolled bevel.

    Coordinates are in metres. Straight outline edges meet small rounded corners;
    front/back surfaces and sidewalls keep independent normals at their seams.
    Plates attach to one bone so rigid armor never bends like the undersuit.
    """
    origin=np.asarray(origin,dtype=float); u=norm(u); v=norm(v); normal=norm(np.cross(u,v))
    outline=np.asarray(outline,dtype=float); count=len(outline); path=[]
    for j,p in enumerate(outline):
        before=p+(outline[(j-1)%count]-p)*.10
        after=p+(outline[(j+1)%count]-p)*.10
        next_before=outline[(j+1)%count]+(p-outline[(j+1)%count])*.10
        for t in np.linspace(0,1,7,endpoint=False):
            path.append((1-t)**2*before+2*(1-t)*t*p+t*t*after)
        for t in np.linspace(0,1,7,endpoint=False): path.append(after*(1-t)+next_before*t)
    path=np.asarray(path+[path[0]])
    distances=np.r_[0.,np.cumsum(np.linalg.norm(np.diff(path,axis=0),axis=1))]
    extent=np.ptp(outline,axis=0).max()
    segments=segments or (16 if extent<.24 else 24 if extent<.5 else 40)
    samples=np.linspace(0,distances[-1],segments,endpoint=False)
    boundary=np.stack([np.interp(samples,distances,path[:,axis]) for axis in [0,1]],axis=1)
    # Maintain outward winding even when a mirrored outline is supplied.
    signed_area=np.sum(boundary[:,0]*np.roll(boundary[:,1],-1)-boundary[:,1]*np.roll(boundary[:,0],-1))
    if signed_area<0: boundary=boundary[::-1]

    def point(xy,r,back=False):
        x,y=xy
        bevel=min(.004,thickness*.33,extent*.025)*max(0.,(r-.92)/.08)**2
        height=bulge*.45*(1-r*r)-curve_u*x*x-curve_v*y*y-bevel
        if back: height-=thickness
        return tuple(origin+u*x+v*y+normal*height)

    rings=radial_rings or [.25,.60,.90,1.]
    for back in [False,True]:
        vertices=[point((0,0),0,back)]
        for r in rings: vertices.extend(point(p*r,r,back) for p in boundary)
        faces=[]
        for j in range(segments): faces.append((0,1+j,1+(j+1)%segments))
        for k in range(len(rings)-1):
            for j in range(segments):
                a=1+k*segments+j; b=1+k*segments+(j+1)%segments; c=a+segments; d=b+segments
                faces.extend([(a,c,b),(b,c,d)])
        if back: faces=[(a,c,b) for a,b,c in faces]
        add(material,vertices,faces,weights)
    vertices=[]; faces=[]
    for p in boundary: vertices.extend([point(p,1),point(p,1,True)])
    for j in range(segments):
        a=j*2; b=((j+1)%segments)*2
        faces.extend([(a,a+1,b),(b,a+1,b+1)])
    add(material,vertices,faces,weights)

def fitted_plate(outline, origin, weights, u=(1,0,0), v=(0,1,0), material='Armor',
                 bulge=.008, curve_u=0., curve_v=0., border=1.07):
    normal=norm(np.cross(u,v)); origin=np.asarray(origin,dtype=float)
    armor_panel('ArmorFrame',np.asarray(outline)*border,origin-normal*.005,weights,u,v,
                bulge,curve_u,curve_v,.012)
    armor_panel(material,outline,origin,weights,u,v,bulge,curve_u,curve_v,.009)

def hair_lock(points, widths, depths):
    """A clean tapered fringe lock with two distinct planes along its ridge."""
    points=np.asarray(points,dtype=float); frame=[]
    for i,p in enumerate(points):
        direction=points[min(i+1,len(points)-1)]-points[max(i-1,0)]
        across=norm((direction[1],-direction[0],0))
        frame.append((p-across*widths[i],p+np.array((0,0,depths[i])),p+across*widths[i]))
    for side in [0,1]:
        vertices=[tuple(p) for row in frame for p in row[side:side+2]]; faces=[]
        for i in range(len(points)-1):
            for f in [(i*2,i*2+1,i*2+2),(i*2+1,i*2+3,i*2+2)]:
                a,b,c=[np.asarray(vertices[k]) for k in f]
                faces.append(f if np.cross(b-a,c-a)[2]>0 else (f[0],f[2],f[1]))
        add('Hair',vertices,faces,rigid('head'))

def garment(material, profiles, weights, sides=48, steps=8, folds=0.):
    """Broad clothing volume with sparse, shallow drape folds, not anatomy."""
    grid=[]
    for j in range((len(profiles)-1)*steps+1):
        y,rx,rz,zoff=catmull(profiles,j/steps)
        row=[]
        for a in np.arange(sides)*math.tau/sides:
            drape=1+folds*math.cos(a*6+y*2)*math.sin(math.pi*j/((len(profiles)-1)*steps))**2
            row.append((rx*math.cos(a)*drape,y,rz*math.sin(a)*drape+zoff))
        grid.append(row)
    surface(material,grid,weights)



def normals(vertices, triangles):
    vertices=np.array(vertices,dtype=float); triangles=np.array(triangles,dtype=int)
    a,b,c=vertices[triangles[:,0]],vertices[triangles[:,1]],vertices[triangles[:,2]]
    face=np.cross(b-a,c-a)
    n=np.zeros_like(vertices)
    for i in range(3): np.add.at(n,triangles[:,i],face)
    lengths=np.linalg.norm(n,axis=1)
    n/=np.maximum(lengths[:,None],1e-10)
    # Weld equivalent positions for seamless lighting across periodic mesh seams.
    return n

def write_asset(asset_name):
    target=ROOT/'assets'/(asset_name+'.meshbin')
    target.parent.mkdir(exist_ok=True)
    def string(f,s):
        b=s.encode(); assert len(b)<128
        f.write(bytes([len(b)])); f.write(b)
    with target.open('wb') as f:
        f.write(b'BAST1')
        f.write(struct.pack('<i',len(BONES)))
        for name,p,child in BONES:
            string(f,name)
            f.write(struct.pack('<3f',*p))
            f.write(struct.pack('<i',BN.get(child,-1)))
        active={name:part for name,part in PARTS.items() if part['v']}
        f.write(struct.pack('<i',len(active)))
        for name,part in active.items():
            string(f,name)
            rgb,smooth,emission=PALETTE[name]
            f.write(struct.pack('<5f',*rgb,smooth,emission))
            f.write(struct.pack('<ii',len(part['v']),len(part['t'])*3))
            n=part['n'] if 'n' in part else normals(part['v'],part['t'])
            for p,normal,w in zip(part['v'],n,part['w']):
                assert abs(sum(weight for _,weight in w)-1)<1e-6
                (b0,w0)=w[0]; (b1,w1)=w[1] if len(w)>1 else (b0,0.)
                f.write(struct.pack('<6fiif',*p,*normal,b0,b1,w0))
            f.write(np.array(part['t'],dtype='<i4').tobytes())
    summary={name:{'vertices':len(p['v']),'triangles':len(p['t'])} for name,p in PARTS.items()}
    (ROOT/'assets'/(asset_name+'-info.json')).write_text(json.dumps({'format':'BAST1','authoring':'scripts/seraph_models.py','parts':summary},indent=2))
    print(f'Authored {target}: {sum(len(p["v"]) for p in PARTS.values()):,} vertices, {sum(len(p["t"]) for p in PARTS.values()):,} triangles')

def render(size, yaw, crop, filename, title=None):
    """Orthographic smooth-shaded rasterizer: preview of these actual mesh vertices."""
    w,h=size; supersample=2
    w*=supersample; h*=supersample
    xx=np.linspace(0,1,w)[None,:,None]; yy=np.linspace(0,1,h)[:,None,None]
    image=np.broadcast_to(np.array([15,17,31])+(1-yy)*np.array([15,9,21]),(h,w,3)).copy()
    depth=np.full((h,w),-1e9,dtype=float)
    glow=np.zeros((h,w,3),dtype=np.uint8)
    angle=math.radians(yaw)
    rotation=np.array([[math.cos(angle),0,-math.sin(angle)],[0,1,0],[math.sin(angle),0,math.cos(angle)]])
    lower,upper,width=crop; scale=h/(upper-lower)
    light=norm((-.5,.8,1.2)); fill=norm((.8,.25,-.6))
    halfway=norm(light+np.array((0,0,1)))
    for name,part in PARTS.items():
        if not part['v']: continue
        v=np.array(part['v'])@rotation.T
        ns=normals(part['v'],part['t'])@rotation.T
        screen=np.stack([w/2+v[:,0]*scale,(upper-v[:,1])*scale,v[:,2]],axis=1)
        rgb=np.array(PALETTE[name][0])*255
        emission=PALETTE[name][2]
        shade=.28+.68*np.clip(ns@light,0,1)+.20*np.clip(ns@fill,0,1)
        if emission>1: shade=.85+.15*np.clip(ns@light,0,1)
        col=rgb[None,:]*shade[:,None]
        # Show the same ceramic/metal/glass specular distinction used by the
        # runtime material. This is studio lighting, not an in-game capture.
        smooth=PALETTE[name][1]
        strength=.55 if name=='Glass' else .38 if name in ('Edge','Joint') else .22
        if emission<=1:
            specular=np.clip(ns@halfway,0,1)**(36 if smooth>.5 else 24)
            col+=255*strength*specular[:,None]
        triangles=np.asarray(part['t'],dtype=np.int32)
        # Match the runtime renderer's backface culling and keep offline previews fast.
        face_direction=np.cross(v[triangles[:,1]]-v[triangles[:,0]],v[triangles[:,2]]-v[triangles[:,0]])[:,2]
        for ids in triangles[face_direction>1e-12]:
            tri=screen[ids]; colors=col[ids]
            x0=max(0,int(np.floor(tri[:,0].min()))); x1=min(w-1,int(np.ceil(tri[:,0].max())))
            y0=max(0,int(np.floor(tri[:,1].min()))); y1=min(h-1,int(np.ceil(tri[:,1].max())))
            if x0>x1 or y0>y1: continue
            a,b,c=tri
            denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(denom)<1e-8: continue
            x,y=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
            u=((b[1]-c[1])*(x-c[0])+(c[0]-b[0])*(y-c[1]))/denom
            vv=((c[1]-a[1])*(x-c[0])+(a[0]-c[0])*(y-c[1]))/denom
            ww=1-u-vv
            z=u*a[2]+vv*b[2]+ww*c[2]
            mask=(u>=0)&(vv>=0)&(ww>=0)&(z>depth[y0:y1+1,x0:x1+1])
            if not mask.any(): continue
            pix=u[:,:,None]*colors[0]+vv[:,:,None]*colors[1]+ww[:,:,None]*colors[2]
            depth[y0:y1+1,x0:x1+1][mask]=z[mask]
            image[y0:y1+1,x0:x1+1][mask]=np.clip(pix[mask],0,255)
            glow[y0:y1+1,x0:x1+1][mask]=np.clip(rgb*(min(emission,3)/3 if emission>1 else 0),0,255)
    light=Image.fromarray(glow)
    image+=np.asarray(light.filter(ImageFilter.GaussianBlur(5*supersample)),float)*.8
    image+=np.asarray(light.filter(ImageFilter.GaussianBlur(14*supersample)),float)*.6
    im=Image.fromarray(np.uint8(np.clip(image,0,255)))
    if supersample>1: im=im.resize(size,Image.Resampling.LANCZOS)
    if title:
        d=ImageDraw.Draw(im)
        d.text((24,20),title,fill='#d9d2ea',font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',22))
    im.save(filename)
    return im

