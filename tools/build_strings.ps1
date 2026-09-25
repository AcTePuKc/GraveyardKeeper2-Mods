param(
    [string]$PackageRoot = "$PSScriptRoot\..\translations\packages",
    [string]$Output = "$PSScriptRoot\..\mods\bulgarian-localization\Languages\bg\strings.csv",
    [switch]$IncludeUntranslatedEnglish
)

$ErrorActionPreference = 'Stop'
$files = Get-ChildItem -LiteralPath $PackageRoot -Filter '*.csv' -File | Sort-Object Name
$rows = @()
foreach ($file in $files) {
    $rows += @(Import-Csv -LiteralPath $file.FullName)
}

$duplicates = @($rows | Group-Object key | Where-Object Count -gt 1)
if ($duplicates.Count -gt 0) {
    throw "Duplicate keys found: $($duplicates.Name -join ', ')"
}

$outputRows = foreach ($row in ($rows | Sort-Object key)) {
    if (-not [string]::IsNullOrWhiteSpace($row.bg)) {
        [pscustomobject]@{ key = $row.key; value = $row.bg }
    } elseif ($IncludeUntranslatedEnglish) {
        [pscustomobject]@{ key = $row.key; value = $row.original }
    }
}

$parent = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$outputRows | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $Output -Encoding utf8
Write-Output "Built $(@($outputRows).Count) rows at $Output"
