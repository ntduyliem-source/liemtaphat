const bindings=new WeakMap();
export function wireResult(root,reference){
  bindings.get(root)?.();const abort=new AbortController(),signal=abort.signal;let serial=0;
  const parent=node=>node?.nodeType===Node.ELEMENT_NODE?node:node?.parentElement;
  const clear=()=>reference.invokeMethodAsync('ResultSelectionChanged',root.closest('.locus-app').dataset.version,0,0,[]).catch(()=>{});
  function endpoint(node,offset,end){
    const element=parent(node),region=element?.closest('.result-region');
    if(region&&root.contains(region)){
      const content=region.querySelector('.region-content');
      if(content.dataset.formula==='false'&&content.contains(node)){
        const prefix=document.createRange();prefix.selectNodeContents(content);prefix.setEnd(node,offset);
        return Number(region.dataset.sourceStart)+prefix.toString().length;
      }
      return Number(region.dataset[end?'sourceEnd':'sourceStart']);
    }
    const text=element?.closest('[data-text-start]');
    if(text&&root.contains(text)){
      const prefix=document.createRange();prefix.selectNodeContents(text);prefix.setEnd(node,offset);
      return Number(text.dataset.textStart)+prefix.toString().length;
    }
    if(node===root){
      const sibling=end?root.childNodes[offset-1]:root.childNodes[offset];
      const source=parent(sibling)?.closest('[data-source-start],[data-text-start]');
      if(source)return Number(source.dataset[end?'sourceEnd':'sourceStart']??source.dataset.textStart);
      return end?Number(root.closest('.locus-app').querySelector('#source').value.length):0;
    }
    return null;
  }
  async function read(){
    const selection=document.getSelection();if(!selection?.rangeCount)return;
    if(selection.isCollapsed){
      if(root.contains(selection.anchorNode)&&!parent(selection.anchorNode)?.closest('.result-region'))
        await reference.invokeMethodAsync('ResultSelectionChanged',root.closest('.locus-app').dataset.version,0,0,[]).catch(()=>{});
      return;
    }
    const range=selection.getRangeAt(0);
    if(!root.contains(range.startContainer)||!root.contains(range.endContainer))return;
    const start=endpoint(range.startContainer,range.startOffset,false),end=endpoint(range.endContainer,range.endOffset,true);
    if(start===null||end===null||start>end)return;
    const partial=[];
    for(const region of root.querySelectorAll('.result-region')){
      const content=region.querySelector('.region-content');if(!range.intersectsNode(content))continue;
      const bounds=document.createRange();bounds.selectNodeContents(content);
      if(range.compareBoundaryPoints(Range.START_TO_START,bounds)>0||range.compareBoundaryPoints(Range.END_TO_END,bounds)<0)partial.push(region.dataset.regionId);
    }
    const app=root.closest('.locus-app');if(app.dataset.inputPending==='true')return;
    const version=app.dataset.version,id=++serial;
    if(id===serial)await reference.invokeMethodAsync('ResultSelectionChanged',version,start,end,partial).catch(()=>{});
  }
  // Read the settled selection. Moving focus to a toolbar does not discard the logical selection.
  root.addEventListener('pointerup',()=>queueMicrotask(read),{signal});
  root.addEventListener('keyup',event=>{if(event.shiftKey||event.key.startsWith('Arrow'))read();},{signal});
  root.addEventListener('keydown',event=>{
    if(event.key==='Escape'){
      event.preventDefault();event.stopPropagation();document.getSelection()?.removeAllRanges();clear();return;
    }
    if((event.key==='Enter'||event.key===' ')&&event.target.matches('.region-content')){
      event.preventDefault();event.target.click();return;
    }
    if((event.ctrlKey||event.metaKey)&&!event.altKey&&event.key.toLowerCase()==='a'){
      event.preventDefault();const range=document.createRange();range.selectNodeContents(root);
      const selection=document.getSelection();selection.removeAllRanges();selection.addRange(range);
      reference.invokeMethodAsync('ResultSelectAll',root.closest('.locus-app').dataset.version).catch(()=>{});
    }
  },{signal});
  root.addEventListener('click',event=>{
    const selected=document.getSelection();
    if(!event.target.closest('button')&&selected&&!selected.isCollapsed&&root.contains(selected.anchorNode)){event.stopPropagation();return;}
    if(!event.target.closest('.result-region'))clear();
  },{signal,capture:true});
  const observer=new MutationObserver(()=>{if(!root.isConnected){abort.abort();observer.disconnect();bindings.delete(root);}});observer.observe(document.body,{subtree:true,childList:true});
  bindings.set(root,()=>{abort.abort();observer.disconnect();});
}
