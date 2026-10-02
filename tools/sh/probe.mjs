import {chromium} from 'playwright';
import {writeFile} from 'node:fs/promises';
const native=process.argv.includes('--desktop');
const browser=native?await chromium.connectOverCDP('http://127.0.0.1:9331'):await chromium.launch({headless:true});
const context=native?browser.contexts()[0]:await browser.newContext({viewport:{width:1280,height:960}});
const page=native?context.pages().find(p=>p.url().includes('0.0.0.1'))??context.pages()[0]:await context.newPage();
const errors=[],requests=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});page.on('response',r=>{requests.push({url:r.url(),status:r.status()});});
if(!native)await page.goto('http://127.0.0.1:4182/');else await page.reload();
try{await page.locator('#formula-preview[data-rendered=true] svg').waitFor({timeout:45000});}catch(e){errors.push(e.message);}
await page.screenshot({path:`artifacts/sh/${native?'desktop':'web'}-probe.png`,fullPage:true});
await writeFile(`artifacts/sh/${native?'desktop':'web'}-probe.json`,JSON.stringify({errors,requests,body:(await page.locator('body').innerText()).slice(0,8000)},null,2));
console.log(JSON.stringify({host:native?'desktop':'web',errors,failedRequests:requests.filter(r=>r.status>=400),body:(await page.locator('body').innerText()).slice(0,5000)}));
await browser.close();
