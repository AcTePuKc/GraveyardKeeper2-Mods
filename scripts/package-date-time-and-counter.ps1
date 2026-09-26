[CmdletBinding()]
param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use MAJOR.MINOR.PATCH format."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "mods\date-time-and-counter\GK2.DayCounter.csproj"
$buildOutput = Join-Path $repoRoot "mods\date-time-and-counter\bin\Release\netstandard2.1"
$pluginId = "com.actepukc.gk2.datetimeandcounter"
$sourceDll = Join-Path $buildOutput "GK2.DayCounter.dll"
$sourceLocales = Join-Path $buildOutput "Locales"
$sourceFrameworkLocales = Join-Path $buildOutput "GK2.Framework\Localization\$pluginId"
$dist = Join-Path $repoRoot "dist"
$archiveName = "DateTimeAndCounter-$Version.zip"
$archivePath = Join-Path $dist $archiveName

& dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Date Time and Counter build failed with exit code $LASTEXITCODE."
}

foreach ($required in @($sourceDll, $sourceLocales, $sourceFrameworkLocales)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Required build output is missing: $required"
    }
}

if (Test-Path -LiteralPath $archivePath) {
    throw "Refusing to overwrite existing release archive: $archivePath"
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
$stage = Join-Path $env:TEMP ("GK2-DateTimeAndCounter-package-" + [Guid]::NewGuid().ToString("N"))
$pluginFolder = Join-Path $stage "BepInEx\plugins\GK2.DayCounter"
$frameworkLocaleFolder = Join-Path $stage "BepInEx\plugins\GK2.Framework\Localization\$pluginId"

try {
    New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null
    New-Item -ItemType Directory -Path $frameworkLocaleFolder -Force | Out-Null
    Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $pluginFolder "GK2.DayCounter.dll")
    Copy-Item -LiteralPath $sourceLocales -Destination $pluginFolder -Recurse
    Copy-Item -Path (Join-Path $sourceFrameworkLocales "*") -Destination $frameworkLocaleFolder -Recurse

    $hudLocales = @(Get-ChildItem -LiteralPath (Join-Path $pluginFolder "Locales") -File -Filter "*.json")
    $frameworkLocales = @(Get-ChildItem -LiteralPath $frameworkLocaleFolder -File -Filter "*.json")
    if ($hudLocales.Count -lt 20 -or $frameworkLocales.Count -lt 20) {
        throw "Locale packaging looks incomplete (HUD: $($hudLocales.Count), Framework: $($frameworkLocales.Count))."
    }

    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $archivePath -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}

Write-Output "Created archive: $archivePath"
Write-Output "Plugin ID: $pluginId"
Write-Output "HUD locale files: $($hudLocales.Count)"
Write-Output "Framework locale files: $($frameworkLocales.Count)"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash)"
