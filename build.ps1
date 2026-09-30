param(
    [switch]$Deploy,                                   # 构建后复制 DLL 到游戏 BepInEx\plugins
    [switch]$WithSample,                               # 同时构建示例 Addon
    [string]$Configuration = "Release",
    [string]$GameFolder = "D:\ruanjian\steam\steamapps\common\Casualties Unknown Demo"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "==> 游戏目录: $GameFolder" -ForegroundColor Cyan
if (-not (Test-Path $GameFolder)) { throw "找不到游戏目录：$GameFolder（用 -GameFolder 指定）" }

Write-Host "==> 编译 NativeUILib ($Configuration)" -ForegroundColor Cyan
dotnet build (Join-Path $root "NativeUILib.csproj") -c $Configuration "/p:GameFolder=$GameFolder"
if ($LASTEXITCODE -ne 0) { throw "NativeUILib 编译失败" }

if ($WithSample) {
    Write-Host "==> 编译 SampleAddon ($Configuration)" -ForegroundColor Cyan
    dotnet build (Join-Path $root "samples\SampleAddon\SampleAddon.csproj") -c $Configuration "/p:GameFolder=$GameFolder"
    if ($LASTEXITCODE -ne 0) { throw "SampleAddon 编译失败" }
}

$dll = Join-Path $root "build\NativeUILib.dll"
Write-Host "==> 产物: $dll" -ForegroundColor Green

if ($Deploy) {
    $plugins = Join-Path $GameFolder "BepInEx\plugins"
    if (-not (Test-Path $plugins)) { New-Item -ItemType Directory -Path $plugins -Force | Out-Null }
    Copy-Item $dll $plugins -Force
    Write-Host "==> 已复制到 $plugins\NativeUILib.dll" -ForegroundColor Green

    if ($WithSample) {
        Copy-Item (Join-Path $root "samples\SampleAddon\build\SampleAddon.dll") $plugins -Force
        Write-Host "==> 已复制 SampleAddon.dll" -ForegroundColor Green
    }
    Write-Host "提示：其他模组引用 NativeUILib 时请设置 <Private>false</Private>，避免出现两份副本。" -ForegroundColor Yellow
}
