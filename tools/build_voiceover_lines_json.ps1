param(
    [string]$VoiceIndex = "$PSScriptRoot\..\references\voiceover_lines.json",
    [string]$TranslatedStrings = "$PSScriptRoot\..\mods\bulgarian-localization\Languages\bg\strings.csv",
    [string]$Output = "$PSScriptRoot\..\translations\voiceover_lines_bg.json",
    [switch]$IncludeUntranslatedEnglish
)

$ErrorActionPreference = 'Stop'
$index = Get-Content -LiteralPath $VoiceIndex -Raw | ConvertFrom-Json
$english = $index.languages | Where-Object language -eq 'en' | Select-Object -First 1
if (-not $english) { throw 'English voice-over language was not found.' }

$translated = @{}
foreach ($row in @(Import-Csv -LiteralPath $TranslatedStrings)) {
    if (-not [string]::IsNullOrWhiteSpace($row.value)) {
        $translated[$row.key] = $row.value
    }
}

$data = foreach ($line in $english.data) {
    if ($translated.ContainsKey($line.key)) {
        [pscustomobject]@{ key = $line.key; value = $translated[$line.key] }
    } elseif ($IncludeUntranslatedEnglish) {
        [pscustomobject]@{ key = $line.key; value = $line.value }
    }
}

$result = [ordered]@{
    languages = @(
        [ordered]@{
            language = 'bg'
            data = @($data)
        }
    )
}

$parent = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding utf8
Write-Output "Built $(@($data).Count) Bulgarian voice-over text entries at $Output"
