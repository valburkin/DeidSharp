"""Tests for bilou.py. No model or Presidio needed: python -m pytest sidecar/tests."""

import random
import re
import sys
from itertools import pairwise
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from bilou import MAX_TOKENS, OVERLAP_TOKENS, Token, dedupe, group_tokens, label_type, token_windows


@pytest.mark.parametrize(
    ("label", "expected"),
    [
        ("B-PATIENT", "PATIENT"),
        ("I-PATIENT", "PATIENT"),
        ("L-PATIENT", "PATIENT"),
        ("U-PATIENT", "PATIENT"),
        ("PATIENT", "PATIENT"),
        ("O", "O"),
        ("X-", "X-"),
        ("AB-CD", "AB-CD"),
    ],
)
def test_label_type_strips_only_a_one_letter_position_prefix(label: str, expected: str) -> None:
    assert label_type(label) == expected


def spans(text: str, tokens: list[Token]) -> list[str]:
    return [text[t.start : t.end] for t in group_tokens(text, tokens)]


def test_subword_tokens_and_words_of_one_entity_merge() -> None:
    text = "Ngozi Okafor met Bjornsson"
    tokens = [
        Token("PERSON", 0, 1, 1.0),
        Token("PERSON", 1, 5, 0.9),
        Token("PERSON", 6, 7, 1.0),
        Token("PERSON", 7, 12, 0.95),
        Token("PERSON", 17, 26, 1.0),
    ]
    assert spans(text, tokens) == ["Ngozi Okafor", "Bjornsson"]


def test_an_entity_covering_part_of_a_word_is_widened_to_the_whole_word() -> None:
    text = "seen by Okafor today"
    assert spans(text, [Token("PERSON", 8, 10, 1.0)]) == ["Okafor"]
    assert spans(text, [Token("PERSON", 11, 14, 1.0)]) == ["Okafor"]


@pytest.mark.parametrize(
    ("text", "start", "end", "expected"),
    [
        ("Email rosa.alvarez@mailbox.example today", 6, 10, "rosa.alvarez@mailbox.example"),
        ("(rosa.alvarez@mailbox.example).", 1, 5, "rosa.alvarez@mailbox.example"),
        ("MRN MRN-44-821, seen", 4, 7, "MRN-44-821"),
    ],
)
def test_a_token_like_entity_is_widened_to_the_whole_token(text: str, start: int, end: int, expected: str) -> None:
    entity = "ID" if expected.startswith("MRN") else "EMAIL_ADDRESS"
    assert spans(text, [Token(entity, start, end, 1.0)]) == [expected]


def test_punctuation_or_a_different_type_keeps_entities_apart() -> None:
    text = "Mercy, Boston"
    assert spans(text, [Token("ORGANIZATION", 0, 5, 1), Token("ORGANIZATION", 7, 13, 1)]) == ["Mercy", "Boston"]
    assert spans(text, [Token("ORGANIZATION", 0, 5, 1), Token("LOCATION", 7, 13, 1)]) == ["Mercy", "Boston"]


def test_overlapping_tokens_of_one_type_merge_and_keep_the_best_score() -> None:
    text = "Northampton"
    [group] = group_tokens(text, [Token("LOCATION", 0, 6, 0.5), Token("LOCATION", 4, 11, 0.9)])
    assert (group.start, group.end, group.score) == (0, 11, 0.9)


def test_order_of_tokens_does_not_matter() -> None:
    text = "Ngozi Okafor"
    forward = [Token("PERSON", 0, 5, 1), Token("PERSON", 6, 12, 1)]
    assert spans(text, list(reversed(forward))) == spans(text, forward) == ["Ngozi Okafor"]


def test_a_token_of_spaces_alone_is_dropped_and_hides_nothing() -> None:
    text = "Name:  Okafor       Unit No:\n\n\nSex:   F"
    # A token of spaces has its offsets trimmed to nothing; a token of line breaks keeps its own.
    assert spans(text, [Token("PERSON", 15, 15, 0.6)]) == []
    assert spans(text, [Token("LOCATION", 28, 30, 0.6)]) == []
    assert spans(text, [Token("ID", 36, 36, 0.6)]) == []
    # Next to a word, it merges as it always did, and the word is still hidden.
    assert [s.strip() for s in spans(text, [Token("PERSON", 7, 13, 1.0), Token("PERSON", 15, 15, 0.6)])] == ["Okafor"]


def test_an_empty_token_at_the_start_of_a_word_is_widened_to_it() -> None:
    assert spans("J  Smith", [Token("PERSON", 3, 3, 0.6)]) == ["Smith"]


def test_every_group_covers_text_and_no_letter_or_digit_a_token_covered_is_lost() -> None:
    rng = random.Random(20261028)
    alphabet = "ab9 \n\t.,@-\u00a0"
    for _ in range(5_000):
        text = "".join(rng.choice(alphabet) for _ in range(rng.randint(1, 30)))
        tokens = []
        for _ in range(rng.randint(1, 4)):
            start = rng.randint(0, len(text))
            end = rng.randint(start, min(len(text), start + 5))
            tokens.append(Token(rng.choice(["PERSON", "ID", "LOCATION"]), start, end, 1.0))
        groups = group_tokens(text, tokens)
        assert all(text[g.start : g.end].strip() for g in groups)
        covered = {i for g in groups for i in range(g.start, g.end)}
        assert all(i in covered for t in tokens for i in range(t.start, t.end) if text[i].isalnum())


def test_no_tokens_no_entities() -> None:
    assert group_tokens("text", []) == []


def test_dedupe_keeps_one_of_each_span_at_its_best_score() -> None:
    tokens = [Token("PERSON", 0, 5, 0.4), Token("PERSON", 0, 5, 0.8), Token("AGE", 0, 5, 0.3)]
    result = sorted(dedupe(tokens), key=lambda t: t.entity)
    assert [(t.entity, t.score) for t in result] == [("AGE", 0.3), ("PERSON", 0.8)]


def word_offsets(text: str) -> list[tuple[int, int]]:
    """Stand-in for a tokenizer: one token per word."""
    return [(m.start(), m.end()) for m in re.finditer(r"\S+", text)]


def test_no_tokens_no_windows() -> None:
    assert token_windows([]) == []


def test_a_short_text_is_one_window() -> None:
    text = "short note about Okafor"
    assert token_windows(word_offsets(text)) == [(0, len(text))]


def test_long_text_windows_cover_every_token_and_stay_within_the_limit() -> None:
    text = " ".join(f"w{i}" for i in range(2000))
    offsets = word_offsets(text)
    result = token_windows(offsets)

    assert result[0][0] == 0
    assert result[-1][1] == len(text)
    for start, end in result:
        assert len([o for o in offsets if start <= o[0] and o[1] <= end]) <= MAX_TOKENS
    for (s1, e1), (s2, _) in pairwise(result):
        assert s1 < s2 < e1, "windows must move forward and overlap"
        shared = [o for o in offsets if s2 <= o[0] and o[1] <= e1]
        assert len(shared) == OVERLAP_TOKENS


def test_every_token_lies_wholly_inside_some_window() -> None:
    text = " ".join(f"tok{i}" for i in range(1234))
    offsets = word_offsets(text)
    result = token_windows(offsets, max_tokens=100, overlap=10)
    for token_start, token_end in offsets:
        assert any(start <= token_start and token_end <= end for start, end in result)


def test_a_multi_word_name_at_a_window_edge_is_whole_in_the_next_window() -> None:
    words = [f"x{i}" for i in range(MAX_TOKENS - 1)] + ["Ngozi", "Okafor"] + [f"y{i}" for i in range(500)]
    text = " ".join(words)
    name_start = text.index("Ngozi")
    name_end = text.index("Okafor") + len("Okafor")
    assert any(start <= name_start and name_end <= end for start, end in token_windows(word_offsets(text)))


@pytest.mark.parametrize(("max_tokens", "overlap"), [(10, 10), (10, -1), (10, 11)])
def test_rejects_an_overlap_that_would_not_move_forward(max_tokens: int, overlap: int) -> None:
    with pytest.raises(ValueError, match="overlap"):
        token_windows([(0, 1)] * 20, max_tokens=max_tokens, overlap=overlap)
