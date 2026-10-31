"""Authored Seraph throne and rail cannon, in the existing siege rig's meters.

Missile racks/doors retain their forward-facing axes and launch markers. The
new raised cockpit is separate from the gun, so recoil never moves the chair.
"""
import math
import numpy as np
import seraph_refinement as d
import mesh_tools as m

THRONE_ANCHOR = np.array([0., 1.43, -.35])
PILOT_CHEST = np.array([0., 1.72, -.27])
CANNON_MUZZLE = np.array([0., 1.02, 2.6366])
SKYFALL_HOUSING_RADIAL_SCALE=.57
SKYFALL_SHOE_HEIGHT=.77
SKYFALL_SHOE_THICKNESS=.15
SKYFALL_HEIGHT=2.10
SKYFALL_MUZZLE_HEIGHT=2.60
SKYFALL_X=.78
SKYFALL_Z=-1.94
SKYFALL_BORE=.490*SKYFALL_HOUSING_RADIAL_SCALE

def skyfall_liner(x,z,bone):
    """Real inward-facing metal, an open rim and a recessed chamber floor."""
    sides=48;radius=.490;heights=[.79,1.03,1.82,2.08]
    angles=np.arange(sides)*math.tau/sides
    vertices=[(x+radius*math.cos(a),y,z+radius*math.sin(a)) for y in heights for a in angles]
    faces=[]
    for row in range(len(heights)-1):
        for i in range(sides):
            a=row*sides+i;b=row*sides+(i+1)%sides;c=a+sides;dd=b+sides
            faces.extend([(a,b,c),(b,dd,c)])
    m.add('Joint',vertices,faces,m.rigid(bone))
    for i,a in enumerate(angles):
        b=angles[(i+1)%sides]
        points=[(x+r*math.cos(angle),2.08,z+r*math.sin(angle))
                for r,angle in [(.490,a),(.490,b),(.520,b),(.520,a)]]
        d.quad('Joint',points,bone,normal=(0,1,0))
    d.disc('Frame',(x,.785,z),.488,.015,bone,normal=(0,1,0),sides=48)
    for y in [1.12,1.67]:
        m.torus('Joint',(x,y,z),.483,.004,m.rigid(bone),normal=(0,1,0))
    for a in np.arange(4)*math.tau/4:
        radial=np.array([math.cos(a),0,math.sin(a)])
        d.line('Blue',np.array([(x,1.03,z),(x,1.88,z)])+radial*.486,.002,bone)

def skyfall_bones():
    bones=[]
    for side,s in [('l',-1),('r',1)]:
        bones.extend([('skyfall_'+side,np.array([s*SKYFALL_X,.93,SKYFALL_Z])),
                      ('skyfall_muzzle_'+side,np.array([s*SKYFALL_X,SKYFALL_MUZZLE_HEIGHT,SKYFALL_Z])),
                      ('skyfall_mount_inner_'+side,np.array([s*.26,.81,-1.14])),
                      ('skyfall_mount_outer_'+side,np.array([s*.56,.84,-1.67]))])
    return bones


def throne():
    b = 'throne'
    # Fixed armored elevator well and telescoping rails, visible below the seat.
    d.box('Frame', (0, .90, -.36), (.66, .36, .56), bevel=.025)
    d.box('Porcelain', (0, .91, -.055), (.58, .22, .065), bevel=.018)
    for s in [-1, 1]:
        d.box('Joint', (s*.236, 1.12, -.48), (.082, .66, .13), bevel=.010)
        d.box('Edge', (s*.236, 1.20, -.397), (.035, .62, .022), b, bevel=.004)
        d.box('Frame', (s*.22, 1.26, -.33), (.076, .32, .10), b, bevel=.008)
        d.box('Blue', (s*.22, 1.27, -.272), (.012, .22, .009), b, bevel=.002)
        d.rod('Frame', (s*.29, .84, -.56), (s*.31, 1.38, -.63), .045, sides=24)
        d.rod('Edge', (s*.31, 1.09, -.60), (s*.32, 1.43, -.65), .022, b, 24)
    # Layered raised seat deck, an actual cushion and a splayed pelvic cradle.
    d.box('Frame', (0, 1.405, -.28), (.70, .115, .69), b, bevel=.025)
    d.box('Porcelain', (0, 1.451, -.26), (.67, .055, .66), b, bevel=.020)
    d.box('Joint', (0, 1.485, -.31), (.29, .055, .35), b, bevel=.014)
    d.box('Glass', (0, 1.516, -.31), (.26, .018, .31), b, bevel=.008)
    for s in [-1, 1]:
        d.box('Blue', (s*.294, 1.459, -.10), (.012, .010, .28), b, bevel=.002)
        d.plate('Ceramic', [(-.10,.045),(.09,.07),(.11,-.07),(.025,-.13),(-.09,-.065)],
                (s*.23,1.48,-.01), b, u=(s,0,0), thickness=.035, bulge=.014)
    # Tall split white throne back: tapered crown, dark spine and recessed pads.
    d.box('Frame', (0, 1.812, -.535), (.29, .69, .12), b, bevel=.020)
    for y, width in [(1.60,.24),(1.73,.23),(1.86,.20),(1.99,.16)]:
        d.box('Joint', (0,y,-.458), (width,.094,.031), b, bevel=.008)
    d.box('Blue', (0, 2.116, -.459), (.065,.016,.009), b, bevel=.002)
    for s in [-1, 1]:
        points = [(s*.23,1.47,-.49),(s*.26,1.74,-.50),
                  (s*.225,2.03,-.51),(s*.13,2.235,-.50)]
        d.profile('Frame', points, [(.045,.052),(.064,.055),(.058,.043),(.009,.010)], b, sides=32, steps=9)
        d.profile('Porcelain', np.array(points)+[0,0,.040],
                  [(.033,.025),(.046,.027),(.041,.022),(.003,.005)], b, sides=32, steps=9)
        d.line('Blue', [(s*.275,1.65,-.478),(s*.268,1.84,-.481),(s*.223,2.02,-.481)], .004, b)
        for y in [1.58,1.91]:d.bolt((s*.265,y,-.448),.008,b)
    d.box('Joint', (0,2.244,-.514), (.17,.055,.115), b, bevel=.010)
    d.box('Porcelain', (0,2.273,-.496), (.22,.035,.12), b, bevel=.008)
    d.box('Blue', (0,2.276,-.430), (.10,.008,.006), b, bevel=.001)
    # Proper armrests with black touch surfaces and small physical control grips.
    for s in [-1, 1]:
        d.box('Frame',(s*.287,1.526,-.185),(.13,.155,.49),b,bevel=.020)
        d.box('Porcelain',(s*.287,1.605,-.185),(.142,.035,.50),b,bevel=.010)
        d.box('Glass',(s*.287,1.626,-.115),(.103,.010,.25),b,bevel=.005)
        for k in range(3):
            d.box('Blue',(s*(.269+k*.018),1.633,-.12),(.007,.003,.080-k*.016),b,bevel=.001)
        d.rod('Joint',(s*.197,1.566,-.005),(s*.197,1.604,.015),.012,b,16)
        d.orb('Frame',(s*.197,1.609,.015),(.019,.014,.021),b)
        d.bolt((s*.340,1.573,-.32),.008,b,(s,0,0))
    # Seated pelvis/thighs/greaves complete the pilot behind the cannon.
    p='torso'
    d.orb('Frame',(0,1.520,-.28),(.105,.058,.078),p)
    d.plate('Ceramic',[(-.07,.035),(.07,.035),(.06,-.026),(0,-.065),(-.06,-.026)],
            (0,1.53,-.195),p,thickness=.020,bulge=.012)
    for s in [-1,1]:
        thigh=[(s*.085,1.51,-.27),(s*.145,1.51,-.09),(s*.169,1.46,.075)]
        d.profile('Frame',thigh,[(.045,.043),(.050,.048),(.029,.030)],p,sides=28,steps=7)
        d.profile('Ceramic',np.array(thigh)+[0,.022,.005],[(.033,.031),(.044,.036),(.023,.020)],p,sides=32,steps=7)
        d.orb('Joint',(s*.170,1.456,.075),(.033,.035,.033),p)
        d.orb('Porcelain',(s*.170,1.456,.103),(.028,.035,.013),p)
        d.disc('Blue',(s*.170,1.46,.118),.009,.004,p,sides=20)
        shin=[(s*.17,1.44,.076),(s*.185,1.26,.15),(s*.19,1.10,.21)]
        d.profile('Frame',shin,[(.026,.027),(.024,.024),(.016,.018)],p,sides=28,steps=7)
        d.profile('Porcelain',np.array(shin)+[0,0,.023],[(.025,.019),(.033,.022),(.015,.014)],p,sides=32,steps=7)
        d.line('Blue',[(s*.185,1.32,.189),(s*.19,1.18,.224)],.003,p)
        d.profile('Ceramic',[(s*.19,1.11,.21),(s*.19,1.075,.28),(s*.19,1.06,.365)],
                  [(.018,.020),(.026,.021),(.005,.007)],p,sides=28,steps=6)
    # A ribbed rear spine connects the throne and halo to the main hull.
    for s in [-1,1]:
        d.profile('Frame',[(s*.37,.74,-.76),(s*.43,1.18,-.78),(s*.39,1.65,-.76)],
                  [(.055,.060),(.065,.055),(.042,.040)],sides=28,steps=7)
        d.profile('Porcelain',[(s*.38,.82,-.82),(s*.44,1.20,-.83),(s*.40,1.58,-.81)],
                  [(.032,.020),(.038,.020),(.022,.016)],sides=28,steps=7)
        for y in [1.02,1.18,1.34]:d.box('Joint',(s*.42,y,-.85),(.090,.030,.026),bevel=.004)


def cannon():
    b='cannon';cy=1.02
    # Octagonal armored breech, with a visibly hollow accelerator chamber.
    d.disc('Frame',(0,cy,.97),.365,.53,b,sides=12)
    d.disc('Ceramic',(0,cy,1.212),.345,.13,b,sides=12)
    d.disc('Joint',(0,cy,1.287),.285,.035,b,sides=16)
    m.torus('Blue',(0,cy,1.310),.235,.023,m.rigid(b),normal=(0,0,1))
    m.torus('Edge',(0,cy,1.315),.290,.010,m.rigid(b),normal=(0,0,1))
    for i in range(8):
        a=i*math.tau/8;radial=np.array([math.sin(a),math.cos(a),0])
        d.bolt(np.array([0,cy,1.294])+radial*.317,.013,b)
    # Broad layered side cheeks and top saddle follow the concept's machinery.
    for s in [-1,1]:
        d.box('Frame',(s*.343,cy,.995),(.18,.48,.64),b,bevel=.025)
        d.box('Porcelain',(s*.414,cy+.018,1.003),(.085,.37,.56),b,bevel=.022)
        d.box('Joint',(s*.461,cy-.02,1.01),(.016,.205,.31),b,bevel=.004)
        d.box('Blue',(s*.472,cy+.025,1.035),(.006,.024,.22),b,bevel=.002)
        for y,z in [(cy+.14,1.20),(cy-.12,.81)]:d.bolt((s*.462,y,z),.011,b,(s,0,0))
        for k in range(5):
            d.box('Frame',(s*.411,cy+.12-k*.052,.689),(.081,.019,.035),b,bevel=.003)
    d.box('Porcelain',(0,cy+.309,1.006),(.58,.075,.53),b,bevel=.017)
    d.box('Frame',(0,cy+.354,.98),(.28,.018,.31),b,bevel=.005)
    d.box('Glass',(0,cy+.366,.98),(.21,.008,.24),b,bevel=.003)
    d.box('Blue',(0,cy+.372,.98),(.11,.004,.17),b,bevel=.002)
    # Long squared accelerator: black core, white rails, blue recessed channels.
    start,end=1.25,CANNON_MUZZLE[2];mid=(start+end)*.5;length=end-start
    d.box('Frame',(0,cy,mid-.10),(.323,.237,length-.20),b,bevel=.014)
    for s in [-1,1]:
        d.box('Joint',(s*.175,cy,mid),(.052,.197,length-.02),b,bevel=.006)
        d.box('Glass',(s*.205,cy,mid),(.013,.100,length-.085),b,bevel=.002)
        d.box('Blue',(s*.214,cy,mid),(.010,.041,length-.115),b,bevel=.002)
        d.box('Core',(s*.220,cy+.008,mid),(.005,.010,length-.18),b,bevel=.001)
        # Split top/bottom ceramic shells taper into the muzzle frame.
        for face in [-1,1]:
            outline=[(-.075,-length*.49),(.075,-length*.49),(.095,-length*.34),
                     (.083,length*.39),(.057,length*.50),(-.057,length*.50),(-.083,length*.39),(-.095,-length*.34)]
            d.plate('Porcelain',outline,(s*.096,cy+face*.139,mid),b,
                    u=(1,0,0),v=(0,0,-face),thickness=.034,bulge=.009)
        for z in [1.41,1.87,2.32]:
            d.box('Ceramic',(s*.207,cy-.077,z),(.032,.044,.073),b,bevel=.005)
            d.bolt((s*.228,cy-.077,z),.008,b,(s,0,0))
    # Open muzzle: four white jaws, a dark recessed bore and narrow blue throat.
    z=end-.075
    for x,y,w,h in [(0,cy+.150,.32,.054),(0,cy-.150,.32,.054),(-.171,cy,.054,.25),(.171,cy,.054,.25)]:
        d.box('Frame',(x,y,z-.015),(w+.026,h+.026,.135),b,bevel=.010)
        d.box('Porcelain',(x,y,z+.045),(w,h,.049),b,bevel=.007)
    for s in [-1,1]:
        d.box('Frame',(s*.121,cy,end-.09),(.018,.235,.18),b,bevel=.002)
        d.box('Frame',(0,cy+s*.112,end-.09),(.24,.018,.18),b,bevel=.002)
    d.disc('Glass',(0,cy,end-.185),.110,.012,b,sides=12)
    m.torus('Blue',(0,cy,z+.033),.081,.008,m.rigid(b),normal=(0,0,1))
    d.box('Core',(0,cy,end-.174),(.054,.010,.003),b,bevel=.0008)
    for i in range(4):
        a=(i+.5)*math.tau/4;radial=np.array([math.cos(a),math.sin(a),0]);tangent=np.array([-radial[1],radial[0],0])
        center=np.array([0,cy,.98])+radial*.35
        for k in range(4):d.box('Joint',center+[0,0,-.12+k*.073],(.115,.031,.043),'vent_'+str(i),u=tangent,v=radial,bevel=.003)
        d.box('Ceramic',center+radial*.022,(.14,.024,.33),'vent_'+str(i),u=tangent,v=radial,bevel=.005)
    # Separate trunnion and recoil sled under the gun; neither belongs to the seat.
    d.box('Frame',(0,.742,.87),(.64,.14,.66),bevel=.020)
    d.box('Porcelain',(0,.793,.89),(.59,.055,.61),bevel=.013)
    for s in [-1,1]:
        d.joint((s*.326,.858,.89),.105,normal=(s,0,0))
        d.disc('Blue',(s*.435,.858,.89),.044,.008,normal=(s,0,0),sides=32)
        d.rod('Frame',(s*.273,.828,.89),(s*.273,.828,1.36),.041,b,24)
        d.rod('Edge',(s*.273,.828,1.13),(s*.273,.828,1.46),.019,b,24)


def skyfall_launchers():
    # Restore the enclosed 0.10.0 design and its height, narrowing only its
    # diameter. Keep the raised forked mounts and clearance fixes.
    import seraph_premium as premium
    for side,s in [('l',-1),('r',1)]:
        b='skyfall_'+side;x=s*SKYFALL_X;z=SKYFALL_Z
        # A forked cantilever reaches the hull through the gap between the
        # rear batteries. Both pieces stay above the six animated feet.
        inner='skyfall_mount_inner_'+side;outer='skyfall_mount_outer_'+side
        inner_path=[(s*.20,.80,-.90),(s*.26,.82,-1.15),(s*.34,.84,-1.40)]
        outer_path=[(s*.34,.84,-1.40),(s*.54,.84,-1.68),(x,.84,z)]
        for name,path in [(inner,inner_path),(outer,outer_path)]:
            d.profile('Frame',path,[(.045,.045),(.045,.045),(.050,.045)],name,sides=24,steps=6)
            d.profile('Porcelain',np.asarray(path)+[0,.043,0],[(.012,.028)]*3,name,sides=24,steps=6)
        starts={key:len(part['v']) for key,part in m.PARTS.items()}
        source_top=2.10
        # Original dark sleeve, white armor panels, curved side fins, bearing
        # collars and segmented open lip. There is still no lid.
        d.box('Frame',(x,SKYFALL_SHOE_HEIGHT,z),(.86,SKYFALL_SHOE_THICKNESS,.86),b,bevel=.028)
        for off in [-1,1]:
            d.rod('Frame',(x+off*.52,.73,z+.23),(x+off*.52,1.02,z+.23),.035,b,sides=24)
            d.disc('Joint',(x+off*.61,.84,z),.10,.06,b,normal=(1,0,0),sides=32)
        d.profile('Frame',[(x,.76,z),(x,1.03,z),(x,1.82,z),(x,2.08,z)],
                  [(.520,.520)]*4,b,sides=32,steps=3)
        for face in range(4):
            angle=face*math.tau/4;normal=np.array([math.sin(angle),0,math.cos(angle)])
            tangent=np.array([math.cos(angle),0,-math.sin(angle)])
            at=np.array([x,1.36,z])+normal*.55
            outline=[(-.10,-.52),(.10,-.52),(.17,-.26),(.16,.28),(.075,.56),(-.075,.56),(-.16,.28),(-.17,-.26)]
            d.plate('Ceramic',outline,at,b,u=tangent,thickness=.040,bulge=.014)
            d.plate('Porcelain',np.asarray(outline)*[.80,.90],at+normal*.023,b,u=tangent,thickness=.014,bulge=.010)
            d.box('Joint',at+normal*.040,(.040,.60,.010),b,u=tangent,bevel=.003)
            d.box('Blue',at+normal*.047,(.010,.48,.004),b,u=tangent,bevel=.001)
            for dy in [-.28,.27]:d.bolt(at+tangent*.12+normal*.036+[0,dy,0],.008,b,normal)
        for off in [-1,1]:
            points=[(x+off*.57,.81,z),(x+off*.61,1.10,z),
                    (x+off*.60,1.74,z),(x+off*.63,2.08,z),(x+off*.65,2.19,z)]
            d.profile('Porcelain',points,[(.035,.07),(.050,.10),(.045,.09),(.014,.040),(.003,.004)],b,sides=32,steps=8)
            d.line('Joint',[(x+off*.60,1.03,z+.10),(x+off*.64,1.62,z+.10),(x+off*.65,1.96,z+.05)],.006,b)
        m.torus('Frame',(x,source_top-.04,z),.514,.024,m.rigid(b),normal=(0,1,0))
        rotation=np.array([[1,0,0],[0,0,1],[0,-1,0]])
        for start in [6,96,186,276]:
            premium.transform_new(lambda start=start:d.arc('Porcelain',(0,0,0),.535,.040,.030,start,start+78,b,24),
                                  lambda p:p@rotation.T+[x,source_top-.010,z])
        m.torus('Blue',(x,source_top-.025,z),.512,.006,m.rigid(b),normal=(0,1,0))
        d.disc('Glass',(x,.72,z),.503,.022,b,normal=(0,1,0),sides=48)
        skyfall_liner(x,z,b)
        # Preserve all vertical dimensions and narrow the restored housing
        # around its axis. The safe hull forks retain their mounting positions.
        for key,part in m.PARTS.items():
            start=starts[key]
            points=np.asarray(part['v'][start:],float).reshape((-1,3))
            points[:,0]=x+(points[:,0]-x)*SKYFALL_HOUSING_RADIAL_SCALE
            points[:,2]=z+(points[:,2]-z)*SKYFALL_HOUSING_RADIAL_SCALE
            part['v'][start:]=points.tolist()


def draw():
    throne()
    cannon()
    skyfall_launchers()
