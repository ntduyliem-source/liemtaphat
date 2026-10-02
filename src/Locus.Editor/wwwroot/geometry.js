// Browser adapter only. All coordinates, relationships, measurements and projection are computed in C#.
const bindings=new WeakMap();
let storage;
export async function createStorage(host){
  if(storage)return storage;
  const prefix='locus.geometry.v1.';let id,release,db,serial=0,unsaved=false,enabled=true,queue=Promise.resolve();
  const get=(area,key)=>{try{return area.getItem(prefix+key);}catch{return null;}};
  const set=(area,key,value)=>{try{area.setItem(prefix+key,value);}catch{}};
  async function claim(candidate){if(!navigator.locks){id=crypto.randomUUID();return;}await new Promise((resolve,reject)=>navigator.locks.request(prefix+candidate,{ifAvailable:true},lock=>{if(!lock){resolve();return;}id=candidate;resolve();return new Promise(done=>release=done);}).catch(reject));if(!id)await claim(crypto.randomUUID());}
  await claim(get(sessionStorage,'tab')||(host==='desktop'?get(localStorage,'desktop'):null)||crypto.randomUUID());set(sessionStorage,'tab',id);if(host==='desktop')set(localStorage,'desktop',id);enabled=get(localStorage,'autosave')!=='false';
  async function transaction(mode,action){
    db??=await new Promise((resolve,reject)=>{const r=indexedDB.open('locus-visual-v1',1);r.onupgradeneeded=()=>r.result.createObjectStore('drafts',{keyPath:'id'});r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
    return new Promise((resolve,reject)=>{const tx=db.transaction('drafts',mode);const request=action(tx.objectStore('drafts'));tx.oncomplete=()=>resolve(request.result);tx.onerror=tx.onabort=()=>reject(tx.error);});
  }
  window.addEventListener('beforeunload',event=>{if(unsaved){event.preventDefault();event.returnValue='';}});
  storage={
    async read(){try{const value=await transaction('readonly',s=>s.get(id));return{plane:value?.plane??null,space:value?.space??null,autoSave:enabled,message:value?'Đã khôi phục nháp hình học.':'Nháp hình học lưu riêng trên thiết bị.'};}catch{return{plane:null,space:null,autoSave:enabled,message:'Bộ nhớ nháp không khả dụng. Hãy lưu .locus.'};}},
    dirty(){unsaved=true;serial++;},downloaded(){unsaved=false;},configure(value){enabled=value;set(localStorage,'autosave',String(value));},
    async fork(){release?.();id=null;await claim(crypto.randomUUID());set(sessionStorage,'tab',id);if(host==='desktop')set(localStorage,'desktop',id);},
    async save(plane,space){let result;const ticket=serial,draftId=id;queue=queue.catch(()=>{}).then(async()=>{try{await transaction('readwrite',s=>s.put({id:draftId,plane,space,time:Date.now()}));if(ticket===serial&&draftId===id)unsaved=false;result={savedOk:true,message:'Đã lưu nháp hình học.'};}catch{result={savedOk:false,message:'Chưa lưu được nháp. Hãy lưu .locus.'};}});await queue;return result;}
  };return storage;
}
export function wire(root,reference){
  const abort=new AbortController(),signal=abort.signal;let chain=Promise.resolve(),drag=null,raf=0,last=null,keyboard={x:500,y:320};
  const surface=root.querySelector('[data-geo-surface]');
  const invoke=(method,...args)=>{chain=chain.then(()=>reference.invokeMethodAsync(method,...args)).catch(()=>{});return chain;};
  const coordinates=event=>{const rect=surface.getBoundingClientRect();return{x:(event.clientX-rect.left)/rect.width*1000,y:(event.clientY-rect.top)/rect.height*640};};
  const hit=event=>({id:event.target.closest('[data-geo-id]')?.dataset.geoId??null,label:event.target.closest('[data-geo-label]')?.dataset.geoLabel??null});
  function send(phase,position,info,alt=false,shift=false){invoke('Pointer',phase,position.x,position.y,info.id,info.label,alt,shift);}
  surface.addEventListener('pointerdown',event=>{if(event.button!==0||event.target.closest('button'))return;surface.focus();drag={...hit(event),pointer:event.pointerId};surface.setPointerCapture(event.pointerId);const p=coordinates(event);keyboard=p;send('down',p,drag,event.altKey,event.shiftKey);event.preventDefault();},{signal});
  const flush=()=>{raf=0;if(last){send('move',last.position,last.info,last.alt,last.shift);last=null;}};
  surface.addEventListener('pointermove',event=>{last={position:coordinates(event),info:drag??hit(event),alt:event.altKey,shift:event.shiftKey};if(!raf)raf=requestAnimationFrame(flush);},{signal});
  surface.addEventListener('pointerup',event=>{if(!drag)return;cancelAnimationFrame(raf);flush();const info=drag;drag=null;if(surface.hasPointerCapture(event.pointerId))surface.releasePointerCapture(event.pointerId);send('up',coordinates(event),info,event.altKey,event.shiftKey);},{signal});
  surface.addEventListener('pointercancel',()=>{if(drag)send('cancel',keyboard,drag);drag=null;last=null;cancelAnimationFrame(raf);},{signal});
  root.addEventListener('keydown',event=>{
    if(event.isComposing||event.keyCode===229)return;
    const edit=event.target.matches('input,textarea,select'),key=event.key.toLowerCase(),ctrl=event.ctrlKey||event.metaKey;let command;
    if(ctrl&&key==='s')command='save';else if(!edit&&ctrl&&['z','y'].includes(key))command=key==='y'||event.shiftKey?'redo':'undo';
    else if(key==='escape'){command='escape';drag=null;last=null;cancelAnimationFrame(raf);}else if(!edit&&key==='delete')command='delete';else if(event.target===surface&&key==='enter')command='enter';
    if(command){event.preventDefault();invoke('Command',command);return;}
    if(event.target===surface&&key.startsWith('arrow')){event.preventDefault();const step=event.shiftKey?50:10;if(key==='arrowleft')keyboard.x-=step;if(key==='arrowright')keyboard.x+=step;if(key==='arrowup')keyboard.y-=step;if(key==='arrowdown')keyboard.y+=step;keyboard.x=Math.max(0,Math.min(1000,keyboard.x));keyboard.y=Math.max(0,Math.min(640,keyboard.y));send('move',keyboard,{id:null,label:null},event.altKey,event.shiftKey);}
    if(event.target===surface&&event.key===' '){event.preventDefault();send('down',keyboard,{id:null,label:null},event.altKey,event.shiftKey);send('up',keyboard,{id:null,label:null},event.altKey,event.shiftKey);}
  },{signal});
  root.querySelector('.geo-toolbar').addEventListener('click',event=>{if(event.target.closest('button'))root.querySelectorAll('.geo-toolbar details[open]').forEach(d=>d.open=false);},{signal});
  bindings.set(root,{settle:()=>chain,dispose:()=>{abort.abort();cancelAnimationFrame(raf);}});
}
export async function settle(root){await bindings.get(root)?.settle();}
export function unwire(root){bindings.get(root)?.dispose();bindings.delete(root);}
