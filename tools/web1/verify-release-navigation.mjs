// Exercise the real staging/boot/service-worker code with a tiny deterministic runtime.
import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir,stat} from 'node:fs/promises';
import {join,resolve,extname,relative} from 'node:path';
import {execFileSync} from 'node:child_process';
import http from 'node:http';
import assert from 'node:assert/strict';

const runId=Date.now();
const output=resolve(process.argv[2]??`artifacts/repair-20261013/navigation/${runId}`);
await mkdir(output,{recursive:true});
const browser=await chromium.launch();
const results=[];
async function check(name,action){try{await action();results.push({name,passed:true});console.log('PASS',name);}catch(error){results.push({name,passed:false,error:error.stack});console.error('FAIL',name,error.message);}}
async function release(id,base){
  const build=join(output,id),source=join(build,'web/wwwroot');
  await mkdir(join(source,'_framework'),{recursive:true});
  await writeFile(join(source,'index.html'),'<html><head><base href="/"></head><body><script src="_framework/blazor.webassembly.js"></script></body></html>');
  await writeFile(join(source,'boot.js'),await readFile('src/Locus.Web/wwwroot/boot.js'));
  await writeFile(join(source,'_framework/blazor.webassembly.js'),"document.documentElement.dataset.testReady='true';");
  await writeFile(join(source,'version.txt'),id);
  execFileSync(process.execPath,['tools/web1/stage.mjs',build,base],{stdio:'pipe',windowsHide:true});
  return join(build,'site');
}
async function ready(page){await page.waitForFunction(()=>document.documentElement.dataset.testReady==='true',null,{timeout:35000});}
try{
  for(const base of ['/','/liemtaphat/']){
    const suffix=base==='/'?'root':'project',idA=`route-${runId}-${suffix}-a`,idB=`route-${runId}-${suffix}-b`;
    const first=await release(idA,base),second=await release(idB,base);
    let active=first,broken=false;
    const server=http.createServer(async(req,res)=>{
      const path=new URL(req.url,'http://localhost').pathname;
      if(!path.startsWith(base)){res.writeHead(404).end('Outside app');return;}
      if(broken&&path===`${base}releases/${idB}/version.txt`){res.writeHead(503).end('Incomplete upload');return;}
      const local=path.slice(base.length),file=resolve(active,local+(path.endsWith('/')?'index.html':''));
      if(relative(active,file).startsWith('..')){res.writeHead(403).end();return;}
      try{const info=await stat(file);if(!info.isFile())throw Error('Missing');res.writeHead(200,{'Content-Type':({'.html':'text/html','.js':'text/javascript','.txt':'text/plain'})[extname(file)]??'application/octet-stream','Cache-Control':'no-cache'}).end(await readFile(file));}
      catch{try{res.writeHead(404,{'Content-Type':'text/html','Cache-Control':'no-cache'}).end(await readFile(join(active,'404.html')));}catch{res.end('Not found');}}
    });
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    const origin=`http://127.0.0.1:${server.address().port}`,url=origin+base;
    const context=await browser.newContext(),page=await context.newPage(),oldTab=await context.newPage();
    try{
      await page.goto(url);await ready(page);await oldTab.goto(url);await ready(oldTab);
      await check(`${suffix}/stable-address`,async()=>assert.equal(page.url(),url));
      active=second;broken=true;
      await check(`${suffix}/partial-release-is-not-activated`,async()=>{
        await page.evaluate(async()=>{const r=await navigator.serviceWorker.getRegistration();await r.update();await new Promise(resolve=>{const worker=r.installing;if(!worker)return resolve();worker.addEventListener('statechange',()=>{if(['installed','redundant'].includes(worker.state))resolve();});});});
        assert.equal(await page.evaluate(async()=>!!(await navigator.serviceWorker.getRegistration()).waiting),false);
        await context.setOffline(true);await page.reload();await ready(page);
        assert.equal(await page.locator('meta[name=locus-build]').getAttribute('content'),idA);
        await context.setOffline(false);
      });
      broken=false;
      await check(`${suffix}/whole-update-and-old-tab-assets`,async()=>{
        await page.evaluate(async()=>{await(await navigator.serviceWorker.getRegistration()).update();});
        // waitForFunction treats an async predicate's Promise as truthy before it resolves.
        // Wait for the same synchronous availability state that enables the real update UI.
        await page.waitForFunction(()=>globalThis.locusBoot?.waiting===true,null,{timeout:35000});
        assert.equal(await page.evaluate(async()=>!!(await navigator.serviceWorker.getRegistration()).waiting),true);
        await page.evaluate(()=>locusBoot.update());
        await page.waitForFunction(id=>document.querySelector('meta[name=locus-build]')?.content===id,idB,{timeout:35000});await ready(page);
        assert.equal(page.url(),url);
        await context.setOffline(true);
        assert.equal(await oldTab.evaluate(async path=>await(await fetch(path)).text(),`${base}releases/${idA}/version.txt`),idA);
        await page.reload();await ready(page);
        assert.equal(await page.locator('meta[name=locus-build]').getAttribute('content'),idB);
        await context.setOffline(false);
      });
      await check(`${suffix}/old-shared-link-without-cache`,async()=>{
        const fresh=await browser.newContext();try{
          const p=await fresh.newPage();await p.goto(`${url}releases/${idA}/?from=old#formula`);await ready(p);
          assert.equal(p.url(),`${url}?from=old#formula`);
          assert.equal(await p.locator('meta[name=locus-build]').getAttribute('content'),idB);
        }finally{await fresh.close();}
      });
      await check(`${suffix}/missing-assets-and-unrelated-paths-stay-404`,async()=>{
        const fresh=await browser.newContext({serviceWorkers:'block'});try{
          for(const path of [`releases/${idA}/missing.js`,'unrelated/']){
            const p=await fresh.newPage(),response=await p.goto(url+path);assert.equal(response.status(),404);
            await p.waitForLoadState('load');assert.equal(p.url(),url+path);await p.close();
          }
        }finally{await fresh.close();}
      });
    }finally{await context.close();await new Promise(resolve=>server.close(resolve));}
  }
}finally{
  await browser.close();
  await writeFile(join(output,'results.json'),JSON.stringify({status:results.every(r=>r.passed)?'PASSED':'FAILED',results},null,2));
  console.log('Results:',join(output,'results.json'));
}
process.exitCode=results.some(r=>!r.passed)?1:0;
