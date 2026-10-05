import {readFile,readdir} from 'node:fs/promises';
import {resolve} from 'node:path';
const root=resolve(new URL('../..',import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1'));
const read=p=>readFile(resolve(root,p),'utf8');
const issues=[];
function assert(ok,message){if(!ok)issues.push(message);}
function checkScope(css,file){
  css=css.replace(/\/\*[\s\S]*?\*\//g,'');
  let pos=0;
  while(pos<css.length){
    const open=css.indexOf('{',pos);if(open<0)break;
    let end=open+1,depth=1;
    for(;end<css.length&&depth;end++){if(css[end]==='{')depth++;if(css[end]==='}')depth--;}
    const selector=css.slice(pos,open).trim(),body=css.slice(open+1,end-1);
    if(selector.startsWith('@'))checkScope(body,file);
    else for(const part of selector.split(','))assert(part.trim().startsWith('.legacy-editors'),`${file}: selector escaped legacy boundary: ${part}`);
    pos=end;
  }
}
const tokens=await read('src/Locus.DesignSystem/wwwroot/styles/tokens.css');
const known=new Set([...tokens.matchAll(/(--[\w-]+)\s*:/g)].map(m=>m[1]));
assert(!/var\([^)]+\)[a-f0-9]+\s*[;]/i.test(tokens),'Malformed token reference');
for(const dir of ['src/Locus.DesignSystem/wwwroot/styles','src/Locus.Editor/wwwroot/formula']){
  for(const file of await readdir(resolve(root,dir))){
    if(!file.endsWith('.css'))continue;
    const css=await read(dir+'/'+file);
    for(const match of css.matchAll(/var\((--[\w-]+)/g))assert(known.has(match[1]),`${file}: unknown ${match[1]}`);
    if(file!=='tokens.css')assert(!/(?:#[0-9a-f]{3,8}\b|rgba?\(\s*\d)/i.test(css),`${file}: palette value outside tokens`);
  }
}
const formula=await read('src/Locus.Editor/wwwroot/formula.css');
assert(!/@import[^;]*legacy/.test(formula),'Formula imports legacy CSS');
for(const file of await readdir(resolve(root,'src/Locus.Editor/wwwroot/legacy'))){if(file.endsWith('.css'))checkScope(await read('src/Locus.Editor/wwwroot/legacy/'+file),file);}
for(const host of ['Locus.Web','Locus.Desktop.Shared']){
  const html=await read(`src/${host}/wwwroot/index.html`);
  assert(html.includes('Locus.DesignSystem/styles/foundations.css'),`${host}: missing foundations`);
  assert(!/Locus.Editor\/(editor|studio|studio-tokens)\.css/.test(html),`${host}: old global stylesheet`);
}
const result=await read('src/Locus.Editor/Formula/Components/FormulaResult.razor');
for(const hook of ['id="mixed-result"','data-region-id','data-source-start','data-source-end','data-text-start','data-ignore-selection'])assert(result.includes(hook),'Selection bridge hook missing: '+hook);
assert(!result.includes('fx'),'fx must not enter the result');
const workspace=await read('src/Locus.Editor/Workspace.razor');
for(const component of ['FormulaDetectionBar','FormulaSourcePanel','FormulaResult','FormulaSuggestions','FormulaExportBar'])assert(workspace.includes('<'+component),`Unused extracted component: ${component}`);
if(issues.length){console.error(issues.join('\n'));process.exitCode=1;}else console.log('Design system ownership, token references and selection hooks: PASS');
