# Genesys banlist for EDOPro

`lflists/genesys.lflist.conf` adds a **Genesys** entry to EDOPro's Ban List dropdown.

- Link and Pendulum monsters are listed with 0 copies, so EDOPro shows them as banned.
- Every card with Genesys points is listed as `code 3 points # name`, alternate artworks included. Stock EDOPro reads only the copy count and ignores the points; the Genesys deck points companion app reads them.
- `$points_limit 100` is the cap.

## How it is built

`build_lflist.py` downloads YGOPRODeck's Genesys data (`cardinfo.php?format=genesys&misc=yes`, keyed by EDOPro card ID) and Konami's official table (https://www.yugioh-card.com/en/genesys/). It refuses to write the file if the two disagree on any card name or point value.

When Konami renames a card before YGOPRODeck does (or the reverse), the check fails. Confirm the two names are the same card, then add the pair to `name_aliases.json`.

The GitHub Action in `.github/workflows/update.yml` reruns it daily and commits only when the list changed. It calls the API once per day, within YGOPRODeck's rules.

## Use it in EDOPro

Add this to `repos` in `config/user_configs.json` (replace the URL with this repository's):

```json
{
  "url": "https://github.com/corrida-repos/edopro-genesys",
  "repo_name": "Genesys banlist",
  "repo_path": "./repositories/genesys",
  "lflist_path": "lflists",
  "should_update": true,
  "should_read": true
}
```

EDOPro then pulls the latest list on each launch and keeps the last copy for offline use.
