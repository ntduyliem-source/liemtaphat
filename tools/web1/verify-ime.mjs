import {readFile,writeFile,copyFile} from 'node:fs/promises';
import {constants} from 'node:fs';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const json=async path=>JSON.parse(await readFile(path,'utf8'));
const sha=async path=>createHash('sha256').update(await readFile(path)).digest('hex');
const base='artifacts/web1/ime/',directory=base+'real-os/';
const build=(await json('artifacts/web1/current-build.json')).root.split(/[\\/]/).at(-1);
const evidence=[],results=[];
async function load(name){const path=directory+name+'.json',report=await json(path);assert.equal(report.status,'PASSED',name);assert.equal(report.state?.build??report.build,build,name);evidence.push({path,sha256:await sha(path)});return report;}
const modes={};
for(const mode of ['telex','vni']){
  const expected={complete:'x mũ 2','tone-changed':'x mú','tone-corrected':'x mũ',backspace:'x mũ ',undo:'x mũ 2',redo:'x mũ ',restored:'x mũ 2'};
  modes[mode]={};
  for(const [stage,raw] of Object.entries(expected)){
    const report=await load(`${mode}-${stage}`),valid=['complete','undo','restored'].includes(stage);
    assert.equal(report.mode,mode);assert.equal(report.stage,stage);assert.equal(report.state.host,'browser');assert.equal(report.state.raw,raw);
    assert.equal(report.state.latex,valid?'{x}^{2}':null);assert.equal(report.state.canExport,valid);
    assert.equal(report.state.documentFocused,true);assert.equal(report.state.active,'source');
    assert(report.events.some(e=>e.trusted&&e.type==='input'),'Actual input events required');
    if(stage==='complete')assert(report.events.some(e=>e.trusted&&e.type==='keyup'&&e.key===(mode==='telex'?'x':'4')&&e.raw==='x mũ'),'Mode-specific OS tone key not observed');
    if(stage==='tone-changed')assert(report.events.some(e=>e.trusted&&e.type==='keyup'&&e.key===(mode==='telex'?'s':'1')&&e.raw==='x mú'),'Tone replacement key not observed');
    if(stage==='backspace')assert(report.events.some(e=>e.trusted&&e.type==='input'&&e.inputType==='deleteContentBackward'&&e.raw==='x mũ '));
    modes[mode][stage]=report;results.push({name:`${mode}-${stage}`,passed:true});
  }
  assert.equal(modes[mode].complete.state.candidate,modes[mode].undo.state.candidate,'Undo must restore the selected candidate');
  const persistence=await load(mode+'-persistence');
  for(const key of ['raw','candidate','latex'])assert.equal(persistence.before[key],modes[mode].restored.state[key]);
  for(const key of ['raw','candidate','latex','rendered'])assert.equal(persistence.after[key],persistence.before[key]);
  assert.equal(persistence.after.rendered,'true');assert.equal(persistence.files.length,3);
  for(const file of persistence.files){assert.equal(await sha(file.path),file.sha256);evidence.push({path:file.path,sha256:file.sha256});}
  results.push({name:mode+'-export-and-reload',passed:true});
}
const blur=await load('telex-blur'),refocus=await load('telex-refocus');
assert.notEqual(blur.state.active,'source');assert.equal(refocus.state.active,'source');
for(const key of ['raw','candidate','latex'])assert.equal(blur.state[key],refocus.state[key]);
results.push({name:'os-focus-out-and-back-retains-source',passed:true});
const restored=await load('restored-telex-confirmed');
const vniCompletedAt=Date.parse((await json(directory+'vni-persistence.json')).capturedAtUtc);
assert.equal(restored.state.raw,'x mũ');assert(restored.events.some(e=>e.trusted&&e.type==='keyup'&&e.key==='x'&&e.raw==='x mũ'&&e.time>vniCompletedAt));
results.push({name:'unikey-returned-to-telex-after-vni',passed:true});
assert.equal(await sha(directory+'telex.png'),await sha(directory+'vni.png'),'Both input methods render the same formula');
results.push({name:'telex-vni-export-identical-png',passed:true});
const report={status:'PASSED_REAL_TELEX_VNI',schemaVersion:'locus-web1-ime/1',capturedAtUtc:new Date().toISOString(),build,scope:'WEB1 published WASM with actual OS keys in WebView2 on Windows; Chromium/Firefox and in-app browser retain their separate automated UI coverage',baseline:{os:'Windows 10 x64',inputMethod:'UniKey 4.3 RC5',encoding:'Unicode',webView2:modes.telex.complete.browser,host:'browser',keyInjection:'sky.press_key -> Windows SendInput -> UniKey -> WebView2 input events'},results,evidence,restoredSettings:'Telex, confirmed by new OS x tone key after VNI; Unicode unchanged',limits:['The real-keyboard baseline is Windows WebView2 running the exact static WASM build. This does not certify real Telex/VNI on every Chromium/Firefox version or on other operating systems.','UniKey emits Backspace and Unicode replacement events in this baseline, not compositionstart/end; synthetic composition cases remain separate.','OS input entered a scoped set of formulas and edits, not every Vietnamese word or IME option.','W0/Word automatic conversion and G2/G3 remain outside this acceptance.']};
await copyFile(base+'status.json',base+'status-before-real-os.json',constants.COPYFILE_EXCL).catch(e=>{if(e.code!=='EEXIST')throw e;});
await writeFile(base+'status.json',JSON.stringify(report,null,2));
console.log(JSON.stringify({status:report.status,build,groups:results.length,evidence:evidence.length,baseline:report.baseline}));
