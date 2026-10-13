import {readFile,writeFile,mkdir,readdir,cp} from 'node:fs/promises';
import {join,relative} from 'node:path';
import {createHash} from 'node:crypto';
const build=process.argv[2];if(!build)throw Error('Build directory required');
const rawBase=process.argv[3]??'/';
if(!rawBase.startsWith('/')||!rawBase.endsWith('/')||rawBase.includes('..')||rawBase.includes('?')||rawBase.includes('#'))throw Error('Base path must start and end with /');
const base=rawBase.replace(/\/+/g,'/');
const source=join(build,'web/wwwroot'),root=join(build,'site'),id=build.replaceAll('\\','/').split('/').at(-1),release=`releases/${id}`;
await mkdir(join(root,release),{recursive:true});
await cp(source,join(root,release),{recursive:true,filter:p=>!p.endsWith('.br')&&!p.endsWith('.gz')&&!p.endsWith('.pdb')});
let html=await readFile(join(source,'index.html'),'utf8');
const assetRoot=`${base}${release}/`;
const resourceUrl=value=>/^(?:[a-z][a-z0-9+.-]*:|\/|#)/i.test(value)?value:assetRoot+value.replace(/^\.\//,'');
// Blazor navigation requires the current URL to be inside <base>. Keep it stable,
// while all resources (including import-map integrity keys) identify one release.
html=html.replace('<base href="/">',`<base href="${base}"><meta name="locus-build" content="${id}"><meta name="locus-assets" content="${assetRoot}">`);
html=html.replace(/<(?:script|link)\b[^>]*>/g,tag=>tag.replace(/\b(src|href)="([^"]+)"/g,(_,attribute,value)=>`${attribute}="${resourceUrl(value)}"`));
html=html.replace(/<script type="importmap">([\s\S]*?)<\/script>/,(_,json)=>{
  const map=JSON.parse(json||'{}');
  const entries=value=>Object.fromEntries(Object.entries(value??{}).map(([key,target])=>[resourceUrl(key),resourceUrl(target)]));
  map.imports=entries(map.imports);
  map.scopes=Object.fromEntries(Object.entries(map.scopes??{}).map(([key,value])=>[resourceUrl(key),entries(value)]));
  map.integrity=Object.fromEntries(Object.entries(map.integrity??{}).map(([key,value])=>[resourceUrl(key),value]));
  return `<script type="importmap">${JSON.stringify(map)}</script>`;
});
html=html.replace(/<script src="([^"]*blazor\.webassembly[^"]*)"><\/script>/,(_,src)=>`<script id="locus-runtime" type="application/json" data-src="${src}"></script><script type="module" src="${assetRoot}boot.js"></script>`);
if(!html.includes('locus-runtime'))throw Error('Blazor boot placeholder not found');
await writeFile(join(root,'index.html'),html);
await writeFile(join(root,release,'index.html'),html);
// GitHub Pages replaces the whole artifact. Recover old application links even in
// a fresh browser, where no historical service worker/cache exists.
const releaseNavigation=/^releases\/[A-Za-z0-9][A-Za-z0-9_-]*(?:\/|\/index\.html)?$/;
const safeBase=base.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
await writeFile(join(root,'404.html'),`<!doctype html><html lang="vi"><meta charset="utf-8"><title>Locus</title><p>Không tìm thấy trang này. <a href="${safeBase}">Mở Locus</a></p><script>
const base=${JSON.stringify(base)},path=location.pathname;
if(path.startsWith(base)&&new RegExp(${JSON.stringify(releaseNavigation.source)}).test(path.slice(base.length)))location.replace(base+location.search+location.hash);
</script></html>`);
const assets=[];
async function walk(dir){for(const entry of await readdir(dir,{withFileTypes:true})){const file=join(dir,entry.name);if(dir===root&&['service-worker.js','_headers','.nojekyll'].includes(entry.name))continue;if(entry.isDirectory())await walk(file);else assets.push({url:base+relative(root,file).replaceAll('\\','/'),integrity:'sha256-'+createHash('sha256').update(await readFile(file)).digest('base64')});}}
await walk(root);
const sw=`const CACHE=${JSON.stringify('locus-app-'+id)},BASE=${JSON.stringify(base)},ASSETS=${JSON.stringify(assets)};
const RELEASE_NAVIGATION=new RegExp(${JSON.stringify(releaseNavigation.source)});
self.addEventListener('install',event=>event.waitUntil((async()=>{
  const cache=await caches.open(CACHE);let cursor=0;
  try{await Promise.all(Array.from({length:6},async()=>{while(cursor<ASSETS.length){const item=ASSETS[cursor++];const response=await fetch(new Request(item.url,{cache:'no-cache',credentials:'same-origin',integrity:item.integrity}));if(!response.ok||response.redirected)throw Error('Incomplete release');await cache.put(item.url,response);}}));}
  catch(error){await caches.delete(CACHE);throw error;}
})()));
self.addEventListener('activate',event=>event.waitUntil(self.clients.claim()));
self.addEventListener('message',event=>{if(event.data?.type==='ACTIVATE')self.skipWaiting();});
self.addEventListener('fetch',event=>{
  const url=new URL(event.request.url);if(url.origin!==self.location.origin||event.request.method!=='GET')return;
  const route=url.pathname.startsWith(BASE)?url.pathname.slice(BASE.length):null;
  if(event.request.mode==='navigate'&&(route===''||route==='index.html'||(route!==null&&RELEASE_NAVIGATION.test(route)))){event.respondWith(caches.open(CACHE).then(cache=>cache.match(BASE+'index.html')).then(r=>r||new Response('Offline release unavailable',{status:503})));return;}
  if(url.pathname.startsWith(BASE+'releases/')){
    // Old live tabs keep their exact runtime, grammar, renderer and font cache after activation.
    const version=url.pathname.slice((BASE+'releases/').length).split('/')[0];event.respondWith(caches.open('locus-app-'+version).then(cache=>cache.match(event.request,{ignoreSearch:true})).then(r=>r||new Response('Release asset unavailable',{status:503})));return;
  }
});
`;
await writeFile(join(root,'service-worker.js'),sw);
await writeFile(join(root,'.nojekyll'),'');
await writeFile(join(root,'_headers'),'/service-worker.js\n  Cache-Control: no-cache\n/index.html\n  Cache-Control: no-cache\n');
await writeFile(join(build,'release.json'),JSON.stringify({id,basePath:base,files:assets.length,assets},null,2));
console.log(JSON.stringify({id,basePath:base,files:assets.length,root}));
