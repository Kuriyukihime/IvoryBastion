"""Sweep the actual runtime mesh with exported C# deployment angles.

Run after utility_checks has been built. No source assets are rewritten.
"""
from pathlib import Path
import argparse
import json
import math
import subprocess
import numpy as np
from scipy.spatial.transform import Rotation
import presentation_mesh as mesh

ROOT=Path(__file__).resolve().parents[1]


def frame():
    return dict(Scale=1,Rotation=dict(X=0,Y=0,Z=0),Offset=dict(X=0,Y=0,Z=0),Bones={})


def mine_pose(sample):
    pose=frame()
    for i in range(8):
        angle=(i+.5)*math.pi/4
        axis=np.array([-math.sin(angle),0,math.cos(angle)])
        for bone,degrees in [('hip',sample['Hip']),('knee',sample['Knee'])]:
            zxy=Rotation.from_rotvec(axis*math.radians(degrees)).as_euler('zxy',degrees=True)
            pose['Bones'][f'mine_{bone}_{i}']=dict(Rotation=dict(X=float(zxy[1]),Y=float(zxy[2]),Z=float(zxy[0])),Offset=dict(X=0,Y=0,Z=0))
    return pose


def lid_pose(data,opened):
    pose=frame()
    for i,key in enumerate(('HatchLeft','HatchRight')):
        pose['Bones'][f'mine_hatch_{i}']=dict(Rotation=dict(X=0,Y=0,Z=data[key]*opened),Offset=dict(X=0,Y=0,Z=0))
    return pose


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--render',action='store_true')
    args=parser.parse_args()
    out=ROOT/'.build/mine-model-proof';out.mkdir(exist_ok=True)
    exported=out/'presentation.json'
    dll=ROOT/'scripts/utility_checks/bin/Release/net10.0/Checks.dll'
    subprocess.run(['dotnet',str(dll),'--mine-presentation',str(exported)],check=True,capture_output=True)
    data=json.loads(exported.read_text(encoding='utf-8'))
    count=0

    def check(condition,reason):
        nonlocal count
        count+=1
        if not condition:
            raise AssertionError(reason)

    mine=mesh.load('spider_mine')
    launchers=mesh.load('mine_launcher')
    rest=mesh.pose(*mine,None)
    floor=min(vertices[:,1].min() for _,vertices,_ in rest)
    lowest=100.0;highest=-100.0
    for sample in data['Samples']:
        posed=mesh.pose(*mine,mine_pose(sample))
        for (_,vertices,_),(_,original,_) in zip(posed,rest):
            check(vertices[:,1].min()>=floor-.002,'Staged unfolding must not push feet below the existing planted mesh thickness')
            check(np.all(vertices.min(0)>=original.min(0)-.6-1e-5) and np.all(vertices.max(0)<=original.max(0)+.6+1e-5),'Articulated mine must remain inside existing expanded renderer bounds')
            lowest=min(lowest,float(vertices[:,1].min()));highest=max(highest,float(vertices[:,1].max()))
    final=mesh.pose(*mine,mine_pose(data['Samples'][-1]))
    check(all(np.allclose(vertices,original,atol=1e-6) for (_,vertices,_),(_,original,_) in zip(final,rest)),'Fully deployed mine pose must remain exactly unchanged')
    folded=np.concatenate([vertices for _,vertices,_ in mesh.pose(*mine,mine_pose(data['Samples'][0]))])
    check(float(folded[:,1].max())<=.301,'Folded leg joints must stay below the carapace crest')
    check(np.max(np.linalg.norm(folded[:,[0,2]],axis=1))<.38,'Folded mine must stay compact under its shell')
    check(data['UnfoldSpeed']==7,'Unfold time must remain unchanged')

    closed=mesh.pose(*launchers,None)
    padding=data['LauncherBoundsExpansion']*.5
    lid_low=100.0;lid_high=-100.0
    for opened in np.linspace(0,1,201):
        posed=mesh.pose(*launchers,lid_pose(data,float(opened)))
        for (part,vertices,_),(_,original,_) in zip(posed,closed):
            check(np.all(vertices.min(0)>=original.min(0)-padding-1e-5) and np.all(vertices.max(0)<=original.max(0)+padding+1e-5),'Launcher material '+part['name']+' must stay within its renderer bounds throughout opening')
            if part['name']=='Ceramic':
                lid_low=min(lid_low,float(vertices[:,1].min()));lid_high=max(lid_high,float(vertices[:,1].max()))
                check(vertices[:,1].min()>=original[:,1].min()-.003,'Launcher cover must lift upward rather than rotate down through the hull')
    check(data['HatchLeft']>0 and data['HatchRight']<0,'Mirrored outer hinges must open upward')
    result=dict(checks=count,rest_min_y=float(floor),unfold_min_y=lowest,unfold_max_y=highest,folded_max_y=float(folded[:,1].max()),launcher_lid_min_y=lid_low,launcher_lid_max_y=lid_high,launcher_bounds_expansion=data['LauncherBoundsExpansion'])
    (out/'geometry.json').write_text(json.dumps(result,indent=2),encoding='utf-8')

    if args.render:
        from PIL import Image,ImageDraw,ImageFont
        font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
        sheet=Image.new('RGB',(1800,410),'#09111c')
        for i,(label,opened) in enumerate([('TUCKED',0),('HIP LIFT',.3),('KNEES OPEN',.62),('FEET LOWER',.9),('PLANTED',1)]):
            sample=data['Samples'][round(opened*200)]
            image=mesh.render('spider_mine',(360,360),yaw=25,pitch=18,frame=mine_pose(sample),view=(0,.2,0,.72))
            sheet.paste(image,(i*360,0));draw=ImageDraw.Draw(sheet);draw.text((i*360+12,370),label,font=font,fill='#d7edff')
        sheet.save(out/'compiled-staged-unfold.png')
    print(f'PASS: {count} actual-mesh mine folding, ground-clearance and swept launcher-bound checks. Proof: {out}')


if __name__=='__main__':
    main()
