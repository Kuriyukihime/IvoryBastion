"""Review the actual embedded siege mesh with the compiled presentation poses."""
import json, math, re
import numpy as np
from scipy.spatial import cKDTree
from PIL import Image, ImageDraw, ImageFont
import presentation_mesh as m
import seraph_refinement as detail
import seraph_siege_refit as refit
from seraph_hybrid import siege_transform

ROOT=m.ROOT
VERSION=json.loads((ROOT/'package/manifest.json').read_text())['version_number']
OUT=ROOT/'dist'/f'IvoryBastion-{VERSION}-siege-refit'
OUT.mkdir(exist_ok=True)
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
VISUAL_SCALE=float(re.search(r'VisualScale=([0-9.]+)f',(ROOT/'src/BastionWarheadGeometry.cs').read_text()).group(1))

def radial_surface_clearance(triangles,center):
    """Minimum distance from a vertical bore axis to the actual mesh faces."""
    q=triangles[:,:,[0,2]]-np.asarray(center)[[0,2]]
    minimum=float('inf')
    crosses=[]
    for i in range(3):
        a=q[:,i];b=q[:,(i+1)%3];edge=b-a
        t=np.clip(-np.sum(a*edge,axis=1)/np.maximum(np.sum(edge*edge,axis=1),1e-20),0,1)
        minimum=min(minimum,float(np.linalg.norm(a+t[:,None]*edge,axis=1).min()))
        crosses.append(a[:,0]*b[:,1]-a[:,1]*b[:,0])
    crosses=np.asarray(crosses);area=crosses.sum(0)
    contains=((crosses>=-1e-12).all(0)|(crosses<=1e-12).all(0))&(np.abs(area)>1e-10)
    return 0. if contains.any() else minimum

def main():
    poses=[('Deployed',dict(Unfold=1,Time=1)),
           ('Throne lift / deployment',dict(Unfold=.58,Time=1)),
           ('Ion Lance / recoil',dict(Unfold=1,Time=1,LaserKick=1)),
           ('Aegis / brace',dict(Unfold=1,Time=1,Defense=1,HaloPhase=7)),
           ('Skyfall / forward launchers',dict(Unfold=1,Time=1,Salvo=1,MissileKick=1,HatchAges=[.15 if i%9==0 else -1 for i in range(72)])),
           ('Elevated gun / independent seat',dict(Unfold=1,Time=1,Pitch=-40))]
    frames=m.export([dict(Form='siege',Input=p) for _,p in poses])
    bones,parts=m.load('siege');anchors=dict(bones)
    assert len(bones)==108 and 'throne' in anchors
    assert not any('skyfall_lid' in name for name,_ in bones)
    assert anchors['torso'][1]>1.70 and anchors['head'][1]>1.98
    # All 72 existing hinge/launch markers retain their original fitted locations.
    port_error=0
    for rack,side,rear in [(0,-1,False),(1,1,False),(2,-1,True),(3,1,True)]:
        for row in range(6):
            for col in range(3):
                name=f'hatch_{rack}_{row}_{col}';p,w=detail.cell_position(side,rear,row,col-1)
                expected=siege_transform(name,p+np.array([side*w*.48,0,.046]))
                port_error=max(port_error,float(np.linalg.norm(anchors[name]-expected)))
    assert port_error<1e-6
    for side,s in [('l',-1),('r',1)]:
        assert np.linalg.norm(anchors['skyfall_muzzle_'+side]-[s*.78,2.60,-1.94])<1e-6
    _,compact=m.load('skyfall')
    compact_v=np.concatenate([p['v'] for p in compact])
    required_radius=float(np.linalg.norm(compact_v[:,:2],axis=1).max())*VISUAL_SCALE/1.45
    actual_bore=[]
    names=[name for name,_ in bones]
    reference_shape_error=0.
    reference=ROOT/'.build/skyfall-size-before.meshbin'
    if reference.exists():
        old_bones,old_parts=m.load('../.build/skyfall-size-before');old_names=[name for name,_ in old_bones]
        for side in ['l','r']:
            old_id=old_names.index('skyfall_'+side);new_id=names.index('skyfall_'+side)
            before=np.concatenate([p['v'][p['b'][:,0]==old_id] for p in old_parts])
            after=np.concatenate([p['v'][p['b'][:,0]==new_id] for p in parts]).copy()
            axis=anchors['skyfall_'+side]
            after[:,0]=axis[0]+(after[:,0]-axis[0])/refit.SKYFALL_HOUSING_RADIAL_SCALE
            after[:,2]=axis[2]+(after[:,2]-axis[2])/refit.SKYFALL_HOUSING_RADIAL_SCALE
            error=float(cKDTree(after).query(before)[0].max())
            assert error<.0001,('Original housing details or height changed',side,error)
            reference_shape_error=max(reference_shape_error,error)
    for side in ['l','r']:
        pod_id=names.index('skyfall_'+side);surfaces=[]
        for p in parts:
            triangles=p['v'][p['t']]
            selected=(p['b'][p['t'],0]==pod_id).all(1)&(triangles[:,:,1].min(1)>.9001)
            surfaces.append(triangles[selected])
        clearance=radial_surface_clearance(np.concatenate(surfaces),anchors['skyfall_'+side])
        assert clearance>required_radius+.003,(side,'fin clearance inside housing',clearance,required_radius)
        actual_bore.append(clearance)
    audits=[];interior_checks=[]
    rest=np.concatenate([p['v'] for p in parts]);cull_min=rest.min(0)-.8;cull_max=rest.max(0)+.8
    for lod in range(3):
        lb,lp=m.load('siege',lod)
        for side in ['l','r']:
            bone_id=[name for name,_ in lb].index('skyfall_'+side);axis=dict(lb)['skyfall_'+side]
            inward=0
            for p in lp:
                triangles=p['v'][p['t']];centers=triangles.mean(1);radial=centers-axis;radial[:,1]=0
                radius=np.linalg.norm(radial,axis=1)
                face_normal=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
                dot=np.sum(face_normal*radial,axis=1)/np.maximum(np.linalg.norm(face_normal,axis=1)*radius,1e-12)
                selected=(p['b'][p['t'],0]==bone_id).all(1)&(centers[:,1]>.95)&(centers[:,1]<1.98)&(radius>.25)&(radius<.29)&(dot<-.6)
                inward+=int(selected.sum())
            assert inward>=8,(lod,side,'Missing inward-facing silo liner',inward)
            interior_checks.append(dict(lod=lod,side=side,inward_faces=inward))
        for (label,_),frame in zip(poses,frames):
            largest=1.;bounds=[]
            for p,v,n in m.pose(lb,lp,frame):
                assert np.isfinite(v).all() and np.isfinite(n).all()
                edges=np.concatenate([p['t'][:,[0,1]],p['t'][:,[1,2]],p['t'][:,[2,0]]])
                before=np.linalg.norm(p['v'][edges[:,0]]-p['v'][edges[:,1]],axis=1)
                after=np.linalg.norm(v[edges[:,0]]-v[edges[:,1]],axis=1)/frame['Scale']
                valid=before>1e-5
                largest=max(largest,float((after[valid]/before[valid]).max()))
                bounds.append(v)
            assert largest<1.005,(lod,label,largest)
            v=np.concatenate(bounds)
            assert np.all(v.min(0)>=cull_min-1e-4) and np.all(v.max(0)<=cull_max+1e-4),(label,'outside permanent-renderer bounds')
            audits.append(dict(lod=lod,pose=label,max_edge_stretch=largest,bounds=[v.min(0).tolist(),v.max(0).tolist()]))
    # Check the complete assembly, including the two forked mounting pieces.
    # Every component must have a disjoint envelope from banks, feet and halo.
    battery_gaps=[];foot_gaps=[];component_gaps=[]
    protected=[(root,[i for i,name in enumerate(names) if name==root or name.startswith(f'hatch_{rack}_')])
               for rack,root in enumerate(['rack_l','rack_r','shield_l','shield_r'])]
    protected += [(name,[names.index(name)]) for name in ['halo','throne','torso','cannon']+[f'anchor_{i}' for i in range(6)]]
    for (label,input_pose),frame in zip(poses,frames):
        if input_pose['Unfold']<1:continue
        posed=m.pose(bones,parts,frame)
        for side in ['l','r']:
            for component in ['skyfall_'+side,'skyfall_mount_inner_'+side,'skyfall_mount_outer_'+side]:
                pod_id=names.index(component)
                pod=np.concatenate([v[p['b'][:,0]==pod_id] for p,v,n in posed])
                for root,ids in protected:
                    other=np.concatenate([v[np.isin(p['b'][:,0],ids)] for p,v,n in posed])
                    gap=float(np.max(np.maximum(other.min(0)-pod.max(0),pod.min(0)-other.max(0))))
                    assert gap>.025,(label,component,root,'launcher assembly overlaps protected envelope',gap)
                    component_gaps.append(gap)
                    if root.startswith('anchor_'):foot_gaps.append(gap)
                    if root.startswith(('rack_','shield_')):battery_gaps.append(gap)
    for name,yaw,pitch in [('preview',33,7),('front',0,5),('side',90,8),('rear',180,8),('rear-quarter',155,8),('top',28,78)]:
        m.render('siege',(1500,1150),yaw,pitch=pitch,frame=frames[0],
                 title='IVORY BASTION / SERAPH THRONE / ACTUAL RUNTIME MESH').save(OUT/f'IvoryBastion-{VERSION}-siege-{name}.png')
        print('RENDER',name,flush=True)
    m.render('siege',(1100,1100),28,pitch=9,frame=frames[0],view=(0,1.74,-.29,.76),title='SERAPH / ELEVATED THRONE DETAIL').save(OUT/f'IvoryBastion-{VERSION}-throne-detail.png')
    m.render('siege',(1400,900),54,pitch=8,frame=frames[0],view=(0,1.05,1.64,.70),title='SERAPH / REBUILT RAIL CANNON').save(OUT/f'IvoryBastion-{VERSION}-cannon-detail.png')
    m.render('siege',(1500,1200),155,pitch=68,frame=frames[0],view=(0,1.48,-1.94,1.05),
             title='SERAPH / OPEN SILOS / INWARD METAL LINERS + RECESSED FLOORS').save(OUT/f'IvoryBastion-{VERSION}-silo-interiors.png')
    # Use the actual giant-warhead asset and the runtime scale ratio, not an
    # illustration of the new tubes. Positive-Z missile noses point upward.
    _,warhead=m.load('skyfall');extra=[];rotation=m.euler(np.array([-90,0,0]))
    warhead_v=np.concatenate([p['v'] for p in warhead])
    required_radius=float(np.linalg.norm(warhead_v[:,:2],axis=1).max())*VISUAL_SCALE/1.45
    assert refit.SKYFALL_BORE>required_radius and refit.SKYFALL_BORE-required_radius<.025,'Bore should closely fit the actual giant-warhead fins'
    _,original=m.load('warhead');original_v=np.concatenate([p['v'] for p in original])
    _,regular=m.load('missile');regular_v=np.concatenate([p['v'] for p in regular])
    regular_radius=float(np.linalg.norm(regular_v[:,:2],axis=1).max())
    heavy_radius=float(np.linalg.norm(original_v[:,:2],axis=1).max())
    heavy_shape=original_v.copy();skyfall_shape=warhead_v.copy()
    heavy_shape[:,:2]/=json.loads((ROOT/'assets/warhead-info.json').read_text())['radial_scale']
    skyfall_shape[:,:2]/=json.loads((ROOT/'assets/skyfall-info.json').read_text())['radial_scale']
    assert np.allclose(heavy_shape,skyfall_shape,atol=1e-7),'Heavy and extra-large warheads must share the same underlying design'
    assert abs(heavy_radius/regular_radius-1.8)<1e-6,'Missile wingspans must scale consistently'
    assert abs(np.ptp(original_v[:,2])/np.ptp(regular_v[:,2])-1.8)<1e-6,'Missile lengths must scale consistently'
    assert abs(np.ptp(regular_v[:,2])-1.12)<1e-6 and abs(np.ptp(original_v[:,2])-2.016)<1e-6,'Preserve normal and heavy missile lengths'
    assert abs(np.ptp(warhead_v[:,2])*VISUAL_SCALE-4.8384)<1e-6,'Restore the original extra-large warhead length'
    shoe_top=refit.SKYFALL_SHOE_HEIGHT+refit.SKYFALL_SHOE_THICKNESS*.5
    assert refit.SKYFALL_MUZZLE_HEIGHT+warhead_v[:,2].min()*VISUAL_SCALE/1.45>shoe_top+.04,'Loaded missile tail must clear its base'
    for side,s,height in [('l',-1,1.8),('r',1,0)]:
        at=anchors['skyfall_muzzle_'+side]+[0,height,0]
        for part in warhead:
            extra.append((part,part['v']@rotation.T*(VISUAL_SCALE/1.45)+at,part['n']@rotation.T))
    m.render('siege',(1300,1500),28,pitch=7,frame=frames[4],extras=extra,
             title='SERAPH / TWIN SKYFALL TUBES / ACTUAL MODEL + WARHEAD ASSETS').save(OUT/f'IvoryBastion-{VERSION}-skyfall-launch.png')
    family_extras=[]
    for source,scale,x in [(original,1,0),(warhead,VISUAL_SCALE,1.4)]:
        bottom=min(p['v'][:,2].min() for p in source)*scale
        for part in source:
            family_extras.append((part,part['v']@rotation.T*scale+[x,-bottom,0],part['n']@rotation.T))
    family_frame=dict(Scale=1,Rotation=dict(X=-90,Y=0,Z=0),Offset=dict(X=-1.4,Y=-float(regular_v[:,2].min()),Z=0),Bones={})
    m.render('missile',(1500,1200),20,pitch=5,frame=family_frame,extras=family_extras,
             title='MATCHING MISSILE FAMILY / REGULAR 1.12m / HEAVY 2.02m / SKYFALL 4.84m / SAME SCALE').save(OUT/f'IvoryBastion-{VERSION}-missile-family.png')
    comparison_dir=ROOT/'dist'/f'IvoryBastion-{VERSION}-launcher-review'
    comparison_dir.mkdir(exist_ok=True)
    for name,yaw in [('rear',180),('quarter',155),('side',90)]:
        after=m.render('siege',(1400,1100),yaw,pitch=8,frame=frames[0],view=(0,1.35,-.05,2.25),
                       title=f'AFTER / {VERSION} / SAME FIXED CAMERA')
        after.save(comparison_dir/f'after-{name}.png')
        before_path=comparison_dir/f'before-{name}.png'
        if before_path.exists():
            with Image.open(before_path) as before:
                pair=Image.new('RGB',(2800,1100),'#0a101a');pair.paste(before,(0,0));pair.paste(after,(1400,0))
                pair.save(comparison_dir/f'IvoryBastion-{VERSION}-{name}-before-after.png')
    sheet=Image.new('RGB',(1500,1340),'#0a101a')
    for i,((label,_),frame) in enumerate(zip(poses,frames)):
        im=m.render('siege',(500,620),30,pitch=7,frame=frame)
        x=i%3*500;y=i//3*665;sheet.paste(im.convert('RGB'),(x,y))
        ImageDraw.Draw(sheet).text((x+12,y+628),label,font=FONT,fill='#c8deef')
    sheet.save(OUT/f'IvoryBastion-{VERSION}-siege-poses.png')
    requests=[]
    for i in range(36):
        t=i/12;ages=[-1]*72
        for j in range(8):
            age=t-1.6-j*.12
            if 0<=age<.72:ages[j]=age
        requests.append(dict(Form='siege',Input=dict(Time=t,Unfold=min(1,t/1.05),
            LaserKick=math.exp(-(t-1.35)*8) if t>=1.35 else 0,
            Salvo=min(1,max(0,(t-1.7)*4)),HatchAges=ages,HaloPhase=t*4,
            LeftSkyfallKick=max(0,math.sin((t-1.8)*math.pi*6))*.6 if t>1.8 else 0,
            RightSkyfallKick=max(0,-math.sin((t-1.8)*math.pi*6))*.6 if t>1.8 else 0)))
    animation=m.export(requests);images=[]
    for frame in animation:
        images.append(m.render('siege',(680,720),30,pitch=7,frame=frame,view=(0,1.50,.23,3.22)).convert('RGB'))
    images[0].save(OUT/f'IvoryBastion-{VERSION}-siege-motion.gif',save_all=True,append_images=images[1:],duration=83,loop=0,optimize=False)
    for name,yaw,shield in [('hud_siege',25,0),('art_ordnance',28,0),('art_aegis',-30,2)]:
        m.render('siege',(768,512),yaw,frame=frames[0],transparent=True,shield=shield,
                 view=(0,1.38,0,3.25) if shield else None).save(ROOT/f'assets/{name}.png')
    report=dict(ports=72,maximum_launch_marker_error_m=port_error,all_assemblies_rigid=True,
                warhead_fin_radius_world_m=required_radius*1.45,launcher_bore_radius_world_m=refit.SKYFALL_BORE*1.45,
                minimum_actual_housing_clearance_world_m=(min(actual_bore)-required_radius)*1.45,
                visual_missile_length_world_m=float(np.ptp(warhead_v[:,2]))*VISUAL_SCALE,
                visual_scale=VISUAL_SCALE,
                restored_housing_maximum_reference_error_m=reference_shape_error,
                housing_diameter_scale=refit.SKYFALL_HOUSING_RADIAL_SCALE,
                missile_family=[dict(name=name,length_m=float(np.ptp(v[:,2]))*scale,wingspan_m=float(np.linalg.norm(v[:,:2],axis=1).max())*2*scale)
                                for name,v,scale in [('regular',regular_v,1),('heavy',original_v,1),('Skyfall',warhead_v,VISUAL_SCALE)]],
                minimum_battery_envelope_gap_world_m=min(battery_gaps)*1.45,
                minimum_foot_envelope_gap_world_m=min(foot_gaps)*1.45,
                minimum_protected_component_gap_world_m=min(component_gaps)*1.45,
                rear_launcher_lids=False,silo_interiors=interior_checks,poses=audits)
    (ROOT/'.build/siege-throne-review.json').write_text(json.dumps(report,indent=2))
    print('SIEGE_REFIT_REVIEW_PASS: 18 pose/detail checks, 72 unchanged forward launch markers, actual-mesh previews.',flush=True)

if __name__=='__main__':main()
