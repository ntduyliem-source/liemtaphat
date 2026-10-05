let startup;
export async function initialize(){
  if(startup)return startup;
  const base=new URL('./vendor/',import.meta.url).href;
  window.MathJax={
    loader:{paths:{mathjax:base+'mathjax','mathjax-newcm':base+'mathjax-newcm-font'},load:['[mathjax-newcm]/svg']},
    output:{font:'mathjax-newcm'},svg:{fontCache:'none'},
    options:{enableMenu:false},startup:{typeset:false}
  };
  startup=new Promise((resolve,reject)=>{const script=document.createElement('script');script.src=base+'mathjax/mml-svg-nofont.js';script.onload=()=>MathJax.startup.promise.then(resolve,reject);script.onerror=()=>reject(new Error('Cannot load local formula renderer.'));document.head.append(script);});
  return startup;
}
export async function render(mathml,candidateId,options){
  await initialize();
  const container=await MathJax.mathml2svgPromise(mathml,{display:true});
  const source=container.querySelector('svg');
  if(!source||source.querySelector('[data-mml-node="merror"]'))throw new Error('Formula rendering failed.');
  const box=source.viewBox.baseVal,fontSize=options.fontSize,pad=options.padding??10;
  const width=box.width/1000*fontSize+2*pad,height=box.height/1000*fontSize+2*pad;
  if(!Number.isFinite(width)||!Number.isFinite(height)||width>12000||height>12000||width*height*options.pixelScale**2>24000000)throw new Error('Image dimensions exceed supported limits.');
  // All glyph outlines are in this SVG, without a font or cross-document ID dependency.
  const svg=source.cloneNode(true);svg.setAttribute('xmlns','http://www.w3.org/2000/svg');
  const padding=pad/fontSize*1000;
  svg.setAttribute('viewBox',`${box.x-padding} ${box.y-padding} ${box.width+2*padding} ${box.height+2*padding}`);
  svg.setAttribute('width',String(width));svg.setAttribute('height',String(height));svg.removeAttribute('style');
  svg.setAttribute('data-candidate-id',candidateId);svg.setAttribute('data-renderer','locus-mathjax/4.1.3');svg.setAttribute('color','#000');
  svg.setAttribute('data-font-size',String(fontSize));svg.setAttribute('data-pixel-scale',String(options.pixelScale));svg.setAttribute('data-background',options.whiteBackground?'white':'transparent');
  svg.querySelectorAll('[fill="currentColor"]').forEach(el=>el.setAttribute('fill','#000'));
  svg.querySelectorAll('[stroke="currentColor"]').forEach(el=>el.setAttribute('stroke','#000'));
  if(options.whiteBackground){const rect=document.createElementNS(svg.namespaceURI,'rect');rect.setAttribute('x',String(box.x-padding));rect.setAttribute('y',String(box.y-padding));rect.setAttribute('width',String(box.width+2*padding));rect.setAttribute('height',String(box.height+2*padding));rect.setAttribute('fill','white');svg.prepend(rect);}
  return {svg:new XMLSerializer().serializeToString(svg),width,height,ascent:pad-box.y/1000*fontSize,descent:pad+(box.y+box.height)/1000*fontSize,candidateId};
}
export async function png(svg,scale){
  const scene=new DOMParser().parseFromString(svg,'image/svg+xml').documentElement;
  const width=Number(scene.getAttribute('width'))*scale,height=Number(scene.getAttribute('height'))*scale;
  if(!Number.isFinite(width)||!Number.isFinite(height)||width<=0||height<=0||!Number.isFinite(scale)||scale<1||scale>4)throw new Error('Invalid PNG dimensions.');
  if(Math.ceil(width)*Math.ceil(height)>24000000||width>32767||height>32767)throw new Error('PNG_LIMIT: Ảnh vượt giới hạn 24 triệu pixel hoặc 32.767 px mỗi cạnh. Chọn SVG hoặc một đoạn ngắn hơn.');
  const url=URL.createObjectURL(new Blob([svg],{type:'image/svg+xml'}));
  try{
    const image=new Image();image.src=url;await image.decode();
    const canvas=document.createElement('canvas');canvas.width=Math.ceil(width);canvas.height=Math.ceil(height);
    const context=canvas.getContext('2d');
    // Fill the entire pixel surface; a fractional SVG edge alone can leave translucent corner pixels.
    if(scene.getAttribute('data-background')==='white'){context.fillStyle='white';context.fillRect(0,0,canvas.width,canvas.height);}
    context.drawImage(image,0,0,width,height);
    const blob=await new Promise(resolve=>canvas.toBlob(resolve,'image/png'));
    if(!blob)throw new Error('PNG encoding failed.');
    return new Uint8Array(await blob.arrayBuffer());
  }finally{URL.revokeObjectURL(url);}
}
