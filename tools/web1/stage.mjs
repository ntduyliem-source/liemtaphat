import {readFile,writeFile,mkdir,readdir,cp} from 'node:fs/promises';
import {join,relative} from 'node:path';
import {createHash} from 'node:crypto';
const build=process.argv[2];if(!build)throw Error('Build directory required');
const source=join(build,'web/wwwroot'),root=join(build,'site'),id=build.replaceAll('\\','/').split('/').at(-1),release=`releases/${id}`;
await mkdir(join(root,release),{recursive:true});
await cp(source,join(root,release),{recursive:true,filter:p=>!p.endsWith('.br')&&!p.endsWith('.gz')&&!p.endsWith('.pdb')});
let html=await readFile(join(source,'index.html'),'utf8');
html=html.replace('<base href="/">',`<base href="/${release}/"><meta name="locus-build" content="${id}">`);
html=html.replace(/<script src="([^"]*blazor\.webassembly[^"]*)"><\/script>/,(_,src)=>`<script id="locus-runtime" type="application/json" data-src="${src}"></script><script type="module" src="boot.js"></script>`);
if(!html.includes('locus-runtime'))throw Error('Blazor boot placeholder not found');
await writeFile(join(root,'index.html'),html);
await writeFile(join(root,release,'index.html'),html);
const assets=[];
async function walk(dir){for(const entry of await readdir(dir,{withFileTypes:true})){const file=join(dir,entry.name);if(entry.isDirectory())await walk(file);else assets.push({url:'/'+relative(root,file).replaceAll('\\','/'),integrity:'sha256-'+createHash('sha256').update(await readFile(file)).digest('base64')});}}
await walk(root);
const sw=`const CACHE=${JSON.stringify('locus-app-'+id)},ASSETS=${JSON.stringify(assets)};
self.addEventListener('install',event=>event.waitUntil((async()=>{
  const cache=await caches.open(CACHE);let cursor=0;
  try{await Promise.all(Array.from({length:6},async()=>{while(cursor<ASSETS.length){const item=ASSETS[cursor++];const response=await fetch(new Request(item.url,{cache:'no-cache',credentials:'same-origin',integrity:item.integrity}));if(!response.ok||response.redirected)throw Error('Incomplete release');await cache.put(item.url,response);}}));}
  catch(error){await caches.delete(CACHE);throw error;}
})()));
self.addEventListener('activate',event=>event.waitUntil(self.clients.claim()));
self.addEventListener('message',event=>{if(event.data?.type==='ACTIVATE')self.skipWaiting();});
self.addEventListener('fetch',event=>{
  const url=new URL(event.request.url);if(url.origin!==self.location.origin||event.request.method!=='GET')return;
  if(event.request.mode==='navigate'){event.respondWith(caches.open(CACHE).then(cache=>cache.match('/index.html')).then(r=>r||new Response('Offline release unavailable',{status:503})));return;}
  if(url.pathname.startsWith('/releases/')){
    // Old live tabs keep their exact runtime, grammar, renderer and font cache after activation.
    const version=url.pathname.split('/')[2];event.respondWith(caches.open('locus-app-'+version).then(cache=>cache.match(event.request,{ignoreSearch:true})).then(r=>r||new Response('Release asset unavailable',{status:503})));return;
  }
});
`;
await writeFile(join(root,'service-worker.js'),sw);
await writeFile(join(root,'_headers'),'/service-worker.js\n  Cache-Control: no-cache\n/index.html\n  Cache-Control: no-cache\n');
await writeFile(join(build,'release.json'),JSON.stringify({id,files:assets.length,assets},null,2));
console.log(JSON.stringify({id,files:assets.length,root}));
