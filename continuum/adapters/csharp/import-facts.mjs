import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export function importFactsOf(root, paths) {
  const scratchRoot = process.env.SIGNAL_AMP_ADAPTER_SCRATCH || join(root, 'artifacts');
  mkdirSync(scratchRoot, { recursive: true });
  const scratch = mkdtempSync(join(scratchRoot, 'cs-'));
  const appOut = join(scratch, 'app');
  const scannerOut = join(scratch, 'scanner');
  const env = { ...process.env, NUGET_SCRATCH: join(scratch, 'nuget-scratch'),
    NUGET_HTTP_CACHE_PATH: join(scratch, 'nuget-http') };
  const dotnet = (args) => execFileSync('dotnet', args, {
    cwd: root, env, encoding: 'utf8', windowsHide: true, timeout: 120000, maxBuffer: 32 * 1024 * 1024
  });
  const restore = ['-p:NuGetAudit=false'];
  if (env.SIGNAL_AMP_NUGET_SOURCE) restore.push('-p:RestoreSources=' + env.SIGNAL_AMP_NUGET_SOURCE);
  if (env.SIGNAL_AMP_NUGET_FALLBACK) restore.push('-p:RestoreFallbackFolders=' + env.SIGNAL_AMP_NUGET_FALLBACK);
  try {
    const project = 'WindowsMp3Player.csproj';
    dotnet(['build', project, '-c', 'Release', '--artifacts-path', appOut, ...restore]);
    const itemJson = dotnet(['msbuild', project, '-nologo', '-t:ResolveReferences,GenerateGlobalUsings',
      '-p:Configuration=Release', '-p:ArtifactsPath=' + appOut, '-getItem:Compile,ReferencePath']);
    const items = join(scratch, 'project-items.json');
    const citations = join(scratch, 'citations.json');
    writeFileSync(items, JSON.stringify(JSON.parse(itemJson.slice(itemJson.indexOf('{')))));
    writeFileSync(citations, JSON.stringify(paths));
    dotnet(['build', 'continuum/adapters/csharp/CSharpFacts.csproj', '-c', 'Release',
      '--artifacts-path', scannerOut, ...restore]);
    const scanner = join(scannerOut, 'bin', 'CSharpFacts', 'release', 'CSharpFacts.dll');
    return JSON.parse(dotnet([scanner, root, items, citations]));
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}
