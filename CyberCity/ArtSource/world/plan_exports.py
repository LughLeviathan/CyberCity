"""Analyze Aseprite exports and write a copy/crop plan; final images are authored/exported by MCP."""
from pathlib import Path
from PIL import Image,ImageFilter
import numpy as np,json
R=Path(__file__).resolve().parent
a=np.asarray(Image.open(R/'export/layers/ground.png').convert('RGBA'))
seen={};unique=[];ids=[]
for y in range(0,576,32):
 for x in range(0,1024,32):
  key=a[y:y+32,x:x+32].tobytes()
  if key not in seen:seen[key]=len(unique);unique.append({'x':x,'y':y})
  ids.append(seen[key])
m=json.loads((R/'world-manifest.json').read_text())
sprites=[]
for b in m['buildings']:
 arr=np.asarray(Image.open(R/'export/layers'/f"{b['layer']}.png").convert('RGBA'))
 ys,xs=np.where(arr[:,:,3]>0);box=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]
 sprites.append({'id':b['id'],'area':b['name'],'layer':b['layer'],'box':box,'kind':'building','door':b['doorPixel']})
props=Image.open(R/'export/layers/props.png').convert('RGBA')
mask=np.asarray(props.getchannel('A').filter(ImageFilter.MaxFilter(7)))>0
visited=np.zeros(mask.shape,bool)
for sy,sx in zip(*np.where(mask)):
 if visited[sy,sx]:continue
 todo=[(int(sx),int(sy))];visited[sy,sx]=True;points=[]
 while todo:
  x,y=todo.pop();points.append((x,y))
  for nx,ny in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
   if 0<=nx<1024 and 0<=ny<576 and mask[ny,nx] and not visited[ny,nx]:visited[ny,nx]=True;todo.append((nx,ny))
 x0=min(x for x,y in points);x1=max(x for x,y in points)+1;y0=min(y for x,y in points);y1=max(y for x,y in points)+1
 crop=props.crop((x0,y0,x1,y1));b=crop.getbbox()
 if b:sprites.append({'id':f'prop_{len(sprites)-6:02}','layer':'props','box':[x0+b[0],y0+b[1],x0+b[2],y0+b[3]],'kind':'prop'})
plan={'width':1024,'height':576,'tileSize':32,'columns':16,'rows':(len(unique)+15)//16,'tileIds':ids,'uniqueTiles':unique,'sprites':sprites}
(R/'export/export-plan.json').write_text(json.dumps(plan,separators=(',',':')))
print('Tiles',len(unique),'sprites',len(sprites))
