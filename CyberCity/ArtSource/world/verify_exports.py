"""Read-only raster export reconstruction; never produces final game art."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
r=Path(__file__).resolve().parent
t=r.parents[1]/'Assets/CyberCity/WorldArt/Textures'
d=json.loads((r.parents[1]/'Assets/CyberCity/WorldArt/world.json').read_text())
atlas=Image.open(t/'terrain.png').convert('RGBA')
ground=Image.new('RGBA',(d['width'],d['height']))
for n,idx in enumerate(d['tileIds']):
    x=idx%d['columns']*32;y=idx//d['columns']*32
    ground.paste(atlas.crop((x,y,x+32,y+32)),(n%32*32,n//32*32))
expected=np.array(Image.open(r/'export/layers/ground.png').convert('RGBA'))
assert np.array_equal(expected,np.array(ground)), 'Tile reconstruction differs'
out=ground.copy()
water=Image.open(t/'water.png').convert('RGBA')
assert water.size==(3072,576)
out.alpha_composite(water.crop((0,0,1024,576)))
out.alpha_composite(Image.open(t/'scenery_edge.png').convert('RGBA'))
for s in d['sprites']:
    out.alpha_composite(Image.open(t/(s['id']+'.png')).convert('RGBA'),tuple(s['box'][:2]))
for s in d['sprites'][:6]:
    out.alpha_composite(Image.open(t/(s['id']+'_glow.png')).convert('RGBA'),tuple(s['box'][:2]))
preview=np.array(Image.open(r/'world_preview.png').convert('RGBA'))
delta=np.any(preview!=np.array(out),axis=2)
assert not delta.any(), f'{delta.sum()} pixels differ after export'
report={'groundReconstructionMismatchPixels':0,'fullReconstructionMismatchPixels':int(delta.sum()),
        'uniqueTiles':d['uniqueTileCount'],'tileCells':len(d['tileIds']),'bottomPivotSprites':len(d['sprites']),
        'waterFrames':3,'paletteColors':len(set(map(tuple,preview[:,:,:3].reshape(-1,3)))),
        'sourceFiles':len(list(r.rglob('*.aseprite')))}
(r/'export-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
