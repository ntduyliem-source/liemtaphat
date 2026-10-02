import http from 'node:http';
import {readFile, stat} from 'node:fs/promises';
import {resolve, relative, extname} from 'node:path';
const root=resolve(process.argv[2]??'artifacts/web/browser/wwwroot');
const port=Number(process.argv[3]??4181);
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.wasm':'application/wasm','.css':'text/css; charset=utf-8','.svg':'image/svg+xml','.png':'image/png','.dat':'application/octet-stream','.woff2':'font/woff2','.dll':'application/octet-stream'};
http.createServer(async(req,res)=>{
  try {
    const route=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const path=resolve(root,'.'+route+(route.endsWith('/')?'index.html':''));
    if(relative(root,path).startsWith('..')){res.writeHead(403).end();return;}
    const info=await stat(path);if(!info.isFile()){res.writeHead(404).end();return;}
    res.writeHead(200,{'Content-Type':types[extname(path)]??'application/octet-stream','Content-Length':info.size,'Cache-Control':'public, max-age=60','X-Content-Type-Options':'nosniff'});
    res.end(await readFile(path));
  }catch{res.writeHead(404).end('Not found');}
}).listen(port,'127.0.0.1',()=>console.log(`Locus static files: http://127.0.0.1:${port}/ from ${root}`));
