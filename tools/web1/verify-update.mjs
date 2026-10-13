// Exercise updates with the published Formula Studio, including real local drafts.
import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,cp,stat,mkdir} from 'node:fs/promises';
import {resolve,extname,join,relative} from 'node:path';
import {execFileSync} from 'node:child_process';
import http from 'node:http';
import assert from 'node:assert/strict';

const runId=Date.now();
const output=resolve(process.argv[2]??'artifacts/repair-20261013/full-update',String(runId));
await mkdir(output,{recursive:true});
const build=JSON.parse(await readFile(process.argv[3]??'artifacts/web1/current-build.json','utf8'));
const release=JSON.parse(await readFile(join(build.root,'release.json'),'utf8'));
const base=release.basePath??'/';
const variant=join(output,`update-${runId}`);
await mkdir(join(variant,'web'),{recursive:true});
await cp(join(build.root,'web/wwwroot'),join(variant,'web/wwwroot'),{recursive:true});
execFileSync(process.execPath,['tools/web1/stage.mjs',variant,base],{stdio:'pipe',windowsHide:true});
const first=release.id,second=JSON.parse(await readFile(join(variant,'release.json'),'utf8')).id;
let selected=build.web,broken=false,brokenRequests=0;
const types={'.html':'text/html','.js':'text/javascript','.json':'application/json','.wasm':'application/wasm','.css':'text/css','.svg':'image/svg+xml','.woff2':'font/woff2','.dat':'application/octet-stream'};
const server=http.createServer(async(req,res)=>{
  try{
    const route=new URL(req.url,'http://localhost').pathname;
    if(!route.startsWith(base)){res.writeHead(404).end();return;}
    if(broken&&route===`${base}releases/${second}/_content/Locus.Editor/editor.js`){brokenRequests++;res.writeHead(503).end('Test incomplete upload');return;}
    const file=resolve(selected,route.slice(base.length)+(route.endsWith('/')?'index.html':''));
    if(relative(selected,file).startsWith('..')){res.writeHead(403).end();return;}
    const info=await stat(file);if(!info.isFile())throw Error('Not a file');
    res.writeHead(200,{'Content-Type':types[extname(file)]??'application/octet-stream','Content-Length':info.size,'Cache-Control':'no-cache'});
    res.end(await readFile(file));
  }catch{res.writeHead(404).end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const url=`http://127.0.0.1:${server.address().port}${base}`;
const browser=await chromium.launch(),context=await browser.newContext();
const page=await context.newPage(),other=await context.newPage();
page.setDefaultTimeout(15000);other.setDefaultTimeout(15000);
const results=[],pageErrors=[],unversionedAssets=[];
context.on('request',request=>{
  const path=new URL(request.url()).pathname;
  if(/\/(?:_framework|_content|worker)\//.test(path)&&
    ![first,second].some(id=>path.startsWith(`${base}releases/${id}/`)))unversionedAssets.push(path);
});
for(const tab of [page,other])tab.on('pageerror',error=>pageErrors.push(error.message));
const ready=tab=>tab.locator('.locus-app[data-ready=true][data-busy=false]').waitFor({timeout:60000});
const buildId=tab=>tab.locator('meta[name=locus-build]').getAttribute('content');
async function powerResult(tab,raw,power,constant){
  await ready(tab);
  // SourceInput is asynchronous and its 180ms debounce starts before data-busy changes.
  // Matching the actual MathML prevents both a stale result and a pending input passing.
  await tab.waitForFunction(({raw,power,constant})=>{
    const app=document.querySelector('.locus-app[data-ready=true][data-busy=false]');
    const math=document.querySelector('#mixed-result math');
    return !!app&&app.dataset.inputPending!=='true'&&document.querySelector('#source')?.value===raw&&
      math?.querySelector('msup')?.lastElementChild?.textContent===power&&
      math?.textContent?.replace(/\s/g,'')===`x${power}+${constant}`;
  },{raw,power,constant},{timeout:45000});
}
async function draft(tab){
  return tab.evaluate(async()=>{
    const local=await import(new URL('_content/Locus.Editor/local-state.js',locusAssetRoot()).href);
    const value=await local.read(sessionStorage.getItem('locus.web1.tab'));
    return {raw:value.raw,payload:value.document?JSON.parse(JSON.parse(value.document).payload):null};
  });
}
async function until(action,description,timeout=15000){
  const deadline=Date.now()+timeout;
  do{if(await action())return;await new Promise(resolve=>setTimeout(resolve,100));}while(Date.now()<deadline);
  throw Error(`Timed out: ${description}`);
}
async function check(name,action){
  try{await action();results.push({name,passed:true});console.log('PASS',name);}
  catch(error){results.push({name,passed:false,error:error.stack});console.error('FAIL',name,error.message);throw error;}
}
try{
  await check('current-formula-studio-input-and-local-draft',async()=>{
    await page.goto(url);await ready(page);await other.goto(url);await ready(other);
    assert.equal(page.url(),url);assert.equal(await buildId(page),first);
    assert.equal(new URL(await page.locator('base').getAttribute('href'),url).pathname,base);
    assert.equal(await page.locator('meta[name=locus-assets]').getAttribute('content'),`${base}releases/${first}/`);
    await page.locator('#source').fill('x mũ 5 + 9');
    await powerResult(page,'x mũ 5 + 9','5','9');
    await until(async()=>{const value=await draft(page);return value.raw==='x mũ 5 + 9'&&value.payload?.raw===value.raw&&value.payload?.content?.regions.length>0;},'full analyzed draft saved');
  });
  await check('partial-update-preserves-active-cache',async()=>{
    selected=join(variant,'site');broken=true;
    await page.evaluate(async()=>{
      const registration=await navigator.serviceWorker.getRegistration();await registration.update();
      const worker=registration.installing;if(!worker)return;
      if(['installed','redundant'].includes(worker.state))return;
      await new Promise((resolve,reject)=>{
        const timeout=setTimeout(()=>reject(Error('Incomplete install did not finish')),45000);
        worker.addEventListener('statechange',()=>{if(['installed','redundant'].includes(worker.state)){clearTimeout(timeout);resolve();}});
      });
    });
    assert(brokenRequests>0,'The update must actually try to fetch the withheld asset');
    assert.equal(await page.evaluate(async()=>!!(await navigator.serviceWorker.getRegistration()).waiting),false);
    await context.setOffline(true);await page.reload();await powerResult(page,'x mũ 5 + 9','5','9');
    assert.equal(await buildId(page),first);assert.equal(page.url(),url);
    await context.setOffline(false);
  });
  await check('complete-update-waits-for-user-action',async()=>{
    broken=false;await page.evaluate(async()=>{await(await navigator.serviceWorker.getRegistration()).update();});
    // waitForFunction must use a synchronous predicate; this Playwright build does not await one.
    await page.waitForFunction(()=>globalThis.locusBoot?.waiting===true,null,{timeout:60000});
    await page.locator('#apply-update').waitFor({timeout:15000});
    assert.equal(await buildId(page),first);assert.equal(await buildId(other),first);
    assert.equal(await page.locator('#apply-update').isEnabled(),true);
  });
  await check('update-forces-draft-save-and-loads-whole-new-release',async()=>{
    // Studio no longer exposes an autosave checkbox. Load the supported legacy preference
    // through its local-state API, then reopen so the managed model also has AutoSave=false.
    await page.evaluate(async()=>{
      const local=await import(new URL('_content/Locus.Editor/local-state.js',locusAssetRoot()).href);
      const preferences=JSON.parse(localStorage.getItem('locus.web1.preferences'));preferences.autoSave=false;
      local.configure(false,JSON.stringify(preferences));
    });
    await page.reload();await powerResult(page,'x mũ 5 + 9','5','9');
    assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('locus.web1.preferences')).autoSave),false);
    await page.locator('#source').fill('x mũ 6 + 11');await powerResult(page,'x mũ 6 + 11','6','11');
    assert.equal((await draft(page)).raw,'x mũ 5 + 9','Autosave off must not journal the new source');
    assert.equal((await draft(page)).payload.raw,'x mũ 5 + 9');
    const rendered=await page.locator('#mixed-result math').evaluate(element=>element.outerHTML);
    await page.locator('#apply-update').click();
    await page.waitForFunction(id=>document.querySelector('meta[name=locus-build]')?.content===id,second,{timeout:60000});
    await powerResult(page,'x mũ 6 + 11','6','11');
    assert.equal(await page.locator('#mixed-result math').evaluate(element=>element.outerHTML),rendered);
    assert.equal(new URL(await page.locator('base').getAttribute('href'),url).pathname,base);
    assert.equal(await page.locator('meta[name=locus-assets]').getAttribute('content'),`${base}releases/${second}/`);
    assert.equal(page.url(),url);assert.equal((await draft(page)).payload.raw,'x mũ 6 + 11');
    assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('locus.web1.preferences')).autoSave),false);
  });
  await check('old-live-tab-retains-own-assets-after-activation',async()=>{
    assert.equal(await buildId(other),first);await context.setOffline(true);
    await other.locator('#source').fill('căn((x+1)/2)');
    await other.waitForFunction(()=>{
      const app=document.querySelector('.locus-app[data-ready=true][data-busy=false]');
      return !!app&&app.dataset.inputPending!=='true'&&document.querySelector('#source')?.value==='căn((x+1)/2)'&&
        !!document.querySelector('#mixed-result math msqrt mfrac');
    },null,{timeout:45000});
    for(const [tab,id] of [[other,first],[page,second]]){
      const asset=await tab.evaluate(async path=>{const response=await fetch(path);return {status:response.status,body:await response.text()};},`${base}releases/${id}/_content/Locus.Editor/editor.js`);
      assert.equal(asset.status,200);assert(asset.body.includes('clipboardSvg'));
    }
    assert.equal(await buildId(other),first);
  });
  await check('new-release-reloads-offline-with-updated-draft',async()=>{
    await page.reload();await powerResult(page,'x mũ 6 + 11','6','11');
    assert.equal(await buildId(page),second);assert.equal(page.url(),url);
    assert.equal((await draft(page)).payload.raw,'x mũ 6 + 11');
    assert.deepEqual(pageErrors,[]);
    assert.deepEqual(unversionedAssets,[],'Runtime, editor and worker requests must stay in an immutable release');
  });
}catch(error){
  if(!results.some(result=>!result.passed))results.push({name:'test-setup',passed:false,error:error.stack});
  await writeFile(join(output,'failed-page.html'),await page.content().catch(()=>''));
  await page.screenshot({path:join(output,'failed-page.png'),fullPage:true}).catch(()=>{});
}finally{
  await writeFile(join(output,'results.json'),JSON.stringify({status:results.length===6&&results.every(result=>result.passed)?'PASSED':'FAILED',build:build.root,first,second,base,url,brokenRequests,pageErrors,unversionedAssets,results},null,2));
  console.log('Results:',join(output,'results.json'));
  await browser.close();await new Promise(resolve=>server.close(resolve));
}
process.exitCode=results.length!==6||results.some(result=>!result.passed)?1:0;
