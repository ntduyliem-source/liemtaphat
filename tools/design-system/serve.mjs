import {createServer} from 'node:http';
import {readFile,stat} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
const receipt=JSON.parse(await readFile(new URL('../../artifacts/design-system/current-build.json',import.meta.url),'utf8'));
const root=resolve(receipt.catalog),port=Number(process.argv[2]??4199);
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json','.wasm':'application/wasm','.woff2':'font/woff2','.svg':'image/svg+xml','.md':'text/plain; charset=utf-8'};
createServer(async(req,res)=>{
  try{
    const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const file=resolve(root,'.'+(pathname==='/'?'/index.html':pathname));
    if(!file.startsWith(root+sep)||!(await stat(file)).isFile()){res.writeHead(404);res.end();return;}
    res.writeHead(200,{'Content-Type':types[extname(file)]??'application/octet-stream','Cache-Control':'no-store'});res.end(await readFile(file));
  }catch{res.writeHead(404);res.end();}
}).listen(port,'127.0.0.1',()=>console.log(`Locus catalog: http://127.0.0.1:${port}/`));
