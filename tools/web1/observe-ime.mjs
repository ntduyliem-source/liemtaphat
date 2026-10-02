// Reattach observation after reload; never synthesizes input or changes the source.
import {chromium} from '../sh/node_modules/playwright/index.mjs';
import assert from 'node:assert/strict';
const browser=await chromium.connectOverCDP('http://127.0.0.1:9335');
try{
  const page=browser.contexts()[0].pages().find(p=>p.url().startsWith('http://127.0.0.1:4183/'));assert(page);
  console.log(await page.locator('#source').evaluate(el=>{
    if(typeof globalThis.recordWeb1Os!=='function')throw Error('The owned observation session is not running');
    if(!el.dataset.imeObserved){
      for(const type of ['keydown','keyup','input','compositionstart','compositionend'])el.addEventListener(type,e=>globalThis.recordWeb1Os({type,key:e.key??null,inputType:e.inputType??null,composing:e.isComposing??false,trusted:e.isTrusted,raw:el.value,time:Date.now()}));
      el.dataset.imeObserved='true';
    }
    return {raw:el.value,active:document.activeElement.id,focused:document.hasFocus(),observing:true};
  }));
}finally{await browser.close();}
