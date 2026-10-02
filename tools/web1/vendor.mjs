import {cp,mkdir,readFile,writeFile,readdir} from 'node:fs/promises';
import {join,relative} from 'node:path';
import {createHash} from 'node:crypto';
const root='src/Locus.Editor/wwwroot/vendor';
const entries=[['mathjax','mathjax',['mml-svg-nofont.js','sre','LICENSE','package.json']],['@mathjax/mathjax-newcm-font','mathjax-newcm-font',['svg.js','svg','package.json']]];
const manifest=[];
async function record(path){for(const item of await readdir(path,{withFileTypes:true})){const file=join(path,item.name);if(item.isDirectory())await record(file);else manifest.push({path:relative(root,file).replaceAll('\\','/'),sha256:createHash('sha256').update(await readFile(file)).digest('hex')});}}
for(const [npm,destination,items] of entries){const pkg=JSON.parse(await readFile(`tools/sh/node_modules/${npm}/package.json`,'utf8'));if(pkg.version!=='4.1.3'||pkg.license!=='Apache-2.0')throw Error('Dependency pin changed');const target=join(root,destination);await mkdir(target,{recursive:true});for(const item of items)await cp(`tools/sh/node_modules/${npm}/${item}`,join(target,item),{recursive:true});}
for(const [,destination,items] of entries)for(const item of items){const file=join(root,destination,item);if(['sre','svg'].includes(item))await record(file);else manifest.push({path:relative(root,file).replaceAll('\\','/'),sha256:createHash('sha256').update(await readFile(file)).digest('hex')});}
await mkdir('artifacts/web1',{recursive:true});await writeFile('artifacts/web1/vendor.json',JSON.stringify(manifest.sort((a,b)=>a.path.localeCompare(b.path)),null,2));console.log(`Pinned renderer assets: ${manifest.length}`);
