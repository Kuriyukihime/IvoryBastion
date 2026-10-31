"""Seraph production details. All pieces become the shipped rigged geometry."""
import math
import numpy as np
import mesh_tools as m
import seraph_refinement as d

def glyph(points,at,bone,scale=.012,u=(1,0,0),v=(0,1,0),mat='Ink',width=.0014):
 at=np.asarray(at);u=np.asarray(u);v=np.asarray(v)
 d.line(mat,[at+u*x*scale+v*y*scale for x,y in points],width,bone)

def badge(at,bone,scale=.014,u=(1,0,0),v=(0,1,0)):
 glyph([(-1,1.5),(0,.5),(1,1.5),(.7,-.5),(0,-1.3),(-.7,-.5),(-1,1.5)],at,bone,scale,u,v,'Ink')
 glyph([(-.5,.7),(0,.1),(.5,.7)],np.asarray(at)+np.cross(u,v)*.0008,bone,scale,u,v,'Blue',.001)

def mobile():
 # A layered cervical collar replaces the exposed cylindrical neck silhouette.
 for k in range(3):
  m.torus('Joint',(0,1.313+k*.015,-.004),.046,.003,m.rigid('head'),normal=(0,1,0))
 for side,s in [('l',-1),('r',1)]:
  arm='arm_'+side;fore='fore_'+side;leg='leg_'+side;boot='boot_'+side
  badge((s*.213,1.18,.072),arm,.010)
  # Paired etched identification bars and ceramic service tabs.
  for k in range(3):d.box('Ink',(s*.127,.70-k*.011,.089),(.031,.003,.002),leg,bevel=.0005)
  glyph([(0,-1),(0,1),(-.45,.4)],(s*.14,.644,.094),leg,.012)
  for k in range(3):d.box('Ink',(s*.310,.807-k*.011,.112),(.020,.002,.002),fore,bevel=.0005)
  # Exposed actuator sleeve, dorsal cable and small cooling fins.
  d.rod('Joint',(s*.254,1.09,-.03),(s*.279,.988,-.021),.010,arm)
  d.rod('Edge',(s*.260,1.07,-.032),(s*.280,.984,-.023),.004,arm)
  d.line('Frame',[(s*.12,.72,-.081),(s*.14,.62,-.075),(s*.12,.51,-.049)],.006,leg)
  for k in range(5):d.box('Edge',(s*.115,.30-k*.019,-.075),(.032,.003,.009),boot,bevel=.0007)
  for upper,name in [(True,'wing_'+side),(False,'vane_'+side)]:
   base=np.array((s*.16,1.16 if upper else 1.05,-.15 if upper else -.16))
   d.disc('Frame',base+(0,0,-.027),.037,.018,name,normal=(0,0,-1),sides=32)
   d.disc('Blue',base+(0,0,-.038),.023,.006,name,normal=(0,0,-1),sides=32)
   for a in np.arange(6)*math.tau/6:
    p=base+np.array((math.cos(a)*.029,math.sin(a)*.029,-.043));d.bolt(p,.003,name,normal=(0,0,-1))
 # Back-mounted reactor spine and curved shoulder-level data conduits.
 for s in [-1,1]:
  d.line('Joint',[(s*.075,1.19,-.075),(s*.095,1.12,-.105),(s*.068,1.04,-.11)],.007,'torso')
  d.line('Blue',[(s*.069,1.18,-.080),(s*.089,1.12,-.110),(s*.062,1.05,-.115)],.0015,'torso')
 badge((0,1.109,-.132),'torso',.018,u=(-1,0,0))

def siege():
 for i in range(12):
  a=i*math.tau/12;r=np.array((math.sin(a),0,math.cos(a)));u=np.array((math.cos(a),0,-math.sin(a)))
  badge(r*1.018+(0,.50,0),'root',.030,u=u)
  for sign in [-1,1]:
   d.box('Ink',r*1.021+u*sign*.14+(0,.52,0),(.016,.074,.003),'root',u=u,bevel=.001)
  for j in range(4):d.box('Edge',r*.98+u*(j-1.5)*.055+(0,.314,0),(.031,.009,.012),'root',u=u,bevel=.002)
 # Split breech heat sinks articulate independently after an Ion Lance shot.
 for i in range(4):
  a=(i+.5)*math.tau/4;u=np.array((math.cos(a),math.sin(a),0));v=np.array((-math.sin(a),math.cos(a),0));b='vent_'+str(i)
  for k in range(4):d.box('Joint',u*.256+(0,.94,.37+k*.071),(.079,.024,.044),b,u=v,v=u,bevel=.004)
  d.box('Ceramic',u*.275+(0,.94,.474),(.094,.019,.295),b,u=v,v=u,bevel=.005)
  d.box('Blue',u*.289+(0,.94,.474),(.036,.004,.182),b,u=v,v=u,bevel=.001)
 # Holographic console housings and the rear coolant manifold.
 for s in [-1,1]:
  d.box('Frame',(s*.292,.970,.26),(.145,.018,.17),u=(1,0,0),v=(0,0,-1))
  for k in range(4):d.box('Blue',(s*.292,.981,.22+k*.022),(.08-k*.010,.002,.005))
  d.line('Joint',[(s*.22,.77,-.38),(s*.37,.68,-.55),(s*.55,.40,-.69)],.022)
  for k in range(5):d.box('Edge',(s*.355,.71-k*.032,-.53),(.10,.012,.023))
 # Restrained warning chevrons on the cannon sides.
 for s in [-1,1]:
  for k in range(3):glyph([(-1,-.8),(0,0),(-1,.8)],(s*.169,.94,1.02+k*.092),'cannon',.021,u=(0,0,1),v=(0,1,0))

def unit(kind):
 for s,side in [(-1,'l'),(1,'r')]:
  b='wing_'+side
  d.joint((s*.255,0,0),.046,b,normal=(1,0,0))
  for k in range(4):d.box('Frame',(s*.41,.077,-.11-k*.026),(.12,.006,.012),b)
  badge((s*.46,.084,-.12),b,.025,u=(1,0,0),v=(0,0,-1))
 for i in range(6):
  a=i*math.tau/6;at=np.array((math.cos(a)*.115,-.161,math.sin(a)*.115))
  d.box('Edge',at,(.019,.016,.024),u=(math.cos(a),0,math.sin(a)))
 d.disc('Joint',(0,.02,.26),.048,.018,'sensor',sides=32)
 d.disc('Glass',(0,.02,.273),.034,.012,'sensor',sides=32)
 d.disc('Core',(0,.02,.280),.018,.003,'sensor',sides=32)
