"""Rebuild the standalone SVG glyph resource from the checked-in OFL Inter subsets.
Requires fonttools[woff] 4.63.0; no runtime font/CDN dependency in an exported SVG.
"""
from pathlib import Path
from hashlib import sha256
import json
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen

root = Path(__file__).resolve().parents[2]
folder = root / 'src/Locus.DesignSystem/wwwroot/fonts'
glyphs = {}
inputs = {}
for subset in ['latin', 'latin-ext', 'vietnamese']:
    path = folder / f'inter-{subset}-wght-normal.woff2'
    inputs[path.name] = sha256(path.read_bytes()).hexdigest()
    font = instantiateVariableFont(TTFont(path), {'wght': 400})
    outlines = font.getGlyphSet()
    for code, name in font.getBestCmap().items():
        pen = SVGPathPen(outlines, ntos=lambda v: str(round(v, 3)))
        outlines[name].draw(pen)
        glyphs[str(code)] = {'d': pen.getCommands(), 'advance': outlines[name].width}
    units = font['head'].unitsPerEm
    ascent = font['hhea'].ascent
    descent = -font['hhea'].descent
target = root / 'src/Locus.Editor/wwwroot/formula/inter-outline.json'
target.write_text(json.dumps({'version': 'locus-inter-outline/1', 'license': 'OFL-1.1', 'sources': inputs,
    'units': units, 'ascent': ascent, 'descent': descent, 'glyphs': glyphs}, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
print(f'{len(glyphs)} glyphs; {target.stat().st_size} bytes')
