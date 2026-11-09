"""Read-only measurements of MCP Aseprite exports; no game artwork is authored here."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import numpy as np
import json,hashlib
ROOT=Path(__file__).resolve().parent
reference=np.asarray(Image.open(ROOT/'_preprocess/reference_scaled.png').convert('RGB')).astype('float64')
result=np.asarray(Image.open(ROOT/'world_preview.png').convert('RGB')).astype('float64')
assert reference.shape==result.shape==(576,1024,3)
manifest=json.loads((ROOT/'world-manifest.json').read_text())

def gaussian(a):
    kernel=np.exp(-(np.arange(-5,6,dtype='float64')**2)/(2*1.5**2));kernel/=kernel.sum()
    p=np.pad(a,((5,5),(0,0)),mode='reflect')
    q=sum(k*p[i:i+a.shape[0],:] for i,k in enumerate(kernel))
    p=np.pad(q,((0,0),(5,5)),mode='reflect')
    return sum(k*p[:,i:i+a.shape[1]] for i,k in enumerate(kernel))

def luma(a):return a[:,:,0]*.2126+a[:,:,1]*.7152+a[:,:,2]*.0722
x,y=luma(reference),luma(result)
ux,uy=gaussian(x),gaussian(y)
vx=np.maximum(0,gaussian(x*x)-ux*ux);vy=np.maximum(0,gaussian(y*y)-uy*uy)
vxy=gaussian(x*y)-ux*uy
c1=(.01*255)**2;c2=(.03*255)**2
ssim=((2*ux*uy+c1)*(2*vxy+c2))/((ux*ux+uy*uy+c1)*(vx+vy+c2))
valid=np.ones(x.shape,bool);valid[:5]=False;valid[-5:]=False;valid[:,:5]=False;valid[:,-5:]=False
replacement=Image.new('L',(1024,576),0)
draw=ImageDraw.Draw(replacement)
for points in manifest['replacementRegions'].values():draw.polygon([tuple(p) for p in points],fill=255)
replacement.save(ROOT/'_checks/replacement-mask.png')
exclude=np.asarray(replacement)>0
exclude_dilated=np.asarray(replacement.filter(ImageFilter.MaxFilter(11)))>0

def metrics(mask,ssim_mask):
    d=(reference-result)[mask]
    mse=float(np.mean(d*d))
    return {'SSIM_luminance':float(ssim[ssim_mask].mean()),'MSE_RGB_0_255':mse,'RMSE_RGB_0_255':mse**.5,'normalized_MSE':mse/(255**2),'pixels':int(mask.sum()),'SSIM_window_centers':int(ssim_mask.sum())}

patches={
 'Cyber Academy emblem':[477,51,44,30],
 'Player House gable':[195,126,40,35],
 'Internet Cafe emblem':[129,254,42,35],
 'Linux Lab penguin':[733,92,30,45],
 'Web Security Lab shield':[756,263,30,36],
 'Cyber Corp emblem':[477,373,45,35],
 'Plaza globe':[460,192,65,67],
 'West bridge arch':[163,436,42,33],
 'Southeast bridge rail':[724,408,55,34]
}
registration=[]
for name,(px,py,w,h) in patches.items():
    target=reference[py:py+h,px:px+w]
    best=(float('inf'),0,0)
    for dy in range(-32,33):
        for dx in range(-32,33):
            xx,yy=px+dx,py+dy
            if xx<0 or yy<0 or xx+w>1024 or yy+h>576:continue
            mse=float(np.mean((target-result[yy:yy+h,xx:xx+w])**2))
            if mse<best[0]:best=(mse,dx,dy)
    mse,dx,dy=best
    registration.append({'landmark':name,'referencePatchXYWH':patches[name],'bestShiftPixels':[dx,dy],'distanceTiles':((dx*dx+dy*dy)**.5)/32,'MSE':mse})

palette=json.loads((ROOT/'palette.json').read_text())['colors']
expected={tuple(bytes.fromhex(c[1:])) for c in palette}
colors={tuple(c) for c in result.astype('uint8').reshape(-1,3)}
brand=['#0B1026','#1F2A6B','#00E5FF','#00D4B7','#8B5CF6','#FF2D9B','#FFB347','#FFF4D6']
layers={}
for p in sorted((ROOT/'_checks/layers').glob('*.png')):
    arr=np.asarray(Image.open(p).convert('RGBA'))
    visible=arr[:,:,3]>0
    ys,xs=np.where(visible)
    layers[p.stem]={'opaquePixels':int(visible.sum()),'alphaValues':np.unique(arr[:,:,3]).tolist(),'boundsXYXYExclusive':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)] if len(xs) else None}
ground=np.asarray(Image.open(ROOT/'_checks/layers/ground.png').convert('RGBA'))
unique_tiles=len({ground[y:y+32,x:x+32].tobytes() for y in range(0,576,32) for x in range(0,1024,32)})
stats={
 'method':'Luminance SSIM, Rec.709 weights, 11x11 Gaussian sigma=1.5, population variance, K1=.01 K2=.03, data_range=255. RGB MSE over unfiltered equal-size images. 5 px SSIM border excluded.',
 'baseline':'Preserved reference JPEG decoded and resized with BOX to 1024x576, including its original HUD/text. The initial extract_palette call re-encoded the supplied JPEG before this snapshot.',
 'whole_map':metrics(np.ones(x.shape,bool),valid),
 'excluding_two_replacement_regions':metrics(~exclude,valid&~exclude_dilated),
 'intentional_replacement_pixel_fraction':float(exclude.mean()),
 'landmark_registration':registration,
 'landmark_caveat':'Translation-only local RGB MSE registration of nine visible landmarks, search +/-32 px. This does not validate whole silhouettes, collisions or navigation.',
 'palette':{'count':len(colors),'maximum':48,'offPaletteColors':len(colors-expected),'brandColorsPresent':{c:tuple(bytes.fromhex(c[1:])) in colors for c in brand}},
 'layers':layers,
 'groundDistinct32pxCellsBeforeTilesetOptimization':unique_tiles,
 'groundCellCount':576,
 'actualExportedTilesetCount':0,
 'runtimeVerification':'Not run: user approval gate after stage 2',
 'previewSHA256':hashlib.sha256((ROOT/'world_preview.png').read_bytes()).hexdigest()
}
assert stats['palette']['count']<=48 and not stats['palette']['offPaletteColors']
assert all(stats['palette']['brandColorsPresent'].values())
assert all(v['opaquePixels']>0 and set(v['alphaValues'])<={0,255} for v in layers.values())
assert all(v['distanceTiles']<=1 for v in registration)
assert layers['ground']['opaquePixels']==1024*576
(ROOT/'similarity-metrics.json').write_text(json.dumps(stats,indent=2)+'\n')
print(json.dumps({k:stats[k] for k in ['whole_map','excluding_two_replacement_regions','intentional_replacement_pixel_fraction','palette','groundDistinct32pxCellsBeforeTilesetOptimization','landmark_registration']},indent=2))
