import {readFile,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const hosts=['chromium','firefox','desktop'];const scenes=await Promise.all(hosts.map(h=>readFile(`artifacts/e3/runs/${h}/scenes.json`,'utf8').then(JSON.parse)));
const results=[];
// Editing histories differ because Firefox fill emits composition events; compare artwork, not the source revision binding.
const artwork=svg=>svg.replace(/data-candidate-id="[a-f0-9]+"/,'data-candidate-id="SOURCE_BINDING"');
for(const sample of scenes[0]){
 const matches=scenes.map(items=>items.find(s=>s.raw===sample.raw&&s.domain===sample.domain));
 const pass=matches.every(s=>s&&s.latex===sample.latex&&artwork(s.svg)===artwork(sample.svg));
 results.push({raw:sample.raw,domain:sample.domain,pass,sha256:matches.map(s=>s?.sha256)});
}
assert.equal(results.length,14);
await writeFile('artifacts/e3/renderer-parity.json',JSON.stringify({hosts,comparison:'Identical SVG artwork and LaTeX after excluding only data-candidate-id, which binds each host editing history. Each host separately asserts preview ID equals its rendered SVG ID; native/WASM wire parity covers identical source revisions.',results,summary:{checks:results.length,passed:results.filter(r=>r.pass).length,failed:results.filter(r=>!r.pass).length}},null,2));
assert(results.every(r=>r.pass),'Renderer output differs across hosts');console.log('14 science SVG artworks and LaTeX outputs match on Chromium, Firefox and Desktop');
