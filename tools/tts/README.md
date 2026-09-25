# Bulgarian VC test tool

Това е изолиран тестов инструмент за българските преводи на Graveyard Keeper
II. Той не променя файлове в играта и никога не записва в
`mod\VoiceOvers\bg`.

## Какво прави

Приема translation key, намира българския текст от
`translations\packages\*.csv`, намира съответния португалски текст и WAV от
`references\portuguese_reference\extracted\VoiceOvers\pt-br\`, след което
пуска voice-cloning backend-а. Всички резултати са само в:

```text
generated_audio\tts_test\<model>\
```

Скриптът проверява отделно липсващ български превод, липсващ португалски
reference текст, липсващ WAV и грешен формат на reference файла.

## Модели

Тестът е **VC-only**. Piper и MMS не са включени, защото са fixed-voice и не
могат да клонират португалския reference speaker.

| ID | Локален проект | Статус |
|---|---|---|
| `bgtts-v7` | `C:\WebStuff\BG-TTS-V7` | сравнителен български VC baseline чрез MioCodec reference |
| `omnivoice-bg` | `C:\WebStuff\Omnivoice-BG` | OmniVoice-BG с нашия Bulgarian frontend/adapter и португалски reference transcript |

Това са единствените два активни профила за игровия тест. OmniVoice не се
пуска през суровия benchmark: bridge-ът използва същите валидирани frontend
настройки като десктоп приложението — нормализация на числа/часове,
регистър на съкращенията и одобрените произносителни override-и.

Профилите използват вече съществуващите локални среди и скриптове. Не се
инсталира нищо автоматично.

## Примери

От корена на проекта:

```powershell
python tools\tts\tts_test.py list
python tools\tts\tts_test.py resolve --key 1_intro_prison_wake_1
python tools\tts\tts_test.py generate --key 1_intro_prison_wake_1 --model bgtts-v7
python tools\tts\tts_test.py generate --key 1_intro_prison_wake_1 --model omnivoice-bg
```

За един и същ key през двата профила:

```powershell
python tools\tts\tts_test.py generate --key 1_intro_prison_wake_1 --model both
```

`all-vc` и `all-bg-vc` са запазени като съвместими имена и също означават
двата активни профила:

```powershell
python tools\tts\tts_test.py generate --key 1_intro_prison_wake_1 --model all-vc
```

## Voice transfer тест - локален workflow

Това е основният ни workflow. `--text` е новата реплика, а
`--reference-audio` и `--reference-text` описват гласа, който искаме да бъде
копиран. За проекта използваме одобрения player voice от `wake_20` като
локален референт:

```powershell
python tools\tts\voice_transfer_test.py `
  --content-key 1_intro_prison_wake_18_a_1 `
  --voice-reference generated_audio\tts_test\omnivoice-bg\1_intro_prison_wake_20.wav `
  --voice-reference-text "Никога не сме имали възможност да поговорим. Ти винаги беше по средата на поредното изтезание..." `
  --model both
```

В този пример:

- `--content-key` избира репликата на Лари, която ще бъде генерирана;
- `--voice-reference` е гласът от `wake_20`, не входната реплика на Лари;
- `--voice-reference-text` е текстът, който реално се чува в `wake_20`;
- изходът е новата реплика на Лари, изговорена с гласа от `wake_20`.

Текстът на reference гласа обикновено се взема от съответния `.json` файл до
WAV-а. При нужда може да се подаде изрично с `--voice-reference-text`.
Резултатите се записват само в
`generated_audio\tts_test\voice_transfer\<model>\<content-key>\`.

Може да се използва и външен готов запис като voice reference. За MP3 е
препоръчително първо да се конвертира до моно WAV; ако до WAV-а няма `.json`
с текста на записа, текстът се подава изрично:

```powershell
python tools\tts\voice_transfer_test.py `
  --content-key 1_intro_prison_wake_18_a_1 `
  --voice-reference generated_audio\tts_test\other\1_intro_prison_wake_18_a_1.wav `
  --voice-reference-text "Лари. Приятно ми е. Почти десетилетие сме съквартиранти... ха ха." `
  --model omnivoice-bg
```

Това ще създаде `transferred.wav` в
`generated_audio\tts_test\voice_transfer\omnivoice-bg\1_intro_prison_wake_18_a_1\`.

Това е voice-conditioned TTS, а не истински audio-to-audio voice
conversion: съдържанието се генерира наново от българския текст. ElevenLabs
файловете не се подават като локален reference, когато целта е да използваме
гласа от `wake_20`; те остават отделни емоционални fallback варианти.

## ElevenLabs fallback workflow

Ако локалният OmniVoice вариант е безжизнен, губи емоцията или не успява да
изговори правилно репликата, използваме ElevenLabs Speech-to-Speech само за
тази реплика. Подаваме готовия български/емоционален запис като `audio`, а
избраният ElevenLabs voice е target voice. Ключът трябва да е наличен в
`ELEVENLABS_API_KEY` и никога не се записва в проекта.

Примерът използва текущия generated voice `My Voice (Testis)`:

```powershell
$key = $env:ELEVENLABS_API_KEY
$voiceId = 'XEjSnaKoOXsyyRMw0z6q'
$input = 'generated_audio\tts_test\other\1_intro_prison_wake_18_a_1.wav'
$output = 'generated_audio\tts_test\elevenlabs\1_intro_prison_wake_18_a_1_my_voice.mp3'

New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
curl.exe -sS -X POST `
  "https://api.elevenlabs.io/v1/speech-to-speech/$voiceId?output_format=mp3_44100_128" `
  -H "xi-api-key: $key" `
  -F 'model_id=eleven_multilingual_sts_v2' `
  -F "audio=@$input;type=audio/wav" `
  -o $output
```

Този резултат е fallback/сравнителен вариант и не се копира автоматично в
`mod\VoiceOvers\bg`. Ако ElevenLabs вариантът е по-добър, той може да бъде
одобрен ръчно за конкретния key. Така не е нужно всички реплики да минават
през платения API - първо използваме локалния OmniVoice, а ElevenLabs само
там, където локалният резултат не е достатъчно добър.

Ако някога трябва да се запази точното изпълнение, паузи и интонация на
входния WAV,
ще е нужен отделен VC модел от типа RVC/So-VITS-SVC.

Първо може да се провери всичко без зареждане на модел:

```powershell
python tools\tts\tts_test.py generate --key 1_intro_prison_wake_1 --model all-vc --dry-run
```

Ако искаме да използваме конкретен португалски WAV вместо автоматичното
съответствие по key:

```powershell
python tools\tts\tts_test.py generate `
  --key 1_intro_prison_wake_1 `
  --model bgtts-v7 `
  --reference references\portuguese_reference\extracted\VoiceOvers\pt-br\1_intro_prison_wake_1.wav
```

## Променливи за локални пътища

Ако някой от проектите е преместен, пътищата се променят без редактиране на
скрипта:

```powershell
$env:BULGARIAN_TOOLKIT = 'C:\WebStuff\Bulgarian-Speech-Toolkit'
$env:OMNIVOICE_ROOT = 'C:\WebStuff\Omnivoice-BG'
$env:OMNIVOICE_MODEL_DIR = 'C:\WebStuff\Omnivoice-BG\models\omnivoice-bg'
```

## Какво инструментът не прави

- не променя преводите;
- не копира генерирани WAV файлове в game/mod директории;
- не приема автоматично, че всички модели поддържат български;
- не приема грешка на модел за доказателство, че преводът е неправилен;
- не публикува и не качва audio/reference файлове никъде.
