$ErrorActionPreference = 'Stop'
$path = "$PSScriptRoot\..\translations\packages\02_quests_progression.csv"
$series = @{
    'insp_aghata_house_' = 'Гост на Агата'
    'insp_alchemy_types_craft_' = 'Миксолог'
    'insp_aple_master_' = 'Берач на ябълки'
    'insp_baiter_' = 'Производител на стръв'
    'insp_baker_' = 'Господар на фурната'
    'insp_belt_workbanches_' = 'Фабрична подготовка'
    'insp_belts_build_' = 'Строител на конвейери'
    'insp_belts_gears_used_' = 'Майстор на зъбчатките'
    'insp_berry_seeker_' = 'Търсач на плодове'
    'insp_better_tools_1_' = 'Подобрение на инструментите'
    'insp_blockages_' = 'Разчисти пътя'
    'insp_books_craft_' = 'Производител на книги'
    'insp_books_quality_' = 'Изискана литература'
    'insp_breer_master_' = 'Пивоварен ум'
    'insp_breer_trader_' = 'Барон на бирата'
    'insp_brewer_' = 'Пивовар'
    'insp_bridge_cross_' = 'Строител на мостове'
    'insp_bronez_crafts_' = 'Бронзов майстор'
    'insp_bronze_ingots_' = 'Бронзова епоха'
    'insp_build_house_' = 'Дом, мил дом'
    'insp_build_shop_' = 'Собственик на магазин'
}
$roman = @{ '1'='I'; '2'='II'; '3'='III'; '4'='IV'; '5'='V'; '6'='VI' }
$rows = @(Import-Csv -LiteralPath $path)
foreach ($row in $rows) {
    foreach ($prefix in $series.Keys) {
        if ($row.key.StartsWith($prefix)) {
            $level = $null
            if ($row.original -match '\b(I|II|III|IV|V|VI)$') { $level = $Matches[1] }
            if ($level) {
                $row.bg = "$($series[$prefix]) $level"
                $row.status = 'review'
                $row.notes = 'Inspiration series candidate; review terminology and tone.'
            }
            break
        }
    }
}
$descriptionTranslations = @{
    'insp_belts_gears_used_d' = 'Върти зъбчатките като истински фабричен майстор.'
}
foreach ($row in $rows) {
    if ($descriptionTranslations.ContainsKey($row.key)) {
        $row.bg = $descriptionTranslations[$row.key]
        $row.status = 'review'
        $row.notes = 'Humorous description paired with the inspiration title.'
    }
}
$rows | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $path -Encoding utf8
Write-Output 'Seeded inspiration series for review.'
