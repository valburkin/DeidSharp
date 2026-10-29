# Surrogate sources

The lists the `mimic-iv-note` corpus draws its surrogate PHI from. All are public domain. Names are drawn in proportion to how many people carry them, so common names come up as often as they do in a population, and rare ones still appear.

| File | Source | License | Retrieved |
|---|---|---|---|
| `first-names.tsv` | US Census Bureau, [1990 Census name files](https://www.census.gov/topics/population/genealogy/data/1990_census.html): `dist.female.first` (SHA-256 `bd2f310fc4e5d5e5ea122c9d4342c9821145823118eb20db1647f305ec77b358`) and `dist.male.first` (`0a5078ef6effe3b483d15b0f7f95047662126c9bfb624ecd5e5b978fc0f2470b`) from https://www2.census.gov/topics/genealogy/1990surnames/. Each name in title case, with its sex and its percentage of people of that sex | Public domain (US government work) | 2026-10-27 |
| `surnames.txt` | The gazetteer's copy (`src/DeidSharp.Gazetteer/Data/surnames.txt`), linked at build time: every Census 2010 surname held by at least 100 people, with how many | Public domain (US government work) | 2026-10-18 |
| `places.txt` | The gazetteer's copy (`src/DeidSharp.Gazetteer/Data/places.txt`), linked at build time: Census 2024 places | Public domain (US government work) | 2026-10-18 |

`first-names.tsv` was made from the two Census files with the command below, which rebuilds it byte for byte:

```bash
{
  echo "# Census 1990 first names with sex and percent of people of that sex, title case, names with a frequency above 0. Sources in SOURCES.md."
  for x in female:F male:M; do
    awk -v sex=${x#*:} '$2+0>0 {n=tolower($1); printf "%s%s\t%s\t%s\n", toupper(substr(n,1,1)), substr(n,2), sex, $2}' dist.${x%:*}.first
  done
} > first-names.tsv
```

The gazetteer (`DeidSharp.Gazetteer`, part of DeidSharp's deterministic layer) is built from the same Census lists, so every surrogate name and place is in its dictionary, which a real one need not be. On this corpus its recall on names and places is therefore optimistic; `--gazetteer with,without` measures every stack without it.
