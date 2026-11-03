# Genesys deck points

A small window that shows the Genesys points of your EDOPro deck. It reads your saved `.ydk` files, so it never touches EDOPro's memory or files and keeps working when EDOPro updates.

Launch **Genesys Deck Points.lnk** in the EDOPro folder. It starts EDOPro too if it isn't running.

- With **Follow last saved deck** on, it switches to whichever deck you last saved and recounts. It updates **when you save** in EDOPro, not while you drag cards.
- The total is green at or below the cap (exactly 100 is fine) and red above it. Yellow `?` means a card ID it doesn't recognise.
- The list shows every card that costs points, plus Link/Pendulum cards (not allowed in Genesys) in red.
- Main, Extra and Side all count. Alternate artworks cost the same as the original.

## Points data

Points, bans and the cap come from `genesys.lflist.conf`, the same file that adds the **Genesys** entry to EDOPro's Ban List dropdown. The app reads `repositories\genesys\lflists\genesys.lflist.conf` (the auto-updated GitHub copy) if it exists, otherwise `lflists\genesys.lflist.conf`. It reloads when that file changes.

## Files

- `GenesysDeckPoints.exe`: the app. `GenesysDeckPoints.Console.exe`: `--check PATH.ydk` (JSON breakdown) and `--self-test`.
- `settings.json`: window position and checkboxes, saved on close.
- `src/`, `build.ps1`: source and build (Windows .NET Framework compiler, no install needed).
- `sqlite3.dll`: reads card names from EDOPro's card databases ([public domain](https://www.sqlite.org/copyright.html)).
