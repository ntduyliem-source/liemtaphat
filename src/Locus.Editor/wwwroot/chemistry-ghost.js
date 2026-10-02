import {journal} from './local-state.js';
// Preview text never enters the textarea, journal or clipboard before a validated transaction.
export function bindGhost(element,reference,enqueue,isComposing,inputSerial){
  const root=element.closest('.locus-app'),shell=element.closest('.source-shell');
  const overlay=shell.querySelector('.source-ghost'),hint=shell.querySelector('.ghost-hint');
  const equation=document.createElement('div');equation.className='ghost-equation';equation.hidden=true;shell.append(equation);
  const abort=new AbortController(),signal=abort.signal;
  let proposal=null,epoch=0,visible=false,caret=-1,committing=false,preparing=false;
  const blocked=new Set();
  const key=p=>p.id+':'+p.epoch;
  function allowed(p){return p&&element.isConnected&&element.value===p.raw&&root.dataset.version===String(p.version)&&root.dataset.inputPending!=='true'&&
    root.dataset.busy!=='true'&&!isComposing()&&document.hasFocus()&&document.activeElement===element&&element.selectionStart===element.selectionEnd&&
    ((element.selectionStart>=p.start&&element.selectionStart<=p.end)||element.selectionStart===p.closedEnd)&&!blocked.has(key(p));}
  function hide(){visible=false;overlay.hidden=true;hint.hidden=true;equation.hidden=true;root.dataset.ghostVisible='false';}
  function invalidate(block=true){epoch++;if(block&&proposal){blocked.add(key(proposal));if(blocked.size>64)blocked.delete(blocked.values().next().value);}hide();}
  function render(){
    if(committing||preparing||!allowed(proposal)){hide();return;}
    const style=getComputedStyle(element);
    for(const name of ['fontFamily','fontSize','fontWeight','fontStyle','lineHeight','letterSpacing','wordSpacing','textIndent','textTransform','paddingTop','paddingBottom','paddingLeft','paddingRight','borderTopWidth','borderBottomWidth','borderLeftWidth','borderRightWidth','tabSize'])overlay.style[name]=style[name];
    overlay.style.width=element.clientWidth+'px';overlay.style.height=element.clientHeight+'px';
    // A closed wrapper stays visible: place its hint after the closer, never paint over source text.
    const position=proposal.closedEnd>=0?proposal.closedEnd:proposal.end;
    const before=document.createTextNode(proposal.raw.slice(0,position)+(proposal.closedEnd>=0?' ':'')),text=document.createElement('span');text.textContent=proposal.products;text.className='ghost-products';
    overlay.replaceChildren(before,text);overlay.hidden=false;overlay.scrollTop=element.scrollTop;overlay.scrollLeft=element.scrollLeft;
    hint.textContent=proposal.space?'Enter hoặc Space: dùng toàn phương trình đang xem · Esc: bỏ gợi ý':'Enter: dùng toàn phương trình đang xem · Esc: bỏ gợi ý';hint.hidden=false;
    const math=document.createElement('div');math.innerHTML=proposal.mathMl;const change=document.createElement('p');change.textContent=proposal.leftChange;equation.replaceChildren(math,change);equation.hidden=false;
    caret=element.selectionStart;visible=true;root.dataset.ghostVisible='true';
  }
  function stamp(){return {raw:element.value,selectionStart:element.selectionStart,selectionEnd:element.selectionEnd,composing:isComposing(),pending:root.dataset.inputPending==='true',focused:document.hasFocus()&&document.activeElement===element};}
  async function accept(p,input,space,ticket){
    if(epoch!==ticket||!allowed(p)){preparing=false;return;}
    let plan;const initialSerial=inputSerial();
    try{
      plan=await reference.invokeMethodAsync('PrepareGhost',p.id,input,space);
      if(!plan||epoch!==ticket||!allowed(p)){
        if(plan)await reference.invokeMethodAsync('CancelGhost',plan.token);return;
      }
      // The DOM changes in one synchronous step after the last live-input check. Later keystrokes
      // see this text and queue after CommitGhost on the same input chain.
      committing=true;root.dataset.inputPending='true';proposal=null;hide();
      element.value=plan.raw;element.setSelectionRange(plan.start,plan.end);
      const ok=await reference.invokeMethodAsync('CommitGhost',plan.token);
      if(!ok&&element.value===plan.raw){element.value=input.raw;element.setSelectionRange(input.selectionStart,input.selectionEnd);}
      if(ok){journal(element.value);await reference.invokeMethodAsync('GhostCommitted');}
    }finally{preparing=false;committing=false;if(inputSerial()===initialSerial&&root.dataset.inputPending==='true')root.dataset.inputPending='false';}
  }
  function keydown(event){
    if(committing)return false;
    if(event.target!==element)return false;
    const noModifiers=!event.ctrlKey&&!event.metaKey&&!event.altKey&&!event.shiftKey;
    const match=(event.key==='Enter'||event.key===' '&&proposal?.space)&&noModifiers;
    if(match&&visible&&allowed(proposal)&&!event.repeat&&!event.isComposing&&event.keyCode!==229&&!event.getModifierState('AltGraph')){
      const p=proposal,input=stamp(),ticket=epoch;event.preventDefault();preparing=true;hide();enqueue(()=>accept(p,input,event.key===' ',ticket));return true;
    }
    if(!['Shift','Control','Alt','Meta'].includes(event.key))invalidate();return false;
  }
  document.addEventListener('pointerdown',event=>{if(preparing)invalidate();if(visible&&event.target!==element)invalidate();},{capture:true,signal});
  root.addEventListener('click',event=>{if(committing){event.preventDefault();event.stopImmediatePropagation();}},{capture:true,signal});
  element.addEventListener('blur',()=>invalidate(visible),{signal});
  window.addEventListener('blur',()=>invalidate(visible),{signal});
  document.addEventListener('selectionchange',()=>{if(visible&&element.selectionStart!==caret)invalidate();else render();},{signal});
  element.addEventListener('focus',render,{signal});element.addEventListener('scroll',render,{signal});
  const resize=new ResizeObserver(render);resize.observe(element);
  return {keydown,invalidate,set(p){if(proposal?.id!==p?.id||proposal?.epoch!==p?.epoch)epoch++;proposal=p;render();},dispose(){abort.abort();resize.disconnect();hide();equation.remove();}};
}
