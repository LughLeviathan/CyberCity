"""Verify saved Aseprite exports without altering artwork."""
from pathlib import Path
from PIL import Image
import numpy as np
import json,hashlib,struct
ROOT=Path(__file__).resolve().parent
order=['ground','water','scenery_edge','buildings_academy','buildings_player_house','buildings_internet_cafe','buildings_linux_lab','buildings_web_lab','buildings_cyber_corp','props','glow']
composite=Image.new('RGBA',(1024,576))
for name in order:composite.alpha_composite(Image.open(ROOT/'_checks/layers'/f'{name}.png').convert('RGBA'))
preview=Image.open(ROOT/'world_preview.png').convert('RGBA')
diff=np.asarray(composite)!=np.asarray(preview)
assert not diff.any(),'Saved layer exports do not reconstruct the final preview'
source=(ROOT/'world.aseprite').read_bytes()
assert struct.unpack_from('<H',source,4)[0]==0xA5E0
frames,width,height,depth=struct.unpack_from('<HHHH',source,6)
assert (frames,width,height,depth)==(1,1024,576,32)
assert not (ROOT/'_checks/layers/guide_collision.png').exists()
assert not (ROOT/'_checks/layers/reference_trace.png').exists()
metrics=json.loads((ROOT/'similarity-metrics.json').read_text())
assert metrics['palette']['count']==47 and metrics['palette']['offPaletteColors']==0
assert all(metrics['palette']['brandColorsPresent'].values())
files=['world.aseprite','ref_clean.aseprite','ref_clean.png','world_preview.png','tree.aseprite','comparison.aseprite','palette.json','world-manifest.json','collision-guide.json','similarity-metrics.json']
checks={name:{'bytes':(ROOT/name).stat().st_size,'sha256':hashlib.sha256((ROOT/name).read_bytes()).hexdigest()} for name in files}
result={'status':'PASS (stage 2 artifact checks only)','canvas':[width,height],'frames':frames,'visibleLayerCount':len(order),'visibleLayers':order,'hiddenExportExclusions':['reference_trace','guide_collision'],'compositeMismatchPixels':0,'paletteColors':47,'brandColorsPresent':8,'files':checks,'unityVerification':'Deferred: user approval required before stage 3','visualLimitations':['Reference-derived quantized texture remains denser than fully hand-clustered pixel art.','Some foliage/lamps share building foundation silhouettes; final sprite/footprint separation is to be refined during stage 3 slicing.','Ground lint has 0 detached/lone alpha pixels but reports 15534 isolated-color pixels, including repeated hidden underpainting and intentional detail; this is not a zero-warning pixel-art lint pass.']}
(ROOT/'stage2-validation.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k!='files'},indent=2))
