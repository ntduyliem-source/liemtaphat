import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import {promisify} from 'node:util';
import {execFile} from 'node:child_process';
import http from 'node:http';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
import {chromium,firefox} from '../sh/node_modules/playwright/index.mjs';

const run=promisify(execFile),out='artifacts/web1/local';
await mkdir(out,{recursive:true});
const build=JSON.parse(await readFile('artifacts/web1/current-build.json','utf8'));
const validation=JSON.parse(await readFile('artifacts/web1/package-validation.json','utf8'));
const archive=JSON.parse(await readFile('artifacts/web1/package.json','utf8')).packages.find(p=>p.name.endsWith('Web-static'));
assert(validation.extractedWeb,'Run validate-package.ps1 -ExtractWeb first');
const extracted=validation.extractedWeb,root=resolve(extracted,'wwwroot'),runner=resolve(extracted,'local/local.mjs');
const port=4186,url=`http://127.0.0.1:${port}/`,results=[],browsers={};
const command=async(action,more=[])=>{const r=await run(process.execPath,[runner,action,'--port',String(port),...more],{cwd:extracted,windowsHide:true,timeout:12000});return JSON.parse(r.stdout);};
async function check(name,fn){try{await fn();results.push({name,passed:true});console.log('PASS',name);}catch(e){results.push({name,passed:false,error:e.message});console.log('FAIL',name,e.message);throw e;}}
const health=()=>fetch(url+'__locus_local/health').then(r=>r.json());
let owned=false;
try{
  await check('extracted-archive-assets-match-tested-build',async()=>{
    const release=JSON.parse(await readFile(resolve(build.root,'release.json'),'utf8'));
    for(const asset of release.assets){const bytes=await readFile(resolve(root,'.'+asset.url));assert.equal('sha256-'+createHash('sha256').update(bytes).digest('base64'),asset.integrity,asset.url);}
    for(const file of ['service-worker.js','_headers'])assert.deepEqual(await readFile(resolve(root,file)),await readFile(resolve(build.web,file)));
    for(const file of ['local.mjs','serve.mjs'])assert.deepEqual(await readFile(resolve(extracted,'local',file)),await readFile(resolve('tools/web1',file)));
    for(const file of ['Start-Locus-Web.cmd','Stop-Locus-Web.cmd'])assert.deepEqual(await readFile(resolve(extracted,file)),await readFile(resolve('tools/web1',file)));
  });
  await check('launch-and-repeated-launch-use-one-server',async()=>{
    const first=await command('start');owned=true;assert.equal(first.status,'STARTED');
    const again=await command('start');assert.equal(again.status,'ALREADY_RUNNING');assert.equal(first.instance,again.instance);assert.equal(first.pid,again.pid);
    assert.equal((await command('status')).status,'RUNNING');
  });
  await check('http-serves-complete-release-and-wasm-mime',async()=>{
    const release=JSON.parse(await readFile(resolve(build.root,'release.json'),'utf8'));
    for(const asset of release.assets){const response=await fetch(url.slice(0,-1)+asset.url);assert.equal(response.status,200,asset.url);assert.equal('sha256-'+createHash('sha256').update(Buffer.from(await response.arrayBuffer())).digest('base64'),asset.integrity,asset.url);if(asset.url.endsWith('.wasm'))assert.equal(response.headers.get('content-type'),'application/wasm');}
    const head=await fetch(url,{method:'HEAD'});assert.equal(head.status,200);assert.equal(await head.text(),'');
    assert.equal((await fetch(url+'missing.file')).status,404);
    assert.equal((await fetch(url+'%2e%2e%5cREADME.md')).status,403);
    assert.equal((await fetch(url,{method:'POST'})).status,405);
    assert.equal((await fetch(url+'__locus_local/stop',{method:'POST'})).status,403);
  });
  await check('stale-receipt-cannot-stop-current-server',async()=>{
    const receipt=resolve(extracted,'.locus-local',`server-${port}.json`),original=await readFile(receipt,'utf8');
    try{await writeFile(receipt,JSON.stringify({...JSON.parse(original),instance:'old-instance'}));await assert.rejects(command('stop'),/does not match/);assert.equal((await health()).application,'locus-web1-local/1');}
    finally{await writeFile(receipt,original);}
  });
  for(const [kind,engine] of Object.entries({chromium,firefox})){
    const browser=await engine.launch({headless:true}),context=await browser.newContext({acceptDownloads:true}),page=await context.newPage();
    const errors=[],external=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url())&&!r.url().startsWith(url))external.push(r.url());});
    browsers[kind]=browser.version();page.setDefaultTimeout(20000);
    const ready=()=>page.locator('.locus-app[data-ready=true]').waitFor({timeout:45000});
    const formula=async value=>{await page.locator('#source').fill(value);await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent===`${raw.length} ký tự`&&document.querySelector('.locus-app')?.dataset.busy==='false',value);await page.locator('#formula-preview[data-rendered=true] svg').waitFor();};
    const saved=()=>page.waitForFunction(()=>document.querySelector('#draft-status')?.textContent.includes('Đã lưu nháp'));
    let candidate;
    try{
      await check(kind+'-first-launch-caches-and-saves-draft',async()=>{
        await page.goto(url);await ready();await formula('x+1/2');
        await page.locator('#show-candidates').click();await page.locator('.candidate-list button').nth(1).click();
        await page.locator('#font-size').selectOption('64');await page.locator('#formula-preview[data-rendered=true] svg').waitFor();await saved();
        candidate=await page.locator('#formula-preview').getAttribute('data-candidate');
        assert.match(await page.locator('#offline-status').innerText(),/offline/);assert(await page.evaluate(()=>!!navigator.serviceWorker.controller));
      });
      await check(kind+'-actual-server-down-reload-retains-draft',async()=>{
        assert.equal((await command('stop')).status,'STOPPED');owned=false;
        await assert.rejects(fetch(url+'__locus_local/health',{signal:AbortSignal.timeout(1000)}));
        // Browser stays online: this proves the cached app works with the serving process stopped.
        await page.reload();await ready();await page.locator('#formula-preview[data-rendered=true] svg').waitFor();
        assert.equal(await page.locator('#source').inputValue(),'x+1/2');assert.equal(await page.locator('#font-size').inputValue(),'64');
        assert.equal(await page.locator('#formula-preview').getAttribute('data-candidate'),candidate);
      });
      await check(kind+'-server-down-new-formula-svg-png-and-file',async()=>{
        await formula('can2');await saved();
        for(const [selector,name,verify] of [
          ['#download-svg','offline.svg',b=>assert.match(b.toString(),/<path/)],
          ['#download-png','offline.png',b=>assert.equal(b.subarray(1,4).toString(),'PNG')],
          ['#save-document','offline.locus',b=>assert.match(b.toString(),/can2/)]
        ]){const pending=page.waitForEvent('download');await page.locator(selector).click();const download=await pending;const path=resolve(out,kind+'-'+name);await download.saveAs(path);verify(await readFile(path));}
      });
      await check(kind+'-restart-same-origin-retains-offline-edits',async()=>{
        assert.equal((await command('start')).status,'STARTED');owned=true;
        await page.reload();await ready();await page.locator('#formula-preview[data-rendered=true] svg').waitFor();
        assert.equal(await page.locator('#source').inputValue(),'can2');assert.equal(await page.locator('#font-size').inputValue(),'64');
        assert.deepEqual(errors,[]);assert.deepEqual(external,[]);
        await page.screenshot({path:resolve(out,kind+'-local.png'),fullPage:true});
      });
    }finally{await browser.close();}
  }
  await command('stop');owned=false;
  await check('busy-port-does-not-stop-unrelated-server',async()=>{
    const dummy=http.createServer((req,res)=>res.end('unrelated local server'));
    await new Promise((done,fail)=>{dummy.once('error',fail);dummy.listen(port,'127.0.0.1',done);});
    try{await assert.rejects(command('start'),/Cannot start Locus/);assert.equal(await(await fetch(url)).text(),'unrelated local server');}
    finally{await new Promise(done=>dummy.close(done));}
  });
}catch{process.exitCode=1;}
finally{
  if(owned)await command('stop').catch(()=>{});
  const report={status:results.length===13&&results.every(r=>r.passed)?'PASSED':'FAILED',capturedAtUtc:new Date().toISOString(),build:build.root.split(/[\\/]/).at(-1),archive:{path:archive.path,sha256:archive.sha256},extracted,browsers,results,limitations:['Automated browser input; no real OS Telex/VNI acceptance is claimed.','Verified with Node '+process.versions.node+' on '+process.platform+'.']};
  await writeFile('artifacts/web1/local-validation.json',JSON.stringify(report,null,2));if(report.status!=='PASSED')process.exitCode=1;
}
