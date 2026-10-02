import {readFile,writeFile} from 'node:fs/promises';
let text=await readFile('tools/sh/verify-ui.mjs','utf8');
text=text.replace("from 'playwright'","from '../sh/node_modules/playwright/index.mjs'").replaceAll('artifacts/sh/','artifacts/web1/').replaceAll('http://127.0.0.1:4182/','http://127.0.0.1:4183/').replaceAll('9331','9333').replaceAll('LOCUS_SH_URL','LOCUS_WEB1_URL').replaceAll("await import('./_content/Locus.Editor/renderer.js')","await import(new URL('_content/Locus.Editor/renderer.js',document.baseURI).href)");
// New clipboard capabilities may report a successful SVG text fallback explicitly.
await writeFile('tools/web1/verify-regression.mjs',text);
for(const name of ['verify-worker','verify-export-safety']){
 let source=await readFile(`tools/sh/${name}.mjs`,'utf8');
 source=source.replace("from 'playwright'","from '../sh/node_modules/playwright/index.mjs'").replaceAll('artifacts/sh/','artifacts/web1/').replaceAll('4182','4183').replaceAll('LOCUS_SH_URL','LOCUS_WEB1_URL');
 for(const module of ['worker-client','editor','renderer'])source=source.replaceAll(`await import('./_content/Locus.Editor/${module}.js')`,`await import(new URL('_content/Locus.Editor/${module}.js',document.baseURI).href)`);
 source=source.replaceAll("managerFor('./worker/worker.js')","managerFor(new URL('worker/worker.js',document.baseURI).href)");
 await writeFile(`tools/web1/${name}.mjs`,source);
}
