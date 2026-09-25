# Translation workflow

Each package has the columns `key`, `original`, `bg`, `status`, and `notes`.

Rules:

1. Search `TERMINOLOGY.csv` before translating a recurring term.
2. Record a new recurring name, title, item, place, or system term in the terminology file.
3. Put uncertain wording in `REVIEW_QUEUE.csv` instead of silently changing it everywhere.
4. Use the Thai, Ukrainian, Polish, Portuguese, and other fan translations as context references only; the English text and in-game context remain authoritative.
5. Keep placeholders and markup unchanged, for example `@(GameKeyInteraction)`, `<sprite name="day_lust">`, and formatting tags.
6. Rebuild `strings.csv` only through `tools/build_strings.ps1`.

`status` values currently used are `untranslated`, `translated`, `review`, and `approved`.
