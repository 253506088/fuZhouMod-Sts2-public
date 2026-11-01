<#
.SYNOPSIS
按当前分支声明的游戏版本构建独立发行包，拒绝混用游戏程序集。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GamePath,
    [Parameter(Mandatory = $true)][string]$GodotPath,
    [string]$BaseLibPath,
    [string]$WorkRoot,
    [string]$ArtifactRoot
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'ShengZhuSts2Mod.csproj'
$workRoot = if ([string]::IsNullOrWhiteSpace($WorkRoot)) { Join-Path $projectRoot '.compat-work' } else { [IO.Path]::GetFullPath($WorkRoot) }
$artifactRoot = if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) { Join-Path $projectRoot 'artifacts' } else { [IO.Path]::GetFullPath($ArtifactRoot) }
$gameRoot = (Resolve-Path -LiteralPath $GamePath).Path
$godotExe = (Resolve-Path -LiteralPath $GodotPath).Path
$compat = Get-Content -LiteralPath (Join-Path $projectRoot 'compatibility.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'ShengZhuSts2Mod.json') -Raw | ConvertFrom-Json
$release = Get-Content -LiteralPath (Join-Path $gameRoot 'release_info.json') -Raw | ConvertFrom-Json

if ($release.version.TrimStart('v') -ne $compat.game_version) {
    throw "本分支只构建游戏 $($compat.game_version)；提供的游戏为 $($release.version)。请使用对应游戏副本。"
}
if ($manifest.min_game_version -ne $compat.game_version) {
    throw 'Mod 清单与 compatibility.json 的目标游戏版本不一致。'
}
if ([string]::IsNullOrWhiteSpace($BaseLibPath)) {
    $BaseLibPath = Join-Path $gameRoot '../../workshop/content/2868840/3737335127/BaseLib'
}
$baseLibRoot = (Resolve-Path -LiteralPath $BaseLibPath).Path
$baseLibManifest = Get-Content -LiteralPath (Join-Path $baseLibRoot 'BaseLib.json') -Raw | ConvertFrom-Json
if ([version]$baseLibManifest.version.TrimStart('v') -lt [version]$compat.baselib_min_version) {
    throw "需要 BaseLib $($compat.baselib_min_version) 或更新版本，当前为 $($baseLibManifest.version)。"
}
$dataPath = Join-Path $gameRoot 'data_sts2_windows_x86_64'
$baseLibDll = Join-Path $baseLibRoot 'BaseLib.dll'
foreach ($dependency in @((Join-Path $dataPath 'sts2.dll'), (Join-Path $dataPath '0Harmony.dll'), $baseLibDll)) {
    if (!(Test-Path -LiteralPath $dependency -PathType Leaf)) { throw "缺少依赖：$dependency" }
}

# 每次使用新暂存目录，任何失败都不能留下看似成功的旧 PCK。
New-Item -ItemType Directory -Force $workRoot | Out-Null
Set-Content -LiteralPath (Join-Path $workRoot '.gdignore') -Value '' -NoNewline
$stage = Join-Path $workRoot ('release-' + [guid]::NewGuid().ToString('N'))
$resourceRoot = Join-Path $stage 'resources'
$payloadRoot = Join-Path $stage 'package'
$modRoot = Join-Path $payloadRoot 'ShengZhuSts2Mod'
New-Item -ItemType Directory -Force $resourceRoot, $modRoot | Out-Null

# 游戏和 BaseLib 只用来编译，发行包只复制本 Mod 的 DLL、JSON、PCK。
& dotnet build $projectFile -c Release -v:minimal "-p:Sts2Path=$gameRoot" "-p:Sts2DataDir=$dataPath" "-p:BaseLibWorkshopDll=$baseLibDll" '-p:CopyModOnBuild=false'
if ($LASTEXITCODE -ne 0) { throw "C# 编译失败，退出码=$LASTEXITCODE。" }
Copy-Item -LiteralPath (Join-Path $projectRoot '.godot/mono/temp/bin/Release/ShengZhuSts2Mod.dll') -Destination $modRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'ShengZhuSts2Mod.json') -Destination $modRoot

# 用纯资源工程导出，避免 Godot 二次编译、打入源码或个人文档。
foreach ($folder in @('ShengZhuSts2Mod', 'images', 'materials', 'shaders')) {
    $source = Join-Path $projectRoot $folder
    if (Test-Path -LiteralPath $source -PathType Container) { Copy-Item -LiteralPath $source -Destination $resourceRoot -Recurse }
}
@'
config_version=5
[application]
config/name="ShengZhuResourceExport"
config/features=PackedStringArray("4.5")
[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $resourceRoot 'project.godot') -Encoding utf8
@'
[preset.0]
name="ModResources"
platform="Windows Desktop"
runnable=false
export_filter="all_resources"
include_filter="*.json"
exclude_filter="*.cs,*.csproj,*.pdb,*.dll,*.zip"
[preset.0.options]
binary_format/architecture="x86_64"
texture_format/s3tc_bptc=true
texture_format/etc2_astc=false
'@ | Set-Content -LiteralPath (Join-Path $resourceRoot 'export_presets.cfg') -Encoding utf8

$importLog = Join-Path $stage 'godot-import.log'
$importErrorLog = Join-Path $stage 'godot-import-errors.log'
$importProcess = Start-Process -FilePath $godotExe -ArgumentList '--headless', '--path', ('"' + $resourceRoot + '"'), '--import' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $importLog -RedirectStandardError $importErrorLog
if ($importProcess.ExitCode -ne 0 -or (Select-String -LiteralPath $importLog, $importErrorLog -Pattern '(^ERROR:|SCRIPT ERROR:)' -Quiet)) {
    throw "Godot 资源导入失败，查看：$importLog"
}
$packPath = Join-Path $modRoot 'ShengZhuSts2Mod.pck'
$exportLog = Join-Path $stage 'godot-export.log'
$exportErrorLog = Join-Path $stage 'godot-export-errors.log'
$exportProcess = Start-Process -FilePath $godotExe -ArgumentList '--headless', '--path', ('"' + $resourceRoot + '"'), '--export-pack', 'ModResources', ('"' + $packPath + '"') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $exportLog -RedirectStandardError $exportErrorLog
if ($exportProcess.ExitCode -ne 0 -or (Select-String -LiteralPath $exportLog, $exportErrorLog -Pattern '(^ERROR:|SCRIPT ERROR:)' -Quiet)) {
    throw "Godot 资源导出失败，查看：$exportLog"
}
if (!(Test-Path -LiteralPath $packPath) -or (Get-Item -LiteralPath $packPath).Length -lt 1024) {
    throw '没有生成有效的 PCK，停止发行。'
}

$releaseName = "ShengZhuSts2Mod-$($manifest.version)-sts2-$($compat.game_version)-$($compat.channel)"
$outputRoot = Join-Path $artifactRoot $releaseName
New-Item -ItemType Directory -Force $artifactRoot, $outputRoot | Out-Null
Set-Content -LiteralPath (Join-Path $artifactRoot '.gdignore') -Value '' -NoNewline
Copy-Item -LiteralPath $modRoot -Destination $outputRoot -Recurse -Force
$zipPath = Join-Path $artifactRoot ($releaseName + '.zip')
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
Write-Host "构建完成：$zipPath"
