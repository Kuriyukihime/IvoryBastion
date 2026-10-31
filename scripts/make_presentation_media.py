"""Original high-resolution Seraph icon family and material surface tiles."""
from pathlib import Path
import math,json
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageFont
ROOT=Path(__file__).resolve().parents[1];ASSETS=ROOT/'assets';SIZE=1024
WHITE=(226,239,250);BLUE=(60,161,255);GOLD=(247,195,66)

def artwork(name,transparent=False):
 yy,xx=np.mgrid[:SIZE,:SIZE];shade=np.clip((1-yy/SIZE)*10+np.exp(-((xx-SIZE*.6)**2+(yy-SIZE*.40)**2)/(SIZE*.45)**2)*12,0,35)
 base=np.stack([shade*.40+7,shade*.70+13,shade+23,np.full_like(shade,255)],axis=-1).astype('uint8')
 im=Image.fromarray(base,'RGBA') if not transparent else Image.new('RGBA',(SIZE,SIZE))
 layer=Image.new('RGBA',im.size);d=ImageDraw.Draw(layer);maskall=Image.new('L',im.size)
 def pts(p):return [(int(x*4),int(y*4)) for x,y in p]
 def metal(p,color=WHITE):
  p=pts(p);d.polygon([(x+14,y+20) for x,y in p],fill=(2,6,12,230));mask=Image.new('L',im.size);ImageDraw.Draw(mask).polygon(p,fill=255)
  val=np.clip(1.20-yy/SIZE*.60,0,1.2);a=np.zeros((SIZE,SIZE,4),dtype='uint8');a[:,:,:3]=np.clip(np.array(color)[None,None,:]*val[:,:,None],0,255);a[:,:,3]=np.array(mask)
  layer.alpha_composite(Image.fromarray(a));ImageDraw.Draw(maskall).polygon(p,fill=255)
  d.line(p+[p[0]],fill=(*np.minimum(np.array(color)+20,255),255),width=5,joint='curve')
 def line(p,color=BLUE,w=4):
  pp=pts(p);glow=Image.new('RGBA',im.size);gd=ImageDraw.Draw(glow);gd.line(pp,fill=(*color,220),width=int(w*7),joint='curve');layer.alpha_composite(glow.filter(ImageFilter.GaussianBlur(w*3)))
  d.line(pp,fill=(*color,255),width=int(w*4),joint='curve');d.line(pp,fill=(220,244,255,255),width=max(2,int(w)),joint='curve');ImageDraw.Draw(maskall).line(pp,fill=255,width=int(w*4),joint='curve')
 def circle(box,color=BLUE,w=3):
  d.ellipse(tuple(int(x*4) for x in box),outline=(*color,255),width=w*4);ImageDraw.Draw(maskall).ellipse(tuple(int(x*4) for x in box),outline=255,width=w*4)
 if not transparent:
  d.line(pts([(19,53),(19,27),(31,15),(219,15),(239,35),(239,220),(221,239),(36,239)]),fill=(77,112,148,255),width=5)
  d.line(pts([(31,15),(95,15)]),fill=(*BLUE,255),width=10)
  for y in [66,79,92]:d.line(pts([(19,y),(24,y)]),fill=(141,180,211,255),width=4)
 if name=='pistol':
  for x,y in [(38,77),(88,133)]:
   metal([(x,y+20),(x+70,y-23),(x+95,y-5),(x+86,y+10),(x+22,y+52),(x+3,y+42)])
   metal([(x+2,y+36),(x+22,y+48),(x+8,y+70),(x-12,y+58)],(108,139,167))
   line([(x+29,y+19),(x+75,y-8)],BLUE,5);line([(x+95,y-13),(x+123,y-30)],BLUE,3)
 elif name=='slip':
  metal([(126,37),(163,116),(148,157),(127,141),(105,159),(91,115)])
  for s in [-1,1]:
   metal([(128+s*35,82),(128+s*80,42),(128+s*64,117),(128+s*42,145)],(151,187,215))
   line([(128+s*29,157),(128+s*40,205)],BLUE,8)
  line([(128,153),(128,224)],BLUE,11)
 elif name in ['deploy','factory']:
  metal([(70,139),(128,116),(189,139),(189,184),(128,207),(70,184)],(135,163,190))
  for s in [-1,1]:
   metal([(128+s*55,139),(128+s*74,58),(128+s*97,34),(128+s*91,156),(128+s*60,177)])
   line([(128+s*71,113),(128+s*81,70)],BLUE,5)
  metal([(110,143),(146,143),(145,185),(113,185)]);line([(128,157),(128,216)],BLUE,7)
  if name=='factory':circle((92,56,166,130),BLUE,5);metal([(116,80),(140,80),(140,104),(116,104)])
 elif name=='redline':
  for s in [-1,1]:metal([(128+s*20,73),(128+s*64,47),(128+s*91,87),(128+s*68,162),(128+s*33,183),(128+s*45,119)],(149,182,212))
  metal([(141,32),(90,132),(120,132),(106,224),(178,107),(143,107)],WHITE)
  line([(133,83),(115,122),(141,122),(127,175)],BLUE,6)
 elif name=='laser':
  metal([(34,157),(84,112),(126,133),(131,169),(76,208),(37,186)],(142,176,206))
  metal([(54,153),(86,130),(108,144),(76,168)])
  line([(105,143),(214,42)],BLUE,15);line([(114,148),(225,49)],WHITE,3)
  for a in [-1,1]:line([(183+a*10,61),(204+a*10,82)],BLUE,2)
 elif name=='protocol':
  metal([(128,32),(211,71),(194,165),(128,222),(62,165),(45,71)],GOLD)
  metal([(128,51),(190,81),(176,153),(128,195),(80,153),(66,81)],(18,50,79))
  metal([(128,70),(171,91),(162,145),(128,173),(94,145),(85,91)],WHITE)
  line([(128,85),(128,158)],BLUE,5);line([(105,120),(151,120)],BLUE,5)
 elif name=='saturation':
  for x,y in [(63,77),(128,43),(193,77)]:
   metal([(x,y),(x+14,y+21),(x+14,y+79),(x+27,y+95),(x-27,y+95),(x-14,y+79),(x-14,y+21)])
   line([(x,y+40),(x,y+77)],BLUE,4);line([(x,y+103),(x,y+133)],BLUE,6)
  line([(72,220),(95,203),(112,215),(137,191),(160,208),(180,195)],BLUE,3)
 im.alpha_composite(layer)
 if transparent:
  im=Image.new('RGBA',im.size,(255,255,255,0));im.putalpha(maskall)
 return im.resize((512,512),Image.Resampling.LANCZOS)

def buff(name):
 im=Image.new('RGBA',(512,512),(255,255,255,0));mask=Image.new('L',(512,512));d=ImageDraw.Draw(mask)
 def poly(points,value=255):d.polygon([(x*2,y*2) for x,y in points],fill=value)
 def stroke(points,w=8):d.line([(x*2,y*2) for x,y in points],fill=255,width=w*2,joint='curve')
 if name=='buff_aegis':
  poly([(128,23),(219,66),(197,163),(128,227),(59,163),(37,66)])
  poly([(128,45),(197,79),(180,153),(128,202),(76,153),(59,79)],0)
  poly([(117,76),(139,76),(139,112),(172,112),(172,134),(139,134),(139,168),(117,168),(117,134),(84,134),(84,112),(117,112)])
 elif name=='buff_shield':
  poly([(128,29),(208,64),(194,154),(128,221),(62,154),(48,64)])
  poly([(128,50),(188,78),(175,145),(128,195),(81,145),(68,78)],0)
  stroke([(92,85),(128,69),(164,85)],9);stroke([(128,95),(128,168)],12)
 elif name=='buff_shift':
  poly([(149,24),(91,129),(122,129),(106,231),(182,103),(145,103)])
  stroke([(72,66),(49,116),(72,164)],11);stroke([(194,73),(218,118),(193,167)],11)
 else:
  poly([(36,46),(55,65),(73,145),(110,130),(110,107),(146,107),(146,130),(183,145),(201,65),(220,46),(213,180),(171,204),(85,204),(43,180)])
  poly([(99,165),(157,165),(157,185),(99,185)],0);stroke([(128,113),(128,151)],9)
 im.putalpha(mask);return im.resize((256,256),Image.Resampling.LANCZOS)

def main():
 names=['pistol','slip','deploy','redline','laser','protocol','saturation','factory']
 for name in names:artwork(name).save(ASSETS/(name+'.png'))
 for name in ['buff_aegis','buff_shield','buff_shift','buff_deployed']:buff(name).save(ASSETS/(name+'.png'))
 rng=np.random.default_rng(733);size=512;y,x=np.mgrid[:size,:size]
 for name in ['ceramic_surface','alloy_surface']:
  grain=rng.normal(0,1.1,(size,size));broad=np.asarray(Image.fromarray(np.uint8(rng.uniform(0,255,(32,32)))).resize((size,size),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(8)),float)/255
  if name=='ceramic_surface':v=242+grain+6*(broad-.5)
  else:v=234+grain*.8+5*np.sin(y*.8)+7*(broad-.5)
  a=np.repeat(np.uint8(np.clip(v,0,255))[:,:,None],3,axis=2);Image.fromarray(a).save(ASSETS/(name+'.png'))
 sheet=Image.new('RGB',(2048,1024),'#101a29')
 for i,name in enumerate(names):sheet.paste(Image.open(ASSETS/(name+'.png')),((i%4)*512,(i//4)*512))
 version=json.loads((ROOT/'package/manifest.json').read_text())['version_number']
 sheet.save(ROOT/f'dist/IvoryBastion-{version}-icons.png')
 print('Created eight 512px skill illustrations, transparent buff masks and surface tiles.')
if __name__=='__main__':main()
