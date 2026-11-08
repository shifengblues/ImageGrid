import {readFile,writeFile} from 'node:fs/promises';
const text=['Image Grid Web — GPL-3.0-only\nSource: https://github.com/shifengblues/ImageGrid\n'];
for(const name of ['fast-png','fflate','iobuffer'])text.push(`\n=== ${name} ===\n${await readFile(new URL(`../node_modules/${name}/LICENSE`,import.meta.url),'utf8')}`);
await writeFile(new URL('../dist/THIRD-PARTY-NOTICES.txt',import.meta.url),text.join('\n'));
await writeFile(new URL('../dist/LICENSE',import.meta.url),await readFile(new URL('../../LICENSE',import.meta.url)));
