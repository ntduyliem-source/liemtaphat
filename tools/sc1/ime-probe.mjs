// Test preparation and observation only. Measured input is sent separately through OS keys.
import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
const [host,action,name,expected]=process.argv.slice(2);
assert(['desktop','wasm'].includes(host));
const build=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8')).root.split(/[\\/]/).at(-1);
const browser=await chromium.connectOverCDP(`http://127.0.0.1:${host==='desktop'?9334:9335}`);
const pages=browser.contexts()[0].pages();
const page=host==='wasm'&&action==='setup'&&pages.length===1?pages[0]:pages.find(p=>host==='desktop'?p.url()==='https://0.0.0.1/':p.url().startsWith('http://127.0.0.1:'));
assert(page,'Owned probe page required');
const source=page.locator('#source'),out=`artifacts/sc1/ime/${build}/${host}`;await mkdir(out,{recursive:true});
try{
 if(action==='setup'){
  if(host==='wasm')await page.goto('http://127.0.0.1:4185/');
  await page.locator('.locus-app[data-ready=true]').waitFor({timeout:45000});
  await page.locator('#open-document').setInputFiles([]);await page.locator('#open-document').setInputFiles('artifacts/sc1/named-chemistry-off.locus');
  await page.locator('#mode').selectOption('Markers');
  await page.locator('.keyboard-help').evaluate(el=>el.open=true);await page.locator('#accept-chemistry-space').setChecked(false);
  await source.fill('');await source.focus();
 }
 if(action==='prepare'){
  await source.fill('');
  if(name==='math'){await source.fill('toan-[');await source.focus();}
  else if(name==='empty'){await source.focus();}
  else if(name==='ghost'||name==='space'){
   await page.locator('#accept-chemistry-space').setChecked(name==='space');
   await source.fill('hoa-[h2+o2=]');await page.locator('.assistance-results[data-status="needs-conditions"]').waitFor();
   await page.locator('#chemistry-condition-activation').selectOption('ignition');await page.locator('.assistance-results[data-status="available"]').waitFor();
   await source.focus();await source.evaluate(el=>el.setSelectionRange(el.value.length,el.value.length));await page.locator('.locus-app[data-ghost-visible=true]').waitFor();
  }else throw Error('Unknown preparation');
 }
 if(action==='setup'||action==='prepare'){
  await source.evaluate(el=>{
   globalThis.__sc1OsEvents=[];
   if(el.dataset.sc1OsObserved)return;el.dataset.sc1OsObserved='true';
   for(const type of ['keydown','keyup','beforeinput','input','compositionstart','compositionend'])el.addEventListener(type,e=>{
    const row={type,key:e.key??null,inputType:e.inputType??null,composing:e.isComposing??false,trusted:e.isTrusted,repeat:e.repeat??false,raw:el.value,start:el.selectionStart,end:el.selectionEnd,time:Date.now()};
    queueMicrotask(()=>{row.prevented=e.defaultPrevented;globalThis.__sc1OsEvents.push(row);});
   });
  });
 }
 await page.waitForTimeout(400);
 const state=await page.evaluate(()=>({url:location.href,build:document.querySelector('meta[name=locus-build]')?.content??null,host:document.querySelector('.locus-app')?.dataset.host,raw:document.querySelector('#source')?.value,latex:document.querySelector('#latex-output')?.textContent??null,active:document.activeElement?.id,focused:document.hasFocus(),ghost:document.querySelector('.locus-app')?.dataset.ghostVisible==='true',pending:document.querySelector('.locus-app')?.dataset.inputPending,space:document.querySelector('#accept-chemistry-space')?.checked,events:globalThis.__sc1OsEvents??[]}));
 if(host==='wasm'){assert.equal(state.build,build);assert.equal(state.host,'browser');}else assert.equal(state.host,'desktop');
 if(action==='capture'){
  assert(/^[a-z0-9-]+$/.test(name));assert.equal(state.raw,expected);assert(state.events.some(e=>e.trusted),'OS events not observed');
  const report={capturedAtUtc:new Date().toISOString(),status:'PASS',build,host,browserVersion:browser.version(),input:'OS keys through sky.press_key, with no Playwright key injection in measured sequence',name,state};
  await writeFile(`${out}/${name}.json`,JSON.stringify(report,null,2));await page.screenshot({path:`${out}/${name}.png`,fullPage:true});
 }
 console.log(JSON.stringify({...state,events:state.events.length,lastEvents:state.events.slice(-5)}));
}finally{await browser.close();}
