import {readFile,realpath,mkdir,open} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {spawn} from 'node:child_process';

const args=process.argv.slice(2),action=args[0]??'start';
const option=name=>{const at=args.indexOf(name);return at<0?undefined:args[at+1];};
if(!['start','stop','status'].includes(action))throw Error('Use start, stop or status');
const here=dirname(fileURLToPath(import.meta.url));
const bundled=await realpath(resolve(here,'../wwwroot')).catch(()=>null);
const pointer=resolve(here,'../../artifacts/web1/current-build.json');
const root=await realpath(option('--root')??bundled??JSON.parse(await readFile(pointer,'utf8')).web);
const rootId=createHash('sha256').update(root.toLowerCase()).digest('hex');
const build=(await readFile(resolve(root,'index.html'),'utf8')).match(/name="locus-build" content="([^"]+)"/)?.[1];
if(!build)throw Error('The directory is not a versioned Locus WEB1 build');
const port=Number(option('--port')??4183);
if(!Number.isInteger(port)||port<1024||port>65535)throw Error('Port must be 1024..65535');
const state=resolve(option('--state-directory')??(bundled?resolve(here,'../.locus-local'):resolve(here,'../../artifacts/web1/local')));
const receiptPath=resolve(state,`server-${port}.json`),url=`http://127.0.0.1:${port}/`;
const request=()=>fetch(url+'__locus_local/health',{signal:AbortSignal.timeout(1000)}).then(async r=>r.ok?await r.json():null).catch(()=>null);
const matches=h=>h?.application==='locus-web1-local/1'&&h.rootId===rootId&&h.build===build;
const emit=(status,h)=>console.log(JSON.stringify({status,url,build,instance:h?.instance??null,pid:h?.pid??null}));
let health=await request();
if(action==='status'){emit(matches(health)?'RUNNING':health?'OTHER_SERVER':'STOPPED',health);process.exitCode=matches(health)?0:1;}
else if(action==='stop'){
  if(!health){emit('NOT_RUNNING');}
  else{
    const receipt=JSON.parse(await readFile(receiptPath,'utf8'));
    if(!matches(health)||receipt.instance!==health.instance||receipt.rootId!==rootId)throw Error('This server does not match the launch receipt; it was left running');
    const stopped=await fetch(url+'__locus_local/stop',{method:'POST',headers:{'X-Locus-Stop':receipt.stopToken},signal:AbortSignal.timeout(2000)});
    if(!stopped.ok)throw Error('The server refused the stop request');
    emit('STOPPED',health);
  }
}else if(matches(health)){emit('ALREADY_RUNNING',health);}
else{
  if(health)throw Error(`Port ${port} belongs to another server. It was left running. Use a different --port explicitly.`);
  await mkdir(state,{recursive:true});
  const log=await open(resolve(state,`server-${port}.log`),'a');
  const child=spawn(process.execPath,[resolve(here,'serve.mjs'),'--root',root,'--port',String(port),'--receipt',receiptPath],{cwd:here,detached:true,windowsHide:true,stdio:['ignore',log.fd,log.fd]});
  let failure;child.once('error',e=>failure=e);child.unref();await log.close();
  for(let count=0;count<50;count++){
    if(failure)throw failure;
    health=await request();if(matches(health)){emit('STARTED',health);break;}
    await new Promise(done=>setTimeout(done,100));
  }
  if(!matches(health))throw Error(`Cannot start Locus on port ${port}. Check server-${port}.log; another process may already use this port. No existing process was stopped.`);
}
