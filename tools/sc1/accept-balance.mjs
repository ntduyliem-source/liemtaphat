import {readFile,writeFile,mkdir,cp,stat} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const build=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8'));
const id=build.root.split(/[\\/]/).at(-1),out=`artifacts/sc1/evidence/${id}/balance`;
try{await stat(out);throw Error('Evidence already exists; do not overwrite an accepted snapshot.');}catch(e){if(e.code!=='ENOENT')throw e;}
const evidence=[];
for(const [path,count] of [['marker-verification.json',109],['assistance-contracts.json',20],['chemistry-input-verification.json',38],['balance-verification.json',31],['assistance-session-verification.json',7],['runs/chromium/ui.json',24],['runs/firefox/ui.json',24],['runs/desktop/ui.json',22]]){
 const bytes=await readFile('artifacts/sc1/'+path),r=JSON.parse(bytes);
 assert.equal(r.summary.checks,count,path);assert.equal(r.summary.failed,0,path);
 if(r.build)assert.equal(r.build,id,path);
 evidence.push({path,sha256:createHash('sha256').update(bytes).digest('hex'),checks:count});
}
await mkdir(out,{recursive:true});
for(const path of [...evidence.map(e=>e.path),'runs/chromium/worker-parity.json','runs/firefox/worker-parity.json','math-regression.json','worker-fixtures.json','chemistry-input-fixtures.json','balance-fixtures.json','accepted-balance.locus','session-accepted-balance.locus']){
 await cp('artifacts/sc1/'+path,out+'/'+path,{recursive:true});
}
await writeFile(out+'/receipt.json',JSON.stringify({capturedAtUtc:new Date().toISOString(),build:id,scope:'SC1-03/04/05 and manual assistance on shared Web/Desktop; no product catalog or ghost keyboard acceptance',evidence},null,2));
console.log(`Accepted ${evidence.length} evidence groups for ${id}: ${out}`);
