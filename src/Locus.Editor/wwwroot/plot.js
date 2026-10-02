// DOM input, pointer coordinates and local persistence only. Grammar/evaluation live in C#.
const bindings=new WeakMap();
let id,db,startup,enabled=true,unsaved=false,releaseLock,hostKind,saveQueue=Promise.resolve(),editSerial=0;
const prefix='locus.plot.v1.';
function storageGet(storage,key){try{return storage.getItem(prefix+key);}catch{return null;}}
function storageSet(storage,key,value){try{storage.setItem(prefix+key,value);}catch{}}
async function database(){
  if(db)return db;
  return db=await new Promise((resolve,reject)=>{const r=indexedDB.open('locus-visual-v1',1);r.onupgradeneeded=()=>r.result.createObjectStore('drafts',{keyPath:'id'});r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
}
async function transaction(mode,action){const databaseValue=await database();return new Promise((resolve,reject)=>{const tx=databaseValue.transaction('drafts',mode);const request=action(tx.objectStore('drafts'));tx.oncomplete=()=>resolve(request.result);tx.onabort=tx.onerror=()=>reject(tx.error);});}
async function claim(candidate){
  if(!navigator.locks){id=crypto.randomUUID();return;}
  await new Promise((resolve,reject)=>navigator.locks.request(prefix+candidate,{ifAvailable:true},lock=>{if(!lock){resolve();return;}id=candidate;resolve();return new Promise(done=>releaseLock=done);}).catch(reject));
  if(!id)await claim(crypto.randomUUID());
}
export async function start(host){
  startup??=(async()=>{hostKind=host;await claim(storageGet(sessionStorage,'tab')||(host==='desktop'?storageGet(localStorage,'desktop'):null)||crypto.randomUUID());storageSet(sessionStorage,'tab',id);if(host==='desktop')storageSet(localStorage,'desktop',id);enabled=storageGet(localStorage,'autosave')!=='false';window.addEventListener('beforeunload',event=>{if(unsaved){event.preventDefault();event.returnValue='';}});})();
  await startup;
  try{const saved=await transaction('readonly',s=>s.get(id));return {document:saved?.document??null,autoSave:enabled,message:saved?'Đã khôi phục nháp đồ thị.':'Nháp đồ thị lưu riêng trên thiết bị.'};}
  catch{return {document:null,autoSave:enabled,message:'Bộ nhớ nháp không khả dụng. Hãy lưu .locus.'};}
}
export function dirty(){unsaved=true;editSerial++;}
export function configure(value){enabled=value;storageSet(localStorage,'autosave',String(value));}
export function downloaded(){unsaved=false;}
export async function save(document){
  const draftId=id,ticket=editSerial;let result;
  saveQueue=saveQueue.catch(()=>{}).then(async()=>{try{await transaction('readwrite',s=>s.put({id:draftId,document,time:Date.now()}));if(ticket===editSerial&&id===draftId)unsaved=false;result={savedOk:true,message:'Đã lưu nháp đồ thị.'};}catch{result={savedOk:false,message:'Chưa lưu được nháp. Hãy lưu .locus.'};}});
  await saveQueue;return result;
}
export async function fork(){releaseLock?.();id=null;await claim(crypto.randomUUID());storageSet(sessionStorage,'tab',id);if(hostKind==='desktop')storageSet(localStorage,'desktop',id);}
export function wire(root,reference){
  const abort=new AbortController(),signal=abort.signal;let composing=false,chain=Promise.resolve(),serial=0,drag=null,raf=0;
  const invoke=(method,...args)=>{chain=chain.then(()=>reference.invokeMethodAsync(method,...args)).catch(()=>{});return chain;};
  const send=event=>{
    const source=event.target.closest('[data-plot-source]');if(!source)return;
    dirty();root.dataset.pending='true';const ticket=++serial;
    invoke('SourceInput',source.dataset.plotSource,source.value,composing||event.isComposing===true).then(()=>{if(serial===ticket)root.dataset.pending='false';});
  };
  root.addEventListener('compositionstart',event=>{composing=true;send(event);},{signal});
  root.addEventListener('compositionend',event=>{composing=false;send(event);},{signal});
  root.addEventListener('input',send,{signal});
  document.addEventListener('click',event=>{if((composing&&event.target.closest('.tab-strip'))||(root.dataset.pending==='true'&&event.target.closest('.plot-actions,.plot-export'))){event.preventDefault();event.stopImmediatePropagation();}},{signal,capture:true});
  root.addEventListener('keydown',event=>{
    if(composing||event.isComposing||event.keyCode===229)return;
    const key=event.key.toLowerCase(),ctrl=event.ctrlKey||event.metaKey;let command;
    if(ctrl&&key==='s')command='save';
    else if(ctrl&&['z','y'].includes(key)&&!event.target.matches('input[type=number]'))command=key==='y'||event.shiftKey?'redo':'undo';
    else if(key==='escape'){command='escape';drag=null;cancelAnimationFrame(raf);}
    if(command){event.preventDefault();invoke('Command',command);}
  },{signal});
  const surface=root.querySelector('[data-plot-surface]');
  const move=()=>{raf=0;if(drag)invoke('Pan','move',drag.dx,drag.dy);};
  surface.addEventListener('pointerdown',event=>{
    if(event.button!==0||event.target.closest('button,.plot-start'))return;
    const curve=event.target.closest('[data-curve]');if(curve){invoke('SelectCurve',curve.dataset.curve);surface.focus();event.preventDefault();return;}
    const rect=surface.getBoundingClientRect();drag={x:event.clientX,y:event.clientY,w:rect.width,h:rect.height,dx:0,dy:0};surface.setPointerCapture(event.pointerId);surface.focus();invoke('Pan','start',0,0);event.preventDefault();
  },{signal});
  surface.addEventListener('pointermove',event=>{if(!drag)return;drag.dx=(event.clientX-drag.x)/drag.w;drag.dy=(event.clientY-drag.y)/drag.h;if(!raf)raf=requestAnimationFrame(move);},{signal});
  surface.addEventListener('pointerup',event=>{if(!drag)return;cancelAnimationFrame(raf);move();drag=null;if(surface.hasPointerCapture(event.pointerId))surface.releasePointerCapture(event.pointerId);invoke('Pan','end',0,0);},{signal});
  surface.addEventListener('pointercancel',()=>{if(drag){drag=null;invoke('Pan','cancel',0,0);}},{signal});
  bindings.set(root,{settle:()=>chain,dispose:()=>{abort.abort();cancelAnimationFrame(raf);}});
}
export async function settle(root){await bindings.get(root)?.settle();}
export function unwire(root){bindings.get(root)?.dispose();bindings.delete(root);}
