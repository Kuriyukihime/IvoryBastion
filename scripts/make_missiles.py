"""Original white/black/blue missile meshes, in meters, with angular control fins."""
import math
import numpy as np
import mesh_tools as m
MISSILE_WIDTH_SCALE=.8
SKYFALL_WIDTH_SCALE=7/12

PALETTE={
 'MissileCeramic':((.90,.95,1),.45,0),
 'MissileFrame':((.025,.035,.055),.42,0),
 'MissileBlue':((.18,.62,1),.3,1.8), 'MissileEdge':((.40,.47,.56),.57,0)}

def shell(material,profile):
    vertices=[];faces=[];sides=64
    for z,r in profile:
        for i in range(sides):
            a=i*math.tau/sides;vertices.append((math.cos(a)*r,math.sin(a)*r,z))
    for row in range(len(profile)-1):
        for i in range(sides):
            a=row*sides+i;b=row*sides+(i+1)%sides;c=a+sides;d=b+sides
            faces.extend([(a,b,c),(b,d,c)])
    m.add(material,vertices,faces,m.rigid('root'))

def fin(angle,front=False,compact=False):
    outline=[(.088,-.16),(.105,-.40),(.25,-.49),(.23,-.36)]
    if front:outline=[(.09,.28),(.09,.10),(.17,.05),(.16,.12)]
    if compact:
        outline=[(.088,-.16),(.105,-.40),(.15,-.49),(.145,-.32)]
        if front:outline=[(.09,.28),(.09,.10),(.13,.05),(.125,.12)]
    radial=np.array((math.cos(angle),math.sin(angle),0));side=np.array((-math.sin(angle),math.cos(angle),0))
    vertices=[radial*r+np.array((0,0,z))+side*t for t in [-.009,.009] for r,z in outline]
    faces=[(0,2,1),(0,3,2),(4,5,6),(4,6,7),(0,1,5),(0,5,4),(1,2,6),(1,6,5),(2,3,7),(2,7,6),(3,0,4),(3,4,7)]
    m.add('MissileCeramic',vertices,faces,m.rigid('root'))
    # Thin blue guide strip on each fin, not a colored replacement for its metal shell.
    if not front:
        pts=[radial*r+np.array((0,0,z))+side*.011 for r,z in [( .115 if compact else .12,-.40),(.135 if compact else .22,-.45)]]
        r=.005 if compact else .007
        m.tube('MissileBlue',pts,[(r,r)]*2,m.rigid('root'),sides=6,steps=1,front=(0,0,1))

def author(heavy=False,skyfall=False):
    m.BONES=[('root',(0,0,0),None)];m.BN={'root':0};m.ANCHORS=np.zeros((1,3));m.PALETTE=PALETTE
    m.PARTS={k:{'v':[],'t':[],'w':[]} for k in PALETTE}
    shell('MissileFrame',[(-.55,.001),(-.55,.062),(-.49,.084),(-.35,.098),(.31,.098),(.48,.046),(.55,.001)])
    shell('MissileCeramic',[(-.38,.101),(-.30,.105),(.19,.105),(.28,.094)])
    shell('MissileBlue',[(-.44,.085),(-.415,.093)])
    shell('MissileBlue',[(.27,.097),(.295,.093)])
    shell('MissileBlue',[(-.556,.001),(-.556,.053),(-.55,.058)])
    for i in range(4):
        angle=math.pi/4+i*math.pi/2;fin(angle,compact=True)
        if heavy:fin(angle,True,compact=True)
        radial=np.array((math.cos(angle),math.sin(angle),0))
        m.tube('MissileFrame',[radial*.106+np.array((0,0,z)) for z in [-.28,.12]],[(.012,.012)]*2,m.rigid('root'),sides=6,steps=1,front=(0,1,0))
    if heavy:shell('MissileBlue',[(-.05,.107),(-.025,.107)])
    # Separate motor collar, ceramic service bands and an inset nozzle.
    for z in [-.365,-.285,.155,.24]:shell('MissileEdge',[(z,.103),(z+.012,.106),(z+.025,.103)])
    shell('MissileEdge',[(-.563,.056),(-.57,.064),(-.55,.075),(-.49,.083)])
    for i in range(8):
        a=i*math.tau/8;radial=np.array((math.cos(a),math.sin(a),0))
        m.tube('MissileEdge',[radial*.082+np.array((0,0,z)) for z in [-.53,-.44]],[(.005,.005)]*2,m.rigid('root'),sides=10,steps=1,front=(0,1,0))
    for i in range(4):
        a=i*math.tau/4;radial=np.array((math.cos(a),math.sin(a),0))
        for z in [.02,.05,.08]:m.tube('MissileFrame',[radial*.107+np.array((0,0,z)),radial*.107+np.array((0,0,z+.009))],[(.005,.005)]*2,m.rigid('root'),sides=8,steps=1,front=(0,1,0))
    scale=1.8 if heavy else 1
    width_scale=SKYFALL_WIDTH_SCALE if skyfall else MISSILE_WIDTH_SCALE
    for part in m.PARTS.values():
        vertices=np.asarray(part['v'])*scale
        vertices[:,:2]*=width_scale
        part['v']=vertices.tolist()
    name='skyfall' if skyfall else 'warhead' if heavy else 'missile';m.write_asset(name)
    import json
    info_path=m.ROOT/'assets'/(name+'-info.json')
    info=json.loads(info_path.read_text());info['authoring']='scripts/make_missiles.py'
    info['compact_fins']=True;info['radial_scale']=width_scale;info['model_scale']=scale
    info_path.write_text(json.dumps(info,indent=2))
    # Rotate the authored geometry only for the offline side-profile render.
    for part in m.PARTS.values():
        v=np.asarray(part['v']);part['v']=np.column_stack((v[:,0],v[:,2],-v[:,1])).tolist()
    m.render((650,1000),28,(-1.15,1.15,1),m.ROOT/f'dist/IvoryBastion-{name}-preview.png',f'IVORY / {name.upper()} / SAME SCALE')

def missiles():author();author(True);author(True,True)
if __name__=='__main__':
    import sys
    if '--skyfall' in sys.argv:author(True,True)
    else:missiles()
