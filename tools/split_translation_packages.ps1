param(
    [string]$Source = "$PSScriptRoot\..\references\original_strings_from_game.csv",
    [string]$OutputRoot = "$PSScriptRoot\..\translations\packages"
)

$ErrorActionPreference = 'Stop'

$seedOriginal = @{
    'ui_loading' = 'Loading'
    'ui_new_game' = 'New Game'
    'ui_settings_header' = 'Settings'
    'ui_menu_credits' = 'Credits'
    'ui_menu_exit' = 'Exit'
}

$seedBulgarian = @{
    'ui_loading' = 'Зареждане'
    'ui_new_game' = 'Нова игра'
    'ui_settings_header' = 'Настройки'
    'ui_menu_credits' = 'Кредити'
    'ui_menu_exit' = 'Изход'
}

function Get-PackageName([string]$key) {
    if ($key -match '^(ui_|btn_|control_|menu_)') { return '00_menu_ui' }
    if ($key -match '^(hint_|tut_)') { return '01_hints_tutorials' }
    if ($key -match '^(quest_|ach_|insp_|perk_|zperk_|buff_|tech_)') { return '02_quests_progression' }
    if ($key -match '^([0-9]+)_') {
        $number = [int]$Matches[1]
        if ($number -le 50) { return '03_dialogue_001_050' }
        if ($number -le 100) { return '04_dialogue_051_100' }
        if ($number -le 150) { return '05_dialogue_101_150' }
        return '06_dialogue_151_plus'
    }
    return '07_systems_misc'
}

$rows = @(Import-Csv -LiteralPath $Source)
$all = foreach ($row in $rows) {
    [pscustomobject]@{
        key = $row.key
        original = $row.value
        bg = ''
        status = 'untranslated'
        notes = ''
    }
}

foreach ($row in $all) {
    if ($seedOriginal.ContainsKey($row.key)) {
        $row.original = $seedOriginal[$row.key]
        $row.bg = $seedBulgarian[$row.key]
        $row.status = 'translated'
    }
}

$missingSeedKeys = $seedOriginal.Keys | Where-Object { $all.key -notcontains $_ }
foreach ($key in $missingSeedKeys) {
    $all += [pscustomobject]@{
        key = $key
        original = $seedOriginal[$key]
        bg = $seedBulgarian[$key]
        status = 'translated'
        notes = 'Seeded from the first language smoke test.'
    }
}

if (Test-Path -LiteralPath $OutputRoot) {
    Get-ChildItem -LiteralPath $OutputRoot -File -Force | Remove-Item -Force
} else {
    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
}

$manifest = [ordered]@{
    source = (Resolve-Path $Source).Path
    totalRows = $all.Count
    packages = @()
}

foreach ($group in ($all | Group-Object { Get-PackageName $_.key } | Sort-Object Name)) {
    $fileName = "$($group.Name).csv"
    $path = Join-Path $OutputRoot $fileName
    $group.Group | Sort-Object key | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $path -Encoding utf8
    $manifest.packages += [ordered]@{
        file = $fileName
        rows = $group.Count
        translated = @($group.Group | Where-Object status -eq 'translated').Count
    }
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputRoot 'manifest.json') -Encoding utf8
Write-Output "Generated $($manifest.totalRows) rows in $($manifest.packages.Count) packages under $OutputRoot"
