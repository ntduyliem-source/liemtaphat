import {readFile,writeFile,mkdir,cp,stat} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const build=JSON.parse(await readFile('artifacts/sc1/current-build.json','utf8'));
const id=build.root.split(/[\\/]/).at(-1),out=`artifacts/sc1/evidence/${id}/catalog`;
try{await stat(out);throw Error('Evidence already exists.');}catch(e){if(e.code!=='ENOENT')throw e;}
const evidence=[];
for(const [path,count] of [['catalog-verification.json',87],['runs/chromium/ui.json',29],['runs/firefox/ui.json',29],['runs/desktop/ui.json',27]]){
 const bytes=await readFile('artifacts/sc1/'+path),r=JSON.parse(bytes);assert.equal(r.summary.checks,count,path);assert.equal(r.summary.failed,0,path);
 if(r.build)assert.equal(r.build,id,path);evidence.push({path,sha256:createHash('sha256').update(bytes).digest('hex'),checks:count});
}
for(const host of ['chromium','firefox']){const p=JSON.parse(await readFile(`artifacts/sc1/runs/${host}/worker-parity.json`));assert.equal(p.length,743);assert(p.every(i=>i.pass));}
await mkdir(out,{recursive:true});
for(const path of [...evidence.map(e=>e.path),'runs/chromium/worker-parity.json','runs/firefox/worker-parity.json','runs/chromium/products-preview.png','runs/desktop/products-preview.png','catalog-manifest.json','catalog-fixtures.json','accepted-products-open.locus','accepted-products-closed.locus'])await cp('artifacts/sc1/'+path,out+'/'+path,{recursive:true});
const binaries=[];for(const name of ['Locus.Core','Locus.Application','Locus.Editor']){const path=build.desktop+'/'+name+'.dll';binaries.push({path,sha256:createHash('sha256').update(await readFile(path)).digest('hex')});}
await writeFile(out+'/receipt.json',JSON.stringify({capturedAtUtc:new Date().toISOString(),build:id,scope:'SC1-06 offline catalog, conditions and manual acceptance. Ghost keys and Word not included.',evidence,binaries},null,2));
console.log(`Accepted catalog: ${out}`);
