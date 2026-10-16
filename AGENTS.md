# AGENTS.md — SolYan Corel AI

## Source of truth
- Repository: `solyan842/SolYan-Corel-AI`.
- `main` is the integration branch. Development must happen on `dev/*` or feature branches.
- Keep CorelDRAW automation deterministic. Prefer object-model operations over screen coordinates.

## Non-negotiable engineering rules
1. CorelDRAW COM/OLE is the primary execution engine.
2. Every COM call must run on the dedicated STA dispatcher. Never access a Corel COM object from a ThreadPool thread.
3. Every AI mutation job must be enclosed in one CorelDRAW command group so the user can undo the whole job in one action.
4. Read document/selection state before geometry-sensitive edits.
5. Use millimetres at the AI tool boundary. Conversion belongs inside the Corel adapter if a target document uses another unit.
6. UI Automation / mouse / keyboard is fallback only for features unavailable through the Corel object model. Never replace deterministic object-model calls with coordinate clicking.
7. Do not send complete `.cdr` files to the model. Send only the minimum structured state and, when vision is enabled, a rendered preview explicitly generated for that purpose.
8. Do not commit OpenAI keys, Corel paths containing user data, generated customer artwork, or local configuration.
9. Keep the initially exposed AI tool set small. Add specialized tools by capability groups rather than exposing hundreds of raw COM methods.
10. Destructive actions must remain reversible through the current AI command group whenever CorelDRAW supports undo.

## Build gate
- `dotnet build SolYan.CorelAI.sln -c Release` must pass on `windows-latest`.
- Core projects must build without CorelDRAW installed; runtime integration tests that require CorelDRAW belong in a separate Windows self-hosted lane later.

## Architecture direction
- `Core`: tool contracts and shared models.
- `Corel`: CorelDRAW COM adapter and deterministic tools.
- `Agent`: OpenAI Responses API orchestration and tool calling.
- `Host`: local Windows host. A WPF/Corel Docker UI will sit on this boundary rather than duplicating automation logic.

## Planned capability groups
Document, page, layer, selection, geometry, text, typography, fill/outline, ordering, grouping, alignment, duplication, import/export, preflight, batch operations, visual inspection, UIA fallback, and reusable workflows/templates.
