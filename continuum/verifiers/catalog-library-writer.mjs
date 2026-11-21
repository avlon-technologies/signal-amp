import { readFileSync, readdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const implementationAt = process.argv.indexOf('--implementation');
const implementation = resolve(implementationAt < 0 ? process.cwd() : process.argv[implementationAt + 1]);
const requireFromApp = createRequire(join(implementation, 'package.json'));
const sdk = process.env.CONTINUUM_VERIFIER_SDK ?? requireFromApp.resolve('@avlon/continuum-conformance');
const { runVerifier } = await import(pathToFileURL(sdk).href);

function sources(directory, prefix = '') {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const relative = prefix + entry.name;
    if (entry.isDirectory() && !['obj', 'bin', 'node_modules', '.git'].includes(entry.name))
      return sources(join(directory, entry.name), relative + '/');
    return entry.isFile() && entry.name.endsWith('.cs') ? [relative] : [];
  });
}

await runVerifier({
  declaration: new URL('../verification/catalog-library-writer.verification.json', import.meta.url),
  sources: [import.meta.url],
  modelSource: 'continuum/model/music-player.adl',
  async run(v) {
    const sites = [];
    for (const file of sources(v.implementation)) {
      const source = readFileSync(join(v.implementation, file), 'utf8');
      for (const match of source.matchAll(/\.(ExecuteNonQuery|ExecuteScalar|ExecuteReader)\s*\(/g)) {
        const line = source.slice(0, match.index).split('\n').length;
        sites.push({ file, path: file + ':' + line, method: match[1] });
      }
    }
    const writes = sites.filter(site => site.method === 'ExecuteNonQuery');
    const outside = writes.find(site => site.file !== 'Catalog.cs');
    const writerFile = outside?.file ?? 'Catalog.cs';
    await v.check('sqlite-sites', { path: '**/*.cs' }, () => ({
      method: 'Scanned C# command execution sites for the SQLite API',
      discovered: [...new Set(writes.map(site => site.file))],
      unresolved: writes.filter(site => site.file !== 'Catalog.cs')
        .map(site => ({ path: site.path, why: 'No ADL service mapping for this write site' })),
    }));
    await v.check('catalog-writer', { path: writerFile }, () => {
      const observed = writes.filter(site => site.file === writerFile);
      if (!observed.length) throw new Error('No direct SQLite write sites were found.');
      return {
        writer: { service: writerFile === 'Catalog.cs'
          ? '::MusicPlayer.Listening.Collection.Catalog'
          : '::MusicPlayer.Listening.UserInterface.DesktopShell' },
        store: '::MusicPlayer.Listening.Collection.Library',
        sites: observed.map(site => site.path),
      };
    });
  },
});
