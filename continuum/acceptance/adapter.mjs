import { join } from 'node:path';
import { mkdir } from 'node:fs/promises';
import { makeWorkspace, prepare, release, smoke } from '../../tests/mvp-smoke.mjs';

export function createAdapter() {
  let buildRoot;
  return {
    async afterScenario(ctx) {
      if (ctx.state.scratch) await release(ctx.state.scratch);
    },
    steps: {
      async 'signalAmp.runProbe'(ctx, { probe }) {
        buildRoot ??= await makeWorkspace();
        ctx.state.binary = await prepare(buildRoot);
        ctx.state.scratch = await makeWorkspace();
        const scratch = join(ctx.state.scratch, probe);
        await mkdir(scratch, { recursive: true });
        await smoke(probe, scratch, ctx.state.binary);
      }
    }
  };
}
