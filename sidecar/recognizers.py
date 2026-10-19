"""
Which of Presidio's predefined recognizers the sidecar leaves out. Kept apart from app.py
so it can be tested without Presidio or a model.
"""

# Presidio's predefined recognizers the sidecar leaves out, and why. On PhysioNet's nursing
# notes (tuning half, docs/evaluation/findings.md F2) these three reported about 13,000 false
# positives and removed 11% of the clinical text that isn't PHI, for about 20 spans nothing
# else found. DeidSharp's own patterns already find URLs and labeled license numbers. spaCy
# still runs: it tokenizes and supplies the context words the pattern recognizers use.
LEFT_OUT_RECOGNIZERS = {
    "SpacyRecognizer": "general-purpose NER (en_core_web_sm), not trained on clinical text",
    "UrlRecognizer": "matches bare dotted words in notes; DeidSharp's url pattern finds real URLs",
    "UsLicenseRecognizer": "matches short letter-digit codes; DeidSharp finds labeled license numbers",
}


def left_out(keep: str | None) -> list[str]:
    """
    The recognizers to remove, given DEIDSHARP_KEEP_RECOGNIZERS: a comma-separated list of
    the left-out recognizers to keep after all, for a deployment where recall must win at any
    cost to the text. A name that isn't one of them stops the sidecar rather than being ignored.
    """
    kept = {name.strip() for name in (keep or "").split(",") if name.strip()}
    unknown = kept - LEFT_OUT_RECOGNIZERS.keys()
    if unknown:
        msg = f"DEIDSHARP_KEEP_RECOGNIZERS may only name {', '.join(sorted(LEFT_OUT_RECOGNIZERS))}."
        raise RuntimeError(msg)
    return sorted(LEFT_OUT_RECOGNIZERS.keys() - kept)
