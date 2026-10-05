// Catalog reads runtime declarations and consumers, never a duplicate palette.
export function readTokens() {
  const definitions=new Map(), consumers=new Map();
  function walk(rules,href) {
    for(const rule of rules) {
      if(rule.styleSheet){walk(rule.styleSheet.cssRules,rule.styleSheet.href);continue;}
      if(rule.cssRules&&!rule.style){walk(rule.cssRules,href);continue;}
      if(!rule.style)continue;
      const text=rule.style.cssText;
      for(const match of text.matchAll(/var\((--[\w-]+)/g)){
        if(!consumers.has(match[1]))consumers.set(match[1],new Set());
        consumers.get(match[1]).add((href?.split('/').at(-1)??'inline')+' · '+rule.selectorText);
      }
      if(!href?.endsWith('/styles/tokens.css') || (rule.selectorText.includes('dark')&&document.documentElement.dataset.theme!=='dark'))continue;
      for(const name of rule.style)if(name.startsWith('--'))definitions.set(name,rule.style.getPropertyValue(name).trim());
    }
  }
  for(const sheet of document.styleSheets)walk(sheet.cssRules,sheet.href);
  const computed=getComputedStyle(document.documentElement),canvas=document.createElement('canvas');canvas.width=canvas.height=1;
  const context=canvas.getContext('2d',{willReadFrequently:true});
  function pixel(color){context.clearRect(0,0,1,1);context.fillStyle=computed.getPropertyValue('--studio-paper');context.fillRect(0,0,1,1);context.fillStyle=color;context.fillRect(0,0,1,1);return [...context.getImageData(0,0,1,1).data].slice(0,3);}
  const luminance=rgb=>rgb.map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);
  const paper=luminance(pixel(computed.getPropertyValue('--studio-paper')));
  return [...definitions].map(([name,raw])=>{
    const value=computed.getPropertyValue(name).trim(),isColor=CSS.supports('color',value);
    const lumin=isColor?luminance(pixel(value)):0;
    return {name,value,raw,isColor,contrast:isColor?((Math.max(lumin,paper)+.05)/(Math.min(lumin,paper)+.05)).toFixed(2):'',uses:[...(consumers.get(name)??[])].join('\n')||'Foundation / tham chiếu trong catalog'};
  });
}
