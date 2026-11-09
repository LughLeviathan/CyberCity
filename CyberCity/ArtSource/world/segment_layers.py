"""Reference-derived semantic cutouts for import into MCP Aseprite.

These are preprocessing intermediates, not engine exports. Preserve exact visible pixel
positions, use binary masks, and reconstruct hidden ground from a nearby paving sample.
Final edits and exports are made in world.aseprite through MCP.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'_preprocess/layers'
OUT.mkdir(parents=True,exist_ok=True)
im=Image.open(ROOT/'ref_clean.png').convert('RGBA')
W,H=im.size
a=np.asarray(im).copy()

buildings={
 'academy': [(331,63),(357,53),(357,30),(368,24),(368,13),(385,22),(386,36),(404,43),(414,44),(414,21),(424,9),(453,13),(464,35),(497,34),(508,39),(534,38),(537,8),(566,10),(568,47),(591,51),(614,65),(613,145),(594,160),(577,167),(559,160),(442,160),(430,171),(413,168),(390,164),(374,153),(332,142)],
 'player_house': [(88,139),(88,125),(111,116),(114,111),(166,115),(178,95),(198,118),(221,126),(236,137),(266,143),(275,159),(282,165),(282,225),(253,229),(242,231),(156,236),(132,222),(111,213),(94,202)],
 'internet_cafe': [(34,299),(45,281),(62,273),(122,269),(130,256),(160,253),(178,250),(224,250),(257,268),(283,286),(283,339),(266,351),(247,361),(219,366),(131,381),(91,382),(58,373),(37,355)],
 'linux_lab': [(640,127),(652,114),(670,107),(689,106),(690,90),(707,85),(722,95),(736,94),(755,99),(771,95),(801,100),(816,109),(820,126),(847,134),(858,141),(855,211),(824,223),(802,221),(762,215),(718,208),(670,202),(645,190)],
 'web_lab': [(726,277),(734,260),(762,248),(795,254),(805,250),(832,256),(856,260),(874,270),(887,281),(919,283),(925,279),(940,283),(954,293),(956,352),(941,369),(916,376),(883,368),(845,358),(820,348),(785,346),(749,333),(728,315)],
 'cyber_corp': [(309,402),(322,396),(356,402),(356,367),(370,357),(397,358),(397,348),(407,343),(428,347),(435,360),(453,359),(473,368),(504,365),(531,370),(561,370),(563,355),(579,353),(598,358),(603,370),(622,370),(631,378),(658,378),(669,388),(671,463),(661,476),(637,483),(601,485),(574,481),(556,480),(431,480),(403,484),(365,476),(348,468),(316,457)]
}

def polygon(points):
    m=Image.new('L',(W,H),0);ImageDraw.Draw(m).polygon(points,fill=255)
    return np.asarray(m)>0

bm={k:polygon(v) for k,v in buildings.items()}
building_union=np.logical_or.reduce(list(bm.values()))
water=polygon([(0,229),(25,242),(62,252),(63,278),(0,324)])
water |= polygon([(0,483),(69,477),(125,464),(155,444),(207,428),(253,422),(276,439),(255,462),(232,487),(211,518),(184,576),(0,576)])
water |= polygon([(186,576),(219,536),(285,515),(302,510),(704,509),(727,487),(714,455),(718,429),(786,450),(828,474),(836,502),(865,530),(1024,532),(1024,576)])
water |= polygon([(878,241),(904,236),(942,247),(974,260),(1024,275),(1024,288),(957,267),(910,259),(877,254)])
water &= ~building_union
seawall=polygon([(300,503),(717,503),(708,536),(667,548),(353,548),(303,533)])
water &= ~seawall

scenery=polygon([(0,0),(1023,0),(1023,111),(889,108),(869,96),(812,90),(695,93),(633,96),(621,81),(302,75),(296,112),(0,111)])
scenery |= polygon([(893,87),(1023,96),(1023,225),(911,220),(863,191),(856,152)])
scenery |= polygon([(899,375),(1023,363),(1023,532),(913,528),(846,494),(836,442)])
scenery |= seawall
scenery &= ~building_union & ~water

# Foreground props: globe/pedestal, sign plinth, planted beds, banners, lampposts and benches.
prop_polygons=[
 [(481,188),(509,189),(531,209),(539,238),(532,259),(518,272),(469,273),(451,258),(445,235),(451,211),(465,194)],
 [(432,275),(550,275),(556,299),(433,300)],
 [(351,214),(365,207),(381,222),(386,247),(374,260),(372,275),(352,275),(345,256),(342,238)],
 [(434,188),(450,182),(461,196),(460,217),(441,222),(430,211)],
 [(543,187),(558,185),(569,201),(565,223),(549,226),(541,213)],
 [(601,216),(620,221),(628,240),(620,262),(605,265),(595,249)],
 [(704,219),(724,227),(725,248),(717,267),(697,267),(694,243)],
 [(597,310),(616,317),(627,343),(618,362),(603,361),(596,341)],
 [(280,310),(299,301),(309,318),(309,342),(296,355),(278,348)],
 [(87,374),(115,374),(116,391),(88,394)],
 [(142,375),(190,365),(201,379),(154,390)],
 [(219,364),(262,352),(270,367),(228,380)],
 [(767,359),(789,353),(809,367),(804,386),(770,373)],
 [(846,376),(870,376),(897,389),(889,402),(851,394)],
 [(355,481),(383,482),(384,498),(354,498)],
 [(574,479),(586,476),(591,499),(575,500)],
 [(397,474),(410,476),(410,500),(395,500)],
 [(562,201),(580,206),(581,247),(562,241)]
]
props=np.zeros((H,W),bool)
for p in prop_polygons: props |= polygon(p)
# Pixel-wide lamps are outlined tightly, preserving adjacent walkable paving.
lamps=[(round(x*.64),round(y*.64)) for x,y in [(95,236),(112,379),(208,378),(442,184),(496,243),(515,200),(600,262),(669,306),(615,326),(544,398),(575,440),(651,397),(835,306),(934,328),(942,264),(1010,392),(897,399),(978,436),(1097,383),(1242,320),(1097,516),(1124,617),(1325,613),(1046,739),(467,780),(527,600),(427,618),(239,648),(153,664),(51,684),(475,555),(450,522),(475,377)]]
for x,y in lamps:
    props |= polygon([(x-3,y-6),(x+3,y-6),(x+5,y-2),(x+3,y+5),(x+2,y+29),(x+5,y+31),(x+4,y+33),(x-5,y+33),(x-5,y+31),(x-2,y+29),(x-2,y+5),(x-5,y-2)])
props &= ~building_union & ~water & ~scenery

# Separate vivid emissive pixels on buildings. Tintable backing remains beneath every glow pixel.
rgb=a[:,:,:3].astype('int16')
r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
emission=((g>145)&(b>170))|((r>210)&(g>135)&(b<160))|((r>190)&(b>120)&(r>g*1.3))|((r>220)&(g>210)&(b>180))
glow=emission & building_union

def save_masked(name,mask,base=None):
    arr=a.copy() if base is None else base.copy()
    arr[:,:,3]=np.where(mask,255,0)
    Image.fromarray(arr).save(OUT/(name+'.png'))
    return int(mask.sum())

counts={}
occupied=building_union|water|scenery|props
# Hidden underpainting is a repeated, nearby source pavement sample, never empty alpha.
# It is a scaffold for stage-2 Aseprite cleanup, and is invisible under the extracted art.
patch=np.asarray(im.crop((487,345,503,353)))
yy,xx=np.indices((H,W))
under=patch[yy%patch.shape[0],xx%patch.shape[1]].copy()
under[:,:,3]=255
ground=a.copy();ground[occupied]=under[occupied]
Image.fromarray(ground).save(OUT/'ground.png');counts['ground']=W*H
counts['water']=save_masked('water',water)
counts['scenery_edge']=save_masked('scenery_edge',scenery)
for name,mask in bm.items():
    backing=a.copy()
    # Neutral dark backing beneath light pixels; the glow overlay restores their original exact color.
    backing[glow & mask,:3]=[24,41,88]
    counts['buildings_'+name]=save_masked('buildings_'+name,mask,backing)
counts['props']=save_masked('props',props)
counts['glow']=save_masked('glow',glow)

doors={'academy':[498,155],'player_house':[219,225],'internet_cafe':[214,358],'linux_lab':[739,207],'web_lab':[824,358],'cyber_corp':[499,483]}
names={'academy':'Cyber Academy','player_house':'Player House','internet_cafe':'Internet Cafe','linux_lab':'Linux Lab','web_lab':'Web Security Lab','cyber_corp':'Cyber Corp'}
unlock={'academy':0,'internet_cafe':1,'linux_lab':4,'web_lab':5}
manifest={'canvas':[W,H],'tileSize':32,'pixelsPerUnit':32,'palette':'palette.json','source':'world.aseprite','approvalStatus':'stage2-review-pending','runtimeIntegrated':False,'pixelCounts':counts,'buildings':[]}
for key,mask in bm.items():
    ys,xs=np.where(mask)
    bounds=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]
    manifest['buildings'].append({'id':key,'name':names[key],'layer':'buildings_'+key,'role':'mission' if key in unlock else 'decoration','boundsXYXYExclusive':bounds,'outline':buildings[key],'doorPixel':doors[key],'pivotPixel':[round((bounds[0]+bounds[2])/2),bounds[3]],'unlockAfterCompleted':unlock.get(key),'collision':'footprint guide only; integrate after approval'})
manifest['replacementRegions']={'northeast':[[880,93],[1023,93],[1023,242],[884,216],[863,189]],'southeast':[[858,426],[902,400],[1023,377],[1023,538],[906,533],[846,497],[840,449]]}
(ROOT/'world-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(counts))

# Validate that semantic preprocessing changes no visible pixel before manual Aseprite refinement.
composite=Image.open(OUT/'ground.png').convert('RGBA')
for name in ['water','scenery_edge']+['buildings_'+x for x in buildings]+['props','glow']:
    composite.alpha_composite(Image.open(OUT/(name+'.png')).convert('RGBA'))
delta=np.abs(np.asarray(composite).astype('int16')-a)
print('Reconstructed composite mismatched pixels:',int(np.any(delta,axis=2).sum()))
assert not delta.any(), 'Semantic masks overlap or leave visible gaps'
