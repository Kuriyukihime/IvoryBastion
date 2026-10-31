"""Original Seraph geometry for Ivory Bastion. Authoring units are meters.
The mobile rig and the unfolding fortress share the same ceramic armor language.
Every visible surface below becomes the actual embedded game mesh.
"""
import math
import numpy as np
import mesh_tools as m
import seraph_refinement as detail
import seraph_production as production

PALETTE={
 'Ceramic':((.86,.90,.95),.42,0), 'Porcelain':((.98,.99,1),.38,0),
 'Edge':((.36,.43,.54),.58,0), 'Frame':((.020,.028,.042),.46,0),
 'Joint':((.085,.115,.16),.62,0), 'Glass':((.008,.018,.035),.72,0),
 'Ink':((.035,.065,.10),.24,0), 'Blue':((.045,.36,.95),.36,1.35), 'Core':((.37,.73,1),.30,2.1)}

def setup(bones):
    m.PALETTE=PALETTE;m.BONES=[(n,p,None) for n,p in bones]
    m.BN={n:i for i,(n,_) in enumerate(bones)};m.ANCHORS=np.array([p for _,p in bones],float)
    m.PARTS={n:{'v':[],'t':[],'w':[]} for n in PALETTE}
def plate(mat,outline,at,bone='root',**kw):m.armor_panel(mat,outline,at,m.rigid(bone),**kw)
def orb(mat,at,size,bone='root',**kw):m.ellipsoid(mat,at,size,m.rigid(bone),sides=32,rings=18,**kw)
def rod(mat,a,b,r,bone='root',sides=16):
    axis=m.norm(np.array(b)-a);front=(0,1,0) if abs(axis[2])>.85 else (0,0,1)
    m.tube(mat,[a,b],[(r,r)]*2,m.rigid(bone),sides=sides,steps=1,front=front)
def box(mat,at,size,bone='root',u=(1,0,0),v=(0,1,0)):
    x,y,z=size;k=min(x,y)*.2
    outline=[(-x/2+k,-y/2),(x/2-k,-y/2),(x/2,-y/2+k),(x/2,y/2-k),(x/2-k,y/2),(-x/2+k,y/2),(-x/2,y/2-k),(-x/2,-y/2+k)]
    normal=m.norm(np.cross(u,v));plate(mat,outline,np.array(at)+normal*z/2,bone,u=u,v=v,thickness=z,bulge=.014)
def ring(mat,at,r,t,bone='root',normal=(0,0,1)):m.torus(mat,at,r,t,m.rigid(bone),normal=normal)
def profile(mat,points,radii,bone='root',sides=32,steps=5):m.tube(mat,points,radii,m.rigid(bone),sides=sides,steps=steps)
def line(mat,points,r,bone='root'):m.curve(mat,points,r,m.rigid(bone),steps=4)

def leaf(mat,points,widths,depths,bone,offset=0):
    """Curved lenticular shell, capped at each end, with clean surface normals."""
    points=np.array(points,float);points[:,2]+=offset
    profile(mat,points,list(zip(widths,depths)),bone,sides=24,steps=7)

head=detail.head
torso=detail.torso
petal=detail.petal

def mobile():
    bones=[('root',(0,0,0)),('torso',(0,1.08,0)),('head',(0,1.43,0))]
    for side,s in [('l',-1),('r',1)]:
        bones.extend([('arm_'+side,(s*.19,1.17,0)),('fore_'+side,(s*.275,.95,.01)),('hand_'+side,(s*.30,.75,.035)),('leg_'+side,(s*.087,.82,0)),('boot_'+side,(s*.104,.43,-.01)),('wing_'+side,(s*.16,1.16,-.15)),('vane_'+side,(s*.16,1.05,-.16))])
    setup(bones);head();torso()
    orb('Frame',(0,.80,0),(.122,.105,.079))
    plate('Ceramic',[(-.05,.03),(.05,.03),(.065,-.035),(0,-.09),(-.065,-.035)],(0,.81,.071),thickness=.022,bulge=.02)
    for side,s in [('l',-1),('r',1)]:
        arm='arm_'+side;fore='fore_'+side;hand='hand_'+side;leg='leg_'+side;boot='boot_'+side
        orb('Joint',(s*.19,1.17,0),(.044,.048,.044),arm)
        leaf('Ceramic',[(s*.17,1.22,.015),(s*.225,1.195,.012),(s*.238,1.13,.02)],[.018,.055,.008],[.017,.044,.01],arm)
        profile('Frame',[(s*.205,1.14,0),(s*.247,1.045,.005),(s*.275,.95,.01)],[(.033,.032),(.032,.031),(.027,.029)],arm,sides=24)
        plate('Ceramic',[(-.023,.065),(.024,.055),(.029,-.043),(0,-.074),(-.022,-.05)],(s*.245,1.05,.035),arm,thickness=.018,bulge=.02)
        orb('Joint',(s*.275,.95,.01),(.032,.034,.032),fore)
        profile('Ceramic',[(s*.277,.935,.015),(s*.292,.84,.025),(s*.30,.755,.035)],[(.034,.033),(.049,.041),(.024,.025)],fore)
        plate('Edge',[(-.025,.072),(.024,.06),(.029,-.03),(0,-.095),(-.023,-.04)],(s*.294,.84,.065),fore,thickness=.01,bulge=.015)
        line('Blue',[(s*.303,.91,.068),(s*.318,.85,.076),(s*.312,.80,.065)],.0035,fore)
        # Integrated needle accelerator, not an oversized external pistol.
        box('Frame',(s*.298,.78,.080),(.045,.15,.045),fore)
        orb('Core',(s*.298,.699,.080),(.013,.004,.013),fore)
        orb('Frame',(s*.304,.715,.032),(.032,.042,.017),hand)
        plate('Porcelain',[(-.028,.027),(.025,.025),(.025,-.022),(-.022,-.028)],(s*.304,.72,.049),hand,thickness=.012)
        for finger in range(4):
            x=s*(.282+finger*.014);line('Joint',[(x,.696,.032),(x,.676,.035),(x,.665,.048)],.0055,hand)
        line('Joint',[(s*.279,.724,.028),(s*.265,.703,.036),(s*.269,.687,.05)],.006,hand)
        profile('Frame',[(s*.087,.82,0),(s*.098,.65,-.004),(s*.104,.43,-.01)],[(.061,.060),(.056,.055),(.027,.031)],leg)
        # Smooth white thigh shell, black inner joint channels, fitted hip petals.
        leaf('Ceramic',[(s*.102,.795,.01),(s*.118,.69,.019),(s*.111,.54,.015),(s*.104,.47,.002)],[.045,.061,.046,.014],[.037,.049,.041,.013],leg)
        plate('Porcelain',[(x*s,y) for x,y in [(-.025,.06),(.035,.12),(.064,.085),(.051,-.045),(.006,-.14),(-.02,-.07)]],(s*.115,.81,.049),leg,thickness=.025,bulge=.025)
        line('Joint',[(s*.126,.73,.069),(s*.139,.64,.064),(s*.115,.53,.052)],.0025,leg)
        leaf('Ceramic',[(s*.103,.78,-.032),(s*.11,.65,-.047),(s*.105,.49,-.023)],[.039,.047,.011],[.025,.026,.008],leg)
        line('Edge',[(s*.11,.74,-.061),(s*.12,.64,-.075),(s*.107,.54,-.053)],.0025,leg)
        orb('Joint',(s*.104,.43,-.01),(.032,.034,.034),boot)
        profile('Frame',[(s*.104,.43,-.01),(s*.112,.28,-.015),(s*.118,.11,-.008)],[(.028,.03),(.032,.032),(.019,.021)],boot)
        leaf('Porcelain',[(s*.104,.42,.02),(s*.111,.31,.026),(s*.118,.14,.026),(s*.118,.08,.042)],[.028,.046,.026,.018],[.019,.032,.025,.012],boot)
        plate('Frame',[(-.02,.03),(.02,.03),(.013,-.027),(0,-.04),(-.014,-.027)],(s*.106,.415,.043),boot,thickness=.007)
        orb('Blue',(s*.106,.419,.05),(.011,.017,.004),boot)
        line('Blue',[(s*.112,.34,.061),(s*.121,.25,.058),(s*.12,.17,.052)],.003,boot)
        leaf('Ceramic',[(s*.107,.38,-.034),(s*.115,.26,-.048),(s*.118,.12,-.028)],[.022,.029,.009],[.018,.019,.006],boot)
        line('Blue',[(s*.115,.31,-.070),(s*.118,.22,-.066)],.0025,boot)
        leaf('Ceramic',[(s*.118,.10,-.05),(s*.118,.073,.03),(s*.118,.048,.125)],[.024,.035,.008],[.025,.031,.008],boot)
        box('Frame',(s*.118,.031,.025),(.062,.016,.15),boot)
        box('Edge',(s*.118,.067,-.047),(.035,.065,.035),boot)
        petal('wing_'+side,s,True);petal('vane_'+side,s,False)
    detail.mobile_details();production.mobile()
    m.write_asset('mobile')
    m.render((1500,1800),23,(-.04,1.87,1),m.ROOT/'dist/IvoryBastion-mobile-preview.png','IVORY BASTION / SERAPH - MOBILE')
    m.render((1200,1500),155,(-.04,1.87,1),m.ROOT/'dist/IvoryBastion-mobile-rear.png','SERAPH / REAR')
    m.render((1100,1400),30,(.72,1.65,1),m.ROOT/'dist/IvoryBastion-torso-detail.png','SERAPH / FITTED ARMOR DETAIL')
    m.render((1024,1024),15,(1.005,1.66,1),m.ROOT/'assets/portrait.png')

def shield(bone,s,rear):
    z=-.59 if rear else .28;dx=-.17 if rear else 0;extra=.16 if rear else 0
    points=[(s*(.79+dx),.46,z),(s*(1.16+dx),.85,z-.04),(s*(1.36+dx),1.50,z-.12),(s*(1.20+dx),2.18+extra,z-.22),(s*(.97+dx),2.59+extra,z-.26)]
    widths=[.15,.32,.34,.23,.0005];depths=[.12,.14,.14,.11,.0005]
    leaf('Ceramic',points,widths,depths,bone)
    inside=np.array(points);inside[:,0]-=s*.06
    leaf('Frame',inside,[.10,.245,.267,.155,.0005],[.06,.075,.075,.045,.0005],bone,.10)
    detail.shield_details(bone,s,rear)


halo=detail.halo

def siege():
    bones=[('root',(0,0,0)),('torso',(0,1.08,0)),('head',(0,1.43,0)),('cannon',(0,.94,.42)),('halo',(0,1.55,-.65)),('radar',(0,1.55,-.65))]
    for name,s,rear in [('rack_l',-1,False),('rack_r',1,False),('shield_l',-1,True),('shield_r',1,True)]:bones.append((name,(s*.8,.47,-.59 if rear else .28)))
    for i in range(6):bones.append(('anchor_'+str(i),(0,.4,0)))
    for name,side in [('l',-1),('r',1)]:
        bones.extend([('arm_'+name,(side*.19,1.17,0)),('fore_'+name,(side*.29,1.01,.12)),('hand_'+name,(side*.24,.92,.35))])
    for i in range(4):
        a=(i+.5)*math.tau/4;bones.append(('vent_'+str(i),(math.cos(a)*.25,.94+math.sin(a)*.25,.32)))
    for rack,side,rear in [(0,-1,False),(1,1,False),(2,-1,True),(3,1,True)]:
        for row in range(6):
            for col in range(3):
                at,width=detail.cell_position(side,rear,row,col-1)
                bones.append((f'hatch_{rack}_{row}_{col}',at+np.array([side*width*.48,0,.046])))
    setup(bones)
    # Radial citadel, with separate armored sections and exposed lower engine.
    profile('Frame',[(0,.22,0),(0,.30,0),(0,.53,0),(0,.66,0)],[(.72,.72),(.99,.92),(.99,.92),(.78,.72)],sides=64)
    for i in range(12):
        a=i*math.tau/12;radial=np.array((math.sin(a),0,math.cos(a)));tangent=np.array((math.cos(a),0,-math.sin(a)))
        plate('Ceramic',[(-.205,.10),(.205,.10),(.23,-.025),(.15,-.15),(-.15,-.15),(-.23,-.025)],radial*.96+np.array((0,.48,0)),u=tangent,v=(0,1,0),thickness=.065,bulge=.03)
        box('Blue',radial*1.003+np.array((0,.39,0)),(.16,.018,.008),u=tangent)
        for j in [-1,1]:box('Joint',radial*.965+tangent*j*.15+np.array((0,.42,0)),(.025,.08,.018),u=tangent)
    ring('Edge',(0,.67,0),.77,.04,normal=(0,1,0));ring('Blue',(0,.684,0),.71,.012,normal=(0,1,0))
    box('Frame',(0,.79,-.05),(.59,.29,.56))
    plate('Porcelain',[(-.33,.06),(.33,.06),(.23,-.14),(0,-.21),(-.23,-.14)],(0,.80,.40),thickness=.07,bulge=.035)
    plate('Blue',[(-.09,.025),(.09,.025),(.06,-.03),(-.06,-.03)],(0,.78,.447),thickness=.005)
    torso();head();halo()
    for s in [-1,1]:
        arm='arm_'+('l' if s<0 else 'r');fore='fore_'+('l' if s<0 else 'r');hand='hand_'+('l' if s<0 else 'r')
        orb('Joint',(s*.19,1.17,0),(.043,.043,.043),arm)
        leaf('Ceramic',[(s*.20,1.18,0),(s*.28,1.06,.065),(s*.29,1.01,.12)],[.035,.037,.025],[.032,.030,.022],arm)
        rod('Joint',(s*.29,1.01,.12),(s*.24,.92,.33),.028,fore)
        leaf('Ceramic',[(s*.29,1.00,.15),(s*.265,.96,.25),(s*.24,.925,.33)],[.028,.036,.019],[.025,.033,.016],fore)
        orb('Frame',(s*.24,.92,.35),(.033,.028,.029),hand);box('Joint',(s*.24,.895,.35),(.025,.12,.025))
    # Six folding stabilizers with articulated hydraulic knees and wide footpads.
    for i in range(6):
        a=(i+.5)*math.tau/6;radial=np.array((math.sin(a),0,math.cos(a)));tangent=np.array((math.cos(a),0,-math.sin(a)));b='anchor_'+str(i)
        p=radial*.78+np.array((0,.53,0));k=radial*1.28+np.array((0,.38,0));foot=radial*1.53+np.array((0,.105,0))
        for s in [-1,1]:rod('Frame',p+tangent*s*.09,k+tangent*s*.09,.055,b);rod('Joint',k+tangent*s*.09,foot+tangent*s*.09,.037,b)
        orb('Joint',k,(.13,.12,.13),b);ring('Blue',k+radial*.115,.055,.009,b,normal=radial)
        plate('Ceramic',[(-.16,.22),(.16,.22),(.12,.03),(.09,-.16),(-.09,-.16),(-.12,.03)],k+radial*.105,b,u=tangent,thickness=.06,bulge=.018)
        box('Core',k+radial*.143+np.array((0,.03,0)),(.026,.17,.009),b,u=tangent)
        box('Frame',foot+np.array((0,-.065,0)),(.39,.065,.37),b,u=tangent)
        box('Ceramic',foot+np.array((0,-.01,0)),(.31,.10,.27),b,u=tangent)
        box('Blue',foot+radial*.15+np.array((0,.015,0)),(.13,.018,.01),b,u=tangent)
    for name,s,rear in [('rack_l',-1,False),('rack_r',1,False),('shield_l',-1,True),('shield_r',1,True)]:shield(name,s,rear)
    detail.cannon()
    detail.siege_details();production.siege()
    m.write_asset('siege')
    m.render((2400,1700),40,(-.20,3.06,1),m.ROOT/'dist/IvoryBastion-siege-preview.png','IVORY BASTION / SERAPH - SIEGE')
    m.render((2100,1600),145,(-.20,3.06,1),m.ROOT/'dist/IvoryBastion-siege-rear.png','SERAPH / SIEGE REAR')

def units():
    for kind in ['guardian','gunner','interceptor','repair']:
        setup([('root',(0,0,0)),('wing_l',(-.25,0,0)),('wing_r',(.25,0,0)),('sensor',(0,.15,0))])
        orb('Frame',(0,0,0),(.31,.17,.26));orb('Ceramic',(0,.09,-.015),(.28,.12,.22))
        plate('Glass',[(-.18,.045),(.18,.045),(.15,-.04),(-.15,-.04)],(0,.015,.235),thickness=.015)
        line('Core',[(-.13,.02,.257),(0,.012,.27),(.13,.02,.257)],.009)
        ring('Joint',(0,-.12,0),.15,.025,normal=(0,-1,0));ring('Blue',(0,-.15,0),.12,.014,normal=(0,-1,0))
        for s,side in [(-1,'l'),(1,'r')]:
            b='wing_'+side
            rod('Joint',(s*.23,0,0),(s*.42,0,-.02),.036,b)
            points=[(s*.28,.03,-.03),(s*.48,.04,-.10),(s*.66,.02,-.31)]
            leaf('Porcelain',points,[.075,.13,.0005],[.035,.036,.0005],b)
            line('Blue',[(s*.32,.065,-.03),(s*.49,.081,-.10),(s*.59,.057,-.20)],.008,b)
            if kind=='guardian':
                pts=[(s*.21,-.02,.27),(s*.30,-.18,.35),(s*.22,-.42,.24)]
                leaf('Ceramic',pts,[.10,.14,.0005],[.06,.065,.0005],b);line('Blue',[(s*.24,-.06,.33),(s*.30,-.18,.42),(s*.25,-.32,.34)],.009,b)
            elif kind=='gunner':
                rod('Frame',(s*.30,-.08,.10),(s*.30,-.08,.65),.045,b);ring('Edge',(s*.30,-.08,.57),.048,.011,b);orb('Core',(s*.30,-.08,.66),(.025,.025,.006),b)
                box('Ceramic',(s*.30,-.04,.32),(.12,.08,.26),b)
            elif kind=='interceptor':
                leaf('Ceramic',[(s*.21,.15,-.07),(s*.31,.32,-.08),(s*.23,.44,-.04)],[.04,.052,.0005],[.028,.027,.0005],b)
                orb('Blue',(s*.28,.32,-.045),(.024,.045,.008),b)
            else:
                rod('Edge',(s*.15,-.11,.15),(s*.27,-.27,.25),.018,b);orb('Core',(s*.27,-.27,.25),(.025,.025,.025),b)
        if kind=='repair':ring('Blue',(0,.20,0),.13,.014,'sensor',normal=(0,1,0))
        production.unit(kind)
        m.write_asset('unit_'+kind)
        m.render((700,600),24,(-.55,.63,1),m.ROOT/('dist/IvoryBastion-unit-'+kind+'.png'),'SERAPH / '+kind.upper())

if __name__=='__main__':mobile();siege();units()
