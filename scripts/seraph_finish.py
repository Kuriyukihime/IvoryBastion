"""Layered Seraph armor and mechanisms on the proven bones and sockets.

The helmet, pauldrons, gauntlets and battery doors are real manufactured
assemblies. Body proportions, launch directions, anchors and hierarchy stay
compatible with the established animations and physical hitbox.
"""
import math
import numpy as np
import seraph_refinement as d
import mesh_tools as m


def helmet(y=1.43):
    """Fitted visor, crown segments and cheek shells; no rig changes."""
    bone = 'head'
    d.profile('Ceramic', [(0,y-.111,-.009),(0,y-.070,-.012),(0,y+.015,-.014),
                       (0,y+.082,-.015),(0,y+.115,-.018)],
              [(.024,.032),(.065,.063),(.079,.076),(.057,.059),(.001,.001)], bone,48,12)
    # Glass is a curved, tapered surface instead of a flat front badge.
    def z_at(x,yy):
        return .069+.047*math.sqrt(max(.025,1-(x/.087)**2-((yy+.003)/.137)**2))
    rows=48; across=56; vertices=[]; widths=[.001,.022,.042,.057,.065,.067,.058,.033,.001]
    for j in range(rows+1):
        yy=-.102+j/rows*.192
        width=float(m.catmull(widths,j/rows*(len(widths)-1)))
        for i in range(across+1):
            x=width*(-1+2*i/across); vertices.append((x,y+yy,z_at(x,yy)))
    faces=[]
    for j in range(rows):
        for i in range(across):
            a=j*(across+1)+i; faces.extend([(a,a+1,a+across+1),(a+1,a+across+2,a+across+1)])
    m.add('Glass',vertices,faces,m.rigid(bone))
    # The former visor was only a front surface and floated ahead of the
    # helmet in profile. A recessed seal closes its sidewall into the shell.
    for sign in [-1,1]:
        for j in range(rows):
            index=0 if sign<0 else across
            a=np.asarray(vertices[j*(across+1)+index]);b=np.asarray(vertices[(j+1)*(across+1)+index])
            rear_a=a.copy();rear_b=b.copy();rear_a[2]=.018;rear_b[2]=.018
            d.quad('Frame',[a,b,rear_b,rear_a],bone,(sign,0,0))
    for sign in [-1,1]:
        # Each lens has area and a tapered inner tip, readable at game distance.
        pts=[]; triangles=[]
        for i in range(33):
            t=i/32; x=.006+.054*t; center=-.019+.029*t
            width=.002+.004*math.sin(t*math.pi*.70)
            for yy in [center-width,center+width]: pts.append((sign*x,y+yy,z_at(sign*x,yy)+.0022))
        for i in range(32):
            a=2*i; triangles.extend([(a,a+2,a+1),(a+1,a+2,a+3)] if sign>0 else [(a,a+1,a+2),(a+1,a+3,a+2)])
        m.add('Blue',pts,triangles,m.rigid(bone))
        d.line('Core',[(sign*x,y+yy,z_at(sign*x,yy)+.0025) for x,yy in [(0.012,-.009),(.036,.002),(.055,.011)]],.0008,bone)
        # Crown plates follow the visor boundary rather than sitting across it.
        d.plate('Ceramic',[(sign*x,yy) for x,yy in [(-.034,.039),(-.004,.051),(.029,.030),(.038,-.012),
                          (.019,-.033),(.008,-.005),(-.021,.012)]],
                (sign*.038,y+.077,.047),bone,thickness=.013,bulge=.014)
        d.plate('Porcelain',[(sign*x,yy) for x,yy in [(-.009,.035),(.014,.051),(.030,.015),(.026,-.048),
                            (.008,-.082),(-.008,-.047)]],
                (sign*.068,y-.003,.061),bone,thickness=.012,bulge=.013)
        d.plate('Joint',[(sign*x,yy) for x,yy in [(-.011,.028),(.012,.032),(.016,-.022),(0,-.050),(-.009,-.027)]],
                (sign*.066,y-.040,.055),bone,thickness=.008,bulge=.005)
        d.joint((sign*.084,y+.004,-.020),.025,bone,(sign,0,0))
        d.disc('Blue',(sign*.106,y+.004,-.020),.010,.002,bone,(sign,0,0),32)
        # A thin swept aerial and segmented rear crown preserve Seraph's silhouette.
        d.plate('Ceramic',[(-.008,-.045),(.012,-.012),(.008,.104),(-.002,.134),(-.009,.017)],
                (sign*.090,y+.017,-.035),bone,u=(sign,0,0),thickness=.009,bulge=.006)
        d.plate('Porcelain',[(-sign*x,yy) for x,yy in [(-.029,.050),(.016,.059),(.041,.015),(.031,-.067),
                            (.010,-.085),(-.025,-.050)]],
                (sign*.039,y+.015,-.093),bone,u=(-1,0,0),thickness=.009,bulge=.011)
        d.line('Joint',[(sign*.012,y+.111,-.028),(sign*.030,y+.075,-.080),
                        (sign*.035,y+.010,-.091),(sign*.033,y-.051,-.077)],.0023,bone)
        for k in range(3):
            d.box('Frame',(sign*.068,y-.030-k*.010,-.050),(.020,.004,.004),bone,
                  u=(sign,0,0),bevel=.0006)
    d.rod('Joint',(0,y-.217,-.012),(0,y-.102,-.008),.030,'torso')
    for k in range(4): d.disc('Frame',(0,y-.198+k*.019,-.01),.033,.009,'torso',(0,1,0))


def shoulder(bone, sign):
    """Swept overlapping pauldron with a dark underside and exposed bearing."""
    center=(sign*.178,1.357,.018)
    outline=[(-.040,.017),(-.014,.052),(.024,.057),(.063,.026),(.077,-.019),
             (.041,-.039),(.010,-.024),(-.028,-.022)]
    d.plate('Frame',[(sign*x,y) for x,y in outline],center,bone,thickness=.054,bulge=.018)
    top=[(-.037,.023),(-.010,.048),(.020,.050),(.054,.023),(.063,-.010),
         (.036,-.022),(.011,-.011),(-.030,-.016)]
    d.plate('Porcelain',[(sign*x,y) for x,y in top],(sign*.178,1.362,.041),bone,thickness=.017,bulge=.014)
    # Rear cap is real armor, so the rear gameplay camera gets the same detail.
    d.plate('Ceramic',[(-sign*x,y) for x,y in top],(sign*.178,1.362,-.045),bone,u=(-1,0,0),thickness=.014,bulge=.012)
    d.plate('Ceramic',[(sign*x,y) for x,y in [(-.024,.014),(.028,.023),(.043,-.001),(.029,-.033),(-.013,-.024)]],
            (sign*.202,1.323,.054),bone,thickness=.010,bulge=.012)
    d.line('Joint',[(sign*.153,1.390,.050),(sign*.179,1.398,.061),
                    (sign*.219,1.375,.061)],.002,bone)
    d.disc('Joint',(sign*.207,1.343,.062),.014,.006,bone,sides=32)
    d.disc('Blue',(sign*.207,1.343,.066),.006,.002,bone,sides=24)
    d.bolt((sign*.154,1.367,.065),.0038,bone)


def gauntlet(side,sign,bones):
    bone='fore_'+side; hand='hand_'+side; elbow=dict(bones)[bone]
    axis=np.array([sign*.40,-.916,0]); across=np.array([.916,sign*.40,0])
    def at(t,z=0): return elbow+axis*t+[0,0,z]
    d.orb('Frame',elbow,(.041,.045,.041),bone)
    d.joint(elbow,.031,bone,(0,0,1)); d.disc('Blue',elbow+[0,0,.032],.010,.003,bone,sides=32)
    d.profile('Frame',[at(.015),at(.10),at(.215)],[(.028,.025),(.034,.029),(.022,.023)],bone)
    d.profile('Ceramic',[at(.023,.012),at(.110,.020),at(.20,.016)],
              [(.029,.023),(.039,.027),(.023,.021)],bone,48,10)
    # Boxed receiver, stepped rail channel, swept ceramic cheeks and open muzzle.
    d.box('Frame',at(.205,.047),(.064,.235,.047),bone,u=across,v=axis,bevel=.006)
    d.box('Joint',at(.202,.067),(.047,.205,.012),bone,u=across,v=axis,bevel=.003)
    for s in [-1,1]:
        outline=[(-.009,-.112),(.013,-.097),(.018,-.055),(.012,.054),
                 (.007,.114),(-.008,.108),(-.012,-.054)]
        d.plate('Porcelain',[(s*x,-y) for x,y in outline],at(.202,.077)+across*s*.026,bone,
                u=across,v=-axis,thickness=.013,bulge=.006)
        d.rod('Edge',at(.147,.069)+across*s*.021,at(.289,.069)+across*s*.021,.0035,bone,16)
    d.box('Blue',at(.234,.078),(.014,.155,.006),bone,u=across,v=axis,bevel=.002)
    d.box('Core',at(.255,.082),(.0035,.104,.002),bone,u=across,v=axis,bevel=.0006)
    d.plate('Ceramic',[(x,-y) for x,y in [(-.034,-.026),(.034,-.026),(.027,.016),(.014,.030),(-.014,.030),(-.027,.016)]],
            at(.112,.080),bone,u=across,v=-axis,thickness=.012,bulge=.004)
    for k in range(4):
        d.box('Ink',at(.107+k*.008,.083),(.029,.003,.002),bone,u=across,v=axis,bevel=.0004)
    d.disc('Frame',at(.328,.045),.029,.012,bone,axis,24)
    d.disc('Core',at(.335,.045),.015,.003,bone,axis,24)
    for t in [.13,.27]:
        for s in [-1,1]: d.bolt(at(t,.087)+across*s*.027,.0035,bone)
    d.orb('Frame',at(.225,-.012),(.025,.027,.018),hand)
    for finger in range(4):
        off=across*((finger-1.5)*.011)
        d.line('Joint',[at(.23,-.014)+off,at(.26,-.009)+off,at(.269,.002)+off],.0048,hand)


def missile_hatches(transform):
    """Replace thin white cards with the concept's framed mechanical doors.

    Geometry is still weighted to the exact existing hinge, and the forward
    aperture and muzzle line remain untouched. Dimensions stay within each cell.
    """
    for rack,sign,rear in [(0,-1,False),(1,1,False),(2,-1,True),(3,1,True)]:
        for row in range(6):
            for col in range(3):
                cell,width=d.cell_position(sign,rear,row,col-1)
                bone=f'hatch_{rack}_{row}_{col}'
                at=transform(bone,cell+np.array([0,0,.041]))
                w=width*1.17*.70; h=.153*.88
                d.box('Frame',at,(w,h,.023),bone,bevel=.005)
                d.box('Edge',at+[0,0,.012],(w*.94,h*.94,.009),bone,bevel=.004)
                # A split ivory face with visible dark side jambs.
                outline=[(-w*.34,-h*.39),(w*.22,-h*.39),(w*.34,-h*.27),
                         (w*.34,h*.37),(w*.18,h*.43),(-w*.34,h*.43)]
                panel=np.asarray(outline); center=panel.mean(axis=0)
                m.armor_panel('Ceramic',panel-center,at+[center[0],center[1],.019],m.rigid(bone),
                              thickness=.009,bulge=.002,segments=16,radial_rings=[.45,.84,1])
                d.box('Blue',at+[w*.405,0,.018],(w*.065,h*.62,.005),bone,bevel=.001)
                d.box('Frame',at+[-w*.05,-h*.17,.026],(w*.38,h*.035,.002),bone,bevel=.0004)
                for yy in [-h*.33,h*.33]:
                    d.box('Joint',at+[-sign*w*.42,yy,.014],(w*.14,h*.12,.016),bone,bevel=.002)


def cooling_hinges():
    """Short captive guides connect the four opening thermal panels."""
    for i in range(4):
        a=(i+.5)*math.tau/4; radial=np.array([math.cos(a),math.sin(a),0])
        pivot=radial*.2875+[0,1.02,.668]
        panel=radial*.35+[0,1.02,.98]
        bone='vent_'+str(i)
        d.rod('Frame',pivot,panel,.018,bone,20)
        d.rod('Edge',pivot*.45+panel*.55,panel,.009,bone,16)
        d.joint(panel,.025,bone,radial)


def mobile():
    for side, sign in [('l', -1), ('r', 1)]:
        arm='arm_'+side
        # Overlapping shoulder skirt, recessed blue service light and hardware.
        # Brushed-metal hinge sleeves join the wing pods to the back frame.
        for y in [1.258,1.320]:
            d.rod('Edge',(sign*.091,y,-.091),(sign*.128,y,-.091),.010,'torso',20)
            d.rod('Frame',(sign*.102,y,-.091),(sign*.116,y,-.091),.012,'torso',20)
        # Slim rear calf maintenance panels remain attached to the calf bone.
        bone='boot_'+side
        d.plate('Frame',[(-.019,-.083),(.019,-.083),(.026,.064),(0,.089),(-.026,.064)],
                (sign*.110,.330,-.084),bone,u=(-1,0,0),thickness=.004,bulge=.002)
        for y in [.294,.310,.326,.342]:
            d.box('Edge',(sign*.110,y,-.089),(.026,.003,.003),bone,bevel=.0005)
        d.box('Blue',(sign*.110,.375,-.088),(.010,.017,.003),bone,bevel=.001)
        # A small double-gasket at the neck gives the helmet a seated connection.
        d.line('Frame',[(sign*.036,1.411,.024),(sign*.052,1.409,-.012),(sign*.033,1.406,-.039)],.003,'torso')


def siege():
    # Fixed recoil slides beneath the moving breech make recoil mechanically legible.
    for sign in [-1,1]:
        d.rod('Frame',(sign*.242,.848,.700),(sign*.242,.848,1.355),.027,'root',24)
        d.rod('Edge',(sign*.242,.848,.765),(sign*.242,.848,1.270),.015,'root',24)
        d.box('Joint',(sign*.242,.849,.713),(.072,.074,.100),'root',bevel=.007)
        d.box('Ceramic',(sign*.242,.897,.724),(.085,.014,.115),'root',bevel=.004)
    # A layered service rim breaks up the broad lower ceramic skirt.
    for i in range(12):
        angle=(i+.5)*math.tau/12
        radial=np.array([math.sin(angle),0,math.cos(angle)])
        tangent=np.array([math.cos(angle),0,-math.sin(angle)])
        at=radial*1.218+[0,.416,0]
        d.box('Frame',at,(.285,.026,.025),'root',u=tangent,bevel=.004)
        d.box('Edge',at+radial*.014+[0,.009,0],(.240,.005,.010),'root',u=tangent,bevel=.001)
        if i%3==0:
            d.box('Blue',at+radial*.021+[0,.002,0],(.070,.005,.005),'root',u=tangent,bevel=.001)
