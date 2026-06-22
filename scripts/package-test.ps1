param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64",

    [switch]$SkipTests,

    [string]$OutputRoot = "artifacts\packages"
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

function Resolve-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "未找到 MSBuild。请安装 Visual Studio 2022，并勾选 .NET desktop development / Windows app SDK 相关工作负载。"
}

function Get-PackageIdentity {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content $ManifestPath
    $identity = $manifest.Package.Identity

    if (-not $identity.Publisher) {
        throw "Package.appxmanifest 缺少 Identity.Publisher，无法准备测试证书。"
    }

    if (-not $identity.Version) {
        throw "Package.appxmanifest 缺少 Identity.Version，无法生成包名。"
    }

    return [pscustomobject]@{
        Publisher = [string]$identity.Publisher
        Version = [string]$identity.Version
    }
}

function Get-OrCreate-TestCertificate {
    param(
        [string]$Publisher,
        [string]$ExportPath
    )

    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -eq $Publisher -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(7)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if (-not $cert) {
        Write-Step "未找到可用测试证书，正在创建 $Publisher"
        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $Publisher `
            -KeyUsage DigitalSignature `
            -FriendlyName "SharpTimer Test Package Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
            -NotAfter (Get-Date).AddYears(3)
    }

    $exportDirectory = Split-Path -Parent $ExportPath
    New-Item -ItemType Directory -Force -Path $exportDirectory | Out-Null
    Export-Certificate -Cert $cert -FilePath $ExportPath | Out-Null

    return $cert
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

$repoRoot = Resolve-RepoRoot
$solutionPath = Join-Path $repoRoot "SharpTimer.slnx"
$projectPath = Join-Path $repoRoot "SharpTimer.App\SharpTimer.App.csproj"
$manifestPath = Join-Path $repoRoot "SharpTimer.App\Package.appxmanifest"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$msbuild = Resolve-MSBuild
$identity = Get-PackageIdentity -ManifestPath $manifestPath

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageRoot = Join-Path $outputRootPath "SharpTimer-$($identity.Version)-$Platform-$stamp"
$appxPackageDir = Join-Path $packageRoot "AppPackages\"
$certificatePath = Join-Path $packageRoot "SharpTimer-Test.cer"
$zipPath = "$packageRoot.zip"

Write-Step "恢复 NuGet 依赖"
Invoke-Checked -FilePath "dotnet" -Arguments @("restore", $solutionPath)

if (-not $SkipTests) {
    Write-Step "运行测试"
    Invoke-Checked -FilePath "dotnet" -Arguments @("test", $solutionPath, "-c", $Configuration, "--no-restore")
}

Write-Step "准备测试签名证书"
$certificate = Get-OrCreate-TestCertificate -Publisher $identity.Publisher -ExportPath $certificatePath

Write-Step "生成 MSIX 测试包"
New-Item -ItemType Directory -Force -Path $appxPackageDir | Out-Null

$msbuildArgs = @(
    $projectPath,
    "/restore",
    "/t:Publish",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:RuntimeIdentifier=win-$Platform",
    "/p:AppxBundle=Never",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:GenerateTestArtifacts=true",
    "/p:AppxPackageSigningEnabled=true",
    "/p:PackageCertificateThumbprint=$($certificate.Thumbprint)",
    "/p:AppxPackageDir=$appxPackageDir"
)

Invoke-Checked -FilePath $msbuild -Arguments $msbuildArgs

Write-Step "整理可发送压缩包"
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$installScript = Get-ChildItem -Path $packageRoot -Recurse -Filter "Add-AppDevPackage.ps1" |
    Sort-Object FullName |
    Select-Object -First 1

if (-not $installScript) {
    throw "已生成包，但未找到 Add-AppDevPackage.ps1，无法整理测试安装包。"
}

$testPackageDir = $installScript.Directory.FullName
Compress-Archive -Path (Join-Path $testPackageDir "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
Write-Host "测试包目录：$testPackageDir"
Write-Host "可发送 zip：$zipPath"
Write-Host ""
Write-Host "测试者解压 zip 后，右键 Add-AppDevPackage.ps1，选择“使用 PowerShell 运行”。"
