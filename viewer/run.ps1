# 관전 화면 실행. 인자는 그대로 화면에 넘긴다.
# 예) .\viewer\run.ps1                       시드 7, 일반 유저 판을 만들어 보여 준다
#     .\viewer\run.ps1 --seed 42 --limits expert
#     .\viewer\run.ps1 --replay replays\seed-7-casual.json
param([Parameter(ValueFromRemainingArguments = $true)] $ViewerArgs)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

$godot = $env:GODOT
if (-not $godot) {
    $found = Get-Command godot -ErrorAction SilentlyContinue
    if ($found) { $godot = $found.Source }
}
if (-not $godot) {
    $godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*\*\Godot_*_mono_win64.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*_console.exe' } | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $godot) { throw "Godot 4 .NET 실행 파일을 찾지 못했다. 환경 변수 GODOT에 경로를 넣어 주세요." }

dotnet build "$here\AiBehaviorLabViewer.csproj" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "관전 화면 빌드 실패" }

# 리플레이 경로는 절대 경로로 바꿔 넘긴다(Godot의 작업 폴더가 viewer라서).
$forward = @()
for ($i = 0; $i -lt $ViewerArgs.Count; $i++) {
    $forward += $ViewerArgs[$i]
    if ($ViewerArgs[$i] -eq '--replay' -and $i + 1 -lt $ViewerArgs.Count) {
        $i++
        $forward += (Resolve-Path $ViewerArgs[$i]).Path
    }
}

& $godot --path $here -- @forward
