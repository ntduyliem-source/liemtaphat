import {chromium,firefox} from 'playwright';
import {readFile,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const kind=process.argv[2]??'chromium';const browser=await(kind==='firefox'?firefox:chromium).launch({headless:true});const page=await browser.newPage();await page.goto(process.env.LOCUS_SH_URL??'http://127.0.0.1:4182/');await page.locator('#formula-preview svg').waitFor({timeout:30000});
const fixtures=JSON.parse(await readFile('artifacts/sh/worker-native-fixtures.json','utf8'));
const results=await page.evaluate(async fixtures=>{
 const {managerFor}=await import('./_content/Locus.Editor/worker-client.js');const real=managerFor('./worker/worker.js');const outcomes=[];
 for(const fixture of fixtures){let result,error;try{result=await real.run(fixture.id,fixture.request);}catch(e){error=String(e.message);}outcomes.push({id:fixture.id,passed:fixture.error?!!error:result===fixture.result,error:error??(result!==fixture.result?'Native/worker bytes differ':null)});}real.dispose();
 return outcomes;
},fixtures);
const scheduling=await page.evaluate(async()=>{
 const {managerFor}=await import('./_content/Locus.Editor/worker-client.js');
 const url=URL.createObjectURL(new Blob([`self.onmessage=({data})=>{if(data.request==='block'){const end=Date.now()+60000;while(Date.now()<end){}}self.postMessage({id:data.id,result:'ok'});};self.postMessage({ready:true});`],{type:'text/javascript'}));
 const m=managerFor(url,{startupMs:3000,workMs:180});const outcome={};
 await m.run('warm','ok');let ticks=0;const heartbeat=setInterval(()=>ticks++,10);const start=performance.now();
 try{await m.run('deadline','block');outcome.deadline='unexpected result';}catch(e){outcome.deadline=e.message;}
 clearInterval(heartbeat);outcome.elapsedMs=performance.now()-start;outcome.uiTimerTicks=ticks;outcome.retry=await m.run('retry','ok');
 const cancelled=m.run('cancel','block').catch(e=>e.message);setTimeout(()=>m.cancel('cancel'),30);outcome.cancel=await cancelled;outcome.afterCancel=await m.run('after-cancel','ok');
 const disposed=m.run('dispose','block').catch(e=>e.message);m.dispose();outcome.dispose=await disposed;outcome.stats=m.stats();URL.revokeObjectURL(url);
 const noReady=URL.createObjectURL(new Blob(['self.onmessage=()=>{}'],{type:'text/javascript'}));const slow=managerFor(noReady,{startupMs:150,workMs:100});try{await slow.run('startup','x');}catch(e){outcome.startup=e.message;}slow.dispose();URL.revokeObjectURL(noReady);return outcome;
});
let status='PASSED';try{assert(results.every(x=>x.passed));assert.match(scheduling.deadline,/TIMEOUT/);assert(scheduling.uiTimerTicks>=5);assert(scheduling.elapsedMs<3000);assert.equal(scheduling.retry,'ok');assert.equal(scheduling.afterCancel,'ok');assert.match(scheduling.cancel,/Cancelled/);assert.match(scheduling.dispose,/Disposed/);assert.match(scheduling.startup,/TIMEOUT/);assert.equal(scheduling.stats.pending,0);}catch(e){status='FAILED';console.error(e.message);}
await writeFile(`artifacts/sh/worker-${kind}.json`,JSON.stringify({status,browserVersion:browser.version(),parity:results,scheduling},null,2));console.log(JSON.stringify({status,parity:results.filter(x=>x.passed).length,total:results.length,scheduling}));await browser.close();process.exitCode=status==='PASSED'?0:1;
