import { chromium, firefox } from 'playwright';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const kind=process.argv[2]??'chromium', hybrid=kind==='hybrid',webview=kind==='webview-wasm',cdp=hybrid||webview;
const output=new URL(`../../artifacts/web/runs/${kind}/`,import.meta.url);await mkdir(output,{recursive:true});
const browser=cdp?await chromium.connectOverCDP(`http://127.0.0.1:${hybrid?9311:9313}`):await (kind==='firefox'?firefox:chromium).launch({headless:true});
const context=cdp?browser.contexts()[0]:await browser.newContext({viewport:{width:1366,height:900},acceptDownloads:true});
const ownedPages=cdp?context.pages().filter(p=>p.url()===(hybrid?'https://0.0.0.1/':'http://127.0.0.1:4181/')):[];
if(cdp&&ownedPages.length!==1)throw new Error(`Expected one owned editor page, found ${ownedPages.length}`);
const page=cdp?ownedPages[0]:await context.newPage();
page.setDefaultTimeout(10000);page.setDefaultNavigationTimeout(15000);
const results=[], errors=[], requests=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push({method:r.method(),url:r.url()}));
async function check(id,run){try{await run();results.push({id,status:'PASS'});console.log('PASS',id);}catch(e){results.push({id,status:'FAIL',error:e.message});console.log('FAIL',id,e.message);}}
async function ready(){await page.locator('[data-ready="true"]').waitFor({timeout:60000});}
async function input(raw){await page.locator('#source').fill(raw);await page.waitForFunction(v=>document.querySelector('#source-count')?.textContent===`${v} ký tự`,raw.length);}
async function waitFormula(text){await page.waitForFunction(v=>document.querySelector('#formula-preview')?.textContent.replace(/\s+/g,'').includes(v),text);}
const beforeLoad=performance.now();if(cdp)await page.reload();else await page.goto('http://127.0.0.1:4181/');await ready();const firstReadyMs=performance.now()-beforeLoad;
try {
  await check('core/parity-116',async()=>{
    const actual=await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Parity'));
    await writeFile(new URL('parity.json',output),actual);
    assert.equal(actual,await readFile(new URL('../../artifacts/web/native/parity.json',import.meta.url),'utf8'));
  });
  await check('core/contract-outcomes',async()=>{
    const report=JSON.parse(await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Contracts')));
    await writeFile(new URL('contracts.json',output),JSON.stringify(report,null,2));
    assert.equal(report.summary.checks,238);
    const failed=report.results.filter(r=>r.status==='FAIL');
    assert.deepEqual(failed,[]);
    const scheduling=JSON.parse(await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Scheduling')));
    await writeFile(new URL('scheduling.json',output),JSON.stringify(scheduling,null,2));
    results.push({id:'core/timer-delivery-observation',status:'OBSERVED',...scheduling});
  });
  await check('formula/direct-and-repair',async()=>{
    await input('x+1/2');await waitFormula('x+12');
    const direct=await page.locator('#formula-preview').getAttribute('data-candidate');
    await page.locator('#show-candidates').click();await page.locator('.candidate-list button').first().waitFor();assert.equal(await page.locator('.candidate-list button').count(),2);
    await page.locator('.candidate-list button').nth(1).click();
    await page.waitForFunction(v=>document.querySelector('#formula-preview')?.dataset.candidate!==v,direct);
    assert.notEqual(await page.locator('#formula-preview').getAttribute('data-candidate'),direct);
    assert.equal(await page.locator('.candidate-label').innerText(),'Đề nghị sửa');
    assert.equal(await page.locator('#source').inputValue(),'x+1/2');
  });
  await check('formula/snapshot-download',async()=>{
    const [download]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:'Tải snapshot'}).click()]);
    await download.saveAs(fileURLToPath(new URL('chosen-snapshot.json',output)));
    const envelope=JSON.parse(await readFile(new URL('chosen-snapshot.json',output),'utf8')), payload=JSON.parse(envelope.payload);
    assert.equal(payload.source.raw,'x+1/2');assert.equal(payload.selectedCandidateId,await page.locator('#formula-preview').getAttribute('data-candidate'));
  });
  await check('formula/nfd-preserved',async()=>{await input('x mu\u0303 2');await waitFormula('x2');assert.equal(await page.locator('#source').inputValue(),'x mu\u0303 2');});
  await check('formula/composition-events',async()=>{
    await page.locator('#source').evaluate(el=>{el.dispatchEvent(new CompositionEvent('compositionstart',{bubbles:true}));el.value='x m';el.dispatchEvent(new InputEvent('input',{bubbles:true,isComposing:true}));});
    await page.locator('#formula-empty').waitFor();assert.equal(await page.locator('#formula-preview').count(),0);
    await page.locator('#source').evaluate(el=>{el.value='x mũ 2';el.dispatchEvent(new CompositionEvent('compositionend',{bubbles:true,data:'ũ'}));el.dispatchEvent(new InputEvent('input',{bubbles:true}));});
    await waitFormula('x2');assert.equal(await page.locator('#source').inputValue(),'x mũ 2');
  });
  await check('formula/custom-markers-multiple-regions',async()=>{
    await page.locator('#mode').selectOption('Markers');await page.locator('#marker-open').fill('toan[[');await page.locator('#marker-open').press('Tab');await page.locator('#marker-close').fill(']]');await page.locator('#marker-close').press('Tab');
    const raw='👩‍🏫 e\u0301 toan[[x^2]] và toan[[1 trên 2]].';await input(raw);await waitFormula('x2');
    assert.equal(await page.locator('.regions button').count(),2);await page.locator('.regions button').nth(1).click();await waitFormula('12');assert.equal(await page.locator('#source').inputValue(),raw);
    await page.locator('#mode').selectOption('Explicit');
  });
  await check('formula/rapid-input-and-oversized-guard',async()=>{
    await input('q^7');await waitFormula('q7');
    await page.locator('#source').evaluate(el=>{for(const v of ['x+1/2','1 trên 2','z^9']){el.value=v;el.dispatchEvent(new InputEvent('input',{bubbles:true}));}});await waitFormula('z9');
    const long='1'.repeat(6000)+'^2';await input(long);await page.locator('#formula-empty').waitFor();assert.equal(await page.locator('#formula-preview').count(),0);assert.equal(await page.locator('#source').inputValue(),long);assert.match(await page.locator('#formula-empty').innerText(),/4.096/);
    await input('x mũ 2 + 1');await waitFormula('x2+1');
  });
  await page.screenshot({path:fileURLToPath(new URL('formula.png',output)),fullPage:true});
  await check('scene/drag-undo-redo',async()=>{
    await page.getByRole('button',{name:'△ Mặt phẳng thử'}).click();await page.locator('#scene').waitFor();
    const before=await page.locator('#scene-state').innerText();const c=page.locator('[data-point="C"] circle'),box=await c.boundingBox();
    await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();await page.mouse.move(box.x+70,box.y+35,{steps:8});await page.mouse.up();
    await page.waitForFunction(v=>document.querySelector('#scene-state').textContent!==v,before);const moved=await page.locator('#scene-state').innerText();
    await page.locator('#undo-scene').click();await page.waitForFunction(v=>document.querySelector('#scene-state').textContent===v,before);assert.equal(await page.locator('#scene-state').innerText(),before);assert.ok(await page.locator('#undo-scene').isDisabled());
    await page.locator('#redo-scene').click();await page.waitForFunction(v=>document.querySelector('#scene-state').textContent===v,moved);assert.equal(await page.locator('#scene-state').innerText(),moved);
  });
  await check('scene/escape-cancels-drag',async()=>{
    const before=await page.locator('#scene-state').innerText(),c=page.locator('[data-point="C"] circle'),box=await c.boundingBox(),original=await c.getAttribute('cx');
    await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.down();await page.mouse.move(box.x-60,box.y+40,{steps:4});await page.keyboard.press('Escape');await page.mouse.up();
    assert.equal(await page.locator('#scene-state').innerText(),before);assert.equal(await c.getAttribute('cx'),original);
  });
  await check('scene/svg-export-matches-points',async()=>{
    const [download]=await Promise.all([page.waitForEvent('download'),page.locator('#download-svg').click()]);await download.saveAs(fileURLToPath(new URL('triangle.svg',output)));
    const svg=await readFile(new URL('triangle.svg',output),'utf8');const p=await page.locator('[data-point="C"]').evaluate(el=>({x:el.dataset.x,y:el.dataset.y}));
    assert.ok(svg.includes(`cx="${p.x}" cy="${p.y}"`));assert.match(svg,/viewBox="0 0 560 340"/);
  });
  await page.screenshot({path:fileURLToPath(new URL('canvas.png',output)),fullPage:true});
  await check('jsxgraph/numeric-curve-adapter',async()=>{
    await page.locator('.technical summary').click();await page.locator('#jsxgraph-start').click();await page.locator('#jsxgraph svg').waitFor();
    assert.equal(await page.evaluate(()=>JXG.version),'1.13.3');assert.ok(await page.locator('#jsxgraph svg path').count()>1);
    await page.screenshot({path:fileURLToPath(new URL('jsxgraph.png',output)),fullPage:true});
  });
  await check('formula/return-to-tab',async()=>{await page.getByRole('button',{name:'ƒ Công thức',exact:true}).click();await input('can2');await waitFormula('2');});
  await check('lifecycle/reload-editor',async()=>{await page.reload();await ready();await input('x^3');await waitFormula('x3');});
  if(!hybrid)await check('offline/analysis-after-loaded',async()=>{
    const count=requests.length;await context.setOffline(true);await input('q^7+1');await waitFormula('q7+1');assert.equal(requests.length,count);await context.setOffline(false);
  });
  const timing=JSON.parse(await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Timing')));
  await writeFile(new URL('timing.json',output),JSON.stringify({firstReadyMs,...timing},null,2));
  await check('network/no-analysis-backend',async()=>assert.deepEqual(requests.filter(r=>r.method!=='GET'||/\/analyze|signalr|_blazor\b/.test(r.url)),[]));
  await check('javascript/no-unhandled-errors',async()=>assert.deepEqual(errors,[]));
}finally{
  await writeFile(new URL('verification.json',output),JSON.stringify({capturedAtUtc:new Date().toISOString(),host:kind,browser:browser.version(),passed:results.filter(r=>r.status==='PASS').length,failed:results.filter(r=>r.status==='FAIL').length,observed:results.filter(r=>r.status==='OBSERVED').length,results,errors,requests,limits:['Playwright pointer and keyboard automation; composition test dispatches synthetic composition/input events, not a physical Telex/VNI acceptance run.','The original 1 ms timer assertion was timing-dependent even on native. It is replaced by deterministic signalled-token coverage; scheduling observations are separate and do not certify timer interruption.','The browser offline check occurs after assets are loaded, without testing offline reload or PWA cache updates.']},null,2));
  await browser.close();
}
if(results.some(r=>r.status==='FAIL'))process.exitCode=1;
