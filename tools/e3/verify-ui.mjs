import {chromium,firefox} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
const kind=process.argv[2]??'chromium',native=kind==='desktop',out=`artifacts/e3/runs/${kind}`;
await mkdir(out,{recursive:true});
const browser=native?await chromium.connectOverCDP('http://127.0.0.1:9334'):await(kind==='firefox'?firefox:chromium).launch({headless:true});
const context=native?browser.contexts()[0]:await browser.newContext({viewport:{width:1280,height:960},acceptDownloads:true});
const page=native?context.pages().find(p=>p.url()==='https://0.0.0.1/'):await context.newPage();
if(!page)throw Error('Owned E3 Desktop missing');
page.setDefaultTimeout(12000);const checks=[],errors=[],requests=[],scenes=[];
page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));
const hash=b=>createHash('sha256').update(b).digest('hex');
async function check(id,fn){try{await fn();checks.push({id,status:'PASS'});console.log('PASS',id);}catch(e){checks.push({id,status:'FAIL',error:e.message});console.log('FAIL',id,e.message);await page.screenshot({path:`${out}/failure-${checks.length}.png`}).catch(()=>{});}}
async function ready(){await page.locator('.locus-app[data-ready=true]').waitFor({timeout:40000});}
async function rendered(){await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:20000});}
async function settled(){await page.waitForTimeout(280);await page.locator('.locus-app[data-busy=false]').waitFor();}
async function input(raw){await page.locator('#source').fill(raw);await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent===`${raw.length} ký tự`,raw);await settled();}
async function flags(mask){for(const [name,bit] of [['math',1],['physics',2],['chemistry',4]]){await page.locator('#detect-'+name).setChecked(!!(mask&bit));await settled();}}
async function formula(raw){await input(raw);await rendered();}
async function latex(){await page.locator('.technical').evaluate(el=>el.open=true);return page.locator('#latex-output').textContent();}
async function svg(){return page.locator('#formula-preview svg').evaluate(el=>new XMLSerializer().serializeToString(el));}
async function download(button,name){const pending=page.waitForEvent('download');await page.locator(button).click();const d=await pending;const target=`${out}/${name}`;await d.saveAs(target);return readFile(target);}
async function snapshot(){const bytes=await download('#save-document','current.locus');return JSON.parse(JSON.parse(bytes).payload);}
try{
 if(native)await page.reload();else await page.goto('http://127.0.0.1:4184/');await ready();
 await check('same-editor-with-three-checkboxes',async()=>{await rendered();assert.equal(await page.locator('.detection-options input').count(),3);assert.equal(await page.locator('.nav-item').count(),1);assert(await page.locator('#detect-math').isChecked());assert(!await page.locator('#detect-physics').isChecked());assert(!await page.locator('#detect-chemistry').isChecked());assert.equal(await latex(),'{x}^{2}+1');});
 await check('eight-combinations-rebuild-current-source',async()=>{
  for(let mask=0;mask<8;mask++){
   await input('');await flags(mask);await input('H2SO4');
   if(mask&4){await rendered();assert.equal((await snapshot()).settings.enabledDomains,mask===7?'All':mask===4?'Chemistry':mask===5?'Math, Chemistry':'Physics, Chemistry');}
   else assert.equal(await page.locator('#formula-preview').count(),0);
  }
 });
 await check('chemistry-and-physics-shapes',async()=>{
  for(const [domain,mask,cases] of [['chemistry',4,['H2SO4','Ca(OH)2','Co','CO','SO₄²⁻','2H2 + O2 -> 2H2O','K4[Fe(CN)6]']],['physics',2,['v_0 = 10 m/s^2','vec(v_0)','alpha = 2','F = 10 kg m/s^2','v = 15 km/h','2 μs','x mu\u0303 2']]]){
   await input('');await flags(mask);
   for(const raw of cases){await formula(raw);const scene=await svg();assert(!/merror|<text|<use|href=/.test(scene));assert(scene.includes('<path'));const id=await page.locator('#formula-preview').getAttribute('data-candidate');assert.equal(id,await page.locator('#formula-preview svg').getAttribute('data-candidate-id'));scenes.push({domain,raw,id,latex:await latex(),sha256:hash(scene),svg:scene});await writeFile(`${out}/shape-${scenes.length}.svg`,scene);}
  }
 });
 await check('ambiguous-domain-fx-keeps-total-limit',async()=>{await input('');await flags(7);await formula('v = 10 m/s');await page.locator('#show-candidates').click();await page.locator('.candidate-list button').nth(1).waitFor();assert.equal(await page.locator('.candidate-list button').count(),2);const first=await latex();await page.locator('.candidate-list button').nth(1).click();await settled();await rendered();assert.notEqual(await latex(),first);await page.locator('#undo').click();await settled();await rendered();assert.equal(await latex(),first);});
 await check('off-preserves-snapshot-and-edit-invalidates',async()=>{
  await input('');await flags(4);await formula('H₂SO₄');const before=await svg();const candidate=await page.locator('#formula-preview').getAttribute('data-candidate');await flags(0);await rendered();assert.equal(await svg(),before);
  const bytes=await download('#save-document','off-snapshot.locus');assert.equal(JSON.parse(bytes).version,2);assert.equal(JSON.parse(JSON.parse(bytes).payload).settings.enabledDomains,'None');
  await input('H2O');assert.equal(await page.locator('#formula-preview').count(),0);await page.locator('#open-document').setInputFiles(`${out}/off-snapshot.locus`);await rendered();assert.equal(await page.locator('#source').inputValue(),'H₂SO₄');assert.equal(await page.locator('#formula-preview').getAttribute('data-candidate'),candidate);assert.equal(await svg(),before);assert(!await page.locator('#detect-chemistry').isChecked());
 });
 await check('native-authored-disabled-science-file',async()=>{await page.locator('#open-document').setInputFiles('artifacts/e3/physics-off.locus');await page.waitForFunction(()=>document.querySelector('#source')?.value==='👩‍🏫 lc[v₀ = 10 m/s²]');await rendered();assert(!await page.locator('#detect-physics').isChecked());assert.equal(await page.locator('#mode').inputValue(),'Markers');});
 await check('legacy-web1-file-and-selection',async()=>{await page.locator('#open-document').setInputFiles('artifacts/web1/runs/chromium/selected.locus');await page.waitForFunction(()=>document.querySelector('#source')?.value==='x+1/2');await rendered();assert.equal(await latex(),'\\frac{x+1}{2}');assert(await page.locator('#detect-math').isChecked());assert(!await page.locator('#detect-chemistry').isChecked());});
 await check('marker-and-prose-domains',async()=>{
  await input('');await flags(7);await page.locator('#mode').selectOption('Markers');await formula('A lc[H2SO4] B lc[v_0] C lc[x^2]');assert.equal(await page.locator('.regions button').count(),3);
  await page.locator('#mode').selectOption('Passive');await formula('Thêm H2SO4 vào nước. Vật có m = 10 kg.');assert.equal(await page.locator('.regions button').count(),2);assert.equal((await snapshot()).raw,'Thêm H2SO4 vào nước. Vật có m = 10 kg.');
  await page.locator('#mode').selectOption('Explicit');
 });
 await check('composition-lock-and-stale-render',async()=>{
  await input('');await flags(7);await formula('x^2');await page.locator('#source').evaluate(el=>{el.dispatchEvent(new CompositionEvent('compositionstart',{bubbles:true}));el.value='x mu';el.dispatchEvent(new InputEvent('input',{bubbles:true,isComposing:true}));});await settled();
  for(const name of ['math','physics','chemistry'])assert(!await page.locator('#detect-'+name).isEnabled());assert.equal(await page.locator('#formula-preview').count(),0);
  await page.locator('#source').evaluate(el=>{el.value='x mu\u0303 2';el.dispatchEvent(new CompositionEvent('compositionend',{bubbles:true,data:'ũ'}));el.dispatchEvent(new InputEvent('input',{bubbles:true}));});await settled();await rendered();assert.equal(await latex(),'{x}^{2}');
  await page.locator('#source').fill('H2SO4');await page.locator('#source').fill('Ca(OH)2');await settled();await rendered();assert((await latex()).includes('Ca'));assert(!(await latex()).includes('SO'));
 });
 await check('svg-png-source-export-and-undo',async()=>{
  await formula('2H2 + O2 -> 2H2O');const current=await svg();const exported=await download('#download-svg','reaction.svg');assert.equal(exported.toString(),current);
  const png=await download('#download-png','reaction.png');assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');assert(png.readUInt32BE(16)>100&&png.readUInt32BE(20)>20);
  await download('#save-document','cross-host.locus');const before=await latex();await formula('H2O');await page.locator('#undo').click();await rendered();assert.equal(await latex(),before);await page.locator('#redo').click();await rendered();assert((await latex()).includes('O'));
 });
 await check('reload-keeps-preferences-and-exact-result',async()=>{await input('');await flags(6);await formula('v = 15 km/h');const before=await svg();await page.waitForTimeout(600);await page.reload();await ready();await rendered();assert.equal(await svg(),before);assert(!await page.locator('#detect-math').isChecked());assert(await page.locator('#detect-physics').isChecked());assert(await page.locator('#detect-chemistry').isChecked());});
 if(!native)await check('worker-native-parity-400-plus-legacy',async()=>{
  const fixtures=[...JSON.parse(await readFile('artifacts/e3/worker-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/web1/worker-native-fixtures.json','utf8'))];
  const parity=await page.evaluate(async fixtures=>{const {managerFor}=await import(new URL('_content/Locus.Editor/worker-client.js',document.baseURI).href);const m=managerFor(new URL('worker/worker.js',document.baseURI).href);const results=[];for(const f of fixtures){let result,error;try{result=await m.run(f.id,f.request);}catch(e){error=e.message;}results.push({id:f.id,pass:f.error?!!error:result===f.result,error:error??(result!==f.result?'Native/WASM mismatch':null)});}m.dispose();return results;},fixtures);
  await writeFile(`${out}/worker-parity.json`,JSON.stringify(parity,null,2));assert(parity.every(p=>p.pass),parity.filter(p=>!p.pass).slice(0,4).map(p=>p.id+': '+p.error).join('; '));
 });
 if(!native)await check('mobile-and-offline-reload',async()=>{await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.screenshot({path:`${out}/mobile.png`,fullPage:true});await page.setViewportSize({width:1280,height:960});await page.waitForFunction(()=>document.querySelector('#offline-status')?.textContent.includes('offline'));await context.setOffline(true);await page.reload();await ready();await rendered();await formula('H2SO4');await context.setOffline(false);});
 await check('no-unhandled-errors-or-external-data',async()=>{assert.deepEqual(errors,[]);assert(requests.every(u=>u.startsWith('http://127.0.0.1:4184/')||u.startsWith('https://0.0.0.1/')||u.startsWith('blob:')||u.startsWith('data:')),requests.filter(u=>!u.includes('127.0.0.1')&&!u.includes('0.0.0.1')).join('\n'));});
 await page.screenshot({path:`${out}/editor.png`,fullPage:true});
}finally{
 await writeFile(`${out}/scenes.json`,JSON.stringify(scenes,null,2));
 const failed=checks.filter(c=>c.status==='FAIL').length;const build=JSON.parse(await readFile('artifacts/e3/current-build.json','utf8'));await writeFile(`${out}/ui.json`,JSON.stringify({capturedAtUtc:new Date().toISOString(),build:build.root.split(/[\\/]/).at(-1),kind,browserVersion:browser.version(),summary:{checks:checks.length,passed:checks.length-failed,failed},checks,errors},null,2));
 await browser.close();process.exitCode=failed?1:0;
}
