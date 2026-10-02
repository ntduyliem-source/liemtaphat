// One worker per session. Timeouts and cancellation run on the UI thread while C# computes in the worker.
export function managerFor(url, {startupMs=20000, workMs=5000}={}) {
  let worker, ready=false, bootTimer, disposed=false;
  const pending=new Map();
  const stats={started:0,completed:0,cancelled:0,timedOut:0};
  function stop(error){
    clearTimeout(bootTimer);worker?.terminate();worker=null;ready=false;
    for(const item of pending.values()){clearTimeout(item.timer);item.reject(new Error(error));}pending.clear();
  }
  function send(item){item.sent=true;item.timer=setTimeout(()=>{stats.timedOut++;stop('TIMEOUT: analysis deadline');},workMs);worker.postMessage({id:item.id,request:item.request});}
  function start(){
    worker=new Worker(new URL(url,document.baseURI),{type:'module'});stats.started++;
    bootTimer=setTimeout(()=>{stats.timedOut++;stop('TIMEOUT: worker startup');},startupMs);
    worker.onerror=()=>stop('Worker could not start or failed.');
    worker.onmessage=({data})=>{
      if(data.ready){clearTimeout(bootTimer);ready=true;for(const item of pending.values())send(item);return;}
      const item=pending.get(data.id);if(!item)return;
      clearTimeout(item.timer);pending.delete(data.id);stats.completed++;
      if(data.error)item.reject(new Error(data.error));else item.resolve(data.result);
    };
  }
  return {
    run(id,request){
      if(disposed)return Promise.reject(new Error('Disposed'));
      if(pending.has(id))return Promise.reject(new Error('Duplicate request'));
      return new Promise((resolve,reject)=>{const item={id,request,resolve,reject,sent:false};pending.set(id,item);if(!worker)start();else if(ready)send(item);});
    },
    cancel(id){const item=pending.get(id);if(!item)return;stats.cancelled++;if(item.sent)stop('Cancelled');else{pending.delete(id);item.reject(new Error('Cancelled'));}},
    dispose(){disposed=true;stop('Disposed');},
    stats(){return {...stats,pending:pending.size,ready};}
  };
}
export function create(url){return managerFor(url);}
