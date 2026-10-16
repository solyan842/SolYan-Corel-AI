# SolYan Corel AI

AI automation layer for CorelDRAW on Windows.

The project is designed around four principles:

1. **Corel object model first** — use CorelDRAW automation/COM for deterministic geometry, typography, colors, layers and export.
2. **AI as planner, not a blind mouse macro** — the model receives document state and invokes typed tools.
3. **Safe reversible actions** — every AI job is grouped into a CorelDRAW undo transaction.
4. **UI automation only as fallback** — unsupported dialogs and commands may later use Windows UI Automation, never as the primary engine.

Development happens on feature branches. `main` is kept as the integration branch.
