import {journal} from './local-state.js';
import {bindGhost} from './chemistry-ghost.js';
export {wireResult} from './result-selection.js';
const bindings=new WeakMap();
const inputStates=new WeakMap();
const ghostStates=new WeakMap();
const inputQueues=new WeakMap();
export function wire(element,reference){
  bindings.get(element)?.();const abort=new AbortController();const signal=abort.signal;
  let composing=false,chain=Promise.resolve(),serial=0;
  inputQueues.set(element,()=>chain);
  const ghost=bindGhost(element,reference,action=>{chain=chain.then(action).catch(()=>{});},()=>composing,()=>serial);ghostStates.set(element,ghost);
  inputStates.set(element,()=>composing);
  const send=()=>{
    ghost.invalidate();
    const value=element.value,active=composing,id=++serial;
    journal(value); // Recover keystrokes even before the managed analysis debounce finishes.
    const root=element.closest('.locus-app');root.dataset.inputPending='true';
    chain=chain.then(()=>reference.invokeMethodAsync('SourceInput',value,active)).then(()=>{if(id===serial)root.dataset.inputPending='false';}).catch(()=>{});
  };
  element.addEventListener('compositionstart',()=>{composing=true;send();},{signal});
  element.addEventListener('compositionend',()=>{composing=false;send();},{signal});
  element.addEventListener('input',send,{signal});
  element.closest('.locus-app').addEventListener('click',event=>{
    if(element.closest('.locus-app').dataset.inputPending==='true'&&event.target.closest('.export-toolbar,.export-row,.export-more,.document-bar,.candidate-list,.result-region,.formula-details,.card-heading,.balance-toolbar,.auto-balance-option')){event.preventDefault();event.stopImmediatePropagation();}
  },{signal,capture:true});
  document.addEventListener('keydown',event=>{
    if(ghost.keydown(event))return;
    if(composing||event.isComposing||event.keyCode===229||event.getModifierState('AltGraph'))return;
    const key=event.key.toLowerCase(),ctrl=event.ctrlKey||event.metaKey;let command=null;
    if(event.target===element&&ctrl&&!event.altKey&&['z','y'].includes(key))command=key==='y'||event.shiftKey?'redo':'undo';
    else if(ctrl&&!event.altKey&&key==='enter')command='analyze';
    else if(ctrl&&!event.altKey&&!event.shiftKey&&key==='s')command='save';
    else if(event.altKey&&!ctrl&&!event.shiftKey&&key==='i'){event.preventDefault();element.focus();return;}
    else if(event.altKey&&!ctrl&&['1','2','3'].includes(key))command='candidate'+key;
    else if(key==='escape')command='dismiss';
    if(command){event.preventDefault();chain=chain.then(()=>reference.invokeMethodAsync('Command',command)).catch(()=>{});}
  },{signal});
  const observer=new MutationObserver(()=>{if(!element.isConnected){abort.abort();observer.disconnect();bindings.delete(element);}});observer.observe(document.body,{subtree:true,childList:true});
  bindings.set(element,()=>{abort.abort();observer.disconnect();ghost.dispose();ghostStates.delete(element);});
}
export function unwire(element){bindings.get(element)?.();bindings.delete(element);inputQueues.delete(element);}
export async function settleInput(element){await inputQueues.get(element)?.();}
export function setSource(element,value){element.value=value;journal(value);}
export function inputStamp(element){return {raw:element.value,selectionStart:element.selectionStart,selectionEnd:element.selectionEnd,composing:inputStates.get(element)?.()??false,pending:element.closest('.locus-app')?.dataset.inputPending==='true',focused:document.hasFocus()};}
export function ghostInputStamp(element){return {...inputStamp(element),focused:document.hasFocus()&&document.activeElement===element};}
export function setGhost(element,proposal){ghostStates.get(element)?.set(proposal);}
export function setSourceAndSelection(element,value,start,end){setSource(element,value);element.focus();element.setSelectionRange(start,end);}
function requireSettledInput(){if(document.querySelector('.locus-app[data-input-pending=true]'))throw new Error('Input is changing.');}
export function download(name,type,bytes){requireSettledInput();const url=URL.createObjectURL(new Blob([bytes],{type}));const link=document.createElement('a');link.href=url;link.download=name;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
export async function clipboardText(text){requireSettledInput();await navigator.clipboard.writeText(text);}
export async function clipboardPng(bytes){requireSettledInput();await navigator.clipboard.write([new ClipboardItem({'image/png':new Blob([bytes],{type:'image/png'})})]);}
export function showSvg(element,svg){element.innerHTML=svg;}
function stamp(){const root=document.querySelector('.locus-app');requireSettledInput();if(!root)throw Error('Editor closed.');return [root,root.dataset.version,root.dataset.view,root.dataset.candidate];}
function current(s){const root=document.querySelector('.locus-app');return root===s[0]&&root?.dataset.inputPending!=='true'&&root.dataset.version===s[1]&&root.dataset.view===s[2]&&root.dataset.candidate===s[3];}
export function capabilities(){return {text:!!navigator.clipboard?.writeText,png:!!navigator.clipboard?.write&&typeof ClipboardItem!=='undefined'&&(ClipboardItem.supports?.('image/png')??true),svg:!!navigator.clipboard?.write&&!!globalThis.ClipboardItem?.supports?.('image/svg+xml'),savePicker:typeof showSaveFilePicker==='function'};}
export async function clipboardSvg(svg){requireSettledInput();await navigator.clipboard.write([new ClipboardItem({'image/svg+xml':new Blob([svg],{type:'image/svg+xml'})})]);}
export async function saveAs(name,type,bytes){
  const state=stamp();let writer;
  try{
    if(typeof showSaveFilePicker!=='function')return {status:2};
    const handle=await showSaveFilePicker({suggestedName:name});
    if(!current(state))return {status:1};
    writer=await handle.createWritable();
    if(!current(state)){await writer.abort();return {status:1};}
    await writer.write(new Blob([bytes],{type}));
    if(!current(state)){await writer.abort();return {status:1};}
    await writer.close();return {status:0};
  }catch(error){if(writer)await writer.abort().catch(()=>{});return {status:error.name==='AbortError'?1:3};}
}
export function appStatus(){return globalThis.locusBoot?.status??'Ứng dụng Desktop';}
export function updateAvailable(){return !!globalThis.locusBoot?.waiting;}
export function applyUpdate(){requireSettledInput();globalThis.locusBoot?.update();}
