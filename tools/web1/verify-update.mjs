import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,cp,stat,mkdir} from 'node:fs/promises';
import {resolve,extname,join} from 'node:path';
import {execFileSync} from 'node:child_process';
import http from 'node:http';
import assert from 'node:assert/strict';
const build=JSON.parse(await readFile('artifacts/web1/current-build.json','utf8'));
const variant=resolve('artifacts/web1/update-builds/'+Date.now());await mkdir(join(variant,'web'),{recursive:true});await cp(join(build.root,'web/wwwroot'),join(variant,'web/wwwroot'),{recursive:true});
execFileSync(process.execPath,['tools/web1/stage.mjs',variant]);
const first=JSON.parse(await readFile(join(build.root,'release.json'),'utf8')).id,second=JSON.parse(await readFile(join(variant,'release.json'),'utf8')).id;
let selected=build.web,broken=false;
const types={'.html':'text/html','.js':'text/javascript','.json':'application/json','.wasm':'application/wasm','.css':'text/css','.svg':'image/svg+xml','.dat':'application/octet-stream'};
const server=http.createServer(async(req,res)=>{try{const route=new URL(req.url,'http://localhost').pathname;if(broken&&route.includes(second)&&route.endsWith('/editor.js')){res.writeHead(503).end('Test incomplete upload');return;}const file=resolve(selected,'.'+route+(route.endsWith('/')?'index.html':''));const s=await stat(file);res.writeHead(200,{'Content-Type':types[extname(file)]??'application/octet-stream','Content-Length':s.size,'Cache-Control':'no-cache'});res.end(await readFile(file));}catch{res.writeHead(404).end();}});
await new Promise(r=>server.listen(4184,'127.0.0.1',r));
const browser=await chromium.launch(),context=await browser.newContext();const page=await context.newPage(),other=await context.newPage();page.setDefaultTimeout(15000);const results=[];
const ready=p=>p.locator('.locus-app[data-ready=true]').waitFor({timeout:45000});
async function check(name,fn){try{await fn();results.push({name,passed:true});console.log('PASS',name);}catch(e){results.push({name,passed:false,error:e.message});console.log('FAIL',name,e.message);}}
try{
await page.goto('http://127.0.0.1:4184/');await ready(page);await other.goto('http://127.0.0.1:4184/');await ready(other);
await page.locator('#source').fill('x mũ 5 + 9');await page.locator('#formula-preview[data-rendered=true]').waitFor();await page.waitForTimeout(800);
await check('partial-update-preserves-active-cache',async()=>{selected=join(variant,'site');broken=true;await page.evaluate(async()=>{const r=await navigator.serviceWorker.getRegistration();await r.update();});await page.waitForTimeout(3000);assert.equal(await page.evaluate(async()=>!!(await navigator.serviceWorker.getRegistration()).waiting),false);await context.setOffline(true);await page.reload();await ready(page);assert.equal(await page.locator('#source').inputValue(),'x mũ 5 + 9');assert.equal(await page.locator('meta[name=locus-build]').getAttribute('content'),first);await context.setOffline(false);});
await check('complete-update-waits-for-user-action',async()=>{broken=false;await page.evaluate(async()=>{const r=await navigator.serviceWorker.getRegistration();await r.update();});await page.locator('#apply-update').waitFor({timeout:45000});assert.equal(await page.locator('meta[name=locus-build]').getAttribute('content'),first);});
await check('update-saves-draft-and-loads-whole-new-release',async()=>{await page.locator('#auto-save').uncheck();await page.locator('#source').fill('x mũ 6 + 11');await page.locator('#formula-preview[data-rendered=true]').waitFor();await page.locator('#apply-update').click();await page.waitForFunction(v=>document.querySelector('meta[name=locus-build]')?.content===v,second,{timeout:45000});await ready(page);await page.locator('#formula-preview[data-rendered=true]').waitFor();assert.equal(await page.locator('#source').inputValue(),'x mũ 6 + 11');assert.equal(await page.locator('#auto-save').isChecked(),false);});
await check('old-live-tab-retains-own-assets-after-activation',async()=>{assert.equal(await other.locator('meta[name=locus-build]').getAttribute('content'),first);await context.setOffline(true);await other.locator('#source').fill('căn((x+1)/2)');await other.locator('#formula-preview[data-rendered=true]').waitFor();const response=await other.evaluate(async id=>{const r=await fetch(`/releases/${id}/_content/Locus.Editor/editor.js`);return {status:r.status,body:await r.text()};},first);assert.equal(response.status,200);assert(response.body.includes('clipboardSvg'));const newAssets=await page.evaluate(async()=>{const url=new URL('_content/Locus.Editor/editor.js',document.baseURI);return (await fetch(url)).status;});assert.equal(newAssets,200);});
await check('new-release-reloads-offline-with-updated-draft',async()=>{await page.reload();await ready(page);await page.locator('#formula-preview[data-rendered=true]').waitFor();assert.equal(await page.locator('#source').inputValue(),'x mũ 6 + 11');assert.equal(await page.locator('meta[name=locus-build]').getAttribute('content'),second);});
}finally{await writeFile('artifacts/web1/update.json',JSON.stringify({status:results.every(r=>r.passed)?'PASSED':'FAILED',first,second,results},null,2));await browser.close();await new Promise(r=>server.close(r));}
process.exitCode=results.some(r=>!r.passed)?1:0;
