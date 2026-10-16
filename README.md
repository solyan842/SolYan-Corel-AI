# SolYan Corel AI

AI automation for CorelDRAW on Windows, built around CorelDRAW's object model rather than blind mouse macros.

## Current v0.1 development scope

- OpenAI Responses API tool-calling agent.
- CorelDRAW COM/OLE adapter using late binding, so builds do not require a pinned Corel interop assembly.
- Dedicated STA thread for all Corel COM access.
- AI geometry normalized to **millimetres** without changing the user's document unit setting.
- Runtime CorelDRAW version/capability probing.
- One undo command group per AI task, with rollback on failed AI jobs.
- Typed Corel tools covering:
  - document, selection, page and layer inspection;
  - exact move, resize, rotate, align, distribute and stacking order;
  - stable StaticID object targeting, search and text replacement;
  - RGB and CMYK fill/outline;
  - text replacement and typography;
  - rectangle/artistic-text creation, duplicate, grid/array, group/ungroup, curves and combine;
  - boolean weld/intersect/trim and PowerClip workflows;
  - page/layer creation, activation, rename/state/delete;
  - local file import, save, Save As, PDF publishing and undo;
  - PNG/JPEG/SVG export and safe multi-page batch export;
  - deterministic fit-to-page/margins and selection-grid layout workflows;
  - whole-document production preflight.
- Multimodal page-preview feedback so the AI can visually verify requested edits after deterministic object-model operations.
- In-Corel **SolYan AI Docker panel** using a thin WPF .NET Framework 4.8/x64 add-on.
- Docker controls for Preflight, non-destructive Diagnostics, explicit Mutation Test, Refresh and Undo AI.
- Modern .NET 8 engine isolated out of process and connected to the Docker through a current-user-only named pipe.
- OpenAI API key encrypted for the current Windows user.
- Default model: `gpt-6.1-sol`, with supported model selection configurable in the Docker.
- Windows GitHub Actions build + self-contained win-x64 package artifact.

## Architecture

```text
CorelDRAW
  └─ SolYan AI Docker (WPF / .NET Framework 4.8)
       └─ current-user Named Pipe
            └─ SolYan Corel AI Engine (.NET 8, win-x64)
                 ├─ OpenAI Responses API
                 └─ typed tools / deterministic workflows
                      └─ CorelSession / dedicated STA
                           └─ CorelDRAW COM object model
```

The model plans the work, but CorelDRAW remains the document and geometry source of truth. Windows UI Automation is reserved for fallback cases where the Corel object model cannot reach a command or dialog.

## Build

Every push to `main` or `dev/**` runs the Windows CI gate. A successful dev build also publishes an artifact named:

```text
SolYanCorelAI-win-x64
```

Its `SolYanCorelAI.zip` contains:

```text
Install-SolYanCorelAI.ps1
SolYanCorelAI/
  CorelDrw.addon
  AppUI.xslt
  SolYan.CorelAI.Docker.dll
  Engine/
    SolYan.CorelAI.Host.exe
    ...self-contained runtime files...
```

`main` currently carries repository governance but not yet the complete dev implementation tree, so the authoritative implementation/build work remains on `dev/v0.1-corel-agent` until integration is explicitly approved.

## Development host

For engine development without installing the Docker:

```powershell
$env:OPENAI_API_KEY="YOUR_KEY"
$env:OPENAI_MODEL="gpt-6.1-sol"   # optional; this is also the default

dotnet run --project .\src\SolYan.CorelAI.Host\SolYan.CorelAI.Host.csproj
```

Open a CorelDRAW document and try tasks such as:

```text
Tell me what is selected.
Move the selected object 10 mm to the right.
Resize the selection to 120 mm wide while preserving aspect ratio.
Fit the selection inside the page with a 10 mm margin and center it.
Arrange these selected objects into a 4-column grid with 5 mm gaps.
Make the fill CMYK 100, 70, 0, 0 and the outline 0.25 mm.
Change selected text to Arial, 18 pt, bold.
Create a layer named Proof and import C:\Temp\logo.svg into it.
Run a preflight and tell me what would block production output.
Export pages 1 through 10 as PNG files into C:\Temp\Proofs without overwriting existing files.
Publish this document to C:\Temp\proof.pdf.
```

Inspection commands available in the development host include:

```text
/capabilities
/context
/preflight
/diagnostics
```

## Corel Docker verification controls

- **Preflight**: scans document production risks without modifying content.
- **Diagnostics**: non-destructive Corel/runtime/preview/preflight health checks.
- **Mutation Test**: explicit opt-in smoke test only; briefly creates a 5 × 5 mm object and immediately undoes it. It warns first because creating a new Corel action may clear existing Redo history.
- **Undo AI**: reverses the most recent grouped AI action when Corel permits it.

## Safety model

- AI mutations are grouped into one CorelDRAW undo transaction when supported.
- Partial changes are rolled back when an AI task fails where possible.
- No arbitrary file path is invented by the model for exports.
- Batch export never replaces existing files unless `overwrite=true` is explicitly supplied.
- Destructive Corel tools are explicit and remain undoable where possible.
- Mutation smoke testing is not in the AI tool catalog and cannot be model-invoked.
- IPC is local and restricted to the current Windows user.
- API keys are never committed to the repository or printed in logs.
- CI build PASS is not treated as Corel runtime PASS; actual CorelDRAW field testing is required.

## Development

Current branch: `dev/v0.1-corel-agent`.

See `AGENTS.md` for non-negotiable automation rules, `docs/PROJECT-STATE.md` for the current verified checkpoint, and `docs/ARCHITECTURE.md` for the expansion plan.
