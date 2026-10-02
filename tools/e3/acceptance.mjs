import {readFile,writeFile,readdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {join} from 'node:path';
const read=async path=>JSON.parse(await readFile(path,'utf8'));
const build=await read('artifacts/e3/current-build.json'),id=build.root.split(/[\\/]/).at(-1),checks=[];
const hash=async path=>createHash('sha256').update(await readFile(path)).digest('hex');
async function check(path,accept){const data=await read(path);if(!accept(data))throw Error('Acceptance not met: '+path);checks.push({path,sha256:await hash(path),status:'PASS'});}
await check('artifacts/e3/core-verification.json',d=>d.summary.failed===0&&d.summary.checks>=416);
await check('artifacts/e3/math-regression.json',d=>d.summary.failed===0&&d.summary.passed===238);
await check('artifacts/e3/application-verification.json',d=>d.summary.failed===0&&d.summary.checks>=19);
await check('artifacts/e3/application-regression/application-tests.json',d=>d.status==='PASSED'&&d.failed===0&&d.total===35);
await check('artifacts/e3/desktop-math-regression/verification.json',d=>d.summary.failed===0&&d.summary.passed===40);
await check('artifacts/e3/word-math-panel-regression/report.json',d=>d.failed===0&&d.passed===7);
for(const host of ['chromium','firefox','desktop'])await check(`artifacts/e3/runs/${host}/ui.json`,d=>d.summary.failed===0&&d.summary.checks===(host==='desktop'?12:14));
for(const host of ['chromium','firefox'])await check(`artifacts/e3/runs/${host}/worker-parity.json`,d=>d.length===504&&d.every(r=>r.pass));
await check('artifacts/e3/renderer-parity.json',d=>d.summary.failed===0&&d.summary.checks===14);
await check('artifacts/e3/word-panel/report.json',d=>d.failed===0&&d.passed===19);
await check('artifacts/e3/word-native-final/report.json',d=>d.failed===0&&d.passed>=33);
await check('artifacts/e3/package-verification.json',d=>d.summary.failed===0&&d.summary.passed===4&&d.build===id);
for(const host of ['web','desktop'])await check(`artifacts/e3/delivery-${host}.json`,d=>d.checks.length===(host==='web'?5:4)&&d.checks.every(c=>c.status==='PASS'));
await check('artifacts/e3/delivery-word.json',d=>d.status==='PASS'&&d.build===id&&d.loadBehavior===3&&d.binaries.length===2&&d.binaries.every(b=>b.sha256===b.testedSha256));
const source=[];
async function walk(dir){for(const entry of await readdir(dir,{withFileTypes:true})){const path=join(dir,entry.name);if(entry.isDirectory()){if(!['bin','obj','vendor','node_modules'].includes(entry.name))await walk(path);}else if(/\.(cs|csproj|razor|js|css|html|xml)$/.test(entry.name))source.push({path:path.replaceAll('\\','/'),sha256:await hash(path)});}}
for(const path of ['src/Locus.Core','src/Locus.Application','src/Locus.Editor','src/Locus.Worker','src/Locus.Web','src/Locus.Desktop.Shared','src/Locus.Word','src/Locus.Desktop/Rendering'])await walk(path);
const binaries=[];
for(const path of [join(build.desktop,'Locus.Core.dll'),join(build.desktop,'Locus.Application.dll'),join(build.desktop,'Locus.Editor.dll'),join(build.desktop,'Locus.Desktop.Shared.dll'),'artifacts/e3/word-trial/Locus.Core.dll','artifacts/e3/word-trial/Locus.Word.dll'])binaries.push({path,sha256:await hash(path)});
await writeFile('artifacts/e3/acceptance.json',JSON.stringify({status:'PASSED_E3_ALPHA_LOCAL',capturedAtUtc:new Date().toISOString(),build:id,scope:'Chemistry/Physics basic formula input on Web and Desktop, Word x86 explicit manual conversion. No Word auto, OS IME renewal, balancing, solving or graph editor.',checks,source,binaries,packages:await read('artifacts/e3/packages.json')},null,2));
console.log('E3 alpha local acceptance: '+checks.length+' evidence groups passed');
