"""Reference-only preparation. Final editing/quantization/export is performed by MCP Aseprite.

No Unity assets are written here. Coordinates are measured in the 1600x900 reference.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import json, hashlib

ROOT = Path(__file__).resolve().parent
SIZE = (1024, 576)
SOURCE = ROOT / 'reference/cybercity_reference.jpeg'
im = Image.open(SOURCE).convert('RGB')
original = im.copy()
log = []

def clone(source, destination, name, feather=3):
    patch = original.crop(source).resize((destination[2]-destination[0], destination[3]-destination[1]), Image.Resampling.BICUBIC)
    mask = Image.new('L', patch.size, 255)
    if feather:
        a = np.asarray(mask).copy()
        yy,xx = np.indices(a.shape)
        distance = np.minimum.reduce([xx,yy,a.shape[1]-1-xx,a.shape[0]-1-yy])
        a = np.minimum(255, (distance+1)*255/feather).astype('uint8')
        mask = Image.fromarray(a)
    im.paste(patch, destination[:2], mask)
    log.append({'kind':'reference_texture_clone','name':name,'source':source,'destination':destination})

# Concealed scene cannot be recovered; use nearby material rather than retaining HUD.
clone((436,0,1120,113),(0,0,436,113),'title and tagline skyline',0)
clone((905,0,1120,122),(1120,0,1442,122),'quest skyline',0)
clone((450,0,620,145),(1442,0,1600,145),'minimap skyline',0)
clone((370,114,510,210),(0,110,140,206),'portrait treeline',2)
clone((345,112,485,198),(137,110,277,196),'stats treeline',2)
clone((135,722,351,797),(0,788,360,900),'lower-left text water',0)
clone((1135,768,1265,895),(1354,815,1600,900),'lower-right text water',0)
clone((133,726,350,795),(462,785,1133,900),'palette panel water',0)

# Remove individual baked people/markers with local boundary diffusion, before palette snapping.
# This is deliberately confined to small cutouts; it does not generate final scenery.
people = [
 (760,207,792,244),(815,232,841,285),(258,326,286,375),(408,332,433,375),
 (505,396,527,441),(604,375,629,416),(1034,426,1060,480),(993,481,1020,528),
 (1030,268,1057,316),(1057,293,1087,343),(1079,269,1107,315),
 (1260,532,1284,585),(1325,550,1352,598),(235,543,261,585),(310,528,335,572),
 (364,512,393,565),(502,720,534,777),(681,705,711,762),(839,702,870,761),
 (765,724,787,766),(611,700,637,735),(931,704,955,736),(749,483,794,554)
]
for box in people:
    x0,y0,x1,y1=box
    crop_box=(max(0,x0-4),max(0,y0-4),min(im.width,x1+5),min(im.height,y1+5))
    a=np.asarray(im.crop(crop_box)).astype('float32')
    mask=Image.new('L',(a.shape[1],a.shape[0]),0)
    ImageDraw.Draw(mask).ellipse((x0-crop_box[0],y0-crop_box[1],x1-crop_box[0],y1-crop_box[1]),fill=255)
    m=np.asarray(mask)>0
    # Fill with nearby boundary mean, then diffuse boundary texture into erased cutout.
    a[m]=np.median(a[~m],axis=0)
    for _ in range(90):
        avg=(np.roll(a,1,0)+np.roll(a,-1,0)+np.roll(a,1,1)+np.roll(a,-1,1))*.25
        a[m]=avg[m]
    im.paste(Image.fromarray(np.clip(a,0,255).astype('uint8')),crop_box[:2])
log.append({'kind':'local_diffusion','name':'baked people and player marker','regions':people})

# Minor skyline lettering, kept separate from the large facade edits authored in Aseprite.
small_signs=[(465,16,485,77),(517,71,551,89),(676,66,695,120),(699,64,717,119),
 (1085,2,1109,66),(636,2,651,25)]
for box in small_signs:
    x0,y0,x1,y1=box
    crop=im.crop(box)
    # A plain material sampled at the sign boundary; no typography is synthesized.
    arr=np.asarray(crop)
    color=tuple(np.median(arr.reshape(-1,3),axis=0).astype('uint8'))
    ImageDraw.Draw(im).rectangle((x0,y0,x1-1,y1-1),fill=color)
log.append({'kind':'plain_sign_inpaint','regions':small_signs})

out=ROOT/'_preprocess'
out.mkdir(exist_ok=True)
im.resize(SIZE,Image.Resampling.BOX).save(out/'reference_prepared.png')
original.resize(SIZE,Image.Resampling.BOX).save(out/'reference_scaled.png')
(ROOT/'preprocess-log.json').write_text(json.dumps({'sourceSHA256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'canvas':SIZE,'operations':log},indent=2)+'\n')
print('Preprocessed reference saved:',SIZE,len(log),'logged operations')
