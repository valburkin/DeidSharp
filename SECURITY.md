# Security policy

DeidSharp processes protected health information, so a security bug can expose a patient. Please report one privately, so that it can be fixed before it is public.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting: on the repository's **Security** tab, choose **Report a vulnerability**. Please do not open a public issue, pull request or discussion for a vulnerability. If the form is not available, open an issue that asks for a private contact and says nothing about the problem itself.

Describe the problem and how to reproduce it with **fictional data only**. Never include real patient data, real identifiers or production logs in a report, even privately. If a real record triggered the problem, recreate it with made-up values.

You can expect an acknowledgment within a week and an assessment after that. When the fix ships, a GitHub Security Advisory describes it (with a CVE where warranted) and credits you if you want. Please allow up to 90 days before disclosing the problem yourself. DeidSharp is maintained by one person and offers no service-level agreement; a report with a minimal reproduction is the fastest to fix.

A problem in Presidio, the Firely FHIR SDK or the clinical model belongs to that project; report it there, and tell us if DeidSharp needs to work around it.

## What counts

Anything that breaks a guarantee in the [threat model](docs/threat-model.md), for example:

- A **known identifier** surviving in output: one passed in `KnownIdentifiers`, or extracted by DeidSharp from the FHIR record itself, in any form the known-identifier detector is documented to recognize.
- A structured FHIR element surviving that the threat model says is removed under Safe Harbor (a name, a contact point, an identifier, a date finer than a year, more of an address than state and three-digit ZIP, a disallowed extension).
- Input quoted in an exception, log entry, finding or report.
- A detector failure that reads as "nothing found" instead of stopping the run.
- A pseudonym key exposed or accepted from a request, or pseudonyms that can be reversed without it.
- A sidecar in `docker-compose.yml` reachable from outside the machine by default.

**Not vulnerabilities:** a name the record does not list that NER misses, or an identifier written in a format no pattern covers. No detector finds everything, and the threat model says so. These are recall problems; report them as ordinary issues with a fictional example, using the "Missed identifier" template.

## Supported versions

Fixes go into the latest release only: upgrade to it to receive them.
