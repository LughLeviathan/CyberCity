"""Write runtime metadata only. Final raster assets are exported by MCP Aseprite."""
from pathlib import Path
import json
root = Path(__file__).resolve().parent
plan = json.loads((root/'export/export-plan.json').read_text())
guide = json.loads((root/'collision-guide.json').read_text())
labels = {'academy':[498,101,121,25], 'player_house':[224,183,118,29],
          'internet_cafe':[181,307,102,31], 'linux_lab':[750,153,102,30],
          'web_lab':[848,295,132,30], 'cyber_corp':[498,420,125,30]}
for s in plan['sprites']:
    if s['kind']=='building': s['label']=labels[s['id']]
plan['uniqueTileCount'] = len(plan.pop('uniqueTiles'))
plan['colliders'] = [{'id':s['id'],'points':[{'x':x,'y':y} for x,y in s['points']]} for s in guide['shapes']]
# Actual northern boundary is a continuous background skyline; gardens are scenery only.
extra = {
 'north_edge':[(0,0),(1024,0),(1024,88),(861,88),(861,78),(625,78),(625,64),(300,64),(300,99),(0,99)],
 'west_water':[(0,230),(25,243),(62,252),(62,277),(0,323)],
 'northeast_canal':[(878,241),(904,236),(942,247),(974,260),(1024,275),(1024,288),(957,267),(910,259),(877,254)],
 'seawall':[(300,505),(718,505),(709,537),(667,550),(352,550),(302,534)],
 'globe_plinth':[(455,249),(530,249),(530,269),(455,269)],
 'plaza_sign':[(433,285),(554,285),(554,300),(433,300)]
}
plan['colliders'] += [{'id':key,'points':[{'x':x,'y':y} for x,y in pts]} for key,pts in extra.items()]
target = root.parents[1]/'Assets/CyberCity/WorldArt/world.json'
target.write_text(json.dumps(plan,indent=2)+'\n')
print(target, plan['uniqueTileCount'], 'tiles',len(plan['sprites']),'sprites',len(plan['colliders']),'footprints')
