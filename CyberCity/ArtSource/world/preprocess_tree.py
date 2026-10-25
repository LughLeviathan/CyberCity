"""Clip an existing reference tree for Aseprite import; no final sprite is exported here."""
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parent
im=Image.open(ROOT/'reference/cybercity_reference.jpeg').convert('RGBA')
mask=Image.new('L',im.size,0)
points=[(563,337),(571,329),(582,334),(589,343),(601,351),(599,362),(608,375),(605,389),(589,399),(581,406),(588,416),(583,429),(563,430),(551,421),(558,405),(555,394),(544,386),(545,375),(553,364),(547,358),(553,347)]
ImageDraw.Draw(mask).polygon(points,fill=255)
im.putalpha(mask)
im.crop((540,327,611,433)).resize((45,68),Image.Resampling.NEAREST).save(ROOT/'_preprocess/tree_clip.png')
