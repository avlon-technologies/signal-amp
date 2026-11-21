# Geometry check: reconstructed-v2.svg

cards: 6 (missing: none; duplicated: none), edges: 4, raster/foreign/script elements: 0, markers: 1 (without orient=auto: 0), full-canvas white background before visible content: yes

## Edges (endpoints found by geometry, tolerance 6 px)

| data-from | data-to | geometry from | geometry to | label | basis | verdict |
|---|---|---|---|---|---|---|
| ImportSelection | Catalog | ImportSelection | Catalog | handled by | declared-flow aggregate handles | PASS |
| Catalog | ImportFinished | Catalog | ImportFinished | publishes | aggregate publishes | PASS |
| ImportFinished | DesktopShell | ImportFinished | DesktopShell | received by | declared-flow aggregate subscribesTo | PASS |
| Catalog | Library | Catalog | Library | reads + writes | aggregate aggregate | PASS |

## Required labels

| label | verdict |
|---|---|
| Collection | PASS |
| UserInterface | PASS |
| ImportSelection | PASS |
| Catalog | PASS |
| Library | PASS |
| ImportFinished | PASS |
| DesktopShell | PASS |
| Import policy — permitted | PASS |

## Element grades, as a reader sees them

Every box the facts draw, and whether the drawing presents it no more strongly than its grade. A speculative or inferred box must be visibly weaker than a declared one; the reverse is understatement and only noted.

| element | facts grade | drawn | verdict |
|---|---|---|---|
| Collection | declared | solid, data-grade="declared" | PASS |
| UserInterface | declared | solid, data-grade="declared" | PASS |
| ImportSelection | declared | solid, data-grade="declared" | PASS |
| Catalog | declared | solid, data-grade="declared" | PASS |
| Library | declared | solid, data-grade="declared" | PASS |
| ImportFinished | declared | solid, data-grade="declared" | PASS |
| DesktopShell | declared | solid, data-grade="declared" | PASS |
| Import policy — permitted | declared | solid, data-grade="declared" | PASS |

## Notation the facts do not carry

Keys, cardinalities and multiplicities written on the canvas that are not in the facts it was drawn from. The pinned model declares none, so each is a mark the reconstruction added.

None.

## Meaning preserved — did the words survive reconstruction?

Every text the facts put on a box is still inside that box, word for word; no box carries text the facts do not give it; and where an arrow is weaker than declared, the arrow key is on the canvas. A change is classified by what it does to meaning.

Nothing changed, added, moved or lost.

## Layout — reported, never a gate

Where the drawing is harder to follow than it needs to be: a connector routed through a card it does not connect, and an edge label with a line running through it. Neither changes what the diagram claims, so neither fails the checks; both are offered to a correction pass that is happening anyway. Geometry is estimated (curves through their control points, label boxes from font size).

None found.

LAYOUT: 0 connector(s) through a card, 0 label(s) on a line (reported, not a gate)

VISUAL INTEGRITY: PASS — does the drawn SVG match the facts it was drawn from?

This establishes endpoints, labels and element presence only. Whether arrows are followable, text fits and the diagram communicates is judged on the rendered PNG by a person.

## Semantic fidelity — are those facts backed by the pinned architecture?

Re-derived independently from the run's own pinned canonical model, attestation `sha256:e3e28661c7808588fe5657f19c838fc781e02d62952a8972cb7da4d49abc39e7`.

Of 18 claims on this canvas: **18 declared**, 0 inferred, 0 speculative, **0 misgraded**.
0 drawn weaker than the model supports; 0 wording warning(s); 0 stale provenance claim(s); 0 declared relationship(s) between drawn boxes neither drawn nor recorded as an omission.

SEMANTIC FIDELITY: SUPPORTED

Beyond boxes and arrows: 0 unsupported assertion(s) of notation, deployment, order, attribution or boundary. UNSUPPORTED ASSERTIONS: NONE. They are listed in the verification report and do not change the verdict above.

This asks whether anything is drawn STRONGER than its evidence. A proposal marked as a proposal is correct, and so is an inference resting on a quoted description; the failure is a claim drawn as declared architecture that the model does not declare. The full claim-by-claim table is in the verification report beside this file. Without this leg, a geometry PASS means only that the picture matches somebody's claim.

RESULT: PASS

## Fulfilment of the composer's plan — 6 visible, 0 not visible, 0 never placed

Each item the composer planned, from the question, for a reader to see; and whether the unit or frame it was placed on is visible on the canvas by its label. Reported, never a gate. Whether a reader can actually answer the question is the communication evaluation.

| the reader must see | placed on | verdict |
|---|---|---|
| ImportSelection carries location and requestId and is handled by Catalog. | ImportSelection | VISIBLE |
| Catalog imports confirmed MP3s individually, is the sole Library writer, preserves identity for the same stored path, and leaves source audio untouched. | Catalog | VISIBLE |
| Library is durable and strongly consistent and stores references and metadata rather than source audio. | Library | VISIBLE |
| Catalog publishes ImportFinished in reaction to ImportSelection, preserving requestId; DesktopShell receives it. | ImportFinished | VISIBLE |
| Failed items remain available for retry in DesktopShell; no automatic retry loop is shown. | DesktopShell | VISIBLE |
| Business policy permits review before confirmation, successful tracks becoming available, and retrying a failed file. | Import policy — permitted | VISIBLE |
