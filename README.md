# Genesys for EDOPro

Play Yu-Gi-Oh! Genesys in EDOPro:

- A **Genesys** entry in EDOPro's Ban List dropdown. Link and Pendulum monsters show as banned, and the list updates itself every time you start EDOPro.
- **Genesys Deck Points**, a small window that shows your deck's points against the 100 cap. It turns red over the cap, lists every card that costs points, and flags Link/Pendulum cards. It updates each time you save a deck.

## Install

1. **[Download GenesysDeckPoints.zip](https://github.com/corrida-repos/edopro-genesys/releases/latest/download/GenesysDeckPoints.zip)**
2. Unzip it anywhere (your EDOPro folder is fine) and double-click **Install.bat**. It finds EDOPro by itself, or asks where it is.
3. Open **Genesys Deck Points** from your desktop. It starts EDOPro and the points window.
4. In Deck Edit, choose **Genesys** in the Ban List dropdown.

If Windows shows "Windows protected your PC", click **More info** → **Run anyway**. The app isn't signed; its full source is in [`app/src`](app/src).

To remove it, run **Uninstall.bat** from the same zip.

Points come from [Konami's official Genesys table](https://www.yugioh-card.com/en/genesys/). The installer only changes EDOPro's `config/user_configs.json` (keeping a `.bak` copy) and adds the app to `addons/genesys-deck-points`; it doesn't modify EDOPro itself, so EDOPro updates keep working.

## How the list is built

`lflists/genesys.lflist.conf` lists every card with Genesys points as `code 3 points # name`, alternate artworks included, and Link/Pendulum monsters with 0 copies. `$points_limit 100` is the cap. EDOPro reads only the copy count; the points window reads the points.

`build_lflist.py` downloads YGOPRODeck's Genesys data (`cardinfo.php?format=genesys&misc=yes`, keyed by EDOPro card ID) and Konami's official table. It refuses to write the file if the two disagree on any card name or point value.

When Konami renames a card before YGOPRODeck does (or the reverse), the check fails. Confirm the two names are the same card, then add the pair to `name_aliases.json`.

The GitHub Action in `.github/workflows/update.yml` reruns it daily and commits only when the list changed. It calls the API once per day, within YGOPRODeck's rules.

## The app

Source, installer and packaging are in [`app`](app). `app/build-package.ps1` compiles the app with Windows' built-in .NET Framework compiler and writes `app/dist/GenesysDeckPoints.zip`, ready to attach to a release.

## Manual setup (without the installer)

Add this to `repos` in EDOPro's `config/user_configs.json`:

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
