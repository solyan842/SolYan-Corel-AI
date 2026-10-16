# SolYan-Corel-AI — Project State

Last updated: 2026-10-16
Active implementation branch: `dev/v0.1-corel-agent`
Integration branch: `main`

## Source of truth

Read `AGENTS.md` before continuing work. Repository state and the latest repository-wide GitHub Actions run override conversation memory and handoff summaries.

## Product target

SolYan Corel AI is a high-capability AI operator for CorelDRAW on Windows. It uses CorelDRAW COM/Object Model for deterministic operations, OpenAI Responses API tool calling for planning/orchestration, and a dockable WPF Corel add-on as the user interface. Windows UI Automation is a fallback only for functions unavailable through the Corel object model.

## Implemented architecture

- C#/.NET solution split into Core, Corel adapter, OpenAI Agent, Host, and Corel Docker UI projects.
- All Corel COM calls run through a dedicated STA dispatcher.
- AI geometry is normalized to millimetres without changing the user's Corel document units.
- Each AI mutation task is wrapped in a Corel command/undo group when available.
- Local Docker-to-engine IPC uses a current-user-only named pipe.
- OpenAI API key is stored using Windows user-scoped secure storage.
- Default model is `gpt-6.1-sol`; supported model selection remains configurable.
- The agent can receive an active-page PNG preview and perform up to two post-edit visual verification passes.
- Read-only inspection tools do not trigger visual mutation review.

## Implemented Corel capability groups

### Document / page / layer
- document context and capability inspection
- page/layer enumeration
- activate page
- add, rename, and delete pages
- create, activate, rename, delete, show/hide, and lock/unlock layers
- save and save-as
- import supported local files

### Selection / object identity
- inspect selection
- recursively list page shapes
- stable `StaticID` object targeting
- select all
- select by StaticID
- select by object name, text, and layer criteria
- find/replace text

### Geometry / layout
- move by delta
- move to exact coordinates
- resize with optional aspect preservation
- rotate
- center on page
- align to page or selection
- distribute with equal gaps
- front/back ordering
- flip/mirror
- duplicate
- duplicate grid/array
- group and ungroup
- move selection to layer

### Shape / vector operations
- rectangle and artistic text creation
- convert selection to curves
- combine
- weld / intersect / trim using explicit StaticIDs
- PowerClip add and extract
- delete selection with undo grouping

### Text / color / styling
- replace selected text
- font family, point size, bold and italic
- RGB fill and outline
- CMYK fill and outline

### Export / visual feedback
- PDF publishing
- PNG/JPEG/SVG export capability
- active-page PNG preview for multimodal review
- self-contained Windows engine publishing
- installable Corel add-on packaging from CI

### Preflight / production inspection
- whole-document preflight tool available to both the AI and Docker UI
- one-click `Preflight` button in the Corel Docker
- missing-font detection against Corel's installed `FontList`
- paragraph-text overflow detection
- bitmap DPI checks with configurable threshold
- RGB uniform fill/outline warnings
- fill/outline color-type summary
- locked shape reporting
- hidden and non-editable layer reporting
- detailed issue payload capped at 500 entries while retaining whole-document summary counts

## Verification state

- GitHub Actions run #98 on `dev/v0.1-corel-agent` completed successfully for commit `6324dd006ad0207a2f1abfbfbcf73f4033d37655`.
- Run #98 passed restore, build, self-contained engine publish, Corel add-on assembly, and artifact upload.
- Artifact `SolYanCorelAI-win-x64` was produced successfully: artifact ID `12025284902`, size `30622046` bytes, SHA-256 digest `c736962d4d5a62eee9f96c983a5b8cf393ca9f0e060228e6508aeb6a508714ef`.
- The preflight engine, bounded payload, AI tool registration, IPC command, Docker client and one-click Docker UI are build-verified by run #98.
- Real CorelDRAW COM behavior is **not yet field-passed** because GitHub-hosted runners do not contain a supported CorelDRAW runtime.

## Known repository condition

`main` currently contains the mandatory agent operating gate but does not yet contain the full implementation tree. A `main` workflow run can therefore fail if it attempts to build solution files that are still only on the dev branch. Do not treat that as a dev-branch compile failure. Keep implementation on the dev branch until the integration decision is made.

## Next implementation priorities

1. Add a non-destructive Windows/Corel runtime diagnostics command that reports exact Corel version, capability probes, document context, preview export, preflight and local IPC/AI configuration health.
2. Surface the diagnostics report in the Docker UI for field testing on the user's CorelDRAW installation.
3. Add an opt-in mutation/undo smoke test that creates a temporary test object inside one command group and restores the document immediately; never run this automatically.
4. Add reusable high-level workflow tools for common production tasks instead of exposing uncontrolled raw COM methods.
5. Field-test the installable artifact against the user's actual CorelDRAW version before claiming runtime PASS.

## Runtime PASS definition

A release candidate is not considered Corel runtime-passed until the packaged add-on has been installed on a supported Windows/CorelDRAW machine and has verified at minimum:

- Docker loads and opens inside CorelDRAW;
- engine starts and current-user IPC connects;
- API key/model configuration works;
- document/selection inspection works;
- exact mm move/resize works;
- one grouped AI job can be undone in one undo operation;
- preview PNG export works;
- at least one text/style edit works;
- at least one import/export operation works;
- preflight completes without modifying the document;
- a representative multi-step natural-language task completes and visual review does not introduce unrelated changes.
