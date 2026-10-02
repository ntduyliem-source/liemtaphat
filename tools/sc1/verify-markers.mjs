import {chromium,firefox} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
const kind=process.argv[2]??'chromium',native=kind==='desktop',out=`artifacts/sc1/runs/${kind}`;
const expectedBuild=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8')).root.split(/[\\/]/).at(-1);
let observedBuild=null;
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
 if(native)await page.reload();else await page.goto('http://127.0.0.1:4185/');await ready();
 await check('published-build-identity',async()=>{
  observedBuild=native?JSON.parse(await readFile('artifacts/sc1/desktop-markers-receipt.json','utf8')).executable.split(/[\\/]/).find(s=>/^\d{8}-\d{6}-\d{3}$/.test(s)):new URL(page.url()).pathname.split('/')[2];
  assert.equal(observedBuild,expectedBuild,'Running host differs from the build under test');
 });
 await check('same-editor-with-three-checkboxes',async()=>{await rendered();assert.equal(await page.locator('.detection-options input').count(),3);assert.equal(await page.locator('.nav-item').count(),1);assert(await page.locator('#detect-math').isChecked());assert(!await page.locator('#detect-physics').isChecked());assert(!await page.locator('#detect-chemistry').isChecked());assert.equal(await latex(),'{x}^{2}+1');});
 await check('four-visible-presets-and-named-overrides',async()=>{
  await page.locator('.marker-settings summary').click();assert.equal(await page.locator('.marker-profile-row').count(),4);
  for(const [id,value] of [['marker-open','lc['],['marker-math-open','toan-['],['marker-physics-open','ly-['],['marker-chemistry-open','hoa-[']]) assert.equal(await page.locator('#'+id).inputValue(),value);
  await input('');await flags(0);
  for(const [raw,domain] of [['toan-[x mũ 2]','math'],['ly-[v=10 m/s]','physics'],['hoa-[K4[Fe(CN)6]]','chemistry']]){
   await formula(raw);const data=await snapshot();const set=JSON.parse(JSON.parse(JSON.parse(data.analysis).Regions[0]).payload);assert.equal(set.intent.domain,domain);assert.equal(data.settings.enabledDomains,'None');assert.equal(data.raw,raw);
  }
  await input('lc[x^2]');assert.equal(await page.locator('#formula-preview').count(),0);
 });
 await check('multiple-regions-and-reserved-incomplete',async()=>{
  await page.locator('#mode').selectOption('Markers');await formula('👩‍🏫 toan-[x mu\u0303 2] và ly-[v=10 m/s] rồi hoa-[H2SO4]');assert.equal(await page.locator('.regions button').count(),3);
  const data=await snapshot();assert.deepEqual(JSON.parse(data.analysis).Regions.map(r=>JSON.parse(JSON.parse(r).payload).intent.domain),['math','physics','chemistry']);
  await flags(7);await page.locator('#mode').selectOption('Passive');
  for(const raw of ['hoa-[H2SO4','lc[hoa-[H2SO4]]','hoa-[https://example.com/H2SO4]']){await input(raw);assert.equal(await page.locator('#formula-preview').count(),0,raw);}
  await formula('Cho hoa-[H2SO4] và toan-[x^2]');assert.equal(await page.locator('.regions button').count(),2);await page.locator('#mode').selectOption('Explicit');
 });
 await check('atomic-config-custom-roundtrip',async()=>{
  await formula('hoa-[H2O]');await page.locator('.marker-settings').evaluate(el=>el.open=true);
  await page.locator('#marker-open').fill('hoa-[');await page.locator('#apply-markers').click();await settled();
  assert((await page.locator('.marker-message').textContent()).includes('giữ'));assert.equal((await snapshot()).settings.open,'lc[');assert.equal(await page.locator('#source').inputValue(),'hoa-[H2O]');
  await page.locator('#marker-open').fill('lc-[');await page.locator('#marker-chemistry-open').fill('chem{{');await page.locator('#marker-chemistry-close').fill('}}');await page.locator('#apply-markers').click();await settled();
  await formula('chem{{K4[Fe(CN)6]}}');const before=await svg();await download('#save-document','custom-markers.locus');
  await page.waitForTimeout(600);await page.reload();await ready();await rendered();assert.equal(await svg(),before);
  await page.locator('.marker-settings').evaluate(el=>el.open=true);assert.equal(await page.locator('#marker-chemistry-open').inputValue(),'chem{{');
  await input('hoa-[H2O]');assert.equal(await page.locator('#formula-preview').count(),0);await formula('lc-[x^2]');
  await page.locator('#open-document').setInputFiles(`${out}/custom-markers.locus`);await page.waitForFunction(()=>document.querySelector('#source')?.value==='chem{{K4[Fe(CN)6]}}');await rendered();assert.equal(await svg(),before);
  await page.locator('.marker-settings').evaluate(el=>el.open=true);await page.locator('#marker-open').fill('lc[');await page.locator('#marker-chemistry-open').fill('hoa-[');await page.locator('#marker-chemistry-close').fill(']');await page.locator('#apply-markers').click();await settled();
 });
 await check('native-authored-v3-file-with-all-detectors-off',async()=>{await page.locator('#open-document').setInputFiles('artifacts/sc1/named-chemistry-off.locus');await rendered();assert.equal(await page.locator('#source').inputValue(),'hoa-[H₂SO₄]');assert.equal((await snapshot()).settings.enabledDomains,'None');});
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
  const bytes=await download('#save-document','off-snapshot.locus');assert.equal(JSON.parse(bytes).version,3);assert.equal(JSON.parse(JSON.parse(bytes).payload).settings.enabledDomains,'None');
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
 await check('chemistry-alias-ambiguity-and-zero',async()=>{
  await input('');await flags(0);await page.locator('#mode').selectOption('Explicit');
  await formula('hoa-[h2+o2=h2o]');assert((await latex()).includes('H'));assert.equal(await page.locator('#source').inputValue(),'hoa-[h2+o2=h2o]');
  await input('hoa-[co]');assert.equal(await page.locator('#formula-preview').count(),0);assert((await page.locator('.input-diagnostic').textContent()).includes('Co hoặc CO'));
  await formula('hoa-[h20]');assert((await page.locator('.input-diagnostic').textContent()).includes('Số 0'));assert((await latex()).includes('20'));
 });
 await check('balance-preview-keeps-source-until-accepted',async()=>{
  await formula('hoa-[h2+o2=h2o]');await page.locator('#request-assistance').click();await page.locator('.assistance-choice').waitFor({timeout:20000});
  assert.equal(await page.locator('.assistance-choice').count(),1);assert.equal(await page.locator('#source').inputValue(),'hoa-[h2+o2=h2o]');
  const data=await snapshot();assert(!data.transformations);assert.equal(data.raw,'hoa-[h2+o2=h2o]');await page.screenshot({path:`${out}/balance-preview.png`,fullPage:true});
 });
 await check('accept-balanced-result-one-undo-and-v4',async()=>{
  await page.locator('.assistance-choice').click();await page.waitForFunction(()=>document.querySelector('#source')?.value==='hoa-[2H2+O2->2H2O]');await rendered();
  const data=await snapshot();assert.equal(data.transformations.length,1);assert.equal(data.transformations[0].before.raw,'hoa-[h2+o2=h2o]');assert.equal(JSON.parse(await download('#save-document','accepted-balance.locus')).version,4);
  await page.locator('#undo').click();await page.waitForFunction(()=>document.querySelector('#source')?.value==='hoa-[h2+o2=h2o]');await rendered();
  assert(await page.locator('#source').evaluate(el=>el.selectionStart===el.value.length&&el.selectionEnd===el.value.length));
  await page.locator('#redo').click();await page.waitForFunction(()=>document.querySelector('#source')?.value==='hoa-[2H2+O2->2H2O]');await rendered();
 });
 await check('accepted-file-reload-restore-and-export',async()=>{
  await input('changed');await page.locator('#open-document').setInputFiles(`${out}/accepted-balance.locus`);await page.waitForFunction(()=>document.querySelector('#source')?.value==='hoa-[2H2+O2->2H2O]');await rendered();
  const before=await svg();await page.waitForTimeout(650);await page.reload();await ready();await rendered();assert.equal(await svg(),before);
  assert.equal((await snapshot()).transformations[0].before.raw,'hoa-[h2+o2=h2o]');
  const png=await download('#download-png','balanced.png');assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
  await page.locator('.assistance-history summary').click();await page.locator('#restore-assistance-source').click();await page.waitForFunction(()=>document.querySelector('#source')?.value==='hoa-[h2+o2=h2o]');await rendered();assert(!(await snapshot()).transformations);
 });
 await check('balance-no-solution-and-no-duplicate',async()=>{
  for(const [raw,status] of [['hoa-[H2->H2O]','no-solution'],['hoa-[C+O2->CO+CO2]','non-unique'],['hoa-[2H2+O2->2H2O]','already-balanced'],['hoa-[h2+o2=h20]','needs-interpretation']]){
   await formula(raw);await page.locator('#request-assistance').click();await page.locator(`.assistance-results[data-status="${status}"]`).waitFor();assert.equal(await page.locator('.assistance-choice').count(),0);assert.equal(await page.locator('#source').inputValue(),raw);
  }
 });
 async function assistance(raw,status='needs-conditions'){await input(raw);await page.locator('#request-assistance').click();await page.locator(`.assistance-results[data-status="${status}"]`).waitFor();}
 async function condition(key,value,status='needs-conditions'){await page.locator('#chemistry-condition-'+key).selectOption(value);await settled();await page.locator(`.assistance-results[data-status="${status}"]`).waitFor();}
 await check('catalog-needs-explicit-conditions-and-preview-only',async()=>{
  const raw='hoa-[h2+o2=]';await assistance(raw);assert.equal(await page.locator('.assistance-choice').count(),0);assert.equal(await page.locator('#chemistry-condition-activation').inputValue(),'');
  await condition('activation','ignition','available');assert.equal(await page.locator('.assistance-choice').count(),1);assert((await page.locator('.assistance-choice').textContent()).includes('Bổ sung sản phẩm'));
  assert.equal(await page.locator('#source').inputValue(),raw);assert(!(await snapshot()).transformations);assert.equal(await page.locator('#formula-preview').count(),0);
  assert((await page.locator('.chemistry-references a').first().getAttribute('href')).startsWith('https://openstax.org/'));await page.screenshot({path:`${out}/products-preview.png`,fullPage:true});
 });
 await check('catalog-accept-whole-equation-undo-file-provenance',async()=>{
  await page.locator('.assistance-choice').click();await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[2H2+O2->2H2O]');await rendered();
  const data=await snapshot(),p=JSON.parse(JSON.parse(data.transformations[0].proposal).Payload);assert.equal(data.transformations[0].before.raw,'hoa-[h2+o2=]');assert(p);
  await download('#save-document','accepted-products.locus');await page.locator('#undo').click();await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[h2+o2=]');assert.equal(await page.locator('.assistance-choice').count(),0);
  await page.locator('#redo').click();await rendered();await input('different');await page.locator('#open-document').setInputFiles(`${out}/accepted-products.locus`);await rendered();assert.equal(await page.locator('#source').inputValue(),'hoa-[2H2+O2->2H2O]');
  await page.locator('.assistance-history').evaluate(el=>el.open=true);await page.locator('#restore-assistance-source').click();await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[h2+o2=]');
 });
 await check('catalog-ratio-is-not-an-input-coefficient',async()=>{
  const raw='hoa-[8NaOH+3CO2=]';await assistance(raw);await condition('medium','water');assert.equal(await page.locator('#chemistry-condition-ratio').inputValue(),'');assert.equal(await page.locator('.assistance-choice').count(),0);
  await condition('ratio','two-to-one','available');const carbonate=await page.locator('.assistance-choice').getAttribute('data-proposal-id');assert.equal(await page.locator('#source').inputValue(),raw);
  await condition('ratio','one-to-one','available');assert.notEqual(await page.locator('.assistance-choice').getAttribute('data-proposal-id'),carbonate);await page.locator('.assistance-choice').click();await rendered();assert.equal(await page.locator('#source').inputValue(),'hoa-[NaOH+CO2->NaHCO3]');
  await page.locator('#undo').click();await page.waitForFunction(raw=>document.querySelector('#source').value===raw,raw);await page.locator('#request-assistance').click();await condition('medium','water');await condition('ratio','mixed','unsupported');assert.equal(await page.locator('.assistance-choice').count(),0);
  await input('hoa-[CO2+NaOH=]');await page.locator('.assistance-results[data-status="needs-conditions"]').waitFor();assert.equal(await page.locator('.assistance-choice').count(),0);assert.equal(await page.locator('#chemistry-condition-medium').inputValue(),'');
 });
 await check('catalog-unknown-and-no-reaction-are-distinct',async()=>{
  await assistance('hoa-[Cu+HCl=]','unsupported');assert((await page.locator('.assistance-results').textContent()).includes('không có nghĩa'));
  await assistance('hoa-[NaCl+KNO3=]');await condition('medium','water','no-reaction');assert.equal(await page.locator('.assistance-choice').count(),0);
  await assistance('hoa-[co2+naoh=]','needs-interpretation');assert.equal(await page.locator('.assistance-choice').count(),0);
  await assistance('hoa-[H2+O2⇌]','unsupported');assert.equal(await page.locator('#source').inputValue(),'hoa-[H2+O2⇌]');
 });
 await check('catalog-open-region-in-prose-keeps-neighbor-and-wrapper',async()=>{
  await page.locator('#mode').selectOption('Markers');const raw='Gõ toan-[x+1/2] rồi hoa-[h2+o2=';await assistance(raw);await condition('activation','ignition','available');
  await page.locator('.assistance-choice').click();await page.waitForFunction(()=>document.querySelector('#source').value==='Gõ toan-[x+1/2] rồi hoa-[2H2+O2->2H2O');
  assert.equal(await page.locator('.regions button').count(),0);await page.locator('#undo').click();await page.waitForFunction(raw=>document.querySelector('#source').value===raw,raw);
  await page.locator('#mode').selectOption('Explicit');await formula('hoa-[H2SO4]');
 });
 if(!native)await check('worker-native-parity-743-inputs-and-assistance',async()=>{
  const fixtures=[...JSON.parse(await readFile('artifacts/sc1/worker-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/e3/worker-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/web1/worker-native-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/sc1/chemistry-input-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/sc1/balance-fixtures.json','utf8')),...JSON.parse(await readFile('artifacts/sc1/catalog-fixtures.json','utf8'))];
  const parity=await page.evaluate(async fixtures=>{const {managerFor}=await import(new URL('_content/Locus.Editor/worker-client.js',document.baseURI).href);const m=managerFor(new URL('worker/worker.js',document.baseURI).href);const results=[];for(const f of fixtures){let result,error;try{result=await m.run(f.id,f.request);}catch(e){error=e.message;}results.push({id:f.id,pass:f.error?!!error:result===f.result,error:error??(result!==f.result?'Native/WASM mismatch':null)});}m.dispose();return results;},fixtures);
  await writeFile(`${out}/worker-parity.json`,JSON.stringify(parity,null,2));assert(parity.every(p=>p.pass),parity.filter(p=>!p.pass).slice(0,4).map(p=>p.id+': '+p.error).join('; '));
 });
 if(!native)await check('mobile-and-offline-reload',async()=>{await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));await page.screenshot({path:`${out}/mobile.png`,fullPage:true});await page.setViewportSize({width:1280,height:960});await page.waitForFunction(()=>document.querySelector('#offline-status')?.textContent.includes('offline'));await context.setOffline(true);await page.reload();await ready();await rendered();await formula('hoa-[H2SO4]');await context.setOffline(false);});
 await check('no-unhandled-errors-or-external-data',async()=>{assert.deepEqual(errors,[]);assert(requests.every(u=>u.startsWith('http://127.0.0.1:4185/')||u.startsWith('https://0.0.0.1/')||u.startsWith('blob:')||u.startsWith('data:')),requests.filter(u=>!u.includes('127.0.0.1')&&!u.includes('0.0.0.1')).join('\n'));});
 await page.screenshot({path:`${out}/editor.png`,fullPage:true});
}finally{
 await writeFile(`${out}/scenes.json`,JSON.stringify(scenes,null,2));
 const failed=checks.filter(c=>c.status==='FAIL').length;await writeFile(`${out}/ui.json`,JSON.stringify({capturedAtUtc:new Date().toISOString(),build:observedBuild,expectedBuild,kind,browserVersion:browser.version(),summary:{checks:checks.length,passed:checks.length-failed,failed},checks,errors},null,2));
 await browser.close();process.exitCode=failed?1:0;
}
