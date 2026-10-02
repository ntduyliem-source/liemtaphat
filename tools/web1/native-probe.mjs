import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
let browser;const deadline=Date.now()+30000;while(!browser){try{browser=await chromium.connectOverCDP('http://127.0.0.1:9333');}catch(e){if(Date.now()>deadline)throw e;await new Promise(r=>setTimeout(r,500));}}const page=browser.contexts()[0].pages().find(p=>p.url()==='https://0.0.0.1/');assert(page);
await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:30000});
if(process.argv.includes('copy-svg')){await page.locator('#copy-svg').click();await page.waitForFunction(()=>document.querySelector('#notice')?.textContent==='Đã sao chép.');}
const state=await page.evaluate(()=>({raw:document.querySelector('#source').value,candidate:document.querySelector('#formula-preview').dataset.candidate,font:document.querySelector('#font-size').value,scale:document.querySelector('#pixel-scale').value,background:document.querySelector('#white-background').checked,draft:document.querySelector('#draft-status').textContent}));
if(process.argv.includes('compare')){const previous=JSON.parse(await readFile('artifacts/web1/native-before-restart.json','utf8'));for(const key of ['raw','candidate','font','scale','background'])assert.deepEqual(state[key],previous[key],key);await writeFile('artifacts/web1/native-restart.json',JSON.stringify({status:'PASSED',state},null,2));}else await writeFile('artifacts/web1/native-before-restart.json',JSON.stringify(state,null,2));
console.log(JSON.stringify({status:'PASSED',state}));await browser.close();
