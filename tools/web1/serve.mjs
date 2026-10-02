import http from 'node:http';
import {readFile,stat,realpath,mkdir,writeFile} from 'node:fs/promises';
import {resolve,relative,extname,dirname,isAbsolute} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash,randomUUID} from 'node:crypto';

const args=process.argv.slice(2);
const option=name=>{const at=args.indexOf(name);return at<0?undefined:args[at+1];};
const port=Number(option('--port')??4183);
if(!Number.isInteger(port)||port<1024||port>65535)throw Error('Port must be 1024..65535');
const workspace=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const pointer=option('--build-file')??resolve(workspace,'artifacts/web1/current-build.json');
const fixedRoot=option('--root');
const root=await realpath(fixedRoot??JSON.parse(await readFile(pointer,'utf8')).web);
const index=await readFile(resolve(root,'index.html'),'utf8');
const build=index.match(/name="locus-build" content="([^"]+)"/)?.[1];
if(!build)throw Error('The directory is not a versioned Locus WEB1 build');
const rootId=createHash('sha256').update(root.toLowerCase()).digest('hex');
const instance=randomUUID(),stopToken=randomUUID(),startedAtUtc=new Date().toISOString();
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.wasm':'application/wasm','.css':'text/css; charset=utf-8','.svg':'image/svg+xml','.png':'image/png','.dat':'application/octet-stream','.txt':'text/plain; charset=utf-8','.woff2':'font/woff2'};
const inside=path=>{const rel=relative(root,path);return !rel.startsWith('..')&&!isAbsolute(rel);};
const health={application:'locus-web1-local/1',instance,rootId,build,port,pid:process.pid,startedAtUtc};
const server=http.createServer(async(req,res)=>{
  res.setHeader('Cache-Control','no-cache');res.setHeader('X-Content-Type-Options','nosniff');
  try{
    if(req.headers.host!==`127.0.0.1:${port}`){res.writeHead(403).end('Use the loopback URL');return;}
    const route=decodeURIComponent(new URL(req.url,`http://127.0.0.1:${port}`).pathname);
    if(route==='/__locus_local/health'&&req.method==='GET'){res.writeHead(200,{'Content-Type':'application/json'}).end(JSON.stringify(health));return;}
    if(route==='/__locus_local/stop'&&req.method==='POST'){
      if(req.headers['x-locus-stop']!==stopToken){res.writeHead(403).end();return;}
      res.writeHead(200).end('Stopped');server.close();server.closeIdleConnections();return;
    }
    if(!['GET','HEAD'].includes(req.method)){res.writeHead(405,{'Allow':'GET, HEAD'}).end();return;}
    const path=resolve(root,'.'+route+(route.endsWith('/')?'index.html':''));
    if(!inside(path)||!inside(await realpath(path))){res.writeHead(403).end();return;}
    const file=await stat(path);
    if(!file.isFile()){res.writeHead(404).end();return;}
    res.writeHead(200,{'Content-Type':types[extname(path)]??'application/octet-stream','Content-Length':file.size});
    res.end(req.method==='HEAD'?undefined:await readFile(path));
  }catch{if(!res.headersSent)res.writeHead(404);res.end('Not found');}
});
await new Promise((done,fail)=>{server.once('error',fail);server.listen(port,'127.0.0.1',done);});
const receipt=option('--receipt');
if(receipt){await mkdir(dirname(resolve(receipt)),{recursive:true});await writeFile(receipt,JSON.stringify({...health,root,stopToken},null,2));}
console.log(`Locus WEB1: http://127.0.0.1:${port}/ (${build})`);
