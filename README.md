# Signal Amp MVP baseline

Signal Amp is a local MP3 player for Windows x64. The accepted MVP uses the wide faceplate, a live radial spectrum, staged song import and a playlist manager.

## Build and run

Install the .NET 10 SDK on Windows and run:

```powershell
dotnet run --project WindowsMp3Player.csproj
```

## MVP scope

One listener on one Windows x64 desktop can stage local MP3 imports, control playback and volume, seek with the dial, and manage named, ordered playlists. The player displays the title, optional artist, elapsed time and a live spectrum. The local catalog stores file references and metadata while the listener retains the original files. The active queue is in memory.

## Architecture handoff

See [the ownership, import, and playback diagrams](continuum/diagrams/README.md).

## Model and binding validation

- ADL compiled with 78 checks passed, zero errors and zero warnings.
- BDL compiled with all four validation stages passed and zero diagnostics.
- The Windows bindings passed Continuum completeness for seven elements, with zero unbound elements. The binding pin matches the revised ADL. The shell uses WPF code-behind on .NET 10, playback uses NAudio with Windows Media Foundation and WASAPI shared mode, and Catalog uses TagLibSharp and Microsoft.Data.Sqlite.
- The BDL-to-ADL realization and runtime corroboration of every technology binding remain open. The modeled request and completion records describe the logical interaction. The implementation uses in-process calls and callbacks.

## Release evidence

The accepted executable passed the catalog, staged import, Play control, and real MP3 playback and live-spectrum smoke checks. The Catalog check exercises duplicate imports, durable playlist entries with repeated tracks, relinking, reopening the catalog and preserving source audio on removal.

The SDK verifier covers direct C# ExecuteNonQuery call sites for the SQLite Library. Its baseline observed Catalog as the writer. The checked-in challenge adds an undeclared DesktopShell write path to a scratch copy. A local run observed the changed owner. The Continuum Host awarded L2 to this scoped claim, exceeding the L1 gate. L3 was not awarded. Other SQL write mechanisms and external writers fall outside the claim.

Before distributing the build, run a manual pass for drag and close, volume, keyboard navigation, display scaling and high contrast. Those interactions were outside the smoke suite.

## Repeat the assessment

Run `npm test` on a Windows x64 desktop with the .NET 10 SDK and a working Windows audio endpoint. The suite and the acceptance adapter build and run the real app. The bundled test MP3 is a generated chime. All four tests and four scenarios passed the Host assessment.

The static evidence map identifies the C# modules implementing the three services. The Roslyn adapter resolves references in the compiled C# project and emits facts for the Continuum Host to judge. Continuum CLI 0.48.x requires the included acquisition shim because its default static reader only supports JavaScript and TypeScript. The shim runs against an isolated CLI copy and replaces only static fact acquisition.

With an authenticated Continuum CLI 0.48.x, set CONTINUUM_CLI_ROOT to its installed package directory and run:

```powershell
node continuum/adapters/csharp/check.mjs conformance check continuum --application . --require static,claims,scenarios,tests --min-level catalog-library-writer=L1
```
