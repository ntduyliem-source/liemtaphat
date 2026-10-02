import {chromium} from 'playwright';
const browser=await chromium.launch({headless:true});const page=await browser.newPage();
page.on('console',m=>console.log('CONSOLE',m.type(),m.text().slice(0,700)));page.on('pageerror',e=>console.log('ERROR',e.message));
await page.goto('http://127.0.0.1:4182/');
console.log(await page.evaluate(async()=>{const {managerFor}=await import('./_content/Locus.Editor/worker-client.js');const m=managerFor('./worker/worker.js');try{return {value:await m.run('probe',JSON.stringify({Raw:'x^2',SourceRevision:1,Settings:{Mode:0,Open:'lc[',Close:']'}}))};}catch(e){return {error:e.stack};}finally{m.dispose();}}));
await browser.close();
