import {chromium,firefox} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
const kind=process.argv[2]??'chromium',only=process.argv[3]??'',native=kind==='desktop',out=`artifacts/sc1/ghost/${kind}`;
const build=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8')).root.split(/[\\/]/).at(-1);await mkdir(out,{recursive:true});
const browser=native?await chromium.connectOverCDP('http://127.0.0.1:9334'):await(kind==='firefox'?firefox:chromium).launch({headless:true});
const context=native?browser.contexts()[0]:await browser.newContext({viewport:{width:1280,height:1000},acceptDownloads:true});
const page=native?context.pages().find(p=>p.url()==='https://0.0.0.1/'):await context.newPage();page.setDefaultTimeout(10000);
const checks=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
const source=page.locator('#source');
const baselineRaw=JSON.parse(JSON.parse(await readFile('artifacts/sc1/named-chemistry-off.locus','utf8')).payload).raw;
async function ready(){await page.locator('.locus-app[data-ready=true]').waitFor({timeout:40000});}
async function settled(){await page.waitForTimeout(250);await page.locator('.locus-app[data-busy=false]').waitFor();}
async function input(raw){await source.fill(raw);await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent===`${raw.length} ký tự`,raw);await settled();}
async function status(value){await page.locator(`.assistance-results[data-status="${value}"]`).waitFor();}
async function start(raw='hoa-[h2+o2='){
 await page.locator('#open-document').setInputFiles([]);
 await page.locator('#open-document').setInputFiles('artifacts/sc1/named-chemistry-off.locus');await page.waitForFunction(raw=>document.querySelector('#source').value===raw,baselineRaw);await settled();
 await input('');await input(raw);await status('needs-conditions');await page.locator('#chemistry-condition-activation').selectOption('ignition');await status('available');await settled();
 await source.focus();await page.locator('.locus-app[data-ghost-visible=true]').waitFor();return raw;
}
async function absent(){await page.locator('.source-ghost').waitFor({state:'hidden'});}
async function saved(name){const next=page.waitForEvent('download');await page.locator('#save-document').click();const d=await next;const path=`${out}/${name}.locus`;await d.saveAs(path);return JSON.parse(JSON.parse(await readFile(path,'utf8')).payload);}
async function check(id,action){if(only&&!id.includes(only))return;try{await action();checks.push({id,status:'PASS'});console.log('PASS',id);}catch(e){checks.push({id,status:'FAIL',error:e.message});console.log('FAIL',id,e.message);await page.screenshot({path:`${out}/failure-${checks.length}.png`,fullPage:true}).catch(()=>{});}}
try{
 if(native)await page.reload();else await page.goto('http://127.0.0.1:4185/');await ready();
 const observedBuild=native?JSON.parse(await readFile('artifacts/sc1/desktop-markers-receipt.json','utf8')).executable.split(/[\\/]/).find(s=>/^\d{8}-\d{6}-\d{3}$/.test(s)):new URL(page.url()).pathname.split('/')[2];assert.equal(observedBuild,build);
 await page.locator('#open-document').setInputFiles('artifacts/sc1/named-chemistry-off.locus');await settled();await page.locator('#mode').selectOption('Explicit');
 await page.locator('.keyboard-help').evaluate(el=>el.open=true);await page.locator('#accept-chemistry-space').setChecked(false);
 await check('auto-draft-asks-before-products',async()=>{
  await input('hoa-[h2+o2=');await status('needs-conditions');await absent();assert.equal(await page.locator('.assistance-choice').count(),0);assert.equal(await source.inputValue(),'hoa-[h2+o2=');
 });
 await check('ghost-is-separate-and-full-equation-visible',async()=>{
  const raw=await start();assert.equal(await source.inputValue(),raw);assert.equal(await page.locator('.ghost-products').textContent(),'2H2O');
  assert((await page.locator('.ghost-equation').textContent()).includes('H2+O2 → 2H2+O2'));assert(await page.locator('.ghost-equation math').isVisible());
  await page.screenshot({path:`${out}/ghost.png`,fullPage:true});const data=await saved('before-accept');assert.equal(data.raw,raw);assert(!data.transformations);
 });
 await check('enter-one-undo-and-closed-wrapper',async()=>{
  const raw=await start('hoa-[h2+o2=]');await source.press('Enter');await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[2H2+O2->2H2O]');await settled();
  assert.equal(await page.locator('.input-diagnostic').count(),0);const data=await saved('enter-accepted');assert.equal(data.transformations.length,1);assert.equal(data.transformations[0].before.raw,raw);
  await page.locator('#undo').click();await page.waitForFunction(raw=>document.querySelector('#source').value===raw,raw);assert(await source.evaluate(el=>el.selectionStart===el.value.length));
  await absent();await page.locator('#redo').click();await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[2H2+O2->2H2O]');
 });
 await check('open-wrapper-stays-open-and-next-input-kept',async()=>{
  const raw=await start();await source.press('Enter');await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[2H2+O2->2H2O');
  await source.pressSequentially('] thêm',{delay:15});await settled();assert.equal(await source.inputValue(),'hoa-[2H2+O2->2H2O] thêm');
  assert.equal((await saved('typed-after-accept')).transformations.at(-1).before.raw,raw);
 });
 await check('space-default-does-not-accept',async()=>{
  const raw=await start();await source.press('Space');await settled();assert.equal(await source.inputValue(),raw+' ');assert(!(await saved('space-default')).transformations);await absent();
 });
 await check('space-opt-in-one-command-and-preferences',async()=>{
  await page.locator('#accept-chemistry-space').setChecked(true);const raw=await start('hoa-[h2+o2=]');await source.press('Space');await page.waitForFunction(()=>document.querySelector('#source').value==='hoa-[2H2+O2->2H2O] ');await settled();
  await page.locator('#undo').click();await page.waitForFunction(raw=>document.querySelector('#source').value===raw,raw);await page.waitForTimeout(600);await page.reload();await ready();
  await page.locator('.keyboard-help').evaluate(el=>el.open=true);assert(await page.locator('#accept-chemistry-space').isChecked());await page.locator('#accept-chemistry-space').setChecked(false);
 });
 await check('escape-dismiss-until-fx-or-source-change',async()=>{
  const raw=await start();await source.press('Escape');await absent();await source.press('Enter');await settled();assert.equal(await source.inputValue(),raw+'\n');
  await input('');await input(raw);await status('needs-conditions');assert.equal(await page.locator('#chemistry-condition-activation').inputValue(),'');
 });
 await check('selection-blur-and-shift-enter-do-not-accept',async()=>{
  let raw=await start();await source.press('ArrowLeft');await source.press('Enter');await settled();assert.equal(await source.inputValue(),raw.slice(0,-1)+'\n=');
  raw=await start();await source.press('Tab');await absent();await source.focus();await source.press('Enter');await settled();assert.equal(await source.inputValue(),raw+'\n');
  raw=await start();await source.press('Shift+Enter');await settled();assert.equal(await source.inputValue(),raw+'\n');
 });
 await check('enter-before-visible-not-replayed',async()=>{
  await input('');await source.pressSequentially('hoa-[H2+O2=',{delay:1});await source.press('Enter');await settled();assert.equal(await source.inputValue(),'hoa-[H2+O2=\n');assert(!(await saved('early-enter')).transformations);
 });
 await check('composition-and-repeat-do-not-consume-key',async()=>{
  let raw=await start();const repeated=await source.evaluate(el=>{const e=new KeyboardEvent('keydown',{key:'Enter',repeat:true,bubbles:true,cancelable:true});el.dispatchEvent(e);return e.defaultPrevented;});assert.equal(repeated,false);await absent();assert.equal(await source.inputValue(),raw);
  raw=await start();await source.dispatchEvent('compositionstart');const during=await source.evaluate(el=>{const e=new KeyboardEvent('keydown',{key:'Enter',isComposing:true,bubbles:true,cancelable:true});el.dispatchEvent(e);return e.defaultPrevented;});assert.equal(during,false);await absent();await source.dispatchEvent('compositionend');await settled();assert.equal(await source.inputValue(),raw);
 });
 await check('quick-edit-cancels-preparation-without-changing-source',async()=>{
  const raw=await start();await source.evaluate(el=>{el.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));el.value+='H';el.setSelectionRange(el.value.length,el.value.length);el.dispatchEvent(new InputEvent('input',{bubbles:true,inputType:'insertText',data:'H'}));});
  await settled();assert.equal(await source.inputValue(),raw+'H');assert(!(await saved('quick-edit')).transformations);
 });
 await check('source-edit-and-domain-change-clear-conditions',async()=>{
  await start();await input('hoa-[HCl+NaOH=]');await status('needs-conditions');assert.equal(await page.locator('#chemistry-condition-medium').inputValue(),'');
  await page.locator('#detect-math').setChecked(true);await absent();assert.equal(await page.locator('.assistance-choice').count(),0);
 });
 await check('delayed-prepare-rechecks-live-dom-before-commit',async()=>{
  const results=await page.evaluate(async()=>{
   const {bindGhost}=await import(new URL('_content/Locus.Editor/chemistry-ghost.js',document.baseURI));const results=[];
   for(const mutation of ['source','selection','blur','composition','settings','conditions','valid-next-input']){
    const host=document.createElement('div');host.className='locus-app';host.dataset.version='5';host.dataset.busy='false';host.dataset.inputPending='false';
    const shell=document.createElement('div');shell.className='source-shell';host.append(shell);const el=document.createElement('textarea');el.value='H2+O2=';shell.append(el);
    const overlay=document.createElement('div');overlay.className='source-ghost';const hint=document.createElement('p');hint.className='ghost-hint';shell.append(overlay,hint);document.body.append(host);
    let composing=false,serial=0,chain=Promise.resolve(),resolvePrepare,resolveCommit,commits=0,cancels=0;
    const prepare=new Promise(r=>resolvePrepare=r),commit=new Promise(r=>resolveCommit=r);
    const reference={invokeMethodAsync(name){if(name==='PrepareGhost')return prepare;if(name==='CommitGhost'){commits++;return commit;}if(name==='CancelGhost')cancels++;return Promise.resolve();}};
    const binding=bindGhost(el,reference,action=>{chain=chain.then(action);},()=>composing,()=>serial);
    el.addEventListener('keydown',binding.keydown);el.focus();el.setSelectionRange(el.value.length,el.value.length);
    const p={id:'owned-test-proposal',epoch:1,version:5,raw:el.value,start:el.value.length,end:el.value.length,closedEnd:-1,products:'2H2O',mathMl:'<math><mi>H</mi></math>',leftChange:'',space:false};binding.set(p);
    const event=new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true});el.dispatchEvent(event);await Promise.resolve();
    if(mutation==='source'){el.value+='H';serial++;binding.invalidate();}
    if(mutation==='selection')el.setSelectionRange(0,2);
    if(mutation==='blur')el.blur();
    if(mutation==='composition')composing=true;
    if(mutation==='settings')host.dataset.version='6';
    if(mutation==='conditions')binding.set({...p,epoch:2,id:'other-result'});
    resolvePrepare({token:'prepared-1',raw:'2H2+O2->2H2O',start:12,end:12});
    await Promise.resolve();await Promise.resolve();
    if(mutation==='valid-next-input'){el.value+='] tiếp';serial++;host.dataset.inputPending='true';}
    resolveCommit(true);await chain;
    results.push({mutation,consumed:event.defaultPrevented,commits,cancels,raw:el.value,pending:host.dataset.inputPending});binding.dispose();host.remove();
   }return results;
  });
  for(const result of results){assert(result.consumed,result.mutation);if(result.mutation==='valid-next-input'){assert.equal(result.commits,1);assert.equal(result.raw,'2H2+O2->2H2O] tiếp');assert.equal(result.pending,'true');}else{assert.equal(result.commits,0,result.mutation);assert.equal(result.cancels,1,result.mutation);assert.equal(result.raw,result.mutation==='source'?'H2+O2=H':'H2+O2=',result.mutation);}}
  await writeFile(`${out}/delayed-transactions.json`,JSON.stringify(results,null,2));
 });
 await check('no-unhandled-errors',async()=>assert.deepEqual(errors,[]));
}finally{
 const failed=checks.filter(c=>c.status==='FAIL').length;await writeFile(`${out}/${only?'targeted-'+only:'report'}.json`,JSON.stringify({capturedAtUtc:new Date().toISOString(),build,kind,browserVersion:browser.version(),summary:{checks:checks.length,passed:checks.length-failed,failed},checks,errors,scope:'Published shared editor, real browser key events plus synthetic composition/race guards. Not OS Telex/VNI evidence.'},null,2));
 await browser.close();process.exitCode=failed?1:0;
}
