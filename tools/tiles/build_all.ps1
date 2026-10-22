# Regenera atlases (Python), TileSets (.tres, via Godot) e previas verificadas no Godot.
# Uso: .\tools\tiles\build_all.ps1 [-Python <python com pillow+numpy>] [-SkipVerify]
# Requer $env:GODOT apontando para o executavel do Godot 4.7.2 (de preferencia o *_console.exe).
param(
    [string]$Python = 'python',
    [switch]$SkipVerify
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $env:GODOT) { Write-Error 'Defina $env:GODOT com o caminho do Godot 4.7.2.' }

& $Python -I (Join-Path $PSScriptRoot 'gen_tiles.py')
& $Python -I (Join-Path $PSScriptRoot 'sample_maps.py')
& $Python -I (Join-Path $PSScriptRoot 'preview_py.py')
& $env:GODOT --headless --path $root --import
& $env:GODOT --headless --path $root --script res://tools/tiles/build_tilesets.gd
if (-not $SkipVerify) {
    & $env:GODOT --path $root --script res://tools/tiles/verify/verify_tileset.gd
}
