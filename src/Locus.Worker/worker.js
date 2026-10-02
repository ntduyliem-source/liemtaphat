import {dotnet} from './_framework/dotnet.js';
const runtime = await dotnet.create();
const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
self.addEventListener('message', event => {
  const {id,request} = event.data;
  try { self.postMessage({id,result:exports.Locus.Worker.WorkerMethods.Analyze(request)}); }
  catch(error) { self.postMessage({id,error:String(error.message??error)}); }
});
self.postMessage({ready:true});
