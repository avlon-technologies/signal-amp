# Geometry check: reconstructed-v3.svg

cards: 8 (missing: none; duplicated: none), edges: 12, raster/foreign/script elements: 0, markers: 2 (without orient=auto: 0), full-canvas white background before visible content: yes

## Edges (endpoints found by geometry, tolerance 6 px)

| data-from | data-to | geometry from | geometry to | label | basis | verdict |
|---|---|---|---|---|---|---|
| DesktopMaintainer | UserInterface | DesktopMaintainer | UserInterface | owns | declared owns | PASS |
| DesktopMaintainer | Transport | DesktopMaintainer | Transport | owns | declared owns | PASS |
| DesktopMaintainer | Collection | DesktopMaintainer | Collection | owns | declared owns | PASS |
| DesktopShell | Desktop | DesktopShell | Desktop | deployed as | declared deployedAs | PASS |
| Playback | Desktop | Playback | Desktop | deployed as | declared deployedAs | PASS |
| Catalog | Desktop | Catalog | Desktop | deployed as | declared deployedAs | PASS |
| DesktopShell | Playback | DesktopShell | Playback | calls with chosen track (dashed) | described route (quoted) | PASS |
| Catalog | Library | Catalog | Library | reads + writes | aggregate aggregate | PASS |
| Playback | PlaybackStateChanged | Playback | PlaybackStateChanged | publishes | declared publishes | PASS |
| PlaybackStateChanged | DesktopShell | PlaybackStateChanged | DesktopShell | received by | declared-flow subscribesTo | PASS |
| Catalog | ImportFinished | Catalog | ImportFinished | publishes | declared publishes | PASS |
| ImportFinished | DesktopShell | ImportFinished | DesktopShell | received by | declared-flow subscribesTo | PASS |

## Required labels

| label | verdict |
|---|---|
| UserInterface | PASS |
| Transport | PASS |
| Collection | PASS |
| Desktop | PASS |
| DesktopMaintainer | PASS |
| DesktopShell | PASS |
| Playback | PASS |
| Catalog | PASS |
| Library | PASS |
| PlaybackStateChanged | PASS |
| ImportFinished | PASS |

## Element grades, as a reader sees them

Every box the facts draw, and whether the drawing presents it no more strongly than its grade. A speculative or inferred box must be visibly weaker than a declared one; the reverse is understatement and only noted.

| element | facts grade | drawn | verdict |
|---|---|---|---|
| UserInterface | declared | solid, data-grade="declared" | PASS |
| Transport | declared | solid, data-grade="declared" | PASS |
| Collection | declared | solid, data-grade="declared" | PASS |
| Desktop | declared | solid, data-grade="declared" | PASS |
| DesktopShell | declared | solid, data-grade="declared" | PASS |
| Playback | declared | solid, data-grade="declared" | PASS |
| Catalog | declared | solid, data-grade="declared" | PASS |
| Library | declared | solid, data-grade="declared" | PASS |
| PlaybackStateChanged | declared | solid, data-grade="declared" | PASS |
| ImportFinished | declared | solid, data-grade="declared" | PASS |
| DesktopMaintainer | declared | solid, data-grade="declared" | PASS |

## Notation the facts do not carry

Keys, cardinalities and multiplicities written on the canvas that are not in the facts it was drawn from. The pinned model declares none, so each is a mark the reconstruction added.

None.

## Meaning preserved — did the words survive reconstruction?

Every text the facts put on a box is still inside that box, word for word; no box carries text the facts do not give it; and where an arrow is weaker than declared, the arrow key is on the canvas. A change is classified by what it does to meaning.

Arrow key required (solid, dashed arrows): present.

Nothing changed, added, moved or lost.

## Layout — reported, never a gate

Where the drawing is harder to follow than it needs to be: a connector routed through a card it does not connect, and an edge label with a line running through it. Neither changes what the diagram claims, so neither fails the checks; both are offered to a correction pass that is happening anyway. Geometry is estimated (curves through their control points, label boxes from font size).

None found.

LAYOUT: 0 connector(s) through a card, 0 label(s) on a line (reported, not a gate)

VISUAL INTEGRITY: PASS — does the drawn SVG match the facts it was drawn from?

This establishes endpoints, labels and element presence only. Whether arrows are followable, text fits and the diagram communicates is judged on the rendered PNG by a person.

## Semantic fidelity — are those facts backed by the pinned architecture?

Re-derived independently from the run's own pinned canonical model, attestation `sha256:e3e28661c7808588fe5657f19c838fc781e02d62952a8972cb7da4d49abc39e7`.

Of 39 claims on this canvas: **37 declared**, 1 inferred, 0 speculative, **1 misgraded**.
0 drawn weaker than the model supports; 0 wording warning(s); 0 stale provenance claim(s); 0 declared relationship(s) between drawn boxes neither drawn nor recorded as an omission.

| MISGRADED claim | why |
|---|---|
| image prompt: "WPF code-behind" | stated as fact of Playback, but wpf-code-behind is declared only for ::MusicPlayer.Listening.UserInterface.DesktopShell |

SEMANTIC FIDELITY: MISGRADED

Beyond boxes and arrows: 0 unsupported assertion(s) of notation, deployment, order, attribution or boundary. UNSUPPORTED ASSERTIONS: NONE. They are listed in the verification report and do not change the verdict above.

This asks whether anything is drawn STRONGER than its evidence. A proposal marked as a proposal is correct, and so is an inference resting on a quoted description; the failure is a claim drawn as declared architecture that the model does not declare. The full claim-by-claim table is in the verification report beside this file. Without this leg, a geometry PASS means only that the picture matches somebody's claim.

RESULT: FAIL (0 visual, undefined unsupported claim(s))

## Fulfilment of the composer's plan — 8 visible, 0 not visible, 0 never placed

Each item the composer planned, from the question, for a reader to see; and whether the unit or frame it was placed on is visible on the canvas by its label. Reported, never a gate. Whether a reader can actually answer the question is the communication evaluation.

| the reader must see | placed on | verdict |
|---|---|---|
| All three services deploy as Desktop, one Windows desktop process. | Desktop | VISIBLE |
| DesktopMaintainer owns the three bounded contexts. | DesktopMaintainer | VISIBLE |
| DesktopShell owns the volatile queue and current entry, presents the UI and advances once on normal completion. | DesktopShell | VISIBLE |
| Playback owns decoding, output and transport controls; completion and failure are distinct. | Playback | VISIBLE |
| Catalog alone writes Library, preserves track identity and playlist order, and leaves source audio untouched. | Catalog | VISIBLE |
| Library persists references, cached metadata, playlists, ordered entries and catalog edit history at the bound SQLite path. | Library | VISIBLE |
| The seven WindowsDesktop bindings map to their specific targets. | Desktop | VISIBLE |
| Playback and import notifications return to DesktopShell. | Playback | VISIBLE |
