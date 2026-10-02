import {readFile,writeFile,mkdir,cp,stat} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const build=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8'));
const id=build.root.split(/[\\/]/).at(-1),out=`artifacts/sc1/evidence/${id}/ghost`;
try{await stat(out);throw Error('Evidence already exists.');}catch(e){if(e.code!=='ENOENT')throw e;}
const files=new Map(),checks=[];
async function capture(path){const bytes=await readFile('artifacts/sc1/'+path);files.set(path,bytes);return JSON.parse(bytes);}
for(const [path,count] of [['ghost-transaction-verification.json',8],...['chromium','firefox','desktop'].map(host=>[`ghost/${host}/report.json`,14]),['runs/chromium/ui.json',29],['runs/firefox/ui.json',29],['runs/desktop/ui.json',27]]){
 const r=await capture(path);assert.equal(r.summary.checks,count,path);assert.equal(r.summary.failed,0,path);
 if(r.build)assert.equal(r.build,id,path);checks.push({path,checks:count});
}
for(const host of ['chromium','firefox']){const p=await capture(`runs/${host}/worker-parity.json`);assert.equal(p.length,743);assert(p.every(i=>i.pass));}
for(const host of ['chromium','firefox','desktop']){
 const path=`ghost/${host}/delayed-transactions.json`,r=await capture(path);assert.equal(r.length,7);
 for(const row of r){assert(row.consumed);assert.equal(row.commits,row.mutation==='valid-next-input'?1:0);}
 for(const name of ['ghost.png','before-accept.locus','enter-accepted.locus','space-default.locus','typed-after-accept.locus','early-enter.locus','quick-edit.locus']){
  const path=`ghost/${host}/${name}`;files.set(path,await readFile('artifacts/sc1/'+path));
 }
}
const binaries=[];
for(const name of ['Locus.Core','Locus.Application','Locus.Editor']){const path=build.desktop+'/'+name+'.dll';binaries.push({path,sha256:createHash('sha256').update(await readFile(path)).digest('hex')});}
await mkdir(out,{recursive:true});
const evidence=[];
for(const [path,bytes] of files){await cp('artifacts/sc1/'+path,out+'/'+path);evidence.push({path,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')});}
await writeFile(out+'/receipt.json',JSON.stringify({capturedAtUtc:new Date().toISOString(),build:id,scope:'SC1-07 published shared ghost, Enter and two-phase transaction; Space code verified. Real OS IME and Word remain separate.',checks,evidence,binaries},null,2));
console.log(`Accepted ghost: ${out}`);
