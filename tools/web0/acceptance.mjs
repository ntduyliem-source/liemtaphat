import {readFile,writeFile,readdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {join,relative,extname} from 'node:path';
import assert from 'node:assert/strict';
const workspace=fileURLToPath(new URL('../../',import.meta.url));
const sha=data=>createHash('sha256').update(data).digest('hex');
const json=async path=>JSON.parse((await readFile(join(workspace,path),'utf8')).replace(/^\uFEFF/,''));
const reference=await readFile(join(workspace,'artifacts/web/native/parity.json'),'utf8');
const native=await json('artifacts/web/native/contracts.json');assert.equal(native.summary.passed,238);assert.equal(native.summary.failed,0);
const runs={};
for(const host of ['chromium','firefox','webview-wasm','hybrid']){
  const root=`artifacts/web/runs/${host}`;
  const verification=await json(`${root}/verification.json`),contracts=await json(`${root}/contracts.json`);
  assert.equal(verification.failed,0);assert.equal(verification.passed,host==='hybrid'?16:17);assert.equal(contracts.summary.passed,238);assert.equal(contracts.summary.failed,0);
  assert.equal(await readFile(join(workspace,root,'parity.json'),'utf8'),reference);
  runs[host]={path:`${root}/verification.json`,sha256:sha(await readFile(join(workspace,root,'verification.json'))),uiGroups:verification.passed,coreChecks:238,paritySources:116,browser:verification.browser,timing:await json(`${root}/timing.json`)};
}
const telex=await json('artifacts/web/ime-wasm-telex.json'),backspace=await json('artifacts/web/ime-wasm-backspace.json'),undo=await json('artifacts/web/ime-wasm-undo.json');
assert.equal(telex.raw,'x mũ 2');assert.equal(telex.host,'browser');assert.equal(telex.preview,'x2');assert.equal(backspace.raw,'x mũ ');assert.equal(backspace.preview,null);assert.equal(undo.raw,'x mũ 2');assert.equal(undo.preview,'x2');
const m3=await json('artifacts/m3/acceptance.json'),coreSources=m3.sources.filter(s=>s.path.startsWith('src/Locus.Core/'));
assert.ok(coreSources.length>0);for(const source of coreSources)assert.equal(sha(await readFile(join(workspace,source.path))),source.sha256.toLowerCase());
const packageInfo=await json('artifacts/web/package.json'),packageCheck=await json('artifacts/web/package-validation.json');assert.equal(packageCheck.status,'PASS');assert.equal(packageInfo.sha256,packageCheck.archiveSha256);assert.equal(sha(await readFile(join(workspace,packageInfo.path))),packageInfo.sha256.toLowerCase());
const perf=await json('artifacts/web/performance.json');assert.equal(perf.payload.allFiles,packageCheck.assetsVerified);
async function walk(dir){const out=[];for(const e of await readdir(dir,{withFileTypes:true})){if(['bin','obj','node_modules'].includes(e.name)||e.name.endsWith('.WebView2'))continue;const path=join(dir,e.name);if(e.isDirectory())out.push(...await walk(path));else out.push(path);}return out;}
const sourceFiles=[];for(const dir of ['src/Locus.Core','tests/Locus.Core.Tests','prototypes/web0','tools/web0','docs/web'])sourceFiles.push(...await walk(join(workspace,dir)));
sourceFiles.push(join(workspace,'Locus.Web.sln'),join(workspace,'corpus/m0/cases.json'));
const sources=[];for(const path of sourceFiles.sort())sources.push({path:relative(workspace,path).replaceAll('\\','/'),sha256:sha(await readFile(path))});
const report={schemaVersion:'locus-web0-compatibility/1',capturedAtUtc:new Date().toISOString(),milestone:'WEB0',status:'PASSED_ARCHITECTURE_PROTOTYPE_BASELINE',tasks:{'WEB0-01':'DONE','WEB0-02':'DONE','WEB0-03':'DONE'},progress:{completedTasks:3,totalTasks:3,percent:100,meaning:'WEB0 feasibility only, not WEB1 completion or total project progress.'},baseline:{sdk:'10.0.400',runtime:'10.0.11',webViewWpf:'10.0.101',core:'locus-core/0.1',grammar:'vi-math-m0-proposal-0.1',productionCoreFilesUnchangedFromM3:coreSources.length,...perf.host},runs,native:{checks:238,paritySources:116,paritySha256:sha(reference)},ime:{status:'PASS_SCOPED_TELEX_WASM_WEBVIEW2',source:'artifacts/web/ime-wasm-telex.json',backspace:'artifacts/web/ime-wasm-backspace.json',undo:'artifacts/web/ime-wasm-undo.json',notes:'OS-keyboard UniKey Telex on published WASM. No real composition events in this UniKey path. Chromium/Firefox composition sequences in UI suite are synthetic. Full Telex/VNI matrix belongs to WEB1.'},performance:'artifacts/web/performance.json',package:packageInfo,packageValidation:packageCheck,decision:'docs/web/ARCHITECTURE.md',limits:['Static prototype is served on localhost; no public deployment, PWA/offline reload, durable drafts or formula SVG/PNG.','One flaky 1 ms timer assertion changed to deterministic pre-cancelled-token coverage. Browser original 237/238 run is preserved in browser-contracts.json. Separate Scheduling reports are OBSERVED, not PASS for in-flight interruption.','Maximum 4096 UTF-16 source units checked before normalization. Worker/deadline scheduling required before heavy work.','Core contracts exclude eight pending corpus cases and Word mutation/focus guards.','Canvas trial is one free triangle; JSXGraph trial is one numeric curve, not completed E1/E2A.','Word W0 remains 4/6; G2/G3 not passed. Existing M2/M3 release scope unchanged.'],sources};
await writeFile(join(workspace,'artifacts/web/compatibility.json'),JSON.stringify(report,null,2)+'\n');console.log(`WEB0: 3/3 tasks; 116-source parity on four hosts; ${sources.length} source hashes; ${packageCheck.assetsVerified} package assets verified.`);
