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
- Local Docker-to-engine IPC uses a named pipe ACL restricted to the current Windows user SID; it does not rely on elevation-level matching.
- The pipe server keeps a listener available for successive short-lived Docker connections.
- OpenAI API key is stored using Windows user-scoped secure storage.
- Default model is `gpt-6.1-sol`; supported model selection remains configurable.
- The agent can receive an active-page PNG preview and perform up to two post-edit visual verification passes.
- Read-only inspection/export tools do not trigger visual mutation review.

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
- high-level `fit selection to page` workflow with exact mm margins, optional aspect preservation, and explicit horizontal/vertical alignment
- high-level `arrange selection grid` workflow that preserves object size and returns final layout geometry

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
- safe multi-page PNG/JPEG/SVG batch export with explicit existing output directory, page range, deterministic names and opt-in overwrite
- batch export restores the originally active page after completion
- export-only tools are not treated as visual document mutations
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

### Runtime diagnostics
- non-destructive diagnostics available from the Docker, IPC and CLI
- reports Corel capability probe, document context, selection context, active-page preview export and preflight health
- reports local runtime OS/framework/process architecture and AI configuration without exposing the API key
- document-dependent checks are explicitly skipped when no document is open rather than reported as false failures
- diagnostics now report `passed`, `failed`, `skipped`, `complete`, and distinguish `limited` from a full `pass`

### Explicit mutation/undo smoke test
- not exposed to the AI tool catalog and therefore cannot be model-invoked
- only runs through the explicit local test path
- Docker requires a Yes/No warning before execution
- warns that a new Corel action may clear existing Redo history
- creates a temporary 5 × 5 mm rectangle inside one Corel command group, performs one Undo, and verifies the temporary object and page shape count are restored
- attempts direct cleanup if the test fails mid-operation

## Verification state

- GitHub Actions run #134 on `dev/v0.1-corel-agent` completed successfully for commit `33f71e0fd22435a685359c60077acd9dc1f54ced`.
- Run #134 passed restore, build, self-contained engine publish, Corel add-on assembly, and artifact upload.
- Artifact `SolYanCorelAI-win-x64` from run #134: artifact ID `12031397693`, size `30651042` bytes, SHA-256 digest `12a5b953ea117a2c5822897a550e858e284b9d91600494661bc6f30050cfc305`.
- CorelDRAW 27.0.0.121 on Windows 11 x64 has now **partial field-passed** the packaged add-on: Docker loads inside CorelDRAW, the packaged engine auto-starts, current-user SID-restricted IPC connects, the capability probe succeeds, and no-document context diagnostics run without failure.
- The observed no-document diagnostics passed `corel_capabilities` and `document_context`; document-dependent selection/preview/preflight checks were correctly skipped because no document was open.
- Full Corel runtime PASS is still pending document-dependent checks, mutation/undo verification, API-key/model configuration, representative deterministic edits, export and a multi-step AI task.

## Known repository condition

`main` currently contains the mandatory agent operating gate but does not yet contain the full implementation tree. A `main` workflow run can therefore fail if it attempts to build solution files that are still only on the dev branch. Do not treat that as a dev-branch compile failure. Keep implementation on the dev branch until the integration decision is made.

## Field-test gate

`docs/FIELD-TEST.md` is the authoritative runtime test sequence. It starts with non-destructive Diagnostics and Preflight, then requires explicit confirmation for the Mutation Test before deterministic edits, layout workflows, export and visual-review testing.

## Current blocker / next action

The add-on/engine/IPC gate is field-passed on CorelDRAW 27. The next runtime step requires an open Corel document: create or open a disposable document, run Diagnostics again so selection/preview/preflight execute, then run the explicit Mutation Test after acknowledging its warning. After those pass, continue through API key/model configuration and the deterministic edit/export/AI-task gates in `docs/FIELD-TEST.md`.

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
- batch page export works and restores the original active page;
- preflight completes without modifying the document;
- a representative multi-step natural-language task completes and visual review does not introduce unrelated changes.
