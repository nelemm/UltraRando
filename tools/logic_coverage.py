"""Logic coverage of UltraRando: how much of the logic is checked against what the game and the mod do.

usage: python tools/logic_coverage.py [--difficulty vanilla_movement] [--areas 0a,1a,...] [--details]

A logic element = one intra-room connection or one location of Open World's logic (data/openworld_logic.json plus
data/cmr_extra_logic.json), with the rule of the chosen difficulty (vm_rule / assist_rule, else rule).
An element is VALIDATED when every automatic check below passes:
  1. tokens   : every token of the rule is an item the generator knows and the mod enforces (or cannot_access);
  2. entities : every interactable type the mod removes while its item is missing, and that is present in the room
                (data/rooms.json, exported from the game), is named by at least one rule of that room. Otherwise the
                logic of the room may count on an object that is not there (or ignore one that is needed);
  3. doors    : the regions of the element exist in the room, and every door of the room is a hole of the game room.
Elements with cannot_access are CLASSIFIED (known impossible / advanced tech: not counted as missing).
Everything else is UNVERIFIED, with the reason. The numbers are a consistency measure, not a proof that a human can
do every jump: the per-element rules come from Open World's logic and are only exercised in game where our tests go.
"""
import argparse, json, os, sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "apworld", "ultrarando", "data")
import importlib.util  # noqa: E402
_spec = importlib.util.spec_from_file_location("cmr_names", os.path.join(ROOT, "apworld", "ultrarando", "core", "names.py"))
N = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(N)


def _apply_extra_logic(ow, extra):
    """Same as core/static_data.py _apply_extra_logic (our replacements of Open World connection rules)."""
    for e in extra["connections"]:
        lv = next(l for l in ow["levels"] if l["name"] == e["level"])
        room = next(r for r in lv["rooms"] if r["name"] == e["room"])
        reg = next(r for r in room["regions"] if r["name"] == e["from"])
        conn = next(c for c in reg["connections"] if c["dest"] == e["to"])
        for k in ("rule", "vm_rule", "assist_rule"):
            if k in e:
                conn[k] = e[k]
    for e in extra.get("locations", []):
        lv = next(l for l in ow["levels"] if l["name"] == e["level"])
        room = next(r for r in lv["rooms"] if r["name"] == e["room"])
        reg = next(r for r in room["regions"] if r["name"] == e["region"])
        loc = next(l for l in reg["locations"] if l["name"] == e["location"])
        for k in ("rule", "vm_rule", "assist_rule"):
            add = e.get("add_" + k)
            if add:
                # a missing key falls back to "rule" (Open World semantics): extend that one
                base = loc.get(k, loc.get("rule", [])) or []
                loc[k] = base + [alt for alt in add if alt not in base]

# entity name (EntityData.Name) -> possible rule tokens (mod: Source/AP/ItemNames.cs ItemForEntity)
ENTITY_TOKENS = {
    "spring": {"springs"}, "wallSpringLeft": {"springs"}, "wallSpringRight": {"springs"},
    "zipMover": {"traffic_blocks"},
    "cassetteBlock": {"blue_cassette_blocks", "pink_cassette_blocks", "yellow_cassette_blocks", "green_cassette_blocks"},
    "dreamBlock": {"dream_blocks"}, "touchSwitch": {"coins"}, "sinkingPlatform": {"sinking_platforms"},
    "movingPlatform": {"moving_platforms"}, "booster": {"blue_boosters", "red_boosters"},
    "cloud": {"blue_clouds", "pink_clouds"}, "moveBlock": {"move_blocks"}, "whiteblock": {"white_block"},
    "swapBlock": {"swap_blocks"}, "torch": {"torches"}, "infiniteStar": {"feathers"}, "bigSpinner": {"bumpers"},
    "crushBlock": {"kevin_blocks"}, "badelineBoost": {"badeline_boosters"}, "fireBall": {"fire_ice_balls"},
    "coreModeToggle": {"core_toggles"}, "bounceBlock": {"core_blocks"}, "eyebomb": {"pufferfish"},
    "glider": {"jellyfish"}, "lightningBlock": {"breaker_boxes"}, "refill": {"dash_refills", "double_dash_refills"},
    "dashSwitchH": {"dash_switches"}, "dashSwitchV": {"dash_switches"}, "seeker": {"seekers"}, "flingBird": {"bird"},
}
# removed by the mod while missing, but never needed to move: a torch only lights the room (no collision)
NON_BLOCKING = {"torch"}
# hazards: removing them can only make the room easier, so a rule that ignores them is safe
HAZARDS = {"seeker", "fireBall"}
SIDE = {"left": "Left", "right": "Right", "up": "Up", "down": "Down"}


def rule_of(obj, diff):
    key = {"vanilla_movement": "vm_rule", "assist_mode": "assist_rule"}.get(diff)
    return obj.get(key) if key and key in obj else obj.get("rule", [])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--difficulty", default="vanilla_movement", choices=["developer_intended", "vanilla_movement", "assist_mode"])
    ap.add_argument("--areas", default="")
    ap.add_argument("--details", action="store_true")
    a = ap.parse_args()

    ow = json.load(open(os.path.join(DATA, "openworld_logic.json"), encoding="utf-8"))
    _apply_extra_logic(ow, json.load(open(os.path.join(DATA, "cmr_extra_logic.json"), encoding="utf-8")))
    rooms_json = json.load(open(os.path.join(DATA, "rooms.json"), encoding="utf-8"))
    game_rooms = {k: v["rooms"] for k, v in rooms_json["areas"].items()}
    areas = [x for x in a.areas.split(",") if x] or N.AREA_ORDER
    known = set(N.TOKEN_TO_ITEM)
    # key tokens of the logic ("Depths Key"...): events, or items with keysanity (Items.KEY_ITEMS)
    is_key = lambda t: t.endswith(" Key") or " Key " in t or t.startswith("Gem ")  # summit gems: events too

    total = Counter()
    per_area = defaultdict(Counter)
    reasons = Counter()
    details = []
    for lv in ow["levels"]:
        ak = lv["name"]
        if ak not in areas:
            continue
        groom = game_rooms.get(ak, {})
        for room in lv["rooms"]:
            gr = groom.get(room["name"])
            regions = {r["name"] for r in room["regions"]}
            # every rule of the room, to see which interactables the logic of the room talks about
            room_tokens = set()
            elems = []
            for r in room["regions"]:
                for c in r.get("connections", []):
                    elems.append((f'{r["name"]}->{c["dest"]}', c, c["dest"] in regions))
                for l in r.get("locations", []):
                    elems.append((f'loc {l["name"]}', l, True))
                for e in [x for x in r.get("connections", [])] + r.get("locations", []):
                    for k in ("rule", "vm_rule", "assist_rule"):
                        for alt in e.get(k, []) or []:
                            room_tokens.update(alt)
            gap = set()
            door_bad = []
            if gr is not None:
                for ent in set(gr.get("entities", [])):
                    toks = ENTITY_TOKENS.get(ent)
                    if toks and ent not in NON_BLOCKING and ent not in HAZARDS and not (toks & room_tokens):
                        gap.add(ent)
                sides = Counter(h["side"] for h in gr.get("holes", []))
                for d in room.get("doors", []):
                    if d["direction"] in SIDE and sides[SIDE[d["direction"]]] == 0:
                        door_bad.append(d["name"])
            for (name, e, reg_ok) in elems:
                rule = rule_of(e, a.difficulty) or []
                toks = {t for alt in rule for t in alt}
                if gr is None:
                    status = "classified"  # room not in the game export: never placed in a built map
                elif any(alt == ["cannot_access"] or "cannot_access" in alt for alt in rule) and all("cannot_access" in alt for alt in rule):
                    status = "classified"
                elif {t for t in toks if not is_key(t) and not (t.startswith("any_dash_") and set(t[9:].split("_")) <= N.DIRECTION_TOKENS)} - known:
                    status = "unverified"; reasons["unknown token " + ",".join(sorted(t for t in toks - known if not t.startswith("any_dash_")))] += 1
                elif not reg_ok or door_bad:
                    status = "unverified"; reasons["door/region mismatch"] += 1
                elif gap:
                    status = "unverified"; reasons["entity not in the room's logic: " + ",".join(sorted(gap))] += 1
                else:
                    status = "validated"
                total[status] += 1
                per_area[ak][status] += 1
                if a.details and status == "unverified":
                    details.append(f"{ak} {room['name']} {name}: gap={sorted(gap)} doors={door_bad}")
    n = sum(total.values())
    ok = total["validated"] + total["classified"]
    print(f"difficulty {a.difficulty}, {n} logic elements")
    for ak in areas:
        c = per_area.get(ak)
        if c:
            m = sum(c.values())
            print(f"  {ak:4} {100 * (c['validated'] + c['classified']) / m:5.1f} %  ({c['validated']} validated, {c['classified']} classified, {c['unverified']} unverified)")
    print(f"COVERAGE {100 * ok / n:.1f} %  ({total['validated']} validated + {total['classified']} classified of {n})")
    for r, k in reasons.most_common(25):
        print(f"  {k:4}  {r}")
    for d in details[:400]:
        print(d)


if __name__ == "__main__":
    main()
