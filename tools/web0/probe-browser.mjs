import {chromium} from 'playwright';
import {writeFile,readFile,mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
// Keep the original WEB0 reports immutable when this exploratory runner is reused.
const out=new URL(`../../artifacts/web/exploratory/${Date.now()}/`,import.meta.url);await mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true});
try{
  const page=await browser.newPage({viewport:{width:1366,height:900}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:4181/');await page.locator('[data-ready="true"]').waitFor({timeout:60000});
  const parity=await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Parity'));
  await writeFile(new URL('browser-parity.json',out),parity);
  const native=await readFile(new URL('../../artifacts/web/native/parity.json',import.meta.url),'utf8');
  console.log('Raw parity equal:',parity===native,'Sources:',JSON.parse(parity).length);
  const contracts=await page.evaluate(()=>DotNet.invokeMethodAsync('Locus.WebProbe.Shared','Contracts'));
  await writeFile(new URL('browser-contracts.json',out),contracts);
  console.log(JSON.parse(contracts).summary);console.log(JSON.parse(contracts).results.filter(r=>r.status!=='PASS'));
  await page.screenshot({path:new URL('first-editor.png',out).pathname.replace(/^\/(\w:)/,'$1'),fullPage:true});
  await writeFile(new URL('first-browser-check.json',out),JSON.stringify({parityEqual:parity===native,sources:JSON.parse(parity).length,errors,browser:browser.version()},null,2));
  assert.equal(parity,native,'Native/browser semantic output differs.');
}finally{await browser.close();}
