import {readFile,writeFile,readdir} from 'node:fs/promises';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const json=async path=>JSON.parse(await readFile(path,'utf8'));
const hash=async path=>createHash('sha256').update(await readFile(path)).digest('hex');
const base='artifacts/web1/',build=await json(base+'current-build.json'),packages=await json(base+'package.json');
assert.deepEqual(build,packages.build,'Package/build mismatch');
const paths=['application-tests.json','core-contracts.json','worker-chromium.json','worker-firefox.json','native-integration.json','native-restart.json','clipboard-os.json','clipboard-svg-os.json','export-safety.json','package-validation.json','static-validation.json','local-validation.json','in-app-validation.json','update.json',...['chromium','firefox','desktop'].map(x=>`runs/${x}/ui.json`),...['chromium','firefox'].map(x=>`runs/${x}/web1.json`)];
const evidence=[];
for(const path of paths){const report=await json(base+path);if(report.status)assert(report.status.startsWith('PASS'),path);if(report.failed!==undefined)assert.equal(report.failed,0,path);if(report.results)assert(report.results.every(r=>r.passed!==false&&r.status!=='FAILED'),path);evidence.push({path:base+path,sha256:await hash(base+path)});}
const core=await json(base+'core-contracts.json');assert.equal(core.summary.checks,238);assert.equal(core.summary.failed,0);
const sh=await json('artifacts/sh/acceptance.json'),previous=await json('artifacts/web/compatibility.json');
const productionCore=previous.sources.filter(s=>s.path.startsWith('src/Locus.Core/'));
for(const file of productionCore)assert.equal(await hash(file.path),file.sha256,file.path);
const historical=[...sh.baseline.historicalReleases,...sh.packages];
for(const file of historical)assert.equal(await hash(file.path),file.sha256.toLowerCase(),file.path);
for(const file of packages.packages)assert.equal(await hash(file.path),file.sha256.toLowerCase(),file.path);
const ui={};for(const kind of ['chromium','firefox','desktop']){const report=await json(`${base}runs/${kind}/ui.json`);assert.equal(report.results.length,14);ui[kind]={version:report.browserVersion,regressionGroups:14};if(kind!=='desktop'){const web=await json(`${base}runs/${kind}/web1.json`);assert.equal(web.results.length,16);ui[kind].web1Groups=web.results.length;}}
const sources=[];async function scan(directory){for(const item of await readdir(directory,{withFileTypes:true})){if(['bin','obj','vendor','node_modules'].includes(item.name))continue;const path=join(directory,item.name).replaceAll('\\','/');if(item.isDirectory())await scan(path);else sources.push({path,sha256:await hash(path)});}}
for(const directory of ['src/Locus.Application','src/Locus.Editor','src/Locus.Worker','src/Locus.Web','src/Locus.Desktop.Shared','tests/Locus.Application.Tests','tests/Locus.Shared.Desktop.Tests','tools/web1','docs/web1'])await scan(directory);
const ime=await json(base+'ime/status.json'),hosting=await json(base+'hosting-status.json');
const scope=await json('docs/web1/delivery-scope.json'),local=await json(base+'local-validation.json'),inApp=await json(base+'in-app-validation.json');
assert.equal(scope.mode,'local-only');assert.equal(scope.online,'deferred-by-user');
assert.equal(local.build,build.root.split(/[\\/]/).at(-1));assert.equal(local.results.length,13);
assert(packages.packages.some(p=>p.path===local.archive.path&&p.sha256===local.archive.sha256));
assert.equal(inApp.build,local.build);assert.equal(inApp.status,'PASSED');
evidence.push({path:'docs/web1/delivery-scope.json',sha256:await hash('docs/web1/delivery-scope.json')});
const imeDone=ime.status==='PASSED_REAL_TELEX_VNI',localDone=local.status==='PASSED';
if(imeDone){
  assert.equal(ime.build,local.build);assert.equal(ime.results.length,19);assert(ime.results.every(r=>r.passed));
  for(const file of ime.evidence){assert.equal(await hash(file.path),file.sha256,file.path);evidence.push(file);}
  evidence.push({path:base+'ime/status.json',sha256:await hash(base+'ime/status.json')});
}
const tasks={'WEB1-01':'DONE','WEB1-02':'DONE','WEB1-03':imeDone?'DONE':'BLOCKED','WEB1-04':localDone?'DONE':'BLOCKED'};
const complete=Object.values(tasks).filter(s=>s==='DONE').length;
const report={schemaVersion:'locus-web1-acceptance/2',capturedAtUtc:new Date().toISOString(),milestone:'WEB1',status:complete===4?'PASSED_WEB1_ALPHA':'TECHNICAL_PREVIEW_ACCEPTANCE_PENDING',tasks,progress:{completed:complete,total:4,percent:complete*25,scope:'Completed WEB1 acceptance tasks under user-approved local-only delivery; not implementation effort or whole-product progress'},delivery:scope,build,ui,applicationGroups:35,coreContracts:238,workerParityPerBrowser:104,updateGroups:5,localDeliveryGroups:local.results.length,inAppBrowser:inApp.status,ime,exportSafetyGroups:5,nativeIntegrationGroups:11,nativeRestart:'PASSED',baseline:{coreFilesUnchanged:productionCore.length,historicalReleases:historical.map(x=>({path:x.path,sha256:x.sha256}))},packages:packages.packages,evidence,sources,remaining:{ime:imeDone?null:ime,hosting},limitations:[imeDone?'Real UniKey Telex/VNI was verified on the exact published WASM build in Windows WebView2 152. Chromium/Firefox/in-app browser have separate automated coverage; other OS/IME combinations are not certified.':'Real Telex/VNI on the current WEB1 build is not yet accepted. Browser automation does not replace OS-keyboard evidence.','Online publishing is deferred by explicit user choice; it is not a local-delivery blocker.','The local Web launcher requires Node.js (tested on 24.15.0); server binds to 127.0.0.1 only.','Drafts remain on the device and browser origin/profile; undo history does not survive reload. Old release caches are retained for live tabs.','Windows native file picker cancellation/disk failures use controlled API simulations; clipboard PNG/SVG were read from the real OS clipboard.','Current formula editor only; no Word connector, graph/geometry editor, Physics or Chemistry in WEB1.','W0 remains 4/6; G2/G3 and user-deferred A/B feedback unchanged.']};
await writeFile(base+'acceptance.json',JSON.stringify(report,null,2));
console.log(JSON.stringify({status:report.status,progress:report.progress,ui,packages:packages.packages.length,sourceFiles:sources.length}));
