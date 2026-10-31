"""Layout proof of the shipped UGUI coordinates and artwork, not a game screenshot."""
from pathlib import Path
import re,json,textwrap
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'dist';A=ROOT/'assets'
VERSION=json.loads((ROOT/'package/manifest.json').read_text())['version_number']
WHITE='#dfeeff';QUIET='#8face0';BLUE='#2994ff';READY='#8ce0ff';GOLD='#ffca4a';BG='#101b2b'
F='C:/Windows/Fonts/segoeui.ttf';FB='C:/Windows/Fonts/seguisb.ttf';issues=[]
def font(n,bold=False):return ImageFont.truetype(FB if bold else F,round(n))
def label(im,text,x,y,w,h,n=13,color=WHITE,wrap=False,bold=False,align='left'):
 d=ImageDraw.Draw(im);f=font(n,bold);lines=[]
 for row in text.split('\n'):
  words=row.split();line=''
  for word in words:
   test=(line+' '+word).strip()
   if wrap and d.textlength(test,font=f)>w and line:lines.append(line);line=word
   else:line=test
  lines.append(line)
 widths=[d.textlength(l,font=f) for l in lines];lineh=n*1.18
 if max(widths,default=0)>w+1 or len(lines)*lineh>h+2:issues.append(dict(text=text,width=max(widths),max_width=w,height=len(lines)*lineh,max_height=h))
 yy=y-len(lines)*lineh/2
 for line,width in zip(lines,widths):
  xx=x-w/2 if align=='left' else x-width/2 if align=='center' else x+w/2-width
  d.text((xx,yy),line,font=f,fill=color);yy+=lineh

def art(im,name,x,y,w,h,opacity=1):
 a=Image.open(A/f'{name}.png').convert('RGBA');a.thumbnail((int(w),int(h)),Image.Resampling.LANCZOS)
 if opacity<1:a.putalpha(a.getchannel('A').point(lambda v:round(v*opacity)))
 im.alpha_composite(a,(int(x-a.width/2),int(y-a.height/2)))
def rect(im,x,y,w,h,color):ImageDraw.Draw(im).rectangle((x-w/2,y-h/2,x+w/2,y+h/2),fill=color)
def bar(im,x,y,w,value,color=BLUE):
 rect(im,x,y,w+2,7,'#34485c');rect(im,x,y,w,5,'#0e1a29');rect(im,x-w*(1-value)/2,y,w*value,5,color)
 for i in range(1,8):rect(im,x-w/2+w*i/8,y,1,5,'#1a3449')
def frame(im,x,y,w,h,color='#52718c'):
 d=ImageDraw.Draw(im)
 for side in (-1,1):
  for top in (-1,1):
   xx=x+side*w/2;yy=y+top*h/2
   d.line([(xx-side*11,yy),(xx,yy),(xx,yy-top*11)],fill=color,width=1)
def cockpit(siege,aegis=False):
 im=Image.new('RGBA',(420,180));d=ImageDraw.Draw(im);accent=GOLD if aegis else BLUE
 d.polygon([(0,16),(16,0),(420,0),(420,164),(404,180),(0,180)],fill='#0b1726',outline='#60758a',width=1)
 d.polygon([(17,1),(106,1),(109,4),(17,4)],fill='#c9dae9');d.line([(114,2),(403,2)],fill=accent,width=2)
 d.line([(6,20),(6,168)],fill='#435d77');d.line([(413,138),(413,162)],fill=accent);d.line([(375,176),(399,176)],fill=accent)
 for i in range(3):d.line([(21+i*7,176),(25+i*7,176)],fill='#758da4')
 def l(text,x,y,w,h,n=13,color=WHITE,**kw):label(im,text,x+210,90-y,w,h,n,color,**kw)
 frame(im,60,55,104,106)
 art(im,'hud_siege' if siege else 'hud_mobile',60,55,102,108)
 l('SERAPH / SIEGE' if siege else 'SERAPH / FLIGHT',53,65,282,22,18,bold=True)
 l('32 / 40 MISSILES' if siege else '86% THRUSTER RESERVE',53,36,282,30,23,BLUE)
 bar(im,263,75,282,.8 if siege else .86)
 l('AEGIS 4.2s  /  RELOAD x2' if aegis else '1 MISSILES/s  /  HEAVY EVERY 8' if siege else 'MISSILES 30/40  /  HITS LOAD STOCKPILE',53,-2,282,20,11.5,QUIET)
 if siege:
  for i in range(8):rect(im,133+i*28,109,23,5,BLUE if i<7 else '#293d54')
 else:bar(im,263,109,282,.75,READY)
 rect(im,210,123,384,1,'#3b576f')
 l('INNATE SHIELD 50/50   /   67.5% DAMAGE REDUCTION' if aegis else 'INNATE SHIELD 50/50   /   50% DAMAGE REDUCTION' if siege else 'INNATE SHIELD 15/15   /   50% DAMAGE REDUCTION',0,-46,382,18,12,GOLD if aegis else READY)
 bar(im,210,150,382,1,GOLD if aegis else READY)
 l('82 FAB',-119,-76,142,19,12);l('[V] ENGINEERING',86,-76,210,19,11.5,QUIET,align='right');l('LANCE LOCKED' if siege else 'RESERVE 2.6s',-150,-20,102,20,10,QUIET,align='center')
 return im

def engineering():
 im=Image.new('RGBA',(1040,680),BG);d=ImageDraw.Draw(im);d.rectangle((0,0,1039,679),outline='#60758a',width=1);d.line((3,2,95,2),fill='#c9dae9',width=3);d.line((100,2,1025,2),fill=BLUE,width=2)
 def l(text,x,y,w,h,n=13,color=WHITE,**kw):label(im,text,x+520,340-y,w,h,n,color,**kw)
 def box(x,y,w,h,c):rect(im,x+520,340-y,w,h,c)
 def a(name,x,y,w,h,opacity=1):art(im,name,x+520,340-y,w,h,opacity)
 a('factory',-464,292,47,47);l('ENGINEERING    182 FABRICATION',30,301,925,29,25,bold=True)
 l('14 FAB/min idle  /  35 FAB/min in combat  /  upgrades persist between stages',30,272,925,23,14,QUIET);bar(im,520,93,984,.61)
 names=['ORDNANCE','AEGIS','LEGION','SKIRMISHER'];ranks=[2,1,2,0]
 texts=[['I  MAGAZINE\n40 → 60 missiles','II  ASSEMBLY\nReload 1 → 1.5/s','III  SPECIALIZATION\nHeavy every 6th, or laser 1200% / 4s'],['I  CAPACITORS\nShield +20%; barrier 50%','II  RECOVERY\nShield +40% total; healing 30%','III  SPECIALIZATION\nStronger reduction, or shield refill'],['I  SECOND HANGAR\nTwo autonomous unit slots','II  BLUEPRINTS\nGunner, Interceptor and Repair','III  THIRD HANGAR\nThree configurable unit slots'],['I  AIRFRAME\nFlight speed +15%','II  THRUSTER TANK\n3 → 5 seconds of reserve','III  BLUE SHIFT\n10s duration; Needlefire ricochets']]
 for i,name in enumerate(names):
  x=-387+i*258;box(x,13,242,434,'#09121c');a('art_'+name.lower(),x,173,228,112);frame(im,x+520,340-173,230,114);box(x,229,238,1,'#607d95');box(x,66,212,1,'#294357');l(name,x,101,212,28,19,bold=True);l(f'RANK {ranks[i]} / 3',x,79,212,18,10.5,QUIET)
  for t in range(3):
   col=WHITE if t<ranks[i] else READY if t==ranks[i] else QUIET
   box(x-101,48-t*51,3,37,BLUE if t<ranks[i] else col);l(texts[i][t],x+7,48-t*51,197,46,13,col,wrap=True)
  box(x,-134,212,35,'#15344d');l('WARHEADS / 300 FAB' if i==0 else f'UPGRADE / {[300,140,300,60][i]} FAB',x,-134,198,35,14,align='center')
  if i==0:box(x,-175,212,31,'#15344d');l('ION LANCE / 300 FAB',x,-175,198,31,14,align='center')
 l('LEGION LOADOUT',-398,-222,206,20,12,QUIET)
 for i,name in enumerate(['guardian','interceptor','repair']):
  x=(i-1)*330;box(x,-259,310,48,'#15344d');a('hud_unit_'+name,x-120,-259,58,43,1 if i<2 else .3);l(f'SLOT {i+1} / '+(['SENTINEL  ›','INTERCEPTOR  ›','LOCKED'][i]),x+26,-259,234,46,14,align='center')
 box(405,-314,174,33,'#15344d');l('CLOSE',405,-314,160,33,14,align='center');l('Upgrades persist for this run. Deploy to purchase or change units.',-101,-314,760,28,12,QUIET)
 return im

if __name__=='__main__':
 eng=engineering();eng.convert('RGB').save(OUT/f'IvoryBastion-{VERSION}-engineering.png')
 sheet=Image.new('RGBA',(1120,640),'#090e18');d=ImageDraw.Draw(sheet);d.text((30,24),'SERAPH / COCKPIT AND SKILL ICONS',font=font(24,True),fill=WHITE)
 for col,(siege,aegis) in enumerate([(False,False),(True,True)]):
  x=70+col*560;sheet.alpha_composite(cockpit(siege,aegis),(x,105))
  for i,name in enumerate(['laser','protocol','deploy','saturation'] if siege else ['pistol','slip','deploy','redline']):
   art(sheet,name,x+47+i*93,340,82,82);label(sheet,['M1','M2','SHIFT','R'][i],x+47+i*93,397,80,20,14,QUIET,align='center')
  label(sheet,'SIEGE / AEGIS ACTIVE' if siege else 'MOBILE / FLIGHT',x+210,457,420,28,21,WHITE,align='center',bold=True)
 for i,name in enumerate(['buff_deployed','buff_aegis','buff_shift','buff_shield']):art(sheet,name,397+i*100,535,43,43)
 label(sheet,'Layout proof / shipped artwork and UGUI coordinates / approximate font metrics',560,603,1060,30,15,QUIET,align='center')
 sheet.convert('RGB').save(OUT/f'IvoryBastion-{VERSION}-hud.png')
 (ROOT/'.build/hud-layout-review.json').write_text(json.dumps(issues,indent=2));print(json.dumps(issues,indent=2))
