"""Rebuild actual Seraph meshes, deferring images to the OpenGL presentation renderer."""
import json
import mesh_tools as m
import seraph_models as models
jobs=[];current=None
write=m.write_asset

def save(name):
 global current
 current=name;write(name)

def queue(size,yaw,crop,filename,title=None):
 jobs.append(dict(asset=current,size=size,yaw=yaw,crop=crop,path=str(filename.relative_to(m.ROOT)).replace(chr(92),"/"),title=title))

if __name__=='__main__':
 m.write_asset=save;m.render=queue
 models.mobile();models.siege();models.units()
 (m.ROOT/'.build').mkdir(exist_ok=True)
 (m.ROOT/'.build/presentation-renders.json').write_text(json.dumps(jobs,indent=2))
 (m.ROOT/'scripts/presentation_render_jobs.json').write_text(json.dumps(jobs,indent=2))
 print('Model geometry complete; preview jobs saved.',flush=True)
