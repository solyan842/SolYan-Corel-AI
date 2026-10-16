# SolYan-Corel-AI — MANDATORY AGENT ENTRY GATE

Status: **REQUIRED / REPO SOURCE OF TRUTH**

This file prevents repeated work, state drift, stale-branch diagnosis, GitHub Actions diagnosis without real logs, unsafe CorelDRAW automation changes, and false success reports.

## 0. STOP GATE

Before diagnosis, code edits, workflow changes, Corel automation changes, OpenAI integration changes, Docker/add-on changes, installer changes, or handoff continuation:

1. verify repository and current repository state from GitHub;
2. read this `AGENTS.md` from the branch/ref being worked on;
3. read `docs/PROJECT-STATE.md` if present;
4. read `docs/GITHUB-ACTIONS-LOGGING-POLICY.md` if present;
5. inspect current repository state before relying on conversation memory or handoff summaries;
6. verify the active branch HEAD and latest repository-wide CI run before changing code because of CI.

**Repository state wins over conversation memory, handoff summaries, screenshots, or prior assistant claims.**

## 1. CONTINUOUS EXECUTION RULE

When the user says `ok`, `tiếp`, `tiếp tục`, or otherwise instructs continuous implementation:

- continue automatically through the next safe implementation steps;
- do not stop for routine choices, internal refactors, naming, test additions, CI checks, or obvious follow-up fixes;
- only stop and ask the user when a real product decision, credential, external permission, irreversible/destructive action, legal/licensing boundary, or unavailable hardware/runtime is required;
- if a safe default is clear from repository state and project rules, use it and continue;
- when blocked by a tool/platform limitation, report the exact blocked boundary immediately instead of looping.

## 2. LATEST WORKFLOW RUN IS THE CI SOURCE OF TRUTH

Before **every** CI diagnosis, CI status report, or continuation after a previous run:

1. fetch the latest workflow runs for the **entire repository with no branch filter**;
2. select the run with the highest/current `run_number`;
3. read `run_number`, `id`, `head_branch`, `head_sha`, `status`, and `conclusion` directly from that run;
4. treat that run's `head_branch` as the active CI branch for the next diagnostic step;
5. only then fetch jobs/logs/artifacts for that exact `run_id`.

**Forbidden:**
- starting CI diagnosis from a branch name remembered from an earlier run, conversation, or handoff;
- using `?branch=...` to decide what the repository's latest run is;
- assuming the active dev branch has not changed;
- continuing to diagnose an older run after a newer run exists, unless explicitly investigating history;
- reporting an older run number as current without first refreshing repository-wide workflow runs.

If the user/UI reports a newer run number than the agent currently sees, immediately refresh the **repository-wide** workflow run list before doing anything else.

Before changing code because of a failed run, verify that the run's `head_sha` still matches the relevant current branch HEAD. If branch HEAD has advanced to a newer run, refresh from step 1 and diagnose the newer run instead unless the older run is explicitly requested.

## 3. NO REPEATED WORK / NO STATE DRIFT

Before proposing or repeating a fix, verify whether it already exists on the current branch.

Forbidden:
- re-implementing functionality that already exists on the active branch;
- retrying the same failed raw-log path after fallback conditions are reached;
- inferring a compiler/build/runtime root cause from run metadata or step names;
- claiming a fix/checkpoint exists without re-fetching it;
- modifying `main` or another branch when active work is on a dev branch unless the user explicitly requests that branch;
- using stale local assumptions about files, models, tool catalogs, Corel APIs, or packaging.

## 4. GITHUB ACTIONS DIAGNOSTIC GATE

For every failed GitHub Action:

1. refresh repository-wide workflow runs and identify the exact current workflow run ID;
2. identify the exact failed job ID;
3. fetch raw job log **once**;
4. if real log text is returned, find the first meaningful/root error;
5. if raw log is empty, inaccessible, suppressed, truncated, `Skipped`, or otherwise unusable, **do not retry the same path**;
6. immediately fetch a durable log artifact if the workflow provides one;
7. inspect the actual artifact log;
8. only after real error text is visible may code/workflow logic be changed.

If neither raw log nor durable artifact can be consumed because of connector/tool limitations, report the exact blocked boundary and STOP CI diagnosis. Do not redesign logging unless repository evidence proves logging is missing or broken.

## 5. BRANCH / SCOPE GATE

Before every write:

- verify the target branch from current GitHub state, not memory;
- preserve `main` unless explicitly asked to change it;
- keep implementation work on dev branches unless the user explicitly requests a direct `main` change;
- do not widen scope beyond the current task without evidence;
- never mix unrelated UI, Corel automation, OpenAI agent, packaging, and CI fixes in one commit when they can be separated cleanly.

## 6. VERIFY EVERY WRITE

After every write/change:

1. re-fetch the changed file, commit, branch HEAD, or workflow state;
2. verify the intended content is actually present;
3. if CI is affected, verify the new run belongs to the new `head_sha`;
4. only then report success.

Never report `done`, `fixed`, `saved`, `locked`, `built`, or `released` based only on an attempted write call.

## 7. COREL AUTOMATION ARCHITECTURE LOCK

Core principles for this project:

- **CorelDRAW object model / COM first** for geometry, text, colors, layers, pages, selection, import/export, PowerClip, boolean operations, and document automation;
- **AI is planner/orchestrator**, not a blind mouse macro;
- **Windows UI Automation / mouse-keyboard is fallback only** when Corel does not expose a deterministic automation API;
- all Corel COM access must stay on the dedicated STA execution path;
- geometry exposed to the AI is normalized to **millimetres** without changing the user's document unit setting;
- every AI task should be grouped into a Corel undo transaction when Corel permits it;
- destructive actions require explicit user intent and should remain undoable where possible;
- exact object identity should use stable Corel identifiers when available instead of ambiguous visual guesses;
- visual preview is secondary verification; Corel object/document state is the source of truth for exact geometry and structure.

## 8. OPENAI AGENT RULES

- use the OpenAI Responses API/tool-calling architecture unless repository evidence justifies a change;
- never claim a Corel action happened unless a tool result confirms it;
- inspect document/selection/context before geometry-sensitive changes;
- use capability probing for uncertain or version-specific Corel behavior;
- keep tool schemas strict and explicit;
- do not invent local file paths;
- model IDs and API behavior must be checked against current OpenAI documentation when they are time-sensitive;
- do not hard-code a model known to be near retirement as the only operational default when a supported replacement exists;
- API keys must never be committed to the repository or printed in logs.

## 9. SECURITY / LOCAL CONTROL LOCK

- local engine IPC must remain restricted to the current Windows user where supported;
- secrets must be stored using Windows user-scoped secure storage, not plaintext repository/config files;
- do not expose a remote unauthenticated command endpoint for CorelDRAW control;
- installer/package changes must not silently weaken IPC or secret-storage protections.

## 10. RUNTIME / HARDWARE VERIFICATION BOUNDARY

GitHub Actions can verify compilation, publish, packaging, and non-Corel unit tests, but it cannot prove real CorelDRAW COM behavior without an actual supported CorelDRAW runtime.

Therefore:

- distinguish **build PASS** from **Corel runtime PASS**;
- never report Corel integration as field-tested unless it has actually run against CorelDRAW;
- when a runtime-only behavior cannot be verified in the available environment, state that boundary exactly and continue with every verification that is still possible;
- design capability probes and diagnostics so the user's machine can report actual Corel version/runtime behavior without guesswork.

## 11. PROJECT SCOPE LOCK

Current product direction:

- Windows + CorelDRAW integration;
- dockable SolYan AI UI inside CorelDRAW;
- local self-contained engine;
- OpenAI-powered planning and multimodal visual review;
- broad Corel object-model control: document/page/layer/shape/text/color/layout/import/export/PowerClip/boolean/selection/undo;
- exact automation first, visual UI fallback second;
- safe reversible operations and strong diagnostics;
- installable package produced by CI.

Do not narrow this into a simple chatbot or macro runner. The target is a high-capability AI operator for CorelDRAW.
