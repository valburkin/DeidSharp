# Safe Harbor coverage

HIPAA's Safe Harbor method (45 CFR 164.514(b)(2)) lists 18 kinds of identifier that must be removed from data about an individual and their relatives, employers and household members. This page shows, for each one, what DeidSharp does in structured FHIR data and in free text, and where coverage stops.

"Free text" means FHIR strings and markdown, inline text attachments, and anything passed to `TextDeidentifier`. Free-text detection depends on the detector stack you configure. The table names the default deterministic stack (**known**: the record's own identifiers; **pattern**: the pattern library) and what an **NER** detector (Presidio or the clinical sidecar) adds.

| # | Identifier | Structured FHIR | Free text |
|---|---|---|---|
| A | Names | `HumanName` removed everywhere (patients, relatives, staff, contacts). `Organization.name/alias`, `Location.name/alias/description` removed. `Annotation.authorString` removed. Reference displays dropped. | **known**: every name in the record, in full or by any part, case- and accent-insensitive. **pattern**: names after a title ("Dr. Hart"), a label ("Patient: …") or a relation ("daughter Carmen"). **NER**: other names. |
| B | Geography smaller than a state | `Address` cut to state, country and three-digit ZIP; sparse ZIP areas become "000". `Location.position` removed. Unknown extensions (e.g. birthplace, geolocation) removed. | **known**: street lines (suffixes in either form), cities, ZIPs from the record. **pattern**: street addresses, PO boxes, a ZIP next to a state or a label, a city before a state. **NER**: other places. |
| C | Dates (except year), ages over 89 | Every `date`/`dateTime` cut to the year; `instant` removed. Birth date removed for anyone 90 or over (age at death if recorded); `Age` values ≥ 90 (any unit), and any other quantity in years ≥ 90, become "≥ 90 years". | **known**: birth and death dates in any common format. **pattern**: ISO, numeric (with a year), month-name, month-year, compact dates; ages over 89 ("92-year-old", "aged 95"). **NER**: other date expressions. |
| D | Telephone numbers | `ContactPoint` removed. | **known** (any punctuation, with or without a country code) and **pattern** (NANP, international, 7-digit local). |
| E | Fax numbers | `ContactPoint` removed. | As phones; a number after "fax" is labeled a fax. |
| F | Email addresses | `ContactPoint` removed. | **known** and **pattern**. |
| G | Social Security numbers | `Identifier` removed. | **known**, **pattern** (dashed, spaced, labeled). |
| H | Medical record numbers | `Identifier` removed. | **known** (any separators), **pattern** (labeled "MRN", "chart #"…; unlabeled numbers of 8+ digits). |
| I | Health plan beneficiary numbers | `Identifier` removed; `Coverage.subscriberId`, `Coverage.dependent`, `Coverage.class`, and `preAuthRef` on claims and explanations of benefit removed. | **known**, **pattern** (labeled member, policy, Medicare/Medicaid ids). |
| J | Account numbers | `Identifier` removed. | **known**, **pattern** (labeled; payment cards that pass the Luhn check). |
| K | Certificate and license numbers | `Identifier` removed. | **known**, **pattern** (labeled license, DEA, NPI). |
| L | Vehicle identifiers and plates | No FHIR element in common use. | **pattern**: VINs that pass the check digit, labeled plates. |
| M | Device identifiers and serial numbers | `Device.udiCarrier`, `serialNumber`, `lotNumber`, `distinctIdentifier` removed; `Identifier` removed. | **pattern**: labeled serial, lot, UDI, IMEI; MAC addresses. |
| N | URLs | `ContactPoint` (url) removed; `Attachment.url` removed; `Meta.source` and `Bundle.link` removed. Any URI element that describes rather than defines (`Device.url`, `Endpoint.address`…) removed; defining URIs (extension urls, profiles, code systems, a definitional resource's canonical url) kept. The server part of absolute references and full URLs is replaced by a placeholder. | **known**, **pattern**. |
| O | IP addresses | Covered by the URI handling above (an IP in a URL or reference goes with it). | **pattern**: IPv4 and IPv6. |
| P | Biometric identifiers | Non-text attachments and `Binary.data` removed. | Not detected. |
| Q | Full-face photographs | `photo` removed from Patient, Practitioner, RelatedPerson, Person; non-text attachments removed. | n/a |
| R | Any other unique identifier | Resource ids and full URLs become pseudonyms (references rewritten to match, with the server part replaced); unknown extensions removed at any depth, including inside codings and on primitive values; `Identifier` removed. | **pattern**: labeled ids ("Patient ID", "passport"); long unlabeled numbers. |

## What Safe Harbor also asks, which software cannot do

- **No actual knowledge.** Safe Harbor also requires that the covered entity has no actual knowledge that the remaining information could identify the individual. That is a judgment about the specific data and recipient; use `ReidentificationRiskAssessor` to look for rare combinations of what remains (e.g. a rare diagnosis plus a three-digit ZIP and a year).
- **Current census data.** The default list of sparse three-digit ZIP areas is the one HHS published from the 2000 Census. Pass the current list to `ZipCodeGeneralizer`.

## Re-identification codes

Safe Harbor allows a code that lets the covered entity re-identify the data (45 CFR 164.514(c)), if the code is not derived from information about the individual in a way that could be reversed and the means of re-identification is not disclosed. DeidSharp's pseudonyms are keyed HMACs:

- With no key (the Safe Harbor default), each run uses a fresh random key that is never stored: the pseudonyms link nothing outside the run.
- With a key you supply, the key is the means of re-identification. Keep it secret, never ship it with the data, and treat anyone who holds it as holding the identifiers.

## Pseudonymized mode is not Safe Harbor

`FhirDeidentificationMode.Pseudonymized` keeps shifted full dates and pseudonymized identifiers so data can be linked over time. That does not meet Safe Harbor. Use it under an expert determination or a data use agreement (a HIPAA limited data set has its own rules).

## Known gaps

- Free text: identifiers that no detector recognizes (a name the record does not list and NER misses, dates written in words, "last Christmas", ages written in words, misspellings, non-English text). Measure recall on your own data; see [testing](testing.md).
- `Coding.display` and `Quantity.unit` are kept as vocabulary and are not scanned (their extensions are removed). A system that writes names into a coding's display will leak them.
- Text attachments are kept only as UTF-8 or US-ASCII without control characters; text in any other charset is removed rather than read, since it cannot be checked.
- Modifier extensions that are not allow-listed are removed, which can change what a resource means. Each removal is counted in `FhirDeidentificationStatistics.ModifierExtensionsRemoved` for review.
- Numeric dates without a year ("3/5") are not treated as dates in text, because that is how pain scores and blood pressures are written. NER detectors may still catch them.
