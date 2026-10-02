const state=globalThis.locusBoot={status:'Đang chuẩn bị bản offline…',waiting:false};
let registration,reloading=false;
state.update=()=>{if(registration?.waiting){reloading=true;registration.waiting.postMessage({type:'ACTIVATE'});}};
function watch(){
  state.waiting=!!registration.waiting;
  state.status=state.waiting?'Có bản mới; phiên đang mở vẫn dùng được.':navigator.onLine?'Sẵn sàng dùng offline trên thiết bị này.':'Đang offline · nháp lưu trên thiết bị.';
}
if('serviceWorker' in navigator&&isSecureContext){
  try{
    const hadController=!!navigator.serviceWorker.controller;
    registration=await navigator.serviceWorker.register('/service-worker.js',{scope:'/',updateViaCache:'none'});
    navigator.serviceWorker.addEventListener('controllerchange',()=>{if(reloading)location.reload();});
    registration.addEventListener('updatefound',()=>{registration.installing?.addEventListener('statechange',watch);});
    await Promise.race([navigator.serviceWorker.ready,new Promise((_,reject)=>setTimeout(()=>reject(Error('Offline cache unavailable')),25000))]);
    // The complete cache takes control before Blazor loads runtime and grammar assets.
    if(!navigator.serviceWorker.controller)await new Promise(resolve=>navigator.serviceWorker.addEventListener('controllerchange',resolve,{once:true}));
    watch();window.addEventListener('online',watch);window.addEventListener('offline',watch);
    if(!hadController){location.reload();await new Promise(()=>{});}
    registration.update().catch(()=>{});
  }catch{state.status='Chưa lưu được bản offline. Bạn vẫn có thể dùng khi có mạng.';}
}else state.status='Trình duyệt này chưa hỗ trợ bản offline.';
history.replaceState(null,'',new URL(document.baseURI).pathname);
const script=document.createElement('script');script.src=document.querySelector('#locus-runtime').dataset.src;document.body.append(script);
