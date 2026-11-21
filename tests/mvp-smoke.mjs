import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { mkdir, mkdtemp, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const root = fileURLToPath(new URL('../', import.meta.url));
let build;

export async function prepare(scratch) {
  const artifacts = join(scratch, 'build');
  const args = ['build', 'WindowsMp3Player.csproj', '-c', 'Release',
    '-r', 'win-x64', '--self-contained', 'false', '--artifacts-path', artifacts, '-p:NuGetAudit=false'];
  if (process.env.SIGNAL_AMP_NUGET_SOURCE) args.push('--source', process.env.SIGNAL_AMP_NUGET_SOURCE);
  if (process.env.SIGNAL_AMP_NUGET_FALLBACK) args.push('-p:RestoreFallbackFolders=' + process.env.SIGNAL_AMP_NUGET_FALLBACK);
  build ??= run('dotnet', args, {
    cwd: root, windowsHide: true, timeout: 120000,
    env: { ...process.env, NUGET_SCRATCH: join(scratch, 'nuget-scratch'),
      NUGET_HTTP_CACHE_PATH: join(scratch, 'nuget-http') }
  }).then(() => join(artifacts, 'bin', 'WindowsMp3Player', 'release_win-x64', 'WindowsMp3Player.dll'));
  return build;
}

export async function makeWorkspace() {
  const artifacts = join(root, 'artifacts');
  await mkdir(artifacts, { recursive: true });
  return mkdtemp(join(artifacts, 'signal-amp-'));
}

export function release(scratch) {
  return rm(scratch, { recursive: true, force: true });
}

export async function smoke(probe, scratch, binary) {
  await mkdir(scratch, { recursive: true });
  const sample = join(root, 'tests', 'fixtures', '01-rising-chimes.mp3');
  const probes = {
    catalog: ['--catalog-smoke-test', scratch],
    import: ['--import-smoke-test', sample, join(scratch, 'catalog')],
    play: ['--ui-smoke-test', sample, join(scratch, 'catalog')],
    spectrum: ['--playback-smoke-test', sample]
  };
  try {
    await run('dotnet', [binary, ...probes[probe]], { cwd: root, windowsHide: true, timeout: 18000 });
  } catch (error) {
    if (typeof error.code !== 'number' || error.code < 1 || error.code > 9) throw error;
    assert.fail(`Signal Amp probe "${probe}" returned ${error.code}: ${error.stderr || error.stdout || 'assertion failed'}`);
  }
}
