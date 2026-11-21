import { cpSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const cliRoot = process.env.CONTINUUM_CLI_ROOT;
if (!cliRoot) throw new Error('Set CONTINUUM_CLI_ROOT to the installed @avlon/continuum-cli package directory.');
const install = resolve(cliRoot);
const metadata = JSON.parse(readFileSync(join(install, 'package.json'), 'utf8'));
if (metadata.name !== '@avlon/continuum-cli' || !/^0\.48\./.test(metadata.version))
  throw new Error('This acquisition shim supports @avlon/continuum-cli 0.48.x.');
const scratch = mkdtempSync(join(tmpdir(), 'acq-'));
try {
  const copy = join(scratch, 'cli');
  cpSync(install, copy, { recursive: true });
  const file = join(copy, 'src', 'commands', 'conformance-assessment.js');
  const source = readFileSync(file, 'utf8');
  const line = "const facts = importFactsOf(co.dir, cited, 'conformance check');";
  if (source.split(line).length !== 2) throw new Error('Continuum static acquisition hook changed.');
  const injected = [
    "const { pathToFileURL } = await import('node:url');",
    "      const adapter = await import(pathToFileURL(join(co.dir, 'continuum/adapters/csharp/import-facts.mjs')).href);",
    "      const facts = adapter.importFactsOf(co.dir, cited);"
  ].join('\n');
  writeFileSync(file, source.replace(line, injected), 'utf8');

  const sdk = join(install, 'node_modules', '@avlon', 'continuum-conformance');
  const result = spawnSync(process.execPath, [join(copy, 'bin', 'continuum.mjs'), ...process.argv.slice(2)], {
    stdio: 'inherit',
    env: { ...process.env,
      CONTINUUM_ENGINE: process.env.CONTINUUM_ENGINE || sdk,
      CONTINUUM_VERIFIER_SDK: process.env.CONTINUUM_VERIFIER_SDK || join(sdk, 'dist', 'index.js') }
  });
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 2;
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
