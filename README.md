# SolYan Corel AI

AI automation for CorelDRAW on Windows, built around CorelDRAW's object model rather than blind mouse macros.

## Current v0.1 development scope

- OpenAI Responses API tool-calling agent.
- CorelDRAW COM/OLE adapter using late binding, so builds do not require a pinned Corel interop assembly.
- Dedicated STA thread for all Corel COM access.
- AI geometry normalized to **millimetres** without changing the user's document unit setting.
- Runtime CorelDRAW version/capability probing.
- One undo command group per AI task, with rollback on failed AI jobs.
- 30+ typed Corel tools covering:
  - document, selection, page and layer inspection;
  - exact move, resize, rotate, align, distribute and stacking order;
  - RGB and CMYK fill/outline;
  - text replacement and typography;
  - rectangle/artistic-text creation, duplicate, group and delete;
  - page/layer activation and layer state;
  - local file import, save, Save As, PDF publishing and undo.
- In-Corel **SolYan AI Docker panel** using a thin WPF .NET Framework 4.8/x64 add-on.
- Modern .NET 8 engine isolated out of process and connected to the Docker through a current-user-only named pipe.
- Windows GitHub Actions build + self-contained win-x64 package artifact.

## Architecture

```text
CorelDRAW
  └─ SolYan AI Docker (WPF / .NET Framework 4.8)
       └─ current-user Named Pipe
            └─ SolYan Corel AI Engine (.NET 8, win-x64)
                 ├─ OpenAI Responses API
                 └─ typed tools
                      └─ CorelSession / dedicated STA
                           └─ CorelDRAW COM object model
```

The model plans the work, but CorelDRAW remains the document and geometry source of truth. Windows UI Automation is reserved for future fallback cases where the Corel object model cannot reach a command or dialog.

## Build

Every push to `main` or `dev/**` runs the Windows CI gate. A successful run also publishes an artifact named:

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

## Development host

For engine development without installing the Docker:

```powershell
$env:OPENAI_API_KEY="YOUR_KEY"
$env:OPENAI_MODEL="gpt-6-astra"   # optional

dotnet run --project .\src\SolYan.CorelAI.Host\SolYan.CorelAI.Host.csproj
```

Open a CorelDRAW document and try tasks such as:

```text
Tell me what is selected.
Move the selected object 10 mm to the right.
Resize the selection to 120 mm wide while preserving aspect ratio.
Align these objects to the page and distribute them evenly.
Make the fill CMYK 100, 70, 0, 0 and the outline 0.25 mm.
Change selected text to Arial, 18 pt, bold.
Create a layer named Proof and import C:\Temp\logo.svg into it.
Publish this document to C:\Temp\proof.pdf.
```

## Safety model

- AI actions are grouped into one CorelDRAW undo transaction.
- Partial changes are rolled back when an AI task fails.
- No arbitrary file path is invented by the model for exports.
- Destructive tools are explicit and remain undoable.
- IPC is local and restricted to the current Windows user.
- API keys are never committed to the repository.

## Development

Current branch: `dev/v0.1-corel-agent`.

See `AGENTS.md` for non-negotiable automation rules and `docs/ARCHITECTURE.md` for the expansion plan.
