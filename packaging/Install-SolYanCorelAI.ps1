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

if (-not $CorelRoot) {
    $base = Join-Path $env:ProgramFiles 'Corel'
    $candidates = @(
        Get-ChildItem -Path $base -Directory -Filter 'CorelDRAW Graphics Suite*' -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'Programs64\Addons' } |
        Where-Object { Test-Path $_ } |
        Sort-Object -Descending
    )

    if ($candidates.Count -eq 0) {
        throw 'Could not find a 64-bit CorelDRAW Programs64\Addons directory. Re-run with -CorelRoot <path-to-Programs64\Addons>.'
    }

    $CorelRoot = $candidates[0]
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
