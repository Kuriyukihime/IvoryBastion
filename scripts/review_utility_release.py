"""Review the exact runtime meshes and compiled choreography, not concept art.
Lighting is approximate; these studies do not replace live Unity review.
"""
from pathlib import Path
import copy,json,math,subprocess,wave
import numpy as np
from scipy.spatial.transform import Rotation
from PIL import Image,ImageDraw,ImageFont
import presentation_mesh as m
from review_animation_quality import smooth_frames
ROOT=m.ROOT
OUT=ROOT/'dist/IvoryBastion-1.0.0-review'
FONT=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
SMALL=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',13)
def export(requests,name):
    for request in requests:
        if request.get('Input',{}).get('MineAge',-1)>=0:request['Input'].setdefault('MineSpan',mine_presentation()['BaseDeploySpan'])
    source=ROOT/f'.build/utility-{name}-input.json';target=ROOT/f'.build/utility-{name}-poses.json'
    source.write_text(json.dumps(requests),encoding='utf-8');dll=ROOT/'scripts/animation_review/bin/Review/net10.0/AnimationReview.dll'
    subprocess.run(['dotnet',str(dll),'--export',str(source),str(target)],check=True,capture_output=True)
    return json.loads(target.read_text(encoding='utf-8'))
def caption(image,title):
    result=Image.new('RGB',(image.width,image.height+58),'#09111c');result.paste(image.convert('RGB'),(0,0));d=ImageDraw.Draw(result)
    d.text((14,image.height+5),title,font=FONT,fill='#d7edff');d.text((14,image.height+31),'1.0.0 review / actual meshes / offline studio render',font=SMALL,fill='#809db6');return result
MINE_PRESENTATION=None
def mine_presentation():
    global MINE_PRESENTATION
    if MINE_PRESENTATION is None:
        target=ROOT/'.build/mine-presentation.json'
        dll=ROOT/'scripts/utility_checks/bin/Release/net10.0/Checks.dll'
        subprocess.run(['dotnet',str(dll),'--mine-presentation',str(target)],check=True,capture_output=True)
        MINE_PRESENTATION=json.loads(target.read_text(encoding='utf-8'))
    return MINE_PRESENTATION

def launchers(frame,age=-1):
    f=copy.deepcopy(frame);amount=min(1,max(0,age*9)) if 0<=age<=mine_presentation()['BaseDeploySpan'] else 0
    for i in range(2):f['Bones'][f'mine_hatch_{i}']=dict(Rotation=dict(X=0,Y=0,Z=mine_presentation()['HatchLeft' if i==0 else 'HatchRight']*amount),Offset=dict(X=0,Y=0,Z=0))
    return m.pose(*m.load('mine_launcher'),f)
def cover():
    f=export([dict(Form='siege',Input=dict(Unfold=1,Time=1))],'cover')[0]
    im=m.render('siege',(1200,1200),yaw=28,pitch=9,frame=f,extras=launchers(f));im.save(ROOT/'assets/package_siege_cover.png');im.save(OUT/'siege-cover.png')
    from make_package_icon import generate
    generate();print('Rendered siege cover with flush mine launchers',flush=True)
def poses():
    cases=[('mobile','HEAVENFALL / compression',dict(Unfold=0,Time=1,WingburstAge=.08)),('mobile','HEAVENFALL / launch',dict(Unfold=0,Time=1,WingburstAge=.32,Forward=.8)),('mobile','HEAVENFALL / brake',dict(Unfold=0,Time=1,WingburstAge=.78,Forward=.2)),('siege','WIDOW ARRAY / eject',dict(Unfold=1,Time=1,MineAge=.2)),('siege','WIDOW ARRAY / launch wells',dict(Unfold=1,Time=1,MineAge=.65)),('siege','WIDOW ARRAY / settle',dict(Unfold=1,Time=1,MineAge=mine_presentation()['BaseDeploySpan']+.3))]
    frames=export([dict(Form=f,Input=p) for f,_,p in cases],'sheet');sheet=Image.new('RGB',(1500,1100),'#09111c')
    for i,((form,title,p),f) in enumerate(zip(cases,frames)):
        im=m.render(form,(500,492),yaw=28,pitch=8,frame=f,extras=launchers(f,p.get('MineAge',-1)) if form=='siege' else None);sheet.paste(caption(im,title),(i%3*500,i//3*550))
    sheet.save(OUT/'utility-model-poses.png');print('Rendered compiled utility poses',flush=True)
def heavenfall():
    step=.04;requests=[dict(Form='mobile',Input=dict(Unfold=0,Time=1+i*step,WingburstAge=i*step-.04,Forward=.65*math.sin(min(1,i*step/.85)*math.pi))) for i in range(27)]
    frames=smooth_frames(export(requests,'heavenfall'),requests,step=step);images=[caption(m.render('mobile',(580,610),yaw=145,pitch=6,frame=f,view=(0,1.05,0,1.5)),'HEAVENFALL RUSH / gather, thrust, brake') for f in frames]
    images[0].save(OUT/'heavenfall-rush-motion.gif',save_all=True,append_images=images[1:],duration=40,loop=0);print('Rendered Heavenfall Rush motion',flush=True)
def selection():
    times=[i*.1 for i in range(28)];requests=[]
    for t in times:
        u=max(0,min(1,(t-.35)/1.65));requests.extend(dict(Form=form,Input=dict(Unfold=u,Time=t)) for form in ('mobile','siege'))
    frames=export(requests,'selection');images=[]
    for i,t in enumerate(times):
        u=requests[i*2]['Input']['Unfold'];s=max(0,min(1,u/.7));scale=.75+(.38-.75)*s*s*(3-2*s);grow=max(0,min(1,u/.16));fortress=.38*grow*grow*(3-2*grow);base=Image.new('RGBA',(640,660),'#09111c')
        for ix,form,visible,base_scale in [(1,'siege',u>.015,1.45),(0,'mobile',u<.28,.78)]:
            if not visible:continue
            f=copy.deepcopy(frames[i*2+ix]);f['Scale']*=(fortress if ix==1 else scale)*base_scale
            im=m.render(form,(640,660),yaw=0,pitch=8,frame=f,view=(.4,1.18,0,1.75),transparent=True,extras=launchers(f) if ix==1 else None);base=Image.alpha_composite(base,im)
        images.append(caption(base,'SURVIVOR SELECT / left-shifted front-facing deployment'))
    images[0].save(OUT/'survivor-select.gif',save_all=True,append_images=images[1:],duration=100,loop=0);images[-1].save(OUT/'survivor-select-siege.png');print('Rendered smaller front-facing selection',flush=True)
def mine_studies():
    sheet=Image.new('RGB',(1600,510),'#09111c')
    for k,state in enumerate(('folded','planted','walking','attached')):
        f=dict(Rotation=dict(X=0,Y=0,Z=0),Offset=dict(X=0,Y=0,Z=0),Scale=1,Bones={})
        for i in range(8):
            a=(i+.5)*math.pi/4;axis=np.array([-math.sin(a),0,math.cos(a)])
            for bone,degrees in [('hip',mine_presentation()['Samples'][0]['Hip'] if k==0 else 0),('knee',mine_presentation()['Samples'][0]['Knee'] if k==0 else max(0,math.sin(.28*25+i*math.pi))*16 if k==2 else 0)]:
                rot=Rotation.from_rotvec(axis*math.radians(degrees))
                if bone=='hip' and k==2:rot=rot*Rotation.from_euler('y',math.sin(.28*25+i*math.pi)*17,degrees=True)
                zxy=rot.as_euler('zxy',degrees=True);f['Bones'][f'mine_{bone}_{i}']=dict(Rotation=dict(X=float(zxy[1]),Y=float(zxy[2]),Z=float(zxy[0])),Offset=dict(X=0,Y=0,Z=0))
        if k==3:f['Rotation']['X']=0
        im=m.render('spider_mine',(400,450),yaw=25,pitch=25,frame=f,view=(0,.2,0,.72));sheet.paste(caption(im,'SPIDER MINE / '+state.upper()),(k*400,0))
    sheet.save(OUT/'widow-array-mine-model.png')
    attached=ROOT/'.build/attached-mine-visual-proof.png'
    if attached.exists():(OUT/'widow-array-mine-model.png').write_bytes(attached.read_bytes())
    from review_widow_field import generate
    generate();print('Rendered mine model and production field layout',flush=True)
def review_extras():
    # Keep older handoff links current rather than retaining superseded poses.
    (OUT/'wingburst-motion.gif').write_bytes((OUT/'heavenfall-rush-motion.gif').read_bytes())
    contact=Image.new('RGB',(960,718),'#09111c')
    with Image.open(OUT/'survivor-select.gif') as animation:
        for i,index in enumerate((0,5,9,14,19,27)):
            animation.seek(index);thumb=animation.convert('RGB').resize((320,359),Image.Resampling.LANCZOS)
            contact.paste(thumb,(i%3*320,i//3*359))
    contact.save(OUT/'survivor-select-contact.png')
    f=export([dict(Form='siege',Input=dict(Unfold=1,Time=1))],'rear-proof')[0]
    caption(m.render('siege',(1200,660),yaw=154,pitch=15,frame=f,extras=launchers(f)),
            'REAR SILOS / retained geometry; mine wells sit forward on the hull').save(OUT/'mine-launcher-rear-check.png')
    sheet=Image.new('RGB',(1200,478),'#09111c')
    for i,(age,title) in enumerate(((-1,'LAUNCH WELLS / closed'),(.4,'LAUNCH WELLS / opening upward'))):
        pose=copy.deepcopy(f);amount=min(1,max(0,age*9)) if age>=0 else 0
        for side in range(2):pose['Bones'][f'mine_hatch_{side}']=dict(Rotation=dict(X=0,Y=0,Z=mine_presentation()['HatchLeft' if side==0 else 'HatchRight']*amount),Offset=dict(X=0,Y=0,Z=0))
        image=m.render('mine_launcher',(600,420),yaw=25,pitch=20,frame=pose,view=(0,1.02,.46,1.25))
        sheet.paste(caption(image,title),(i*600,0))
    sheet.save(OUT/'mine-launcher-hinges.png')
    cues=[('transform_deploy',.75),('transform_deploy_game',.8),('transform_pack_game',.8),
          ('wingburst_start',.65),('mine_deploy',.65),('mine_arm',.4),
          ('mine_chain_prime',.8),('mine_chain_burst',.8)]
    samples=[];silence=np.zeros(14400,dtype=np.float32)
    for name,gain in cues:
        with wave.open(str(ROOT/f'assets/sfx_{name}.wav'),'rb') as source:
            assert source.getsampwidth()==2
            rate=source.getframerate();channels=source.getnchannels()
            pcm=np.frombuffer(source.readframes(source.getnframes()),dtype='<i2').astype(np.float32).reshape(-1,channels).mean(axis=1)/32768
            if rate!=48000:
                from scipy.signal import resample_poly
                divisor=math.gcd(rate,48000);pcm=resample_poly(pcm,48000//divisor,rate//divisor)
        samples.extend((pcm*gain,silence))
    with wave.open(str(OUT/'utility-sound-preview.wav'),'wb') as target:
        target.setnchannels(1);target.setsampwidth(2);target.setframerate(48000)
        target.writeframes(np.clip(np.concatenate(samples)*32767,-32768,32767).astype('<i2').tobytes())
    (OUT/'sound-preview-order.txt').write_text('\n'.join(name for name,_ in cues)+'\n\nAuthored cues at representative gain, separated by silence. Not a live game mix.\n',encoding='utf-8')
    print('Refreshed selection contact sheet, launcher proof, legacy GIF link and sound preview',flush=True)

if __name__=='__main__':
    OUT.mkdir(parents=True,exist_ok=True);subprocess.run(['dotnet','build',str(ROOT/'scripts/animation_review/AnimationReview.csproj'),'-c','Release','--nologo'],check=True,capture_output=True)
    cover();poses();heavenfall();selection();mine_studies();review_extras()
