// Restore the known WEB1 synthetic restart fixture after the first E3 test used the default profile.
import {chromium} from '../sh/node_modules/playwright/index.mjs';
import {readFile,writeFile} from 'node:fs/promises';
const reference=JSON.parse(await readFile('artifacts/web1/native-before-restart.json','utf8'));
const browser=await chromium.connectOverCDP('http://127.0.0.1:9334');
const page=browser.contexts()[0].pages().find(p=>p.url()==='https://0.0.0.1/');
await page.locator('#source').fill('');
for(const [name,on] of [['math',true],['physics',false],['chemistry',false]])await page.locator('#detect-'+name).setChecked(on);
await page.locator('#mode').selectOption('Explicit');
await page.locator('#font-size').selectOption(reference.font);await page.locator('#pixel-scale').selectOption(reference.scale);await page.locator('#white-background').setChecked(reference.background);
await page.locator('#source').fill(reference.raw);
await page.waitForFunction(raw=>document.querySelector('#source-count')?.textContent===`${raw.length} ký tự`,reference.raw);
await page.waitForTimeout(800);await page.locator('#formula-preview[data-rendered=true] svg').waitFor();
const state=await page.evaluate(()=>({raw:document.querySelector('#source').value,font:document.querySelector('#font-size').value,scale:document.querySelector('#pixel-scale').value,background:document.querySelector('#white-background').checked}));
if(['raw','font','scale','background'].some(key=>state[key]!==reference[key]))throw Error('Restoration mismatch');
await writeFile('artifacts/e3/default-profile-restored.json',JSON.stringify({reference:'artifacts/web1/native-before-restart.json',...state,scope:'Known synthetic WEB1 fixture restored; all subsequent tests use --profile.'},null,2));
await browser.close();
