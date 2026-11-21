# Signal Amp MVP architecture handoff

Start with ownership, then use the flow diagrams when changing import or playback. These views are grounded in the ADL, BDL and Windows technology bindings from the [MVP baseline commit](https://github.com/avlon-technologies/signal-amp/commit/882007a4d18c7f409040b37c3b6a5522eb2a293d).

| View | Architect's question | Files | Continuum status |
|---|---|---|---|
| System ownership | Who owns the queue, audio output, and durable library inside the Windows desktop process? | [SVG](01-system-ownership.svg) · [PNG](01-system-ownership.png) · [checks](01-system-ownership-check.md) | Diagnostic. Editable verification flagged a WPF code-behind attribution in the generation prompt. The visible diagram places it under DesktopShell. |
| Confirmed MP3 import | Where are files confirmed, imported, persisted, and reported back for retry? | [SVG](02-mp3-import.svg) · [PNG](02-mp3-import.png) · [checks](02-mp3-import-check.md) | Verified editable SVG |
| Playback flow | Where are track selection, queue advancement, audio transport, and spectrum analysis handled? | [SVG](03-playback-flow.svg) · [PNG](03-playback-flow.png) · [checks](03-playback-flow-check.md) | Verified editable SVG |

| Windows playback technology | Which libraries and Windows APIs decode and play MP3s, and what does Signal Amp implement itself? | [SVG](04-windows-audio-technology.svg) · [PNG](04-windows-audio-technology.png) · [checks](04-windows-audio-technology-check.md) | Conceptual. All claims passed semantic fidelity, with one visual-integrity finding. |

## Where to start in code

- **DesktopShell** owns queue state and advances after normal completion. See [PlayerWindow.cs](../../PlayerWindow.cs), [ArtworkSkin.cs](../../ArtworkSkin.cs), [ImportSkin.cs](../../ImportSkin.cs) and [PlaylistSkin.cs](../../PlaylistSkin.cs).
- **Playback** owns decoding, the WASAPI output and audio-derived spectrum analysis. See [Playback.cs](../../Playback.cs).
- **Catalog** owns library writes and playlist transactions. See [Catalog.cs](../../Catalog.cs).

The model depicts logical commands and completion notifications. The app realizes them through in-process calls and callbacks. The [ADL](../model/music-player.adl), [BDL](../model/music-player.bdl) and [Windows bindings](../model/windows-desktop.adl-binding.json) are the source of architectural intent. Diagram verification does not assign implementation conformance levels.
