"""
Turning a BILOU token tagger's output into entities, with no dependencies, so it is
tested without loading a model.

obi/deid_roberta_i2b2 tags tokens with BILOU prefixes (B-, I-, L-, U-). Hugging Face's
built-in aggregation only understands B-/I-: it mislabels or silently drops entities
whose last token is L- or that are a single U- token ("Okafor", "Northampton", ages).
So the model runs without aggregation and tokens are grouped here: a prefix only names
the position, the entity type is what follows it, and consecutive tokens of the same
type that overlap, touch or are separated by whitespace become one entity. An entity is
then widened to whole words, so a word whose first sub-token was tagged and the rest
were not is still hidden entirely.

Without aggregation the pipeline neither strides over long texts nor warns when it cuts
one at the model's limit, so texts are split here into overlapping windows counted in
the model's own tokens.
"""

from dataclasses import dataclass

# Model label (without its BILOU prefix) -> Presidio entity type.
LABEL_TO_ENTITY = {
    "PATIENT": "PERSON",
    "STAFF": "PERSON",
    "AGE": "AGE",
    "DATE": "DATE_TIME",
    "EMAIL": "EMAIL_ADDRESS",
    "HOSP": "ORGANIZATION",
    "PATORG": "ORGANIZATION",
    "LOC": "LOCATION",
    "ID": "ID",
    "PHONE": "PHONE_NUMBER",
    "OTHERPHI": "ID",
}

# Entity types written as one unbroken token ("rosa.alvarez@mailbox.example", "MRN-44-821"):
# widened over every non-space character, not just letters and digits.
TOKEN_LIKE = {"EMAIL_ADDRESS", "ID", "PHONE_NUMBER", "URL"}

# Punctuation that ends a sentence or wraps a token, never part of the identifier itself.
EDGE_PUNCTUATION = ".,;:!?\"'()[]{}<>"

# Tokens per window, well under RoBERTa's 512 (a window's text can tokenize slightly
# differently on its own), and how many tokens consecutive windows share, so an entity
# cut at one window's edge is whole, with context, in the next.
MAX_TOKENS = 400
OVERLAP_TOKENS = 64


@dataclass
class Token:
    entity: str
    start: int
    end: int
    score: float


def label_type(label: str) -> str:
    """'L-PATIENT' -> 'PATIENT'; 'PATIENT' -> 'PATIENT'."""
    prefix, dash, rest = label.partition("-")
    return rest if dash and len(prefix) == 1 and rest else label


def group_tokens(text: str, tokens: list[Token]) -> list[Token]:
    """
    Merges same-type tokens that overlap, touch or are separated only by whitespace, then
    widens to whole words. A group that covers only whitespace is dropped: the tokenizer
    trims spaces from its offsets, so a token of spaces alone has an empty span, and the
    model sometimes tags one in a run of spaces. It hides nothing, and an empty span would
    make the caller fail closed.
    """
    groups: list[Token] = []
    for token in sorted(tokens, key=lambda t: (t.start, t.end)):
        last = groups[-1] if groups else None
        if (
            last is not None
            and last.entity == token.entity
            and (token.start <= last.end or text[last.end : token.start].strip() == "")
        ):
            last.end = max(last.end, token.end)
            last.score = max(last.score, token.score)
        else:
            groups.append(Token(token.entity, token.start, token.end, token.score))
    for group in groups:
        _widen(text, group)
    return [group for group in groups if text[group.start : group.end].strip()]


def _widen(text: str, group: Token) -> None:
    """Extends a group to whole words, or for token-like types to the whole non-space token."""
    if group.entity in TOKEN_LIKE:
        while group.start > 0 and not text[group.start - 1].isspace():
            group.start -= 1
        while group.end < len(text) and not text[group.end].isspace():
            group.end += 1
        while group.end > group.start + 1 and text[group.end - 1] in EDGE_PUNCTUATION:
            group.end -= 1
        while group.start < group.end - 1 and text[group.start] in EDGE_PUNCTUATION:
            group.start += 1
        return
    while group.start > 0 and text[group.start - 1].isalnum():
        group.start -= 1
    while group.end < len(text) and text[group.end].isalnum():
        group.end += 1


def token_windows(
    offsets: list[tuple[int, int]], max_tokens: int = MAX_TOKENS, overlap: int = OVERLAP_TOKENS
) -> list[tuple[int, int]]:
    """
    (start, end) character ranges covering every token, each at most max_tokens long,
    consecutive ones sharing `overlap` tokens. `offsets` are the tokenizer's character
    offsets for the whole text, without special tokens.
    """
    if not 0 <= overlap < max_tokens:
        raise ValueError("overlap must be at least 0 and smaller than max_tokens")
    result: list[tuple[int, int]] = []
    first = 0
    while first < len(offsets):
        last = min(first + max_tokens, len(offsets)) - 1
        result.append((offsets[first][0], offsets[last][1]))
        if last == len(offsets) - 1:
            break
        first = last + 1 - overlap
    return result


def dedupe(tokens: list[Token]) -> list[Token]:
    """Overlapping windows see some tokens twice; keep each (entity, span) once, at its best score."""
    seen: dict[tuple[str, int, int], Token] = {}
    for token in tokens:
        key = (token.entity, token.start, token.end)
        if key not in seen or token.score > seen[key].score:
            seen[key] = token
    return list(seen.values())
