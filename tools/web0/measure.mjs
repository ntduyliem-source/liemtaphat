import {chromium} from 'playwright';
import {readFile,writeFile,readdir,stat} from 'node:fs/promises';
import {join,relative} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import os from 'node:os';
const root=fileURLToPath(new URL('../../artifacts/web/browser/wwwroot/',import.meta.url));
async function files(dir){const out=[];for(const entry of await readdir(dir,{withFileTypes:true})){const p=join(dir,entry.name);if(entry.isDirectory())out.push(...await files(p));else out.push(p);}return out;}
const manifest=[];
for(const path of await files(root)){const data=await readFile(path);manifest.push({path:relative(root,path).replaceAll('\\','/'),bytes:data.length,sha256:createHash('sha256').update(data).digest('hex')});}
const content=manifest.filter(f=>!f.path.endsWith('.br')&&!f.path.endsWith('.gz'));
const sum=a=>a.reduce((n,v)=>n+v.bytes,0);
const samples=[],browser=await chromium.launch({headless:true});
for(let i=0;i<7;i++){
  const context=await browser.newContext(),page=await context.newPage();
  for(const cache of ['cold-context','warm-reload']){
    const start=performance.now();if(cache==='cold-context')await page.goto('http://127.0.0.1:4181/');else await page.reload();await page.locator('[data-ready="true"]').waitFor();
    const readyMs=performance.now()-start;
    const network=await page.evaluate(()=>{
      const entries=[...performance.getEntriesByType('navigation'),...performance.getEntriesByType('resource')];
      return {transferBytes:entries.reduce((n,e)=>n+(e.transferSize??0),0),decodedBytes:entries.reduce((n,e)=>n+(e.decodedBodySize??0),0),requests:entries.length};
    });samples.push({iteration:i+1,cache,readyMs,...network});
  }await context.close();
}
const percentile=(a,p)=>[...a].sort((a,b)=>a-b)[Math.ceil(a.length*p)-1];
const report={capturedAtUtc:new Date().toISOString(),host:{os:`${os.type()} ${os.release()} ${os.arch()}`,cpu:os.cpus()[0].model,logicalCpus:os.cpus().length,memoryBytes:os.totalmem(),browser:browser.version()},payload:{allFiles:manifest.length,allBytes:sum(manifest),contentFiles:content.length,uncompressedBytes:sum(content),brotliAlternativeBytes:sum(content.map(f=>manifest.find(c=>c.path===f.path+'.br')??f)),gzipAlternativeBytes:sum(content.map(f=>manifest.find(c=>c.path===f.path+'.gz')??f))},startup:['cold-context','warm-reload'].map(cache=>{const rows=samples.filter(s=>s.cache===cache);return{cache,iterations:rows.length,p50ReadyMs:percentile(rows.map(s=>s.readyMs),.5),p95ReadyMs:percentile(rows.map(s=>s.readyMs),.95),p50TransferBytes:percentile(rows.map(s=>s.transferBytes),.5)};}),samples,limits:['Release trimmed IL interpreter; no wasm-tools native relinking or AOT.','Loopback HTTP server serves uncompressed files with Cache-Control max-age=60. Browser contexts are fresh for cold samples; OS disk cache is not flushed.','Brotli/gzip totals are available file alternatives, not measured compressed wire transfer. Includes test corpus/entry points and whole-Core preservation, not a production payload budget.','Seven samples estimate local startup only; no internet/mobile latency claim. Warm core timing is reported separately per host.']};
await browser.close();await writeFile(new URL('../../artifacts/web/payload-manifest.json',import.meta.url),JSON.stringify(manifest,null,2));await writeFile(new URL('../../artifacts/web/performance.json',import.meta.url),JSON.stringify(report,null,2));console.log(JSON.stringify({payload:report.payload,startup:report.startup},null,2));
