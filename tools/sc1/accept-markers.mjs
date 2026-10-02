import {readFile,writeFile,readdir} from 'node:fs/promises';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const read=async p=>JSON.parse(await readFile(p,'utf8'));
const hash=async p=>createHash('sha256').update(await readFile(p)).digest('hex');
const build=await read('artifacts/sc1/current-build.json'),id=build.root.split(/[\\/]/).at(-1),checks=[];
async function check(path,predicate){const value=await read(path);assert(predicate(value),path);checks.push({path,sha256:await hash(path),status:'PASS'});}
await check('artifacts/sc1/marker-verification.json',d=>d.summary.failed===0&&d.summary.passed===109);
await check('artifacts/sc1/math-regression.json',d=>d.summary.failed===0&&d.summary.passed===238);
await check('artifacts/sc1/e3-regression/core-verification.json',d=>d.summary.failed===0&&d.summary.passed===416);
await check('artifacts/sc1/e3-regression/application-verification.json',d=>d.summary.failed===0&&d.summary.passed===19);
await check('artifacts/sc1/application-regression/application-tests.json',d=>d.failed===0&&d.total===35);
for(const host of ['chromium','firefox','desktop'])await check(`artifacts/sc1/runs/${host}/ui.json`,d=>d.build===id&&d.expectedBuild===id&&d.summary.failed===0&&d.summary.passed===(host==='desktop'?17:19));
for(const host of ['chromium','firefox'])await check(`artifacts/sc1/runs/${host}/worker-parity.json`,d=>d.length===603&&d.every(c=>c.pass));
const source=[];
async function walk(dir){for(const e of await readdir(dir,{withFileTypes:true})){const p=join(dir,e.name);if(e.isDirectory()){if(!['bin','obj','node_modules'].includes(e.name))await walk(p);}else if(/\.(cs|csproj|razor|js|css|html)$/.test(e.name))source.push({path:p.replaceAll('\\','/'),sha256:await hash(p)});}}
for(const p of ['src/Locus.Core','src/Locus.Application','src/Locus.Editor','src/Locus.Worker','src/Locus.Web','src/Locus.Desktop.Shared'])await walk(p);
const binaries=[];for(const name of ['Locus.Core','Locus.Application','Locus.Editor','Locus.Desktop.Shared']){const path=join(build.desktop,name+'.dll');binaries.push({path,sha256:await hash(path)});}
await writeFile('artifacts/sc1/markers-acceptance.json',JSON.stringify({status:'PASSED_SC1_01_02_LOCAL',capturedAtUtc:new Date().toISOString(),build:id,scope:'Versioned specification and named/custom markers on Web/Desktop. No balancing, prediction, ghost, SC1 Word or renewed OS IME claim.',checks,source,binaries},null,2));
console.log(`SC1-01/02: ${checks.length} evidence groups passed; build ${id}`);
