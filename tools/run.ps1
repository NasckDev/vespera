# Compila e roda o Véspera. Uso: .\tools\run.ps1 [-Editor] [-Smoke]
# Defina $env:GODOT com o caminho do Godot .NET (ex.: C:\Godot\Godot_v4.7.2-stable_mono_win64.exe).
param(
    [switch]$Editor,
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not $env:GODOT) {
    Write-Error 'Defina a variável GODOT com o caminho do executável do Godot .NET 4.7.2.'
}

dotnet build (Join-Path $root 'Vespera.sln')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Editor) {
    & $env:GODOT --path $root --editor
} elseif ($Smoke) {
    & $env:GODOT --headless --path $root -- --smoke-test
    exit $LASTEXITCODE
} else {
    & $env:GODOT --path $root
}
