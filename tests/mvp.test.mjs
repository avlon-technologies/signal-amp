import { join } from 'node:path';
import { after, before, test } from 'node:test';
import { makeWorkspace, prepare, release, smoke } from './mvp-smoke.mjs';

let scratch;
let binary;
before(async () => {
  scratch = await makeWorkspace();
  binary = await prepare(scratch);
});
after(async () => {
  if (scratch) await release(scratch);
});
for (const [probe, name] of [
  ['catalog', 'persists ordered repeated tracks and preserves identity on relink'],
  ['import', 'stages duplicate selections once and adds the confirmed MP3'],
  ['play', 'Play starts a selected library track and displays its title'],
  ['spectrum', 'decoded MP3 drives the spectrum and reaches normal completion']
]) {
  test(name, () => smoke(probe, join(scratch, probe), binary));
}
