import {firefox} from 'playwright';
const b=await firefox.launch();const p=await b.newPage();await p.goto('http://127.0.0.1:4182/');await p.locator('#formula-preview svg').waitFor();
await p.locator('#open-document').setInputFiles('artifacts/sh/runs/firefox/selected.locus');await p.waitForFunction(()=>document.querySelector('#source').value==='x+1/2');
for(let i=0;i<3;i++){await p.locator('#close-editor-tab').click();await p.locator('#reopen-editor-tab').click();await p.locator('#formula-preview svg').waitFor();}
await p.evaluate(()=>{window.trace=[];const r=document.querySelector('.locus-app');const src=document.querySelector('#source');for(const type of ['input','compositionstart','compositionend'])src.addEventListener(type,e=>trace.push({type,value:src.value,composition:e.isComposing,version:r.dataset.version}));new MutationObserver(()=>trace.push({type:'state',version:r.dataset.version,busy:r.dataset.busy,raw:src.value})).observe(r,{attributes:true});});
await p.locator('#source').fill('1/2');await p.waitForTimeout(700);console.log(await p.evaluate(()=>trace));await b.close();
