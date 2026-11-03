"""Build lflists/genesys.lflist.conf from YGOPRODeck, checked against Konami's official table.

Usage: python build_lflist.py [--out PATH] [--api-json FILE] [--official-html FILE]

Line format (stock EDOPro reads "code count" and ignores the rest):
    14558127 3 18 # Ash Blossom & Joyous Spring
Link and Pendulum monsters are listed with a copy count of 0, so EDOPro shows them as banned.
Exits non-zero without writing anything when the two sources disagree.
"""
import argparse
import html
import json
import os
import re
import sys
import unicodedata
import urllib.request

API_URL = "https://db.ygoprodeck.com/api/v7/cardinfo.php?format=genesys&misc=yes"
OFFICIAL_URL = "https://www.yugioh-card.com/en/genesys/"
USER_AGENT = "genesys-lflist/1.0 (+EDOPro banlist generator)"
POINTS_LIMIT = 100
MIN_ENTRIES = 200
ALIASES_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "name_aliases.json")


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8")


def normalize(name):
    s = unicodedata.normalize("NFKC", html.unescape(name))
    for a, b in (("’", "'"), ("‘", "'"), ("“", '"'), ("”", '"')):
        s = s.replace(a, b)
    s = re.sub(r"\s+", " ", s).strip().upper()
    return s.replace("Ω7", "O7").replace("ARS MAGNA - ", "ARS MAGNA ")


def official_points(page):
    table = re.search(r"<table\b[^>]*\bid\s*=\s*['\"]tablepress-genesys['\"][^>]*>([\s\S]*?)</table>", page, re.I)
    if not table:
        raise ValueError("official Genesys table not found")
    points = {}
    for row in re.finditer(r"<tr\b[^>]*>([\s\S]*?)</tr>", table.group(1), re.I):
        cells = re.findall(r"<td\b[^>]*>([\s\S]*?)</td>", row.group(1), re.I)
        if len(cells) != 2:
            continue
        name = html.unescape(re.sub(r"<[^>]*>", "", cells[0])).strip()
        value = html.unescape(re.sub(r"<[^>]*>", "", cells[1])).strip()
        if not name or not value.isdigit():
            raise ValueError(f"invalid official row: {name!r} {value!r}")
        key = normalize(name)
        if points.get(key, int(value)) != int(value):
            raise ValueError(f"conflicting official rows for {name}")
        points[key] = int(value)
    if len(points) < MIN_ENTRIES:
        raise ValueError(f"official table looks incomplete ({len(points)} rows)")
    return points


def card_points(card):
    return max((m.get("genesys_points", 0) or 0 for m in card.get("misc_info", [])), default=0)


def is_forbidden(card):
    t = card.get("type", "")
    return "Link" in t or "Pendulum" in t


def load_aliases():
    with open(ALIASES_FILE, encoding="utf-8") as f:
        return {normalize(k): normalize(v) for k, v in json.load(f).items() if not k.startswith("_")}


def build(cards, official, aliases):
    official = {aliases.get(name, name): value for name, value in official.items()}
    api = {normalize(c["name"]): card_points(c) for c in cards if card_points(c) > 0}
    problems = []
    for name in sorted(official.keys() - api.keys()):
        problems.append(f"only on Konami's page: {name} ({official[name]})")
    for name in sorted(api.keys() - official.keys()):
        problems.append(f"only on YGOPRODeck: {name} ({api[name]})")
    for name in sorted(official.keys() & api.keys()):
        if official[name] != api[name]:
            problems.append(f"different points for {name}: Konami {official[name]}, YGOPRODeck {api[name]}")
    if problems:
        raise ValueError("sources disagree:\n  " + "\n  ".join(problems))

    lines = []
    for card in sorted(cards, key=lambda c: c["name"]):
        points, forbidden = card_points(card), is_forbidden(card)
        if not points and not forbidden:
            continue
        ids = sorted({card["id"], *(i["id"] for i in card.get("card_images", []))})
        for code in ids:
            if forbidden:
                lines.append(f"{code} 0 # {card['name']} (Link/Pendulum)")
            else:
                lines.append(f"{code} 3 {points} # {card['name']}")
    return lines, len(api)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default="lflists/genesys.lflist.conf")
    parser.add_argument("--api-json", help="use a saved API response instead of downloading")
    parser.add_argument("--official-html", help="use a saved copy of Konami's page instead of downloading")
    args = parser.parse_args()

    api_text = open(args.api_json, encoding="utf-8").read() if args.api_json else fetch(API_URL)
    page = open(args.official_html, encoding="utf-8").read() if args.official_html else fetch(OFFICIAL_URL)
    try:
        lines, count = build(json.loads(api_text)["data"], official_points(page), load_aliases())
    except ValueError as e:
        print(f"Not publishing: {e}", file=sys.stderr)
        return 1

    header = [
        "#[Genesys]",
        "# Generated from YGOPRODeck (https://ygoprodeck.com/api-guide/), checked against",
        f"# Konami's official table ({OFFICIAL_URL}). {count} cards have points.",
        "# Format: code copies points # name. Link and Pendulum monsters are banned (0 copies).",
        "!Genesys",
        f"$points_limit {POINTS_LIMIT}",
    ]
    text = "\n".join(header + lines) + "\n"
    try:
        old = open(args.out, encoding="utf-8").read()
    except FileNotFoundError:
        old = None
    if old == text:
        print("No changes.")
        return 0
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"Wrote {args.out}: {count} cards with points, {len(lines)} lines.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
