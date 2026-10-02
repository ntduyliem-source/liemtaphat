import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const host=process.argv[2]??'web',desktop=host==='desktop';
const browser=desktop?await chromium.connectOverCDP('http://127.0.0.1:9334'):await chromium.launch({headless:true});
const context=desktop?browser.contexts()[0]:await browser.newContext();
const page=desktop?context.pages().find(p=>p.url()==='https://0.0.0.1/'):await context.newPage();
const checks=[],cases=['H2SO4','v_0 = 10 m/s^2'];
const wait=async()=>{await page.waitForTimeout(350);await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:30000});};
try{
 if(!desktop)await page.goto('http://127.0.0.1:4185/');await page.locator('.locus-app[data-ready=true]').waitFor({timeout:40000});
 for(const name of ['math','physics','chemistry'])await page.locator('#detect-'+name).setChecked(true);
 for(const raw of cases){await page.locator('#source').fill(raw);await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent===`${raw.length} ký tự`,raw);await wait();assert.equal(await page.locator('#source').inputValue(),raw);const pending=page.waitForEvent('download');await page.locator('#download-png').click();const png=await pending;const path=`artifacts/e3/delivery-${host}-${cases.indexOf(raw)}.png`;await png.saveAs(path);const bytes=await readFile(path);assert.equal(bytes.subarray(0,8).toString('hex'),'89504e470d0a1a0a');checks.push({id:host+'/'+raw,status:'PASS',pngSha256:createHash('sha256').update(bytes).digest('hex')});}
 const opposite=desktop?'chromium':'desktop';await page.locator('#open-document').setInputFiles(`artifacts/e3/runs/${opposite}/cross-host.locus`);await page.waitForFunction(()=>document.querySelector('#source')?.value==='2H2 + O2 -> 2H2O');await wait();
 const expected=JSON.parse(JSON.parse(await readFile(`artifacts/e3/runs/${opposite}/cross-host.locus`,'utf8')).payload);assert.equal(await page.locator('#formula-preview').getAttribute('data-candidate'),expected.candidateId);checks.push({id:host+'/open-other-host-snapshot',status:'PASS'});
 await page.waitForTimeout(500);const id=await page.locator('#formula-preview').getAttribute('data-candidate');await page.reload();await wait();assert.equal(await page.locator('#formula-preview').getAttribute('data-candidate'),id);checks.push({id:host+'/packaged-reload',status:'PASS'});
 if(!desktop){await page.waitForFunction(()=>document.querySelector('#offline-status')?.textContent.includes('offline'));await context.setOffline(true);await page.reload();await wait();assert.equal(await page.locator('#formula-preview').getAttribute('data-candidate'),id);checks.push({id:host+'/packaged-offline',status:'PASS'});}
 await page.screenshot({path:`artifacts/e3/delivery-${host}.png`,fullPage:true});
}finally{await writeFile(`artifacts/e3/delivery-${host}.json`,JSON.stringify({capturedAtUtc:new Date().toISOString(),host,checks},null,2));await browser.close();}
console.log(host+': '+checks.length+' packaged runtime checks passed');
