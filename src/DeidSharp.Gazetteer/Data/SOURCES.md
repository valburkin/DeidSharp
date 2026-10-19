# Gazetteer sources

The lists in this folder come from public-domain sources, except `not-alone.txt`, which is written by hand for DeidSharp. `tools/gazetteer/build.cs` regenerates the other four from the downloaded sources:

```bash
dotnet run tools/gazetteer/build.cs -- <folder with the unzipped sources> src/DeidSharp.Gazetteer/Data
```

| File | Source | License | Retrieved |
|---|---|---|---|
| `surnames.txt` | US Census Bureau, [Frequently Occurring Surnames from the 2010 Census](https://www.census.gov/topics/population/genealogy/data/2010_surnames.html): `Names_2010Census.csv` from https://www2.census.gov/topics/genealogy/2010surnames/names.zip. Every surname held by at least 100 people, with how many | Public domain (US government work) | 2026-10-18 |
| `first-names.txt` | US Census Bureau, [1990 Census name files](https://www.census.gov/topics/population/genealogy/data/1990_census.html): `dist.female.first` and `dist.male.first` from https://www2.census.gov/topics/genealogy/1990surnames/ | Public domain (US government work) | 2026-10-18 |
| `places.txt` | US Census Bureau, [2024 Gazetteer Files](https://www.census.gov/geographies/reference-files/time-series/geo/gazetteer-files.html), places: `2024_Gaz_place_national.txt` from https://www2.census.gov/geo/docs/maps-data/data/gazetteer/2024_Gazetteer/2024_Gaz_place_national.zip. Incorporated places and census-designated places, with their legal description ("city", "CDP") dropped | Public domain (US government work) | 2026-10-18 |
| `common-words.txt` | [12dicts](http://wordlist.aspell.net/12dicts/) 6.0.2 by Alan Beale, `American/2of12.txt`: common English words, base forms | Released to the public domain by its author, who asks for acknowledgment, given here | 2026-10-18 |
| `not-alone.txt` | Written for DeidSharp: names and places in the lists above that are also clinical abbreviations, drugs, devices or eponyms | MIT, as the rest of DeidSharp | |

The Social Security Administration's first-name lists would be larger, but its site refuses scripted downloads.
