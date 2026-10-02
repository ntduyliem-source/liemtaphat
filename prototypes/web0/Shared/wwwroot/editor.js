let cleanup=()=>{}, board;
export function wire(element,reference,space){
  cleanup();const abort=new AbortController(), options={signal:abort.signal};
  const observer=new MutationObserver(()=>{if(!element.isConnected)stop();});
  const stop=()=>{abort.abort();observer.disconnect();if(space==='canvas'&&board){JXG.JSXGraph.freeBoard(board);board=null;}};
  observer.observe(document.body,{childList:true,subtree:true});cleanup=stop;
  if(space==='formula'){
    let composing=false,chain=Promise.resolve();
    const send=()=>{const value=element.value,active=composing;chain=chain.then(()=>reference.invokeMethodAsync('SourceInput',value,active)).catch(console.error);};
    element.addEventListener('compositionstart',()=>{composing=true;send();},options);
    element.addEventListener('compositionend',()=>{composing=false;send();},options);
    element.addEventListener('input',send,options);return;
  }
  let drag=null;
  const draw=points=>{
    element.querySelector('polygon').setAttribute('points',points.map(p=>`${p.x},${p.y}`).join(' '));
    for(const p of points){const g=element.querySelector(`[data-point="${p.id}"]`);g.querySelector('circle').setAttribute('cx',p.x);g.querySelector('circle').setAttribute('cy',p.y);g.querySelector('text').setAttribute('x',p.x+14);g.querySelector('text').setAttribute('y',p.y-12);}
  };
  const update=e=>{const xy=new DOMPoint(e.clientX,e.clientY).matrixTransform(element.getScreenCTM().inverse());drag.current=drag.before.map(p=>p.id===drag.id?{...p,x:Math.min(540,Math.max(20,xy.x)),y:Math.min(320,Math.max(20,xy.y))}:p);draw(drag.current);};
  element.addEventListener('pointerdown',e=>{const g=e.target.closest('[data-point]');if(e.button!==0||!g)return;e.preventDefault();element.focus();drag={id:g.dataset.point,pointer:e.pointerId,before:[...element.querySelectorAll('[data-point]')].map(p=>({id:p.dataset.point,x:+p.dataset.x,y:+p.dataset.y}))};drag.current=drag.before;element.setPointerCapture(e.pointerId);},options);
  element.addEventListener('pointermove',e=>{if(drag&&e.pointerId===drag.pointer)update(e);},options);
  const cancel=()=>{if(drag){draw(drag.before);drag=null;}};
  element.addEventListener('pointerup',e=>{if(!drag||e.pointerId!==drag.pointer)return;update(e);const p=drag.current.find(p=>p.id===drag.id);drag=null;reference.invokeMethodAsync('MovePoint',p.id,p.x,p.y).catch(console.error);},options);
  element.addEventListener('pointercancel',cancel,options);
  window.addEventListener('keydown',e=>{if(e.key==='Escape')cancel();},options);
  element.addEventListener('keydown',e=>{const g=e.target.closest('[data-point]');const delta={ArrowLeft:[-5,0],ArrowRight:[5,0],ArrowUp:[0,-5],ArrowDown:[0,5]}[e.key];if(g&&delta){e.preventDefault();reference.invokeMethodAsync('MovePoint',g.dataset.point,Math.min(540,Math.max(20,+g.dataset.x+delta[0])),Math.min(320,Math.max(20,+g.dataset.y+delta[1]))).catch(console.error);}},options);
}
export function setSource(element,value){element.value=value;}
export async function copyText(value){await navigator.clipboard.writeText(value);}
export function download(name,type,value){const url=URL.createObjectURL(new Blob([value],{type}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export async function graph(xs,ys){
  if(!window.JXG){await new Promise((resolve,reject)=>{const s=document.createElement('script');s.src='./_content/Locus.WebProbe.Shared/vendor/jsxgraphcore.js';s.onload=resolve;s.onerror=reject;document.head.append(s);});}
  if(board)JXG.JSXGraph.freeBoard(board);
  board=JXG.JSXGraph.initBoard('jsxgraph',{boundingbox:[-5,18,5,-2],axis:true,showCopyright:false,showNavigation:false,renderer:'svg'});
  board.create('curve',[xs,ys],{strokeColor:'#28705c',strokeWidth:2});
  board.create('point',[1,1],{name:'P',size:3});
}
export function dispose(){cleanup();if(board){JXG.JSXGraph.freeBoard(board);board=null;}}
window.addEventListener('pagehide',()=>cleanup());
