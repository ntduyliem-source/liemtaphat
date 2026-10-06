import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const build=JSON.parse(await readFile('artifacts/web1/current-build.json','utf8'));
const release=JSON.parse(await readFile(join(build.root,'release.json'),'utf8'));
const digest=bytes=>'sha256-'+createHash('sha256').update(bytes).digest('base64');
const basePath=release.basePath??'/';
for(const asset of release.assets){
  assert.equal(asset.url.startsWith(basePath),true,`${asset.url} must stay inside ${basePath}`);
  assert.equal(digest(await readFile(join(build.web,asset.url.slice(basePath.length)))),asset.integrity,asset.url);
}
const worker=await readFile(join(build.web,'service-worker.js'),'utf8');
const embedded=JSON.parse(worker.match(/,ASSETS=(\[[^\n]+\]);/)[1]);
assert.deepEqual(embedded,release.assets,'Service worker must cache the complete release');
assert.equal((await readFile(join(build.web,'index.html'),'utf8')).includes(`name="locus-build" content="${release.id}"`),true);
const vendor=JSON.parse(await readFile('artifacts/web1/vendor.json','utf8'));
for(const file of vendor)assert.equal(createHash('sha256').update(await readFile(join(build.web,'releases',release.id,'_content/Locus.Editor/vendor',file.path))).digest('hex'),file.sha256,file.path);
const report={status:'PASSED',capturedAtUtc:new Date().toISOString(),build:release.id,basePath,immutableAssets:release.assets.length,rendererAssets:vendor.length,serviceWorkerManifestMatchesBuild:true,scope:basePath==='/'?'Local static build':'Project-path static build'};
await writeFile('artifacts/web1/static-validation.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report));
