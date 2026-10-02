import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
const [mode,stage,raw,latex]=process.argv.slice(2);
assert(['telex','vni','restored'].includes(mode));assert(/^[a-z-]+$/.test(stage));assert.notEqual(raw,undefined);
const browser=await chromium.connectOverCDP('http://127.0.0.1:9335');
try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('http://127.0.0.1:4183/'));assert(page);
  if(latex)await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:15000});
  const state=await page.evaluate(()=>({url:location.href,build:document.querySelector('meta[name=locus-build]')?.content,host:document.querySelector('.locus-app')?.dataset.host,raw:document.querySelector('#source')?.value,latex:document.querySelector('#latex-output')?.textContent??null,candidate:document.querySelector('#formula-preview')?.dataset.candidate??null,rendered:document.querySelector('#formula-preview')?.dataset.rendered??null,canExport:!!document.querySelector('#download-svg:not(:disabled)'),draft:document.querySelector('#draft-status')?.textContent,active:document.activeElement?.id,documentFocused:document.hasFocus()}));
  assert.equal(state.host,'browser');assert.equal(state.build,'20260914-070957-601');assert.equal(state.raw,raw);
  if(latex){assert.equal(state.latex,latex);assert.equal(state.rendered,'true');assert.equal(state.canExport,true);}else{assert.equal(state.latex,null);assert.equal(state.canExport,false);}
  const events=JSON.parse(await readFile('artifacts/web1/ime/wasm-events.json','utf8'));
  assert(events.some(e=>e.type==='input'&&e.trusted),'No observed trusted input');
  const out='artifacts/web1/ime/real-os';await mkdir(out,{recursive:true});
  const report={status:'PASSED',capturedAtUtc:new Date().toISOString(),mode,stage,engine:'Published WEB1 WASM in Windows WebView2',browser:browser.version(),input:'OS keys through sky.press_key; this script only observes the page',state,events};
  await writeFile(`${out}/${mode}-${stage}.json`,JSON.stringify(report,null,2));
  await page.screenshot({path:`${out}/${mode}-${stage}.png`,fullPage:true});
  console.log(JSON.stringify({status:report.status,mode,stage,state,events:events.length}));
}finally{await browser.close();}
