param(
    [string]$CorelRoot
)

$ErrorActionPreference = 'Stop'

if (Get-Process -Name CorelDRW -ErrorAction SilentlyContinue) {
    throw 'Close CorelDRAW before installing or updating SolYan Corel AI.'
}

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $packageRoot 'SolYanCorelAI'
if (-not (Test-Path $source)) {
    throw "Package folder not found: $source"
}

function Get-CorelAddonCandidates {
    $results = New-Object System.Collections.Generic.List[string]

    # Prefer the Windows App Paths registration because it also covers custom
    # install drives/folders and current Corel layouts such as
    # ...\CorelDRAW Graphics Suite\25\Programs64\CorelDRW.exe.
    $appPathKeys = @(
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\CorelDRW.exe',
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\CorelDRW.exe',
        'Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\CorelDRW.exe'
    )

    foreach ($key in $appPathKeys) {
        try {
            $registryKey = Get-Item -LiteralPath $key -ErrorAction Stop
            $exePath = $registryKey.GetValue('')
            if ($exePath -and (Test-Path -LiteralPath $exePath)) {
                $programDir = Split-Path -Parent $exePath
                if ((Split-Path -Leaf $programDir) -in @('Programs64', 'Programs')) {
                    $addonPath = Join-Path $programDir 'Addons'
                    if (-not $results.Contains($addonPath)) { $results.Add($addonPath) }
                }
            }
        }
        catch {
            # Fall back to filesystem discovery below.
        }
    }

    # Search Corel's normal Program Files roots recursively. Newer releases can
    # place a numeric version folder between the suite folder and Programs64.
    $programRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
        Select-Object -Unique

    foreach ($programRoot in $programRoots) {
        $corelBase = Join-Path $programRoot 'Corel'
        if (-not (Test-Path -LiteralPath $corelBase)) { continue }

        Get-ChildItem -LiteralPath $corelBase -Directory -Recurse -Filter 'Addons' -ErrorAction SilentlyContinue |
            Where-Object { $_.Parent -and $_.Parent.Name -in @('Programs64', 'Programs') } |
            ForEach-Object {
                if (-not $results.Contains($_.FullName)) { $results.Add($_.FullName) }
            }

        # Some installations have no Addons directory until the first custom
        # add-on is installed. Discover CorelDRW.exe and allow creation beside it.
        Get-ChildItem -LiteralPath $corelBase -File -Recurse -Filter 'CorelDRW.exe' -ErrorAction SilentlyContinue |
            ForEach-Object {
                if ($_.Directory -and $_.Directory.Name -in @('Programs64', 'Programs')) {
                    $addonPath = Join-Path $_.Directory.FullName 'Addons'
                    if (-not $results.Contains($addonPath)) { $results.Add($addonPath) }
                }
            }
    }

    return $results
}

if (-not $CorelRoot) {
    $candidates = @(Get-CorelAddonCandidates)

    if ($candidates.Count -eq 0) {
        throw 'Could not locate CorelDRAW automatically. Re-run with -CorelRoot <path-to-Programs64\Addons>. You can obtain the exact path from CorelDRAW Application.AddonPath.'
    }

    $CorelRoot = $candidates |
        Sort-Object { if ($_ -match '\\Programs64\\') { 0 } else { 1 } }, { $_ } |
        Select-Object -First 1
}

$CorelRoot = [System.IO.Path]::GetFullPath($CorelRoot)
$parentName = Split-Path -Leaf (Split-Path -Parent $CorelRoot)
if ((Split-Path -Leaf $CorelRoot) -ne 'Addons' -or $parentName -notin @('Programs64', 'Programs')) {
    throw "CorelRoot must point to CorelDRAW's Programs64\Addons (or Programs\Addons) directory. Received: $CorelRoot"
}

if (-not (Test-Path -LiteralPath $CorelRoot)) {
    $programDir = Split-Path -Parent $CorelRoot
    if (-not (Test-Path -LiteralPath (Join-Path $programDir 'CorelDRW.exe'))) {
        throw "Corel Addons directory does not exist and CorelDRW.exe was not found beside it: $CorelRoot"
    }
    New-Item -ItemType Directory -Path $CorelRoot -Force | Out-Null
}

$destination = Join-Path $CorelRoot 'SolYanCorelAI'
Write-Host "Installing SolYan Corel AI to: $destination"

if (Test-Path $destination) {
    Remove-Item -Path $destination -Recurse -Force
}

Copy-Item -Path $source -Destination $destination -Recurse -Force

Write-Host ''
Write-Host 'Installed successfully.' -ForegroundColor Green
Write-Host 'Start CorelDRAW, then open the SolYan AI docker from the Dockers panel/menu.'
Write-Host 'The AI engine runs locally and requires an OpenAI API key before AI tasks can be sent.'
