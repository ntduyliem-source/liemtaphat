import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const mode=process.argv[2];assert(['telex','vni'].includes(mode));
const out='artifacts/web1/ime/real-os',input=JSON.parse(await readFile(`${out}/${mode}-restored.json`,'utf8'));
const browser=await chromium.connectOverCDP('http://127.0.0.1:9335');
try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('http://127.0.0.1:4183/'));assert(page);
  const state=()=>page.evaluate(()=>({raw:document.querySelector('#source')?.value,candidate:document.querySelector('#formula-preview')?.dataset.candidate,latex:document.querySelector('#latex-output')?.textContent,rendered:document.querySelector('#formula-preview')?.dataset.rendered,draft:document.querySelector('#draft-status')?.textContent}));
  await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:20000});
  const before=await state();assert.equal(before.raw,input.state.raw);assert.equal(before.candidate,input.state.candidate);assert.equal(before.latex,'{x}^{2}');
  const files=[];
  for(const [selector,extension] of [['#download-svg','svg'],['#download-png','png'],['#save-document','locus']]){
    const pending=page.waitForEvent('download');await page.locator(selector).click();const download=await pending;
    const path=`${out}/${mode}.${extension}`;await download.saveAs(resolve(path));const bytes=await readFile(path);
    if(extension==='svg')assert(bytes.toString().includes('<path'));
    if(extension==='png')assert.equal(bytes.subarray(1,4).toString(),'PNG');
    if(extension==='locus'){const document=JSON.parse(bytes.toString()),payload=JSON.parse(document.payload);assert.equal(payload.raw,before.raw);assert.equal(payload.candidateId,before.candidate);assert.equal(createHash('sha256').update(document.payload).digest('hex'),document.sha256);}
    files.push({path,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')});
  }
  await page.waitForFunction(()=>document.querySelector('#draft-status')?.textContent.includes('Đã lưu nháp'));
  await page.reload();await page.locator('.locus-app[data-ready=true]').waitFor({timeout:45000});await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:20000});
  const after=await state();for(const key of ['raw','candidate','latex','rendered'])assert.equal(after[key],before[key],key);
  await page.screenshot({path:resolve(out,mode+'-reload.png'),fullPage:true});
  const report={status:'PASSED',capturedAtUtc:new Date().toISOString(),build:input.state.build,mode,inputEvidence:`${out}/${mode}-restored.json`,method:'Export and reload the exact source entered earlier by OS keys; this script does not enter or replace source.',before,after,files};
  await writeFile(`${out}/${mode}-persistence.json`,JSON.stringify(report,null,2));console.log(JSON.stringify(report));
}finally{await browser.close();}
