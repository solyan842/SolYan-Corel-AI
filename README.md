# SolYan Corel AI

Local AI automation for CorelDRAW on Windows.

## What exists in v0.1 bootstrap

- OpenAI Responses API tool-calling agent.
- CorelDRAW COM/OLE adapter using late binding, so CI does not require CorelDRAW interop assemblies.
- Dedicated STA thread for all Corel COM access.
- One undo command group per AI task.
- 16 initial high-level Corel tools for inspection, geometry, color, text, creation, grouping, duplication, deletion, PDF export and undo.
- Windows GitHub Actions build gate.

## Architecture

```text
Natural language
    -> OpenAI Responses API
        -> typed tools
            -> CorelSession / STA
                -> CorelDRAW object model
```

The model plans the work; CorelDRAW remains the geometry/document source of truth. Windows UI Automation will be added only as fallback for commands or dialogs unavailable through the object model.

## Requirements

- Windows 10/11
- CorelDRAW installed
- .NET 8 SDK
- OpenAI API key

## Run the bootstrap host

PowerShell:

```powershell
$env:OPENAI_API_KEY="YOUR_KEY"
$env:OPENAI_MODEL="gpt-6-astra"   # optional

dotnet run --project .\src\SolYan.CorelAI.Host\SolYan.CorelAI.Host.csproj
```

Open a CorelDRAW document, select an object, then try:

```text
Tell me what is selected.
Move the selected object 10 units to the right.
Resize the selection to width 120 while preserving its aspect ratio.
Center it on the page and make the fill RGB 20, 100, 220.
Publish this document to C:\Temp\proof.pdf
```

> The initial bridge passes numeric values in the current Corel document units. A unit-normalization layer to millimetres is a required next milestone before production use.

## Development

Current development branch: `dev/v0.1-corel-agent`.

See `AGENTS.md` for non-negotiable automation rules and `docs/ARCHITECTURE.md` for the expansion plan.
