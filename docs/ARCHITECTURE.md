# Architecture

## Goal
SolYan Corel AI is a local Windows automation agent that lets a user give natural-language design tasks while retaining CorelDRAW's precision, native document model and undo history.

## Execution stack

```text
User / Corel Docker UI
        |
        v
OpenAI Responses API agent
        |
        v
Typed capability tools (<20 active at once)
        |
        v
CorelSession + STA dispatcher
        |
        v
CorelDRAW COM/OLE object model
        |
        +----> optional Windows UI Automation fallback
```

## Why this split
The model plans and selects typed operations. Geometry, colors, typography, pages, layers and export remain deterministic local operations. This avoids making screen-coordinate automation the source of truth.

## Safety / reversibility
An AI job is wrapped in `BeginCommandGroup` / `EndCommandGroup`. A multi-step task should therefore appear as one undoable action in CorelDRAW. The host closes the group in `finally`, including after model or tool failures.

## COM threading
CorelDRAW automation is isolated on one long-lived STA thread. The agent and network calls never block that thread. Tool calls marshal short COM operations through `StaComDispatcher`.

## Initial tool surface
The bootstrap capability set intentionally stays below twenty tools:

- inspect document
- inspect selection
- select all
- move selection
- resize selection
- center selection on page
- set uniform RGB fill
- set RGB outline
- replace selected text
- create rectangle
- create artistic text
- duplicate selection
- group selection
- delete selection
- publish PDF
- undo

The model sees high-level operations rather than the entire Corel object model. Later, capability routing/tool search can load specialized typography, prepress, layer, page, vector, bitmap, tracing and batch tools only when needed.

## High-end expansion roadmap

### Visual feedback loop
Render a temporary page/selection preview, send the image plus structured object metadata to a vision-capable model, apply edits, render again, and verify layout. The original CDR remains local.

### Design intelligence
- spacing/grid analysis
- alignment and optical balance checks
- font and hierarchy audit
- palette normalization
- duplicate/near-duplicate detection
- bleed, trim, safe-area and print preflight
- low-resolution bitmap detection
- RGB/CMYK spot-color checks
- missing-font and overflow-text checks

### Autonomous workflows
High-level workflows should compile to deterministic tools, for example:

- "prepare this business card for print"
- "make 30 name-card variants from CSV"
- "replace the old logo across all pages"
- "fit these labels on an SRA3 sheet with 3 mm gap"
- "translate all text while preserving typography"
- "export each page as PDF and PNG with naming rules"

### Fallback automation
Windows UI Automation is reserved for modal dialogs, third-party plugins, or commands not exposed through COM. It must verify window identity and control state instead of relying on raw screen coordinates wherever possible.

### UI
A docked Corel panel should expose chat, current document context, action log, stop, undo-AI-job, preview/approve mode, and an autonomous mode. The UI must call the same Host/Agent/Corel layers used by CLI tests.
