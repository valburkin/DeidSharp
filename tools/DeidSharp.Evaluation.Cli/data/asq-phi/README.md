# ASQ-PHI

A copy of the ASQ-PHI benchmark: 1,051 fully synthetic clinical search queries, 832 with PHI and 219 hard negatives without, annotated with the HIPAA Safe Harbor identifier types. No real patient is involved.

- Source: [JamesWeatherhead/asq-phi](https://github.com/JamesWeatherhead/asq-phi), `data/synthetic_clinical_queries.txt` at commit `5f32d8d9083fa9db835a046064c503c93c0ef269` (15 Aug 2026)
- SHA-256: `cf00e424b8d2347d019f9f34e2ad1510cb4d853605410f8314bef44df8021fc8`
- License: MIT, in [LICENSE](LICENSE)
- Paper: Weatherhead J. et al., "ASQ-PHI: An adversarial synthetic data benchmark for clinical de-identification and search utility", *Data in Brief* (2026), [PMC12926592](https://pmc.ncbi.nlm.nih.gov/articles/PMC12926592/)

The file is unchanged. `AsqPhiCorpus` reads it, and CI measures the deterministic detectors on it with every change ([docs/evaluation](../../../../docs/evaluation/README.md)).
