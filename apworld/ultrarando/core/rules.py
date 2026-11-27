"""Rule representation shared by the layout validator and the Archipelago rules.

A rule is a tuple of alternatives (OR); each alternative is a frozenset of item names (AND).
FREE = one empty alternative, IMPOSSIBLE = no alternative.
"""
from __future__ import annotations

import itertools

from .names import TOKEN_TO_ITEM, CANNOT_ACCESS, DIR_DASHES, DIRECTION_TOKENS

Rule = tuple  # tuple[frozenset[str], ...]
FREE: Rule = (frozenset(),)
IMPOSSIBLE: Rule = ()

DIFFICULTIES = ["easy", "normal", "hard", "expert", "master", "perfect"]
LOGIC_KEYS = ["rule", "vm_rule", "assist_rule"]


def simplify(alts) -> Rule:
    """Remove duplicate alternatives and alternatives that are supersets of another one."""
    uniq = sorted(set(alts), key=lambda s: (len(s), sorted(s)))
    out: list[frozenset] = []
    for a in uniq:
        if not any(b <= a for b in out):
            out.append(a)
    return tuple(out)


def rule_and(r1: Rule, r2: Rule) -> Rule:
    if not r1 or not r2:
        return IMPOSSIBLE
    return simplify(a | b for a in r1 for b in r2)


def rule_or(r1: Rule, r2: Rule) -> Rule:
    return simplify(tuple(r1) + tuple(r2))


def satisfied(rule: Rule, items) -> bool:
    for alt in rule:
        if alt <= items:
            return True
    return False


def is_free(rule: Rule) -> bool:
    return any(len(a) == 0 for a in rule)


def ow_token(token: str, level_display: str) -> str:
    if "Key" in token or "Gem" in token:
        return f"{level_display} - {token}"
    if token not in TOKEN_TO_ITEM:
        raise KeyError(f"Unknown Open World token {token!r}")
    return TOKEN_TO_ITEM[token]


def ow_rule(holder: dict, logic_difficulty: int, level_display: str) -> Rule:
    """Rule of an Open World location/connection for logic_difficulty 0/1/2 (same semantics as Open World:
    a missing key or an empty list means no requirement; any_dash_x_y = any of these directions)."""
    raw = holder.get(LOGIC_KEYS[logic_difficulty])
    if not raw:
        return FREE
    alts: list[frozenset] = []
    for possible in raw:
        fixed: set[str] = set()
        multi: list[list[str]] = []
        for tok in possible:
            if tok.startswith("any_dash_"):
                dirs = tok[len("any_dash_"):].split("_")
                for d in dirs:
                    if d not in DIRECTION_TOKENS:
                        raise KeyError(f"Bad dash direction in {tok!r}")
                multi.append([TOKEN_TO_ITEM[d + "_dash"] for d in dirs])
            else:
                fixed.add(ow_token(tok, level_display))
        if CANNOT_ACCESS in fixed:
            continue
        for comb in itertools.product(*multi):
            alts.append(frozenset(fixed) | frozenset(comb))
    return simplify(alts)


DASH_ABILITY: Rule = (frozenset(DIR_DASHES),)  # conservative: direction unknown -> all of them


def rhelmot_rule(req, room_difficulty: int) -> Rule:
    """Conservative conversion of a rhelmot <req> (rooms absent from Open World)."""
    if req is None:
        return FREE
    if req == "impossible":
        return IMPOSSIBLE
    if "and" in req:
        r = FREE
        for sub in req["and"]:
            r = rule_and(r, rhelmot_rule(sub, room_difficulty))
        return r
    if "or" in req:
        r = IMPOSSIBLE
        for sub in req["or"]:
            r = rule_or(r, rhelmot_rule(sub, room_difficulty))
        return r
    if "dashes" in req:
        return FREE if req["dashes"] == "zero" else DASH_ABILITY
    if "difficulty" in req:
        return FREE if DIFFICULTIES.index(req["difficulty"]) <= room_difficulty else IMPOSSIBLE
    # keys and session flags: not modelled -> impossible (conservative)
    return IMPOSSIBLE
