import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';

const url=process.env.LOCUS_REPAIR_URL??'http://127.0.0.1:4203/';
const output='artifacts/repair-20261013/web';await mkdir(output,{recursive:true});
const browser=await chromium.launch(),context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});
await context.grantPermissions(['clipboard-read','clipboard-write']);
const page=await context.newPage(),results=[],errors=[];page.setDefaultTimeout(15000);
page.on('pageerror',error=>errors.push(error.message));
async function check(name,action){try{await action();results.push({name,passed:true});console.log('PASS',name);}catch(error){results.push({name,passed:false,error:error.stack});console.error('FAIL',name,error.message);await page.screenshot({path:`${output}/failure-${results.length}.png`}).catch(()=>{});}}
const ready=()=>page.locator('.locus-app[data-ready=true]').waitFor({timeout:60000});
async function mode(name){
  await page.locator(`#detect-${name}`).click();
  await page.waitForFunction(name=>document.querySelector(`#detect-${name}`)?.getAttribute('aria-pressed')==='true'&&document.querySelector('.locus-app')?.dataset.busy==='false',name);
}
async function input(raw,count){
  await page.locator('#source').fill(raw);
  // Ctrl+Enter invokes the supported explicit analysis command after the input queue.
  await page.locator('#source').press('Control+Enter');
  await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent.endsWith(`${raw.length} ký tự`)&&document.querySelector('.locus-app')?.dataset.inputPending!=='true'&&document.querySelector('.locus-app')?.dataset.busy==='false',raw);
  if(count!==undefined)await page.waitForFunction(n=>document.querySelectorAll('#mixed-result .result-region').length===n,count);
}
async function download(selector,name){const event=page.waitForEvent('download');await page.locator(selector).click();const d=await event;const file=`${output}/${name}`;await d.saveAs(file);return readFile(file);}
const mathText=()=>page.locator('#mixed-result').textContent();
try{
  await page.goto(url);await ready();
  await check('stable-root-and-math-recognition',async()=>{
    assert.equal(page.url(),url);await mode('all');
    await input('Tính “x^2+1”. Xét (x-1/2).',2);
    assert.equal(await page.locator('#mixed-result math').count(),2);
    assert.equal(await page.locator('#mixed-result [data-text-start]').first().textContent(),'Tính “');
    await input('sin (x+1); y=2',1);assert((await mathText()).startsWith('sin (x+1); '));
  });
  await check('physics-exponent-and-arithmetic',async()=>{
    await mode('physics');await input('10 kg*(m/s)^2',1);
    const base=await page.locator('#mixed-result msup').first().evaluate(n=>n.firstElementChild.textContent);
    assert(!base.includes('10')&&!base.includes('kg'));assert(base.includes('m')&&base.includes('s'));
    for(const raw of ['10 kg * 2','10 kg*(m+1)','10 kg/t']){await input(raw,1);assert.equal(await page.locator('#mixed-result math').count(),1);}
  });
  await check('rapid-mode-change-retains-choice-during-input',async()=>{
    await mode('math');
    await page.locator('#detect-physics').click();
    // Deliberately type before the old-source worker finishes. The clicked mode
    // must already be part of state and must survive cancellation by this input.
    await input('10 kg*(m/s)^2',1);
    assert.equal(await page.locator('#detect-physics').getAttribute('aria-pressed'),'true');
    const base=await page.locator('#mixed-result msup').first().evaluate(n=>n.firstElementChild.textContent);
    assert(!base.includes('10')&&!base.includes('kg')&&base.includes('m')&&base.includes('s'));
  });
  await check('complete-chemistry-one-product-and-undo',async()=>{
    await mode('all');const raw='Phản ứng H2+O2= 2 H2O.';await input(raw,1);
    await page.locator('#balance-step').click();await page.waitForFunction(()=>document.querySelector('#balance-step')?.textContent.includes('Hủy cân bằng'));
    assert.equal(await page.locator('#mixed-result .result-region').count(),1);
    assert.equal((await mathText()).replace(/\s/g,''),'Phảnứng2H2+O2→2H2O.');
    assert.equal(await page.locator('#source').inputValue(),raw);
    await page.locator('#undo').click();assert.equal(await page.locator('#source').inputValue(),raw);
    await page.locator('#redo').click();await page.waitForFunction(()=>document.querySelector('#balance-step')?.textContent.includes('Hủy cân bằng'));
  });
  await check('unicode-fill-cancel-drop-preserves-source',async()=>{
    const raw='Phản ứng H₂+O₂=';await input(raw,1);await page.locator('#balance-step').click();
    await page.waitForFunction(()=>document.querySelector('#balance-step')?.textContent.includes('Hủy cân bằng'));
    await page.locator('#mixed-result .region-content').click();
    await page.locator('#balance-step').click();await page.locator('#drop-products').waitFor();
    assert((await mathText()).includes('O'));assert.equal(await page.locator('#source').inputValue(),raw);
    await page.locator('#drop-products').click();await page.waitForFunction(raw=>document.querySelector('#mixed-result')?.textContent===raw,raw);
  });
  await check('repair-selection-and-image-exports',async()=>{
    await mode('math');await input('x^2+1/2',1);
    await page.locator('#mixed-result .region-content').click();await page.locator('.suggestion-chip').first().click();
    await page.locator('#restore-direct').waitFor();assert.equal(await page.locator('#source').inputValue(),'x^2+1/2');
    const svg=await download('#download-svg','selected.svg');assert.match(svg.toString(),/<svg/);assert(!svg.toString().includes('fx'));
    const png=await download('#download-png','selected.png');assert.equal(png.subarray(1,4).toString(),'PNG');
    await page.locator('#copy-png').click();
    await page.waitForFunction(()=>Array.from(document.querySelectorAll('[role=status]')).some(n=>n.textContent.includes('Đã copy ảnh.')));
    const clipboard=await page.evaluate(async()=>{const entries=await navigator.clipboard.read();return entries.flatMap(x=>x.types);});assert(clipboard.includes('image/png'));
    await input('Bài toán: x^2+1. Giữ nguyên câu này.',1);await page.locator('#select-result-all').click();
    await download('#download-svg','paragraph.svg');await download('#download-png','paragraph.png');
    await page.locator('#mixed-result .region-content').click();assert.equal(await page.locator('#select-result-all').getAttribute('aria-pressed'),'false');
    await page.screenshot({path:`${output}/formula.png`,fullPage:true});
  });
  await check('plot-parameter-and-svg',async()=>{
    await page.locator('#plot-tab').click();await page.getByRole('button',{name:'＋ Vẽ mới',exact:true}).click();
    await page.getByRole('textbox',{name:'Nhập hàm 1',exact:true}).fill('a*x^2+1');
    await page.locator('#value-a').fill('2');await page.locator('#value-a').blur();
    const button=page.locator('.plot-workspace .plot-export').getByRole('button',{name:'Tải SVG',exact:true});
    await button.waitFor();await page.waitForFunction(()=>!Array.from(document.querySelectorAll('.plot-export button')).find(b=>b.textContent==='Tải SVG')?.disabled);
    const event=page.waitForEvent('download');await button.click();await(await event).saveAs(`${output}/plot.svg`);
    assert.match(await readFile(`${output}/plot.svg`,'utf8'),/<path/);
  });
  await check('geometry-gesture-undo-and-svg',async()=>{
    await page.locator('#geometry-tab').click();await page.locator('.geo-toolbar details').filter({has:page.locator('summary',{hasText:'Điểm'})}).locator('summary').click();
    await page.getByRole('button',{name:'Điểm tự do',exact:true}).click();await page.locator('.geo-surface').click({position:{x:230,y:210}});
    await page.waitForFunction(()=>document.querySelector('.plot-export span')?.textContent.startsWith('1 điểm'));
    await page.locator('.geo-toolbar summary').filter({hasText:/^Đường$/}).click();await page.getByRole('button',{name:'Đoạn thẳng',exact:true}).click();
    await page.locator('.geo-surface').click({position:{x:360,y:270}});
    await page.waitForFunction(()=>document.querySelector('.plot-export span')?.textContent.startsWith('2 điểm'));
    await page.getByRole('button',{name:'Hoàn tác hình học',exact:true}).click();
    await page.waitForFunction(()=>document.querySelector('.plot-export span')?.textContent.startsWith('1 điểm'));
    const event=page.waitForEvent('download');await page.locator('.geometry-workspace .plot-export').getByRole('button',{name:'Tải SVG',exact:true}).click();await(await event).saveAs(`${output}/geometry.svg`);
    await page.screenshot({path:`${output}/geometry.png`,fullPage:true});
  });
  await check('no-runtime-errors',async()=>assert.deepEqual(errors,[]));
}catch(error){results.push({name:'startup-or-harness',passed:false,error:error.stack});console.error(error.message);}
finally{
  await writeFile(`${output}/results.json`,JSON.stringify({status:results.length>0&&results.every(r=>r.passed)?'PASSED':'FAILED',url,results,errors},null,2));
  await browser.close();
}
process.exitCode=results.some(r=>!r.passed)?1:0;
