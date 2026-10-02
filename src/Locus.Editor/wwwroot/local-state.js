const prefix='locus.web1.',dbName='locus-local-v1';
let id,db,started,releaseLock,hostKind='browser',enabled=true,dirty=false,journalFailure=false,queue=Promise.resolve();
const request=r=>new Promise((resolve,reject)=>{r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
async function database(){
  if(!db){const r=indexedDB.open(dbName,1);r.onupgradeneeded=()=>r.result.createObjectStore('drafts',{keyPath:'id'});db=await request(r);db.onversionchange=()=>{db.close();db=null;};}return db;
}
async function transact(mode,fn){const d=await database();return new Promise((resolve,reject)=>{const tx=d.transaction('drafts',mode);let result;try{result=fn(tx.objectStore('drafts'));}catch(e){tx.abort();reject(e);return;}tx.oncomplete=()=>resolve(result?.result);tx.onerror=()=>reject(tx.error);tx.onabort=()=>reject(tx.error??Error('Aborted'));});}
function readLocal(key){try{return localStorage.getItem(prefix+key);}catch{return null;}}
async function claim(candidate){
  if(!navigator.locks){id=crypto.randomUUID();return;}
  await new Promise((resolve,reject)=>{navigator.locks.request(prefix+candidate,{ifAvailable:true},lock=>{
    if(!lock){resolve(false);return;}id=candidate;resolve(true);return new Promise(done=>releaseLock=done);
  }).catch(reject);});
  if(!id)await claim(crypto.randomUUID());
}
export async function start(host='browser'){
  if(!started)started=(async()=>{
    hostKind=host;
    let candidate;try{candidate=sessionStorage.getItem(prefix+'tab')||(host==='desktop'?readLocal('desktop.tab'):null);}catch{}
    await claim(candidate||crypto.randomUUID());try{sessionStorage.setItem(prefix+'tab',id);}catch{}
    if(host==='desktop')try{localStorage.setItem(prefix+'desktop.tab',id);}catch{}
    window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
  })();
  await started;
  let entry=null,error='';try{entry=await transact('readonly',s=>s.get(id));}catch{error='Bộ nhớ nháp không khả dụng. Hãy lưu tệp .locus.';}
  return {id,preferences:readLocal('preferences'),document:entry?.document??null,raw:readJournal(id),error,drafts:await list()};
}
function readJournal(key){try{const value=JSON.parse(readLocal('raw.'+key));return typeof value?.raw==='string'?value.raw:null;}catch{return null;}}
export function journal(raw){
  dirty=true;if(!enabled||!id)return;
  try{localStorage.setItem(prefix+'raw.'+id,JSON.stringify({raw,time:Date.now()}));journalFailure=false;}
  catch{journalFailure=true;}
}
export function configure(autoSave,preferences){
  enabled=autoSave;
  try{localStorage.setItem(prefix+'preferences',preferences);return true;}catch{return false;}
}
export async function save(document,raw,force=false){
  if(!enabled&&!force)return {saved:false,message:'Tự lưu đang tắt. Lưu tệp trước khi đóng.'};
  if(force){try{localStorage.setItem(prefix+'raw.'+id,JSON.stringify({raw,time:Date.now()}));journalFailure=false;}catch{journalFailure=true;}}
  const key=id;let result;
  queue=queue.catch(()=>{}).then(async()=>{
    try{
      await transact('readwrite',s=>s.put({id:key,document,raw:raw.slice(0,100),time:Date.now()}));
      // A later keystroke owns the journal; an older snapshot must not clear it.
      const latest=readJournal(key);if(key===id&&(latest===null||latest===raw)&&!journalFailure)dirty=false;
      result={saved:!journalFailure,message:journalFailure?'Đã lưu công thức; chưa lưu được nguồn đang gõ. Hãy tải tệp.':'Đã lưu nháp trên thiết bị.'};
    }catch{result={saved:false,message:'Chưa lưu được nháp. Bộ nhớ có thể đầy hoặc bị chặn; hãy tải tệp .locus.'};}
  });await queue;return result;
}
export async function list(){try{return (await transact('readonly',s=>s.getAll())).sort((a,b)=>b.time-a.time).slice(0,10).map(x=>({id:x.id,label:(x.raw||'Công thức trống')+' · '+new Date(x.time).toLocaleString('vi-VN')}));}catch{return [];}}
export async function read(key){const value=await transact('readonly',s=>s.get(key));return {document:value?.document??null,raw:readJournal(key)};}
export async function fork(){releaseLock?.();id=null;await claim(crypto.randomUUID());try{sessionStorage.setItem(prefix+'tab',id);if(hostKind==='desktop')localStorage.setItem(prefix+'desktop.tab',id);}catch{}return id;}
export function markDownloaded(){dirty=false;}
