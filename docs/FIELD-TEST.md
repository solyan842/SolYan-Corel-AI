# SolYan Corel AI — Field Test

This checklist is the runtime gate for `dev/v0.1-corel-agent`. CI can prove compilation and packaging, but only a real supported CorelDRAW installation can prove COM/add-on behavior.

## 1. Install the latest CI artifact

1. From the latest successful `dev/v0.1-corel-agent` workflow run, download artifact `SolYanCorelAI-win-x64`.
2. Extract `SolYanCorelAI.zip` to a local folder.
3. Close CorelDRAW completely.
4. Run PowerShell from the extracted package directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-SolYanCorelAI.ps1
```

If the installer cannot find the Corel add-ons directory automatically, pass the exact `Programs64\Addons` directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-SolYanCorelAI.ps1 -CorelRoot "C:\Program Files\Corel\<your CorelDRAW suite>\Programs64\Addons"
```

5. Start CorelDRAW and open the **SolYan AI** docker from CorelDRAW's Dockers/panels menu.

## 2. Non-destructive gate first

Open a normal test CDR document, preferably a copy rather than production-only artwork.

Press **Diagnostics**. Record the complete Diagnostics entry from the activity panel.

Expected minimum:
- Corel capability probe: pass;
- document context: pass;
- selection context: pass when a document is open;
- preview export: pass and non-empty;
- preflight check executes;
- no API key is printed in the diagnostics output.

Then press **Preflight** and verify that it completes without changing document content.

## 3. Explicit mutation/undo gate

Use a disposable/copy document. Existing CorelDRAW Redo history may be cleared by creating a new action.

Press **Mutation Test** and read the confirmation dialog. Continue only after accepting that warning.

Expected result:
- a temporary 5 × 5 mm test rectangle is created inside one command group;
- one Undo removes it;
- the final shape count equals the initial shape count;
- the activity result reports `restored=true`.

If this fails, do not run arbitrary AI editing tasks yet. Save the exact Mutation Test output for diagnosis.

## 4. AI configuration

Paste the OpenAI API key into the Docker and choose **Save key**. The key is stored for the current Windows user and should not be displayed back in the UI or logs.

Leave the default model at `gpt-6.1-sol` for the first field test.

## 5. Deterministic editing gate

On a copy/test document, select a simple object and run these tasks one at a time:

```text
Tell me the exact size and position of the selected object in millimetres. Do not modify it.
```

```text
Move the selected object exactly 10 mm to the right.
```

```text
Resize the selected object to 80 mm wide while preserving aspect ratio.
```

```text
Center the selected object on the active page.
```

After the three editing tasks, use **Undo AI** and verify each AI task behaves as a grouped undo operation as expected by CorelDRAW.

## 6. High-level layout gate

Select one or more simple objects and test:

```text
Fit the current selection inside the page with a 10 mm margin, preserve aspect ratio, and center it.
```

Select several objects and test:

```text
Arrange the selected objects into 3 columns with 5 mm horizontal and vertical gaps.
```

Verify the result visually and by asking the AI to report the resulting selection geometry.

## 7. Export gate

Create an empty test output directory first; SolYan Corel AI does not invent export paths or silently create arbitrary directories.

Test one file:

```text
Export the active page to C:\Temp\SolYanCorelTest\page.png at 150 DPI without transparency.
```

Then test batch output on a multi-page copy document:

```text
Export pages 1 through 3 as PNG into C:\Temp\SolYanCorelTest using base name proof, 150 DPI, include page names, and do not overwrite existing files.
```

Expected:
- one output per requested page;
- deterministic names;
- original active Corel page restored after the batch;
- rerunning with overwrite=false should refuse to replace existing output files.

## 8. Visual-review gate

Run one representative multi-step layout task that visibly changes the active page. The agent should use exact object-model operations, capture the page preview, and perform visual verification without making unrelated aesthetic changes.

## PASS criteria

Field runtime PASS requires all of the following:
- Docker loads inside CorelDRAW;
- local engine starts and IPC works;
- Diagnostics completes;
- preview export works;
- Preflight works and is non-destructive;
- Mutation Test reports restored=true;
- mm inspection/move/resize works;
- grouped undo works;
- text/style or another non-geometry edit works;
- single-file export works;
- batch export works;
- one representative multi-step AI task completes with visual review.

Until those checks pass on a real CorelDRAW installation, report the project as **CI/build PASS, Corel runtime not yet field-passed**.
