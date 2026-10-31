"""Authored ceramic shells and engineering detail for the production Seraph.

All attachments keep the established bones. Broad armor surfaces, fine seams,
vents and mechanical joints are actual geometry. The approved reconstruction
is used for proportions and anchor placement, never as a painted substitute
for manufactured armor edges.
"""
import math
import numpy as np
import mesh_tools as m
import seraph_refinement as d


def transform_new(callback, transform):
    starts = {key: len(value["v"]) for key, value in m.PARTS.items()}
    callback()
    for key, part in m.PARTS.items():
        start = starts[key]
        if len(part["v"]) > start:
            points = transform(np.asarray(part["v"][start:], float))
            part["v"][start:] = points.tolist()


def sculpt_body(mesh):
    """Keep the proven limb surface, replace the noisy head, recess the torso."""
    vertices = np.asarray(mesh.vertices).copy()
    # The authored shell covers the reconstructed chest without a second
    # competing surface poking through it.  The rear torso and all joint
    # transition vertices retain their original position.
    region = ((vertices[:, 1] > 1.177) & (vertices[:, 1] < 1.407)
              & (np.abs(vertices[:, 0]) < .149) & (vertices[:, 2] > .0))
    vertices[region, 2] *= .52
    abdomen=((vertices[:,1]>1.032)&(vertices[:,1]<1.205)
             &(np.abs(vertices[:,0])<.065)&(vertices[:,2]>0))
    vertices[abdomen,2]*=.52
    knee=(vertices[:,1]<.715)&(vertices[:,1]>.615)
    blend=np.clip((.715-vertices[knee,1])/.065,0,1)
    vertices[knee,2]*=(1-.40*blend)
    signed=np.sign(vertices[knee,0])
    vertices[knee,0]=signed*.108+(vertices[knee,0]-signed*.108)*(1-.12*blend)
    mesh.vertices = vertices
    # Replace exposed joint/armor margins too. Merely painting these black
    # would leave chipped reconstructed contours along the clean new shells.
    replaced = (vertices[:,1]>1.375)|(vertices[:,1]<1.037)|(np.abs(vertices[:,0])>.145)
    return mesh.submesh([np.flatnonzero(~replaced[mesh.faces].any(axis=1))],
                        append=True, repair=False)


def torso_and_helmet():
    # Reconstructed silhouette, manufactured surface.  This continuous breast-
    # plate has shallow feminine fullness and an actual rolled lower edge.
    def chest_transform(points):
        points[:, 2] *= 1.12
        return points
    transform_new(lambda: d.chest(1.25), chest_transform)

    def collar_transform(points):
        points[:, 2] *= 1.12
        high = points[:, 1] > 1.386
        points[high, 1] = 1.386 + (points[high, 1] - 1.386) * .47
        return points
    transform_new(lambda: d.clavicle(1.25), collar_transform)
    d.head(1.525)
    # Separate overlapping abdominal lames retain a slim waist, instead of a
    # long soft white patch with painted seams.
    for i in range(3):
        width=.049-i*.006;y=1.165-i*.043
        d.plate("Joint",[(-width,.025),(width,.025),(width*.78,-.017),
                         (0,-.034),(-width*.78,-.017)],(0,y,.088-i*.004),
                "torso",thickness=.008,bulge=.008)
        d.plate("Ceramic",[(-width*.93,.021),(width*.93,.021),(width*.72,-.014),
                            (0,-.029),(-width*.72,-.014)],(0,y+.003,.096-i*.004),
                "torso",thickness=.005,bulge=.012)
    # A tapered rear crown spine and cheek vents read clearly from gameplay's
    # rear camera, without covering the dark visor in luminous paint.
    for sign in [-1, 1]:
        d.line("Joint", [(sign*.024,1.628,-.045),(sign*.030,1.588,-.081),
                         (sign*.037,1.536,-.089)], .0019, "head")
        for y in [1.495,1.504,1.513]:
            d.box("Frame",(sign*.080,y,.061),(.017,.0024,.003),"head",
                  u=(sign,0,0),bevel=.0005)
        d.line("Blue",[(sign*.028,1.587,-.084),(sign*.031,1.553,-.091)],.0018,"head")
        # Layered scapular plates and service strips flank the wing pivots.
        d.plate("Ceramic",[(-.035,.068),(.025,.076),(.048,.035),(.039,-.075),
                            (-.018,-.09),(-.040,-.025)],
                (sign*.066,1.294,-.111),"torso",u=(-sign,0,0),
                thickness=.009,bulge=.016)
        for y in [1.284,1.300,1.316]:
            d.box("Frame",(sign*.067,y,-.132),(.042,.005,.004),"torso",
                  u=(-1,0,0),bevel=.001)
        d.plate("Porcelain", [(-.018,.022),(.022,.018),(.018,-.022),(-.010,-.031)],
                (sign*.084,1.173,-.096),"torso",u=(-1,0,0),
                thickness=.007,bulge=.009)
        d.line("Blue",[(sign*.052,1.365,.069),(sign*.078,1.373,.063)],.0022,"torso")
    # Segmented armored spine, integrated into the dark flexible waist.
    for y in np.linspace(1.091,1.335,8):
        depth=-.098 if y>1.19 else -.087
        d.box("Joint",(0,y,depth),(.034,.022,.012),"torso",u=(-1,0,0),bevel=.003)
        d.box("Blue",(0,y,depth-.008),(.006,.010,.003),"torso",u=(-1,0,0),bevel=.001)
    d.plate("Frame",[(-.023,.017),(.023,.017),(0,-.024)],(0,1.201,.107),
            "torso",thickness=.005,bulge=.004)
    d.plate("Blue",[(0,.011),(.006,0),(0,-.012),(-.006,0)],(0,1.198,.114),
            "torso",thickness=.003,bulge=.002)


def greaves(side,sign):
    """Layered, slender ceramic shin shells and pointed mechanical feet."""
    bone="boot_"+side
    def p(x,y,z):
        return (sign*x,y,z)
    # Dark load-bearing structure, exposed between front/back armor.
    d.profile("Frame",[p(.108,.669,.01),p(.112,.52,.0),p(.118,.31,-.004),p(.121,.13,-.006)],
              [(.027,.028),(.032,.033),(.029,.031),(.020,.022)],bone,sides=40,steps=12)
    # Long leaf-like greave narrows into the ankle instead of a cylinder.
    d.profile("Porcelain",[p(.108,.633,.030),p(.115,.524,.041),p(.120,.365,.030),
                           p(.122,.195,.026),p(.122,.127,.022)],
              [(.019,.018),(.050,.043),(.039,.034),(.023,.023),(.012,.013)],
              bone,sides=64,steps=16)
    d.profile("Ceramic",[p(.107,.591,-.025),p(.112,.468,-.042),p(.120,.278,-.029),p(.121,.147,-.010)],
              [(.020,.020),(.034,.033),(.024,.023),(.009,.011)],bone,sides=48,steps=12)
    # Narrow recessed blue spine with pale specular edge.
    front=[p(.111,.571,.075),p(.118,.482,.084),p(.121,.348,.066),p(.122,.211,.048)]
    d.line("Joint",front,.0055,bone)
    d.line("Blue",np.asarray(front)+[0,0,.003],.0027,bone)
    d.line("Core",np.asarray(front)[1:3]+[sign*.0015,0,.006],.0008,bone)
    for s in [-1,1]:
        d.line("Joint",[p(.112+s*.025,.554,.060),p(.119+s*.025,.431,.058),
                        p(.122+s*.014,.240,.044)],.0014,bone)
    # Exposed ankle bearing above the armored toe.
    d.joint(p(.122,.130,-.002),.023,bone,(sign,0,0))
    d.disc("Blue",p(.145,.130,-.002),.008,.002,bone,(sign,0,0),32)
    d.profile("Ceramic",[p(.122,.126,-.035),p(.122,.085,.020),p(.122,.038,.134)],
              [(.022,.021),(.033,.029),(.001,.002)],bone,sides=48,steps=16)
    d.profile("Frame",[p(.122,.063,-.027),p(.122,.040,.045),p(.122,.026,.132)],
              [(.025,.012),(.034,.010),(.001,.002)],bone,sides=40,steps=12)
    d.box("Joint",p(.122,.052,-.044),(.030,.080,.034),bone,bevel=.004)
    d.box("Edge",p(.122,.012,-.046),(.034,.014,.036),bone,bevel=.002)
    for y,z,width in [(.104,.040,.024),(.083,.071,.021),(.060,.101,.014)]:
        d.line("Joint",[p(.122-width,y-.005,z),p(.122,y+.004,z+.003),
                        p(.122+width,y-.005,z)],.0016,bone)
    # Floating shield over the knee junction, angled and diamond tipped.
    d.plate("Frame",[(-.024,.035),(.024,.035),(.029,-.006),(0,-.046),(-.029,-.006)],
            p(.108,.651,.077),bone,thickness=.007,bulge=.012)
    d.plate("Porcelain",[(-.019,.029),(.019,.029),(.022,-.004),(0,-.038),(-.022,-.004)],
            p(.108,.653,.087),bone,thickness=.006,bulge=.015)
    d.plate("Blue",[(-.008,.013),(.008,.013),(0,-.012)],p(.108,.653,.108),
            bone,thickness=.003,bulge=.002)


def limbs(side,sign,bones):
    """Manufactured upper limbs, clean joint envelopes and separate hip armor."""
    anchors=dict(bones)
    arm="arm_"+side;leg="leg_"+side
    shoulder=anchors[arm];elbow=anchors["fore_"+side]
    axis=(elbow-shoulder)/np.linalg.norm(elbow-shoulder)
    def arm_at(t,z=0):
        return shoulder+axis*t+[0,0,z]
    d.orb("Frame",shoulder,(.041,.042,.040),arm)
    d.joint(shoulder+[sign*.027,0,0],.029,arm,(sign,0,0))
    d.profile("Frame",[arm_at(.012),arm_at(.113),arm_at(.211)],
              [(.030,.032),(.033,.035),(.025,.027)],arm,sides=40,steps=12)
    # Shoulder shell is broad and shallow, flowing into a tapered biceps plate.
    import seraph_finish
    seraph_finish.shoulder(arm, sign)
    d.profile("Ceramic",[arm_at(.057,.021),arm_at(.123,.030),arm_at(.188,.016)],
              [(.026,.022),(.030,.025),(.012,.013)],arm,sides=48,steps=12)
    for k in range(3):
        d.box("Frame",arm_at(.103+k*.013,.053),(.018,.003,.003),arm,bevel=.0005)
    d.rod("Joint",arm_at(.055,-.031),arm_at(.184,-.026),.008,arm,20)
    d.rod("Edge",arm_at(.116,-.033),arm_at(.201,-.025),.0035,arm,16)

    def p(x,y,z):
        return (sign*x,y,z)
    # The upper leg is an articulated assembly around the existing hip/knee.
    # Front and rear plates sit on opposite sides of a black tendon channel.
    d.orb("Frame",p(.086,1.020,-.005),(.055,.057,.054),leg)
    d.profile("Frame",[p(.086,1.019,-.005),p(.105,.870,-.005),p(.108,.674,.012)],
              [(.050,.052),(.052,.055),(.026,.030)],leg,sides=48,steps=16)
    d.profile("Porcelain",[p(.100,1.002,.026),p(.117,.904,.036),
                            p(.118,.795,.034),p(.109,.710,.018)],
              [(.035,.030),(.060,.056),(.043,.040),(.010,.012)],leg,sides=64,steps=16)
    d.profile("Ceramic",[p(.092,.991,-.026),p(.106,.889,-.045),p(.108,.729,-.018)],
              [(.032,.026),(.045,.033),(.011,.012)],leg,sides=56,steps=14)
    # Narrow channels between layered plates reveal dark mechanical structure.
    d.line("Joint",[p(.125,.965,.089),p(.144,.891,.090),
                    p(.134,.801,.073),p(.114,.738,.043)],.0014,leg)
    d.line("Joint",[p(.111,.966,-.058),p(.123,.869,-.076),
                    p(.112,.763,-.048)],.0017,leg)
    for k in range(3):
        d.box("Ink",p(.124,.946-k*.010,.100),(.023,.002,.002),leg,bevel=.0004)
    # Swept hip petal gives the reference's feminine silhouette; it is one
    # clean shell per side, not the reconstructed clusters of feather fingers.
    d.profile("Frame",[p(.122,1.038,.002),p(.166,.989,.014),p(.171,.935,.006)],
              [(.023,.025),(.034,.030),(.002,.002)],leg,sides=48,steps=12)
    d.profile("Ceramic",[p(.121,1.041,.024),p(.163,1.000,.037),p(.176,.929,.017)],
              [(.019,.016),(.031,.027),(.001,.002)],leg,sides=56,steps=14)
    d.line("Joint",[p(.137,1.029,.044),p(.167,.988,.063),p(.174,.949,.037)],.0015,leg)
    d.disc("Joint",p(.142,1.025,.018),.025,.013,leg,(sign,0,0),40)
    d.disc("Blue",p(.151,1.025,.018),.009,.003,leg,(sign,0,0),32)
    # Continuous knee bearing physically connects both moving assemblies.
    d.orb("Frame",p(.108,.674,.012),(.030,.032,.030),"boot_"+side)
    d.joint(p(.108,.674,.012),.025,"boot_"+side,(sign,0,0))


def pelvis():
    d.orb("Frame",(0,1.023,-.005),(.111,.072,.070),"root")
    d.plate("Joint",[(-.047,.031),(.047,.031),(.059,-.004),
                     (0,-.074),(-.059,-.004)],(0,1.030,.058),
            "root",thickness=.012,bulge=.009)
    d.plate("Ceramic",[(-.041,.028),(.041,.028),(.049,-.003),
                       (0,-.066),(-.049,-.003)],(0,1.032,.068),
            "root",thickness=.008,bulge=.013)
    for sign in [-1,1]:
        d.plate("Ceramic",[(-.037,.032),(.032,.033),(.039,-.020),
                           (.012,-.055),(-.035,-.035)],(sign*.047,1.017,-.068),
                "root",u=(-1,0,0),thickness=.007,bulge=.014)


def core():
    """Closed, smooth undersuit with no clipped reconstruction boundaries."""
    d.profile("Frame",[(0,.995,0),(0,1.052,0),(0,1.141,0),(0,1.258,0),(0,1.372,-.005)],
              [(.093,.057),(.096,.063),(.078,.063),(.132,.074),(.123,.061)],
              "torso",sides=64,steps=18)
    for sign in [-1,1]:
        for k in range(4):
            y=1.182+k*.025
            d.line("Joint",[(sign*(.086+k*.009),y,.027),
                            (sign*(.086+k*.009),y-.010,-.015),
                            (sign*(.077+k*.009),y-.015,-.046)],.0023,"torso")


def wing_detail(bone, sign, upper):
    hinge = np.array([sign*.155,1.36,-.065] if upper else [sign*.15,1.22,-.08])
    tip = np.array([sign*.48,1.79,-.14] if upper else [sign*.49,.91,-.14])
    start = hinge+[sign*.055,.055 if upper else -.04,-.015]
    direction=tip-start
    along=direction/np.linalg.norm(direction)
    across=np.array([along[1],-along[0],0])
    # Distinct root yoke and inset fasteners: wing hinges no longer end in a
    # smooth white lump. All pieces move with the original wing.
    for face in [-1,1]:
        normal=np.array([0,0,face])
        root=start+direction*.10
        d.box("Frame",root+normal*.026,(.041,.068,.014),bone,u=across,v=along,bevel=.004)
        d.box("Ceramic",root+normal*.035,(.029,.057,.008),bone,u=across,v=along,bevel=.003)
        d.bolt(root+normal*.041,.005,bone,normal)
        # Fine articulated panel cut beside the luminous inset, stopping before
        # the tip so the main petal remains a clean uninterrupted silhouette.
        seam=[]
        for t in np.linspace(.28,.75,16):
            offset=.046*math.sin((t-.08)*math.pi)
            seam.append(start+direction*t+across*sign*offset+normal*.026)
        d.line("Joint",seam,.0013,bone)
        for t in [.32,.37,.42]:
            at=start+direction*t-across*sign*.039+normal*.028
            d.box("Frame",at,(.016,.003,.003),bone,u=across,v=along,bevel=.0006)


def gauntlet_detail(side, sign, bones):
    bone="fore_"+side
    axis=np.array([sign*.40,-.916,0]);across=np.array([.916,sign*.40,0])
    elbow=dict(bones)[bone]
    def at(t,z=0):
        return elbow+axis*t+[0,0,z]
    for t in np.linspace(.105,.183,5):
        d.box("Frame",at(t,.071)+across*.037,(.013,.006,.007),bone,
              u=across,v=axis,bevel=.001)
    for s in [-1,1]:
        d.line("Joint",[at(.245,.071)+across*s*.035,
                         at(.303,.064)+across*s*.025],.0022,bone)
        d.box("Joint",at(.323,.044)+across*s*.022,(.011,.020,.025),bone,
              u=across,v=axis,bevel=.002)


def siege_detail(transform):
    # Recessed launcher rails run between rows, with cable guides and real
    # ribs instead of large empty flat black panels. Hatch bones stay untouched.
    for side,sign,rear in [("rack_l",-1,False),("rack_r",1,False),
                           ("shield_l",-1,True),("shield_r",1,True)]:
        def remap(p):
            return transform(side,np.asarray(p))
        for row in range(6):
            p,width=d.cell_position(sign,rear,row,0)
            at=remap(p+[0,-.120,.020])
            d.box("Frame",at,(.52 if row<4 else .44,.032,.058),side,bevel=.005)
            d.box("Edge",at+[0,.002,.031],(.42 if row<4 else .35,.008,.005),side,bevel=.001)
            for s in [-1,1]:
                edge=remap(p+[s*(.215 if row<4 else .185),0,-.005])
                d.box("Joint",edge,(.018,.174,.034),side,bevel=.003)
                d.box("Blue",edge+[0,0,.019],(.005,.067,.004),side,bevel=.001)
                d.bolt(edge+[0,.069,.022],.006,side)
            for col in [-1,0,1]:
                cell,w=d.cell_position(sign,rear,row,col)
                hatch=f"hatch_{(2 if rear else 0)+(0 if sign<0 else 1)}_{row}_{col+1}"
                # Shallow shoulder chamfers and a serial-like three-bar marking
                # add scale cues while keeping the 72 animated doors separate.
                for k in range(3):
                    at=transform(hatch,cell+[-w*.19+k*.014,.043,.049])
                    d.box("Ink",at,(.005,.010,.003),hatch,bevel=.0005)
        # Slender swept armor insets divide the white outer shell into nested
        # plates. Repeating the main curve is more faithful than straight bars.
        z=-.59 if rear else .28;dx=-.17 if rear else 0;extra=.16 if rear else 0
        points=[(sign*(.79+dx),.46,z),(sign*(1.16+dx),.85,z-.04),
                (sign*(1.36+dx),1.50,z-.12),(sign*(1.20+dx),2.18+extra,z-.22),
                (sign*(.97+dx),2.59+extra,z-.26)]
        for t in [1.42,2.12,2.78]:
            p=np.array(m.catmull(points,t))
            p+=np.array([sign*(.190-.030*max(0,t-2)),0,.128])
            at=remap(p)
            outline=np.array([(-.049,.137),(.031,.165),(.065,.079),(.022,-.143),(-.035,-.106)])*[.62,.85]
            d.plate("Joint",outline,at,side,u=(sign,0,0),thickness=.010,bulge=.006)
            d.plate("Porcelain",np.asarray(outline)*.91,at+[0,.004,.009],
                    side,u=(sign,0,0),thickness=.006,bulge=.008)
            d.bolt(at+[sign*.006,.100,.023],.007,side)
        # Dorsal maintenance rails sit on the flat centers of the rear plates.
        # Their low blue status lights give the camera-facing side useful detail.
        for t in [1.25,1.85,2.45,3.05]:
            p=np.array(m.catmull(points,t))+[0,0,-.157]
            at=remap(p)
            for s in [-1,1]:
                d.box("Joint",at+[s*.154,0,-.004],(.016,.175,.012),
                      side,u=(-1,0,0),bevel=.003)
                d.bolt(at+[s*.154,.064,-.013],.006,side,(0,0,-1))
            d.box("Frame",at+[0,-.084,-.007],(.115,.025,.010),side,u=(-1,0,0),bevel=.004)
            for k in [-1,0,1]:
                d.box("Blue",at+[k*.028,-.084,-.014],(.019,.004,.003),
                      side,u=(-1,0,0),bevel=.0008)
    # A rear-facing light guide complements the halo's main front emitter.
    # It remains narrower, so the player still reads the front/back orientation.
    for start in [12,132,252]:
        transform_new(lambda: d.arc("Blue",np.array([0,1.55,-.704]),.613,.042,.005,
                                     start+5,start+98,"halo",48),
                      lambda p: transform("halo",p))
    # The barrel's blue energy core sits behind dark apertures, with four
    # ceramic muzzle fingers and vented rail housings.
    muzzle=transform("cannon",np.array([0,.94,2.25]))
    for i in range(8):
        angle=i*math.tau/8
        radial=np.array([math.cos(angle),math.sin(angle),0])
        d.box("Joint",muzzle+radial*.095+[0,0,-.018],
              (.026,.042,.049),"cannon",u=np.array([-radial[1],radial[0],0]),v=radial,bevel=.003)
    for sign in [-1,1]:
        for j in range(7):
            at=np.array([sign*.177,1.093,1.58+j*.09])
            d.box("Frame",at,(.025,.048,.022),"cannon",bevel=.003)
        for z in [1.30,1.51,1.72,1.93]:
            d.box("Joint",(sign*.12,1.153,z),(.036,.028,.065),"cannon",bevel=.004)
            d.box("Porcelain",(sign*.12,1.169,z),(.030,.007,.052),"cannon",bevel=.002)
        # Paired hydraulic breech cylinders tie the accelerator into its cradle.
        d.rod("Frame",(sign*.29,.79,.75),(sign*.29,.78,1.26),.044,"cannon",24)
        d.rod("Edge",(sign*.29,.78,1.00),(sign*.29,.78,1.30),.021,"cannon",24)
        d.disc("Joint",(sign*.29,.78,1.30),.035,.016,"cannon",sides=32)
    # Treads, toe hardware and armored ankle pistons at the six deployment feet.
    for i in range(6):
        angle=(i+.5)*math.tau/6
        radial=np.array([math.sin(angle),0,math.cos(angle)])
        tangent=np.array([math.cos(angle),0,-math.sin(angle)])
        # Attach to the existing anchor bone so unfolding remains synchronized.
        bone="anchor_"+str(i)
        for k in [-1,0,1]:
            at=radial*1.90+tangent*k*.11+[0,.166,0]
            d.box("Joint",at,(.063,.015,.19),bone,u=tangent,bevel=.004)
        d.box("Blue",radial*2.105+[0,.126,0],(.11,.015,.018),bone,u=tangent,bevel=.003)
