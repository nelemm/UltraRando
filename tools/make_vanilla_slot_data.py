"""Construit un slot_data "vanilla" (salles à leur position d'origine) à partir de data/rooms.json.

Sert à tester la construction des cartes dans le mod sans serveur Archipelago :
    python tools/make_vanilla_slot_data.py [--areas 1a,2a] [--out chemin.json]
                                          [--owned "Dash,Climb"] [--dash-shuffle 1] [--climb-shuffle 0]
                                          [--crouch-shuffle 0] [--wall-jump-shuffle 0] [--interactables]
Puis, dans Celeste (console ~) : cmr_offline <chemin.json>

- rooms : toutes les salles de l'aire (première variante de chaque niveau), à leurs bornes vanilla ;
- links : paires de trous qui se touchent dans la géométrie vanilla (côtés opposés, salles collées,
  intervalles qui se recouvrent) ; on préfère la paire dont le décalage rhelmot (compatible) correspond
  exactement à la géométrie ;
- closed_holes : tous les autres trous ;
- location_ids : ids factices (à partir de 900000) pour toutes les clés de checks du contrat ;
- owned_items : seulement si --owned est donné (sinon le mod considère que tout est possédé).
"""
import argparse, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOMS = os.path.join(ROOT, "data", "rooms.json")

INCOMPATIBLE = -2 ** 31

PICKUP_KIND = {  # kind rooms.json -> kind du contrat
    "strawberry": "strawberry", "goldenBerry": "golden", "key": "key", "summitgem": "gem",
    "blackGem": "heart", "heartGem": "heart", "cassette": "cassette",
}


def low_open(h): return h["low"] == 0
def both_open(h): return low_open(h) and h["high_open"]
def half_open(h): return low_open(h) != h["high_open"]
def closed(h): return not low_open(h) and not h["high_open"]
def size(h): return h["high"] - h["low"] + 1
def r8(n): return (n + 7) // 8 * 8


def compatible(a, b):  # port exact de Hole.Compatible (voir EXPORT_FORMAT.md)
    if (b["side"] == "Up" and a["side"] not in ("Up", "Left")) or \
       (b["side"] == "Left" and a["side"] not in ("Left", "Up")):
        r = compatible(b, a)
        return r if r == INCOMPATIBLE else -r
    align_low, align_high = a["low"] - b["low"], a["high"] - b["high"]
    if a["side"] == "Up" and b["side"] == "Down":
        one_way_up = a["kind"] == "out" or b["kind"] in ("in", "unknown")
        one_way_down = a["kind"] in ("in", "unknown") or b["kind"] == "out"
        top_is_larger = size(a) <= size(b)
        if a["launch"] is not None:
            if b["launch"] is not None:
                return INCOMPATIBLE if b["launch"] == -1 else a["launch"] - b["launch"]
            if closed(b): return a["launch"] - (b["low"] + b["high"]) // 2
            if b["high_open"]: return a["launch"] - (b["low"] + 2)
            if low_open(b): return a["launch"] - (b["high"] - 2)
        elif both_open(a) or both_open(b):
            if both_open(a) and both_open(b) and size(a) == size(b):
                return 0
        elif half_open(a) or half_open(b):
            if low_open(a) == low_open(b):
                return align_high if low_open(a) else align_low
            elif low_open(a) != b["high_open"]:
                if (one_way_up and top_is_larger) or (one_way_down and not top_is_larger):
                    return align_high if (low_open(a) or low_open(b)) else align_low
        else:
            if size(a) == size(b):
                return align_low
            if (one_way_up and top_is_larger) or (one_way_down and not top_is_larger):
                return align_low if (size(a) + size(b)) % 2 == 0 else align_high
    if a["side"] == "Left" and b["side"] == "Right":
        if a["high_open"] or b["high_open"]:
            if a["high_open"] and b["high_open"]:
                return align_high
        elif low_open(a) or low_open(b):
            if low_open(a) and low_open(b): return align_high
            if closed(a) and size(a) > 2: return align_high
            if closed(b) and size(b) > 2: return align_high
        else:
            if size(a) == size(b) or (size(a) > 2 and size(b) > 2):
                return align_high
    return INCOMPATIBLE


def find_links(rooms):
    """rooms: {key: room}. Renvoie (links, nb_exacts)."""
    cands = []  # (score, a_key, ai, b_key, bi)
    keys = list(rooms)
    for ak in keys:
        A = rooms[ak]; ab = A["bounds"]
        for bk in keys:
            if ak == bk: continue
            B = rooms[bk]; bb = B["bounds"]
            for ha in A["holes"]:
                if ha["kind"] == "none": continue
                if ha["side"] == "Right" and bb["x"] == ab["x"] + r8(ab["w"]):
                    off = (bb["y"] - ab["y"]); want = "Left"
                elif ha["side"] == "Down" and bb["y"] == ab["y"] + r8(ab["h"]):
                    off = (bb["x"] - ab["x"]); want = "Up"
                else:
                    continue
                if off % 8: continue
                off //= 8
                for hb in B["holes"]:
                    if hb["side"] != want or hb["kind"] == "none": continue
                    lo, hi = max(ha["low"], hb["low"] + off), min(ha["high"], hb["high"] + off)
                    if lo > hi: continue
                    exact = compatible(ha, hb) == off
                    cands.append((0 if exact else 1, -(hi - lo), ak, ha["index"], bk, hb["index"]))
    cands.sort()
    used, links, exact = set(), [], 0
    for score, _, ak, ai, bk, bi in cands:
        if (ak, ai) in used or (bk, bi) in used: continue
        used.add((ak, ai)); used.add((bk, bi))
        links.append({"a": ak, "a_hole": ai, "b": bk, "b_hole": bi})
        exact += score == 0
    return links, exact, used


def build(areas_data, area_keys, args):
    out = {"version": 1, "options": {
        "dash_shuffle": args.dash_shuffle, "climb_shuffle": args.climb_shuffle,
        "crouch_shuffle": args.crouch_shuffle, "wall_jump_shuffle": args.wall_jump_shuffle,
        "interactable_shuffle": 1 if args.interactables else 0,
        "logic_difficulty": 1, "room_difficulty": 2, "goal_area": area_keys[-1],
        "strawberries_required": 0, "death_link": 0},
        "chapters": {}, "location_ids": {}, "active_areas": list(area_keys)}
    next_id = 900000

    def loc(key):
        nonlocal next_id
        if key not in out["location_ids"]:
            out["location_ids"][key] = next_id; next_id += 1

    for ak in area_keys:
        area = areas_data[ak]
        # première variante de chaque niveau uniquement
        rooms = {k: r for k, r in area["rooms"].items() if "#" not in k}
        links, exact, used = find_links(rooms)
        chap = {"rooms": [], "links": links, "closed_holes": []}
        for k, r in rooms.items():
            chap["rooms"].append({"room": r["level"], "variant": k, "x": r["bounds"]["x"], "y": r["bounds"]["y"]})
            for h in r["holes"]:
                if (k, h["index"]) not in used:
                    chap["closed_holes"].append({"room": k, "hole": h["index"]})
            loc(f"{ak}|{r['level']}|room")
            if r.get("checkpoint") or r["level"] in area.get("checkpoint_rooms", []):
                loc(f"{ak}|{r['level']}|checkpoint")
            for p in r.get("pickups", []):
                kind = PICKUP_KIND.get(p["kind"])
                if kind:
                    loc(f"{ak}|{r['level']}|{kind}|{p['entity_id']}")
        loc(f"{ak}|clear")
        out["chapters"][ak] = chap
        print(f"{ak}: {len(chap['rooms'])} salles, {len(links)} liens ({exact} exacts), "
              f"{len(chap['closed_holes'])} trous fermés", file=sys.stderr)
    if args.owned is not None:
        out["owned_items"] = [s.strip() for s in args.owned.split(",") if s.strip()]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--areas", default="1a")
    ap.add_argument("--out", default=os.path.join(ROOT, "tools", "out", "vanilla_slot_data.json"))
    ap.add_argument("--rooms", default=ROOMS)
    ap.add_argument("--owned", default=None, help="liste d'items possédés séparés par des virgules")
    ap.add_argument("--dash-shuffle", type=int, default=0)
    ap.add_argument("--climb-shuffle", type=int, default=0)
    ap.add_argument("--crouch-shuffle", type=int, default=0)
    ap.add_argument("--wall-jump-shuffle", type=int, default=0)
    ap.add_argument("--interactables", action="store_true")
    args = ap.parse_args()
    with open(args.rooms, encoding="utf-8") as f:
        data = json.load(f)
    keys = [k.strip() for k in args.areas.split(",") if k.strip()]
    for k in keys:
        if k not in data["areas"]:
            sys.exit(f"aire inconnue : {k}")
    out = build(data["areas"], keys, args)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("écrit :", args.out, file=sys.stderr)


if __name__ == "__main__":
    main()
