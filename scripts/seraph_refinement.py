"""Detailed geometry matching the selected Seraph sheet, using the shipped rig."""
import math
import numpy as np
import mesh_tools as m

def plate(mat,outline,at,bone='root',**kw):
    outline=np.asarray(outline,float);center=outline.mean(axis=0)
    u=np.asarray(kw.get('u',(1,0,0)));v=np.asarray(kw.get('v',(0,1,0)))
    at=np.asarray(at)+u*center[0]+v*center[1]
    m.armor_panel(mat,outline-center,at,m.rigid(bone),segments=48,
                  radial_rings=[.20,.46,.73,.90,.97,1],**kw)

def rod(mat,a,b,r,bone='root',sides=16):
    axis=m.norm(np.array(b)-a);front=(0,1,0) if abs(axis[2])>.85 else (0,0,1)
    m.tube(mat,[a,b],[(r,r)]*2,m.rigid(bone),sides=sides,steps=1,front=front)

def line(mat,points,r,bone='root'):
    m.curve(mat,points,r,m.rigid(bone),steps=6)

def profile(mat,points,radii,bone='root',sides=40,steps=8):
    m.tube(mat,points,radii,m.rigid(bone),sides=sides,steps=steps)

def orb(mat,at,size,bone='root',**kw):
    m.ellipsoid(mat,at,size,m.rigid(bone),sides=40,rings=24,**kw)

def quad(mat,points,bone='root',normal=None):
    points=np.asarray(points,float)
    faces=[(0,1,2),(0,2,3)]
    if normal is not None and np.dot(np.cross(points[1]-points[0],points[2]-points[0]),normal)<0:
        faces=[(a,c,b) for a,b,c in faces]
    m.add(mat,points,faces,m.rigid(bone))

def box(mat,at,size,bone='root',u=(1,0,0),v=(0,1,0),bevel=.006):
    """True planar faces and modeled chamfers, rather than inflated plates."""
    at=np.array(at,float);u=m.norm(u);v=m.norm(v);n=m.norm(np.cross(u,v))
    x,y,z=np.asarray(size)/2;cut=min(x,y)*.25;bevel=min(bevel,min(x,y,z)*.7)
    outline=np.array([(-x+cut,-y),(x-cut,-y),(x,-y+cut),(x,y-cut),
                      (x-cut,y),(-x+cut,y),(-x,y-cut),(-x,-y+cut)])
    outer=[at+u*a+v*b+n*(z-bevel) for a,b in outline]
    inner=[at+u*a*(1-bevel/x)+v*b*(1-bevel/y)+n*z for a,b in outline]
    back=[at+u*a+v*b-n*z for a,b in outline]
    for i in range(8):
        j=(i+1)%8;side=m.norm(u*(outline[i][0]+outline[j][0])+v*(outline[i][1]+outline[j][1]))
        quad(mat,[back[i],back[j],outer[j],outer[i]],bone,side)
        quad(mat,[outer[i],outer[j],inner[j],inner[i]],bone,n+side)
    for cap,normal in [(inner,n),(back,-n)]:
        vertices=[np.mean(cap,axis=0)]+cap
        faces=[(0,i+1,(i+1)%8+1) for i in range(8)]
        if np.dot(np.cross(vertices[1]-vertices[0],vertices[2]-vertices[0]),normal)<0:
            faces=[(a,c,b) for a,b,c in faces]
        m.add(mat,vertices,faces,m.rigid(bone))

def disc(mat,at,r,depth,bone='root',normal=(0,0,1),sides=24):
    at=np.array(at,float);n=m.norm(normal);u=m.norm(np.cross((0,1,0),n))
    if np.linalg.norm(u)<.1:u=m.norm(np.cross((1,0,0),n))
    v=np.cross(n,u)
    front=[at+n*depth*.5+(u*math.cos(a)+v*math.sin(a))*r for a in np.arange(sides)*math.tau/sides]
    back=[p-n*depth for p in front]
    verts=front+back;faces=[]
    for i in range(sides):
        j=(i+1)%sides;faces.extend([(i,sides+i,j),(j,sides+i,sides+j)])
    m.add(mat,verts,faces,m.rigid(bone))
    for points,reverse in [(front,False),(back,True)]:
        verts=[np.mean(points,axis=0)]+points;faces=[(0,i+1,(i+1)%sides+1) for i in range(sides)]
        if reverse:faces=[(a,c,b) for a,b,c in faces]
        m.add(mat,verts,faces,m.rigid(bone))

def bolt(at,r=.009,bone='root',normal=(0,0,1)):
    n=m.norm(normal);at=np.asarray(at);disc('Edge',at,r,r*.5,bone,n,12)
    disc('Frame',at+n*r*.28,r*.38,r*.09,bone,n,6)

def joint(at,r,bone='root',normal=(1,0,0)):
    n=m.norm(normal);at=np.asarray(at)
    disc('Frame',at,r,r*1.3,bone,n,32)
    for s in [-1,1]:
        disc('Joint',at+n*s*r*.69,r*.85,r*.15,bone,n,32)
        disc('Edge',at+n*s*r*.80,r*.61,r*.08,bone,n,24)
        disc('Frame',at+n*s*r*.87,r*.34,r*.07,bone,n,12)

def chest_point(u,v,y=1.08):
    """Fitted armor surface, continuous across the sternum and around the ribs.

    u is the signed angle around the front of the torso, v runs from the
    underbust to the collar. Broad, shallow curvature replaces separate cups.
    """
    a=abs(u)*math.pi/2
    width=.149+.008*math.sin(math.pi*v)-.009*(1-v)**2
    x=math.copysign(width*math.sin(a),u)
    bottom=y-.018-.033*math.sin(a*2)**1.3+.030*math.sin(a)**5
    top=y+.112+.027*math.sin(a)**.8
    yy=bottom*(1-v)+top*v
    # Full-width base joins both halves, with a restrained outward fullness.
    base=(.099-.015*v-.013*(1-v)**3)*math.cos(a)
    fullness=.056*math.sin(a*2)**2*math.sin(math.pi*v)**.95
    return np.array((x,yy,-.002+base+fullness))

def chest(y=1.08):
    """One joined breastplate with actual curved panel cuts, not round inserts."""
    across=128;rows=48
    vertices=[chest_point(-1+2*i/across,j/rows,y)
              for j in range(rows+1) for i in range(across+1)]
    faces=[]
    for j in range(rows):
        for i in range(across):
            a=j*(across+1)+i;b=a+1;c=a+across+1
            faces.extend([(a,b,c),(b,c+1,c)])
    m.add('Porcelain',vertices,faces,m.rigid('torso'))
    # Curved, narrow lower lip shows armor thickness without a floating rim.
    for v in [0,1]:
        for i in range(across):
            a=chest_point(-1+2*i/across,v,y);b=chest_point(-1+2*(i+1)/across,v,y)
            quad('Ceramic',[a,b,b-(0,0,.007),a-(0,0,.007)],'torso',(0,2*v-1,0))
    for s in [-1,1]:
        # Outer panel seam curves inward as it approaches the underbust.
        seam=[]
        for t in np.linspace(0,1,22):
            u=s*(.79-.34*t+.07*math.sin(math.pi*t))
            p=chest_point(u,.72*(1-t)+.025,y)
            p[2]+=.0005;seam.append(p)
        line('Frame',seam,.0012,'torso')
        # The shallow collar chevron is fitted to the same surface.
        seam=[]
        for t in np.linspace(0,1,25):
            p=chest_point(s*t,.80+.18*t,y);p[2]+=.0004;seam.append(p)
        line('Joint',seam,.0011,'torso')
        p=chest_point(s*.76,.75,y)
        line('Blue',[p+(-s*.007,0,.001),p+(s*.007,.002,.001)],.0018,'torso')
    # A hairline at the sternum, not a deep triangular gap between the shells.
    line('Joint',[chest_point(0,v,y)+(0,0,.0007) for v in np.linspace(0,.80,20)],.0009,'torso')

def clavicle(y=1.08):
    """The chest shell flows up into a high collar around the neck."""
    across=96;rows=16;vertices=[]
    for j in range(rows+1):
        v=j/rows
        for i in range(across+1):
            u=-1+2*i/across;a=abs(u)*math.pi/2
            low=chest_point(u,1,y)
            high=np.array((.149*math.sin(u*math.pi/2),
                y+.147+.047*math.exp(-((abs(u)-.20)/.19)**2),.042*math.cos(a)-.002))
            p=low*(1-v)+high*v;p[2]+=.004*math.sin(math.pi*v)*math.cos(a)
            vertices.append(p)
    faces=[]
    for j in range(rows):
        for i in range(across):
            a=j*(across+1)+i;b=a+1;c=a+across+1
            faces.extend([(a,b,c),(b,c+1,c)])
    m.add('Ceramic',vertices,faces,m.rigid('torso'))
    # This raised collar is visible from behind as well as from the front.
    # Give it a real inside surface instead of a one-sided sheet.
    inside=[p-(0,0,.006) for p in vertices]
    m.add('Ceramic',inside,[(a,c,b) for a,b,c in faces],m.rigid('torso'))
    # Rolled collar edge and restrained throat transponder.
    for i in range(across):
        a=vertices[rows*(across+1)+i];b=vertices[rows*(across+1)+i+1]
        quad('Edge',[a,b,b-(0,.002,.004),a-(0,.002,.004)],'torso',(0,1,1))
    plate('Blue',[(-.006,.012),(.006,.012),(0,-.010)],
          (0,y+.138,.058),'torso',thickness=.002,bulge=.002)
    # Side/back collar completes the neck opening instead of two loose tabs.
    vertices=[];count=64
    for v in [0,.25,.5,.75,1]:
        for a in np.linspace(.36*math.pi,1.64*math.pi,count+1):
            vertices.append((.047*math.sin(a),y+.137+v*.057,.043*math.cos(a)-.008))
    faces=[]
    for j in range(4):
        for i in range(count):
            a=j*(count+1)+i;b=a+1;c=a+count+1
            faces.extend([(a,b,c),(b,c+1,c)])
    m.add('Ceramic',vertices,faces,m.rigid('torso'))

def visor(y,bone):
    """A smooth glass shell with a tailored jaw and forehead silhouette."""
    across=64;rows=64;vertices=[]
    def height(x,yy):
        return .071+.047*math.sqrt(max(.03,1-(x/.090)**2-((yy+.004)/.140)**2))
    widths=[.001,.027,.049,.061,.070,.073,.067,.048,.001]
    for j in range(rows+1):
        v=j/rows;yy=-.099+v*.184;width=float(m.catmull(widths,v*(len(widths)-1)))
        for i in range(across+1):
            x=width*(-1+2*i/across)
            vertices.append((x,y+yy,height(x,yy)))
    faces=[]
    for j in range(rows):
        for i in range(across):
            a=j*(across+1)+i;b=a+1;c=a+across+1
            faces.extend([(a,b,c),(b,c+1,c)])
    m.add('Glass',vertices,faces,m.rigid(bone))
    for s in [-1,1]:
        points=[(s*.007,-.015),(s*.030,-.003),(s*.058,.009)]
        line('Blue',[(x,y+yy,height(x,yy)+.002) for x,yy in points],.0027,bone)
        points=[(s*.007,-.015),(s*.008,-.027)]
        line('Core',[(x,y+yy,height(x,yy)+.0025) for x,yy in points],.0013,bone)

def abdomen(y=1.08):
    """A continuous fitted abdominal cuirass with a V around the blue reactor."""
    def point(u,v):
        a=abs(u)*math.pi/2
        width=.062+.036*v+.012*(1-v)**6
        x=math.copysign(width*math.sin(a),u)
        low=y-.246+.028*math.sin(a)**2
        high=y-.080+.064*math.sin(a)**.8
        z=(.089+.011*math.sin(math.pi*v))*math.cos(a)+.009*(1-v)
        return np.array((x,low*(1-v)+high*v,z))
    across=96;rows=40
    vertices=[point(-1+2*i/across,j/rows) for j in range(rows+1) for i in range(across+1)]
    faces=[]
    for j in range(rows):
        for i in range(across):
            a=j*(across+1)+i;b=a+1;c=a+across+1
            faces.extend([(a,b,c),(b,c+1,c)])
    m.add('Ceramic',vertices,faces,m.rigid('torso'))
    for v in [0,1]:
        for i in range(across):
            a=point(-1+2*i/across,v);b=point(-1+2*(i+1)/across,v)
            quad('Porcelain',[a,b,b-(0,0,.006),a-(0,0,.006)],'torso',(0,2*v-1,0))
    line('Joint',[point(0,v)+(0,0,.0006) for v in np.linspace(.035,.96,30)],.0009,'torso')
    for s in [-1,1]:
        line('Joint',[point(s*u,.46+.15*u)+(0,0,.0006) for u in np.linspace(.02,.95,30)],.001,'torso')

def head(y=1.43):
    import seraph_finish
    seraph_finish.helmet(y)

def torso(y=1.08):
    b='torso'
    profile('Frame',[(0,y-.29,0),(0,y-.20,0),(0,y-.08,0),(0,y+.04,0),(0,y+.13,0)],
            [(.115,.073),(.081,.062),(.103,.070),(.147,.077),(.089,.055)],b)
    chest(y);clavicle(y);abdomen(y)
    for s in [-1,1]:
        for k in range(4):
            box('Joint',(s*(.084+k*.006),y-.17+k*.034,.045),(.027,.014,.039),b)
        line('Edge',[(s*.119,y+.048,.041),(s*.109,y-.025,.050),(s*.078,y-.10,.033)],.005,b)
        plate('Ceramic',[(-.040,.105),(.035,.099),(.041,-.095),(.010,-.143),(-.028,-.105)],
              (s*.076,y-.012,-.077),b,u=(-s,0,0),thickness=.018,bulge=.027)
        for k in range(3):
            box('Frame',(s*.078,y+.02-k*.027,-.103),(.039,.009,.007),b,u=(-1,0,0))
        bolt((s*.127,y+.09,.045),.007,b)
    plate('Frame',[(-.030,.035),(.030,.035),(0,-.035)],
          (0,y-.058,.085),b,thickness=.004,bulge=.006)
    plate('Blue',[(0,.012),(.006,0),(0,-.015),(-.006,0)],(0,y-.043,.091),b,thickness=.003)
    box('Frame',(0,y-.015,-.093),(.066,.246,.035),b)
    for k in range(6):
        box('Edge',(0,y+.088-k*.035,-.117),(.048,.018,.015),b)
        box('Blue',(0,y+.089-k*.035,-.128),(.012,.010,.004),b,u=(-1,0,0))

def petal(bone,sign,upper):
    if upper:
        points=[(sign*.16,1.16,-.15),(sign*.25,1.30,-.19),(sign*.35,1.51,-.23),(sign*.48,1.72,-.27)]
        widths=[.018,.063,.070,.0005];depths=[.017,.029,.025,.0005]
    else:
        points=[(sign*.16,1.05,-.16),(sign*.27,.985,-.20),(sign*.37,.895,-.24),(sign*.48,.775,-.28)]
        widths=[.018,.070,.054,.0005];depths=[.017,.028,.021,.0005]
    rod('Frame',points[0],points[1],.023,bone)
    joint(points[0],.030,bone,normal=(0,0,1))
    # Mechanical base, pointed ceramic fairing and a recessed light well.
    profile('Joint',points,[(w*.88,d*.85) for w,d in zip(widths,depths)],bone,32,9)
    profile('Porcelain',points,list(zip(widths,depths)),bone,48,11)
    for direction in [-1,1]:
        p=np.array(points);p[0]=p[0]*.20+p[1]*.80;p[-1]=p[-1]*.87+p[-2]*.13;p[:,2]+=direction*.027
        profile('Frame',p,[(.012,.004),(.029,.009),(.027,.007),(.0005,.0005)],bone,32,9)
        q=p.copy();q[0]=q[0]*.4+q[1]*.6;q[:,2]+=direction*.007
        profile('Blue',q,[(.001,.002),(.009,.003),(.012,.003),(.0005,.0005)],bone,32,9)
        seam=np.array(points);seam[1,0]+=sign*.036;seam[2,0]+=sign*.035;seam[:,2]+=direction*.018
        line('Edge',seam[1:3],.002,bone)
    c=np.array(points[0])*.35+np.array(points[1])*.65
    box('Joint',c+np.array((0,0,.029)),(.045,.055,.024),bone)
    bolt(c+np.array((0,0,.044)),.009,bone)

def mobile_details():
    for side,s in [('l',-1),('r',1)]:
        arm='arm_'+side;fore='fore_'+side;leg='leg_'+side;boot='boot_'+side
        joint((s*.19,1.17,0),.043,arm)
        joint((s*.275,.95,.01),.029,fore)
        joint((s*.104,.43,-.01),.029,boot)
        plate('Porcelain',[(-.037,.035),(.035,.035),(.054,-.006),(.036,-.055),(-.019,-.044)],
              (s*.205,1.195,.040),arm,u=(s,0,0),thickness=.018,bulge=.022)
        line('Joint',[(s*.181,1.213,.054),(s*.218,1.203,.062),(s*.245,1.175,.042)],.0025,arm)
        for k in range(3):box('Frame',(s*.231,1.186-k*.012,.055),(.020,.004,.004),arm)
        plate('Porcelain',[(-.021,.063),(.018,.056),(.029,-.023),(.002,-.069),(-.025,-.026)],
              (s*.243,1.055,.048),arm,thickness=.011,bulge=.026)
        for k in range(3):
            box('Joint',(s*.269,.981+k*.014,-.013),(.040,.007,.026),arm)
        # Separate forearm plates, long accelerator and small heat-sink slots.
        plate('Porcelain',[(-.032,.078),(.027,.066),(.031,-.042),(.008,-.077),(-.026,-.057)],
              (s*.294,.839,.069),fore,thickness=.012,bulge=.020)
        box('Joint',(s*.298,.782,.093),(.039,.166,.027),fore)
        for k in range(5):box('Frame',(s*.298,.823-k*.014,.110),(.026,.004,.004),fore)
        box('Ceramic',(s*.322,.788,.094),(.014,.151,.023),fore)
        bolt((s*.322,.845,.108),.006,fore)
        # Hip bearings, armor breaks and white rear thigh coverage.
        joint((s*.105,.792,-.007),.047,leg)
        plate('Ceramic',[(-.038,.075),(.031,.071),(.047,-.052),(.009,-.137),(-.030,-.077)],
              (s*.118,.663,.061),leg,thickness=.012,bulge=.034)
        plate('Porcelain',[(-.030,.056),(.026,.052),(.033,-.056),(0,-.091),(-.025,-.044)],
              (s*.111,.611,-.074),leg,u=(-1,0,0),thickness=.010,bulge=.024)
        line('Joint',[(s*.135,.745,.071),(s*.151,.678,.075),(s*.137,.583,.065)],.0024,leg)
        bolt((s*.151,.746,.043),.007,leg)
        plate('Ceramic',[(-.022,.026),(.023,.026),(.031,-.014),(0,-.046),(-.029,-.015)],
              (s*.106,.427,.050),boot,thickness=.013,bulge=.022)
        plate('Blue',[(-.006,.009),(.006,.009),(0,-.014)],(s*.106,.433,.064),boot,thickness=.003)
        plate('Ceramic',[(-.028,.089),(.026,.081),(.022,-.058),(0,-.105),(-.022,-.065)],
              (s*.116,.281,.054),boot,thickness=.013,bulge=.021)
        line('Joint',[(s*.137,.355,.045),(s*.145,.275,.044),(s*.127,.155,.048)],.0023,boot)
        for k in range(3):box('Joint',(s*.118,.136+k*.012,-.025),(.040,.005,.024),boot)
        plate('Porcelain',[(-.023,.036),(.020,.036),(.026,-.025),(.004,-.060),(-.022,-.034)],
              (s*.118,.078,.083),boot,thickness=.012,bulge=.014)
        for z in [.035,.071,.103]:line('Joint',[(s*.094,.064,z),(s*.118,.074,z),(s*.143,.064,z)],.0023,boot)

def arc(mat,center,radius,width,depth,start,end,bone='halo',steps=64):
    center=np.array(center);angles=np.linspace(math.radians(start),math.radians(end),steps+1)
    # Independent front, back and radial walls preserve crisp section edges.
    for surface in range(4):
        verts=[]
        for a in angles:
            radial=np.array((math.sin(a),math.cos(a),0))
            section=[(-width/2,-depth/2),(width/2,-depth/2),(width/2,depth/2),(-width/2,depth/2)]
            for i in [surface,(surface+1)%4]:
                dr,z=section[i];verts.append(center+radial*(radius+dr)+(0,0,z))
        faces=[]
        for i in range(steps):
            a=i*2;faces.extend([(a,a+2,a+1),(a+1,a+2,a+3)])
        # Verify each strip faces away from the ring's rectangular core.
        tri=np.array(verts)[list(faces[len(faces)//2])]
        mid=tri.mean(axis=0)-center;radial=m.norm((mid[0],mid[1],0))
        desired=[np.array((0,0,-1)),radial,np.array((0,0,1)),-radial][surface]
        if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),desired)<0:faces=[(a,c,b) for a,b,c in faces]
        m.add(mat,verts,faces,m.rigid(bone))

def halo():
    c=np.array((0,1.55,-.65));b='halo'
    for start in [12,132,252]:
        arc('Frame',c,.62,.13,.095,start,start+103,b)
        arc('Edge',c+(0,0,.051),.621,.121,.012,start+1,start+102,b)
        arc('Blue',c+(0,0,.060),.613,.077,.009,start+3,start+100,b)
        arc('Ceramic',c+(0,0,.053),.674,.019,.025,start,start+103,b)
        for angle in np.linspace(start+3,start+100,7):
            a=math.radians(angle);r=np.array((math.sin(a),math.cos(a),0));t=np.array((math.cos(a),-math.sin(a),0))
            box('Joint',c+r*.622+(0,0,.073),(.013,.127,.030),b,u=t,v=r)
        for angle in [start,start+103]:
            a=math.radians(angle);r=np.array((math.sin(a),math.cos(a),0));t=np.array((math.cos(a),-math.sin(a),0))
            box('Ceramic',c+r*.622+(0,0,.026),(.045,.160,.133),b,u=t,v=r)
            bolt(c+r*.662+(0,0,.096),.009,b)
    for s in [-1,1]:
        rod('Frame',(s*.33,.69,-.56),(s*.40,1.08,-.65),.049)
        rod('Edge',(s*.33,.75,-.56),(s*.39,1.04,-.65),.021)

def cell_position(s,rear,row,col):
    z=-.59 if rear else .28;dx=-.17 if rear else 0;extra=.16 if rear else 0
    pts=[(s*(.79+dx),.46,z),(s*(1.16+dx),.85,z-.04),(s*(1.36+dx),1.50,z-.12),(s*(1.20+dx),2.18+extra,z-.22),(s*(.97+dx),2.59+extra,z-.26)]
    c=m.catmull(pts,1+(row+.6)/6.9*2.4)
    return c+np.array([col*(.145 if row<4 else .12),0,.166]),.116 if row<4 else .096

def shield_details(bone,s,rear):
    z=-.59 if rear else .28;dx=-.17 if rear else 0;extra=.16 if rear else 0
    pts=[(s*(.79+dx),.46,z),(s*(1.16+dx),.85,z-.04),(s*(1.36+dx),1.50,z-.12),(s*(1.20+dx),2.18+extra,z-.22),(s*(.97+dx),2.59+extra,z-.26)]
    # Raised transverse frames and distinct individual missile cells.
    for row in range(6):
        f=(row+.6)/6.9;c=m.catmull(pts,1+f*2.4)
        for col in [-1,0,1]:
            p=c+np.array([col*(.145 if row<4 else .12),0,.166]);w=.116 if row<4 else .096
            box('Frame',p,(w,.205,.045),bone)
            box('Joint',p+(0,0,.025),(w*.78,.169,.018),bone,bevel=.004)
            rack=(2 if rear else 0)+(0 if s<0 else 1);hatch=f'hatch_{rack}_{row}_{col+1}'
            box('Ceramic',p+(0,0,.038),(w*.70,.153,.016),hatch,bevel=.004)
            box('Blue',p+(w*.27,0,.048),(.004,.096,.004),hatch,bevel=.001)
            box('Ink',p+(-w*.16,-.046,.048),(w*.27,.006,.002),hatch,bevel=.0005)
            for dy in [-.077,.077]:bolt(p+np.array((-w*.28,dy,.039)),.005,bone)
        box('Joint',c+(0,-.119,.12),(.43 if row<4 else .35,.024,.080),bone)
    for direction in [-1,1]:
        edge=np.array(pts[1:4]);edge[:,0]+=s*direction*np.array([.28,.29,.19]);edge[:,2]+=.088
        line('Joint',edge,.018,bone)
        line('Ceramic',edge+np.array([s*direction*.013,0,.004]),.012,bone)
        for row in [1.25,1.90,2.55,3.10]:
            p=m.catmull(pts,row);p[0]+=s*direction*(.265 if row<2.6 else .21);p[2]+=.098
            bolt(p,.012,bone)
    # Broad rear armor follows the curvature, with panel splits, ribs and ducts.
    for t in [1.25,1.85,2.45,3.05]:
        p=m.catmull(pts,t)
        plate('Ceramic',[(-.18,.14),(.16,.15),(.20,-.08),(.07,-.17),(-.16,-.12)],
              p+np.array([0,0,-.137]),bone,u=(-1,0,0),thickness=.016,bulge=.012)
        for k in range(3):box('Frame',p+np.array([0,.045-k*.026,-.158]),(.17,.008,.009),bone,u=(-1,0,0))
    base=np.array(pts[0])
    joint(base+np.array([0,.03,0]),.13,bone,normal=(0,0,1))
    rod('Frame',base+np.array([-s*.17,.0,-.08]),m.catmull(pts,1.15)+np.array([-s*.18,0,-.11]),.046,bone)
    rod('Edge',base+np.array([-s*.17,.04,-.08]),m.catmull(pts,1.15)+np.array([-s*.18,-.03,-.11]),.020,bone)

def cannon():
    b='cannon';c=np.array((0,.94,0))
    # Broad octagonal breech and a rectangular rail assembly, as in the sheet.
    disc('Frame',c+(0,0,.55),.277,.42,b,sides=12)
    disc('Ceramic',c+(0,0,.755),.261,.12,b,sides=12)
    disc('Joint',c+(0,0,.821),.213,.035,b,sides=16)
    disc('Blue',c+(0,0,.843),.187,.014,b,sides=48)
    disc('Frame',c+(0,0,.855),.151,.026,b,sides=16)
    for a in np.arange(8)*math.tau/8:
        p=c+np.array((math.cos(a)*.228,math.sin(a)*.228,.824));bolt(p,.014,b)
    box('Frame',c+(0,0,1.49),(.222,.198,1.38),b,bevel=.018)
    for s in [-1,1]:
        # Side windows expose the long blue accelerator beneath white armor.
        box('Joint',c+(s*.126,0,1.50),(.035,.134,1.34),b,bevel=.006)
        box('Blue',c+(s*.148,0,1.51),(.009,.047,1.24),b,bevel=.003)
        plate('Porcelain',[(-.073,-.64),(.073,-.64),(.086,.42),(.048,.70),(-.048,.70),(-.086,.42)],
              c+(0,s*.132,1.45),b,u=(1,0,0),v=(0,0,-s),thickness=.029,bulge=.016)
        for z in [.93,1.42,1.97]:
            box('Edge',c+(s*.141,s*.071,z),(.032,.039,.070),b)
            bolt(c+(s*.164,s*.071,z),.009,b,normal=(s,0,0))
    disc('Joint',c+(0,0,2.135),.153,.104,b,sides=8)
    disc('Ceramic',c+(0,0,2.190),.138,.034,b,sides=8)
    disc('Glass',c+(0,0,2.211),.108,.015,b,sides=8)
    disc('Blue',c+(0,0,2.222),.073,.007,b,sides=32)
    box('Core',c+(0,0,2.229),(.090,.023,.005),b,bevel=.001)
    for s in [-1,1]:
        box('Ceramic',c+(s*.244,-.041,.47),(.094,.280,.42),b,bevel=.012)
        for k in range(4):box('Frame',c+(s*.247,.05-k*.035,.685),(.059,.013,.012),b)

def siege_details():
    for i in range(12):
        a=i*math.tau/12;r=np.array((math.sin(a),0,math.cos(a)));t=np.array((math.cos(a),0,-math.sin(a)))
        box('Frame',r*.86+(0,.62,0),(.36,.074,.17),u=t)
        box('Ceramic',r*.87+(0,.668,0),(.35,.037,.20),u=t,bevel=.012)
        for s in [-1,1]:
            bolt(r*1.015+t*s*.145+(0,.53,0),.013,normal=r)
        for k in range(4):
            box('Joint',r*.85+t*(k-1.5)*.038+(0,.245,0),(.017,.070,.06),u=t)
        box('Blue',r*.94+(0,.279,0),(.16,.014,.012),u=t)
    for i in range(6):
        a=(i+.5)*math.tau/6;r=np.array((math.sin(a),0,math.cos(a)));t=np.array((math.cos(a),0,-math.sin(a)));b='anchor_'+str(i)
        p=r*.78+(0,.53,0);k=r*1.28+(0,.38,0);foot=r*1.53+(0,.105,0)
        joint(k,.102,b,normal=t)
        for s in [-1,1]:
            rod('Frame',p+t*s*.075,k+t*s*.075,.049,b)
            rod('Edge',p+t*s*.075+(0,-.01,0),k+t*s*.075,.020,b)
        plate('Porcelain',[(-.104,.132),(.104,.132),(.127,-.092),(.065,-.152),(-.065,-.152),(-.127,-.092)],
              k+r*.151,b,u=t,thickness=.038,bulge=.025)
        box('Blue',k+r*.175+(0,.025,0),(.018,.158,.008),b,u=t)
        for s in [-1,1]:
            bolt(k+r*.172+t*s*.080+(0,.094,0),.010,b,normal=r)
        for toe in [-1,0,1]:
            q=foot+t*toe*.12+r*.08
            box('Frame',q+(0,-.054,0),(.104,.070,.37),b,u=t,bevel=.012)
            box('Ceramic',q+(0,-.003,.0),(.090,.073,.29),b,u=t,bevel=.011)
    for s in [-1,1]:
        plate('Ceramic',[(-.092,.22),(.088,.20),(.11,-.05),(.039,-.14),(-.072,-.086)],
              (s*.315,1.06,-.18),u=(s,0,0),thickness=.038,bulge=.026)
        box('Joint',(s*.293,.85,.22),(.130,.13,.29))
        box('Ceramic',(s*.293,.925,.20),(.125,.045,.28))
        for k in range(3):box('Blue',(s*(.265+k*.024),.952,.24),(.010,.006,.031))
    # Recessed central console and backrest frame make the pilot part of the machine.
    box('Frame',(0,1.01,-.14),(.36,.43,.12))
    for s in [-1,1]:
        plate('Porcelain',[(-.032,.23),(.037,.25),(.081,.15),(.048,-.16),(-.031,-.18)],
              (s*.19,1.14,-.12),u=(s,0,0),thickness=.052,bulge=.025)
