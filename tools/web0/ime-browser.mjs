// Isolated, disposable browser for OS-keyboard/UniKey verification. No user profile.
import {chromium} from 'playwright';
const browser=await chromium.launch({channel:'chrome',headless:false,args:['--remote-debugging-port=9312','--remote-debugging-address=127.0.0.1']});
const context=await browser.newContext({viewport:{width:1366,height:900}});
const page=await context.newPage();await page.goto('http://127.0.0.1:4181/');await page.locator('[data-ready="true"]').waitFor();
await page.locator('#source').fill('');await page.locator('#source').click();
await page.locator('#source').evaluate(el=>{window.locusImeEvents=[];for(const type of ['keydown','beforeinput','input','compositionstart','compositionupdate','compositionend'])el.addEventListener(type,e=>window.locusImeEvents.push({type:e.type,key:e.key,data:e.data,isComposing:e.isComposing,inputType:e.inputType,value:el.value}));});
console.log('Owned WEB0 browser ready for OS keyboard input; CDP 127.0.0.1:9312.');
await new Promise(resolve=>browser.on('disconnected',resolve));
