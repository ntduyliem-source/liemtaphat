import { render } from '../renderer.js';
const ns='http://www.w3.org/2000/svg';
let fontPromise;
const node=(name,attrs={})=>{const element=document.createElementNS(ns,name);for(const [key,value] of Object.entries(attrs))element.setAttribute(key,String(value));return element;};
const n=value=>Math.round(value*1000)/1000;
async function font(){
  if(!fontPromise)fontPromise=fetch(new URL('./inter-outline.json',import.meta.url)).then(r=>{if(!r.ok)throw new Error('FONT_LOAD');return r.json();}).catch(e=>{fontPromise=null;throw e;});
  return fontPromise;
}
// Runs and geometry come from an immutable application projection, never from preview innerText.
export async function compose(parts, options, preview){
  const size=options.fontSize, padding=10;
  if(!Array.isArray(parts)||parts.length===0||!Number.isFinite(size)||size<8||size>120)throw new Error('Invalid image request.');
  if(parts.length===1&&parts[0].mathMl)return render(parts[0].mathMl,parts[0].candidateId,{...options,pixelScale:1});
  const face=await font(), scale=size/face.units;
  const requested=typeof preview==='number'?preview:preview?.clientWidth;
  const wrap=Math.max(160,Math.min(2400,Number.isFinite(requested)?requested-32:680));
  const svg=node('svg',{xmlns:ns,'data-renderer':'locus-result-image/1','data-background':options.whiteBackground?'white':'transparent','data-font-size':size});
  const defs=node('defs');svg.append(defs);
  const paths=new Map(), bitmaps=new Map(), math=new Map();
  const segmenter=new Intl.Segmenter('vi',{granularity:'grapheme'});
  let bitmapCount=0,glyphCount=0,processed=0;
  function raster(grapheme){
    if(bitmaps.has(grapheme))return bitmaps.get(grapheme);
    // Non-Latin scripts and emoji keep browser shaping. Embed the rendered glyph pixels so
    // the SVG recipient never needs the generator's emoji/system fonts.
    const canvas=document.createElement('canvas'),ctx=canvas.getContext('2d'),ratio=4;
    const style=`${size*ratio}px "Inter Variable", "Segoe UI Emoji", "Apple Color Emoji", sans-serif`;
    ctx.font=style;const m=ctx.measureText(grapheme);
    const left=Math.max(0,m.actualBoundingBoxLeft),right=Math.max(m.width,m.actualBoundingBoxRight);
    const ascent=Math.max(size*ratio*.8,m.actualBoundingBoxAscent),descent=Math.max(size*ratio*.2,m.actualBoundingBoxDescent);
    canvas.width=Math.ceil(left+right+8);canvas.height=Math.ceil(ascent+descent+8);
    ctx.font=style;ctx.fillStyle='#000';ctx.textBaseline='alphabetic';ctx.fillText(grapheme,left+4,ascent+4);
    const id=`text-raster-${bitmapCount++}`;defs.append(node('image',{id,width:canvas.width/ratio,height:canvas.height/ratio,href:canvas.toDataURL('image/png')}));
    const item={width:m.width/ratio,ascent:(ascent+4)/ratio,descent:(descent+4)/ratio,make:()=>node('use',{href:'#'+id,x:-(left+4)/ratio,y:-(ascent+4)/ratio})};
    bitmaps.set(grapheme,item);return item;
  }
  function textItem(grapheme){
    const normalized=grapheme.normalize('NFC'),chars=[...normalized];
    if(chars.length!==1||!face.glyphs[chars[0].codePointAt(0)])return raster(grapheme);
    const code=chars[0].codePointAt(0),glyph=face.glyphs[code];
    if(!paths.has(code)){const id=`text-glyph-${glyphCount++}`;paths.set(code,id);defs.append(node('path',{id,d:glyph.d}));}
    return {width:glyph.advance*scale,ascent:face.ascent*scale,descent:face.descent*scale,make:()=>node('use',{href:'#'+paths.get(code),transform:`scale(${scale} ${-scale})`,fill:'#000'})};
  }
  const lines=[];let line=[],width=0;
  function finish(){lines.push({items:line,width,ascent:Math.max(size*.85,...line.map(i=>i.ascent)),descent:Math.max(size*.25,...line.map(i=>i.descent))});line=[];width=0;}
  function put(item){line.push({...item,x:width});width+=item.width;}
  for(const part of parts){
    if(part.mathMl){
      let item=math.get(part.candidateId);
      if(!item){const scene=await render(part.mathMl,part.candidateId,{...options,whiteBackground:false,pixelScale:1,padding:0});
        const image=new DOMParser().parseFromString(scene.svg,'image/svg+xml').documentElement;
        item={width:scene.width,ascent:scene.ascent,descent:scene.descent,make:()=>{const clone=image.cloneNode(true);clone.setAttribute('y',String(-scene.ascent));return clone;}};math.set(part.candidateId,item);}
      if(width>0&&width+item.width>wrap)finish();put(item);
    }else{
      const tokens=String(part.text??'').match(/\r\n|\r|\n|[^\S\r\n]+|[^\s]+/gu)??[];
      for(const token of tokens){
        if(token==='\n'||token==='\r'||token==='\r\n'){finish();continue;}
        const items=[...segmenter.segment(token.replaceAll('\t','    '))].map(s=>textItem(s.segment));
        const total=items.reduce((sum,i)=>sum+i.width,0);
        if(width>0&&!/^\s/u.test(token)&&width+total>wrap)finish();
        for(const item of items){if(width>0&&width+item.width>wrap)finish();put(item);}
      }
    }
    if(++processed%16===0)await new Promise(resolve=>setTimeout(resolve,0));
  }
  finish();let y=padding,maxWidth=0;
  for(const row of lines){y+=row.ascent;maxWidth=Math.max(maxWidth,row.width);
    for(const item of row.items){const group=node('g',{transform:`translate(${n(padding+item.x)} ${n(y)})`});group.append(item.make());svg.append(group);}
    y+=row.descent+size*.3;
  }
  const resultWidth=Math.ceil(maxWidth+padding*2),height=Math.ceil(y-size*.3+padding);
  if(!Number.isFinite(height)||height>2000000)throw new Error('SVG_LIMIT: Chọn một đoạn ngắn hơn để xuất ảnh.');
  svg.setAttribute('width',resultWidth);svg.setAttribute('height',height);svg.setAttribute('viewBox',`0 0 ${resultWidth} ${height}`);
  if(options.whiteBackground)svg.insertBefore(node('rect',{width:resultWidth,height,fill:'white'}),defs);
  return {svg:new XMLSerializer().serializeToString(svg),width:resultWidth,height,lines:lines.length,rasterGlyphs:bitmapCount};
}
