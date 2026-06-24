param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64",

    [switch]$SkipTests,

    [switch]$SkipInstaller,

    [string]$OutputRoot = "artifacts\release"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-RepoRoot {
    $scriptDir = Split-Path -Parent $PSCommandPath
    return (Resolve-Path (Join-Path $scriptDir "..")).Path
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "命令执行失败：$FilePath $($Arguments -join ' ')"
    }
}

function Get-AppVersion {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content $ManifestPath
    $version = [string]$manifest.Package.Identity.Version

    if (-not $version) {
        return "0.0.0"
    }

    return ($version -replace "\.0$", "")
}

function Resolve-InnoSetupCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidatePaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $candidatePaths) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    return $null
}

function Copy-WinUIAppResources {
    param(
        [string]$BuildOutputDir,
        [string]$PublishDir
    )

    $rootResourceFiles = @(
        "*.xbf",
        "*.pri"
    )

    foreach ($pattern in $rootResourceFiles) {
        Copy-Item -Path (Join-Path $BuildOutputDir $pattern) -Destination $PublishDir -Force -ErrorAction SilentlyContinue
    }

    foreach ($directory in @("Controls", "Rendering", "Views")) {
        $source = Join-Path $BuildOutputDir $directory
        if (Test-Path $source) {
            Copy-Item -Path $source -Destination $PublishDir -Recurse -Force
        }
    }
}

$repoRoot = Resolve-RepoRoot
$solutionPath = Join-Path $repoRoot "SharpTimer.slnx"
$projectPath = Join-Path $repoRoot "SharpTimer.App\SharpTimer.App.csproj"
$manifestPath = Join-Path $repoRoot "SharpTimer.App\Package.appxmanifest"
$innoScriptPath = Join-Path $repoRoot "installer\SharpTimer.iss"
$version = Get-AppVersion -ManifestPath $manifestPath
$runtimeIdentifier = "win-$Platform"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$buildOutputDir = Join-Path $repoRoot "SharpTimer.App\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$runtimeIdentifier"
$publishDir = Join-Path $outputRootPath "publish\$runtimeIdentifier"
$portableRoot = Join-Path $outputRootPath "portable"
$installerRoot = Join-Path $outputRootPath "installer"
$portableName = "SharpTimer-$version-$runtimeIdentifier-portable"
$portableStage = Join-Path $portableRoot $portableName
$portableZip = Join-Path $portableRoot "$portableName.zip"

Write-Step "恢复 NuGet 依赖"
Invoke-Checked -FilePath "dotnet" -Arguments @("restore", $solutionPath)

if (-not $SkipTests) {
    Write-Step "运行测试"
    Invoke-Checked -FilePath "dotnet" -Arguments @("test", $solutionPath, "-c", $Configuration, "--no-restore")
}

Write-Step "发布 unpackaged self-contained 程序"
if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

Invoke-Checked -FilePath "dotnet" -Arguments @(
    "restore",
    $projectPath,
    "-r", $runtimeIdentifier,
    "-p:Platform=$Platform",
    "-p:WindowsPackageType=None",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:PublishReadyToRun=true"
)

Invoke-Checked -FilePath "dotnet" -Arguments @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $runtimeIdentifier,
    "--self-contained", "true",
    "--no-restore",
    "-p:Platform=$Platform",
    "-p:WindowsPackageType=None",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:PublishSingleFile=false",
    "-o", $publishDir
)

Copy-WinUIAppResources -BuildOutputDir $buildOutputDir -PublishDir $publishDir

Write-Step "生成便携版 zip"
if (Test-Path $portableStage) {
    Remove-Item -LiteralPath $portableStage -Recurse -Force
}
if (Test-Path $portableZip) {
    Remove-Item -LiteralPath $portableZip -Force
}

New-Item -ItemType Directory -Force -Path $portableStage | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $portableStage -Recurse -Force
Compress-Archive -Path $portableStage -DestinationPath $portableZip -Force

$installerPath = $null

if (-not $SkipInstaller) {
    Write-Step "生成普通安装包 exe"
    $iscc = Resolve-InnoSetupCompiler

    if (-not $iscc) {
        Write-Warning "未找到 Inno Setup 编译器 ISCC.exe，已跳过安装包 exe。安装 Inno Setup 6 后重新运行本脚本即可生成。"
    }
    else {
        if (-not (Test-Path $innoScriptPath)) {
            throw "未找到 Inno Setup 脚本：$innoScriptPath"
        }

        New-Item -ItemType Directory -Force -Path $installerRoot | Out-Null

        Invoke-Checked -FilePath $iscc -Arguments @(
            "/DAppVersion=$version",
            "/DSourceDir=$publishDir",
            "/DOutputDir=$installerRoot",
            $innoScriptPath
        )

        $installerPath = Join-Path $installerRoot "SharpTimer-$version-$runtimeIdentifier-setup.exe"
    }
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
Write-Host "发布目录：$publishDir"
Write-Host "便携版 zip：$portableZip"
if ($installerPath -and (Test-Path $installerPath)) {
    Write-Host "安装包 exe：$installerPath"
}
elseif (-not $SkipInstaller) {
    Write-Host "安装包 exe：未生成（需要安装 Inno Setup 6）"
}
