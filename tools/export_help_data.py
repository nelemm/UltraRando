"""Exporte la logique de Celeste (Open World) vers les données du mod d'aide.

usage : python tools/export_help_data.py
Lit   : upstream/Archipelago/worlds/celeste_open_world (apworld 1.1.3 de PoryGone, licence « Modified MIT »)
Écrit : mod/CelesteMovementHelper/Data/logic.json et mod/UltraRando/HelpData/logic.json

Format d'une règle (identique à l'apworld) : liste d'alternatives (OU), chaque alternative est une
liste de jetons (ET). Liste vide = aucune condition. Une clé absente (vm/as) = aucune condition,
exactement comme ParseData.py le fait pour 'vm_rule' et 'assist_rule'.
"""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AP = os.path.join(ROOT, "upstream", "Archipelago")
WORLD = os.path.join(AP, "worlds", "celeste_open_world")
OUT = os.path.join(ROOT, "mod", "CelesteMovementHelper", "Data", "logic.json")
OUT_CMR = os.path.join(ROOT, "mod", "UltraRando", "HelpData", "logic.json")  # aide F6 du nouveau mod

sys.path.insert(0, AP)
os.chdir(AP)
from worlds.celeste_open_world.Names import ItemName  # noqa: E402
from worlds.celeste_open_world import Items  # noqa: E402

MOVEMENT = {"u_dash", "ur_dash", "r_dash", "dr_dash", "d_dash", "dl_dash", "l_dash", "ul_dash",
            "r_climb", "l_climb", "climb", "crouch", "dash"}


def main() -> None:
    with open(os.path.join(WORLD, "data", "CelesteLevelData.json"), encoding="utf-8") as f:
        data = json.load(f)
    # nos ajouts à la logique d'Open World (Demo Dash...), les mêmes que l'apworld
    sys.path.insert(0, os.path.join(ROOT, "apworld", "ultrarando"))
    from core.static_data import _apply_extra_logic
    with open(os.path.join(ROOT, "apworld", "ultrarando", "data", "cmr_extra_logic.json"), encoding="utf-8") as f:
        _apply_extra_logic(data, json.load(f))

    all_items = Items.generate_item_data_table()
    tokens: dict[str, dict] = {}

    def note_token(tok: str, level_display: str) -> None:
        if tok in tokens or tok.startswith("any_dash_"):
            return
        if tok in MOVEMENT:
            tokens[tok] = {"kind": "movement", "name": getattr(ItemName, tok, tok)}
            return
        if tok == "demo_dash":
            tokens[tok] = {"kind": "movement", "name": "Demo Dash"}
            return
        if tok == "cannot_access":
            tokens[tok] = {"kind": "impossible", "name": "Cannot Access"}
            return
        if "Key" in tok or "Gem" in tok:
            return  # propre à un niveau : voir level_tokens
        name = getattr(ItemName, tok, None)
        if name and name in Items.interactable_item_data_table:
            tokens[tok] = {"kind": "interactable", "name": name,
                           "id": Items.interactable_item_data_table[name].code}
        elif name and name in all_items:
            tokens[tok] = {"kind": "item", "name": name, "id": all_items[name].code}
        else:
            tokens[tok] = {"kind": "other", "name": name or tok}

    levels_out: dict[str, dict] = {}
    for level in data["levels"]:
        lname = level["name"]                     # "1a", "10c"…
        ldisp = level["display_name"]
        level_tokens: dict[str, dict] = {}

        def rules_of(obj: dict) -> dict:
            out = {}
            for key, short in (("rule", "di"), ("vm_rule", "vm"), ("assist_rule", "as")):
                alts = obj.get(key, [])
                for alt in alts:
                    for tok in alt:
                        if "Key" in tok or "Gem" in tok:
                            full = f"{ldisp} - {tok}"
                            entry = {"kind": "key" if "Key" in tok else "gem", "name": full}
                            if full in all_items:
                                entry["id"] = all_items[full].code
                            level_tokens[tok] = entry
                        else:
                            note_token(tok, ldisp)
                out[short] = alts
            return out

        rooms_out = {}
        for room in level["rooms"]:
            conns, locs = [], []
            for region in room["regions"]:
                for c in region["connections"]:
                    conns.append({"from": region["name"], "to": c["dest"], **rules_of(c)})
                for loc in region.get("locations", []):
                    locs.append({"region": region["name"], "name": loc["display_name"],
                                 "type": loc["type"], **rules_of(loc)})
            doors = [{"name": d["name"], "dir": d["direction"]} for d in room["doors"]]
            rooms_out[room["name"]] = {"connections": conns, "locations": locs, "doors": doors}

        levels_out[lname] = {"name": ldisp, "tokens": level_tokens, "rooms": rooms_out}

    out = {
        "source": "Celeste (Open World) apworld 1.1.3 - Copyright (c) 2025 PoryGone - Modified MIT License "
                  "(see THIRD_PARTY_NOTICES.md)",
        "tokens": tokens,
        "levels": levels_out,
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    for path in (OUT, OUT_CMR):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    n_rooms = sum(len(l["rooms"]) for l in levels_out.values())
    print(f"{len(levels_out)} niveaux, {n_rooms} salles, {len(tokens)} jetons -> {OUT}")
    unknown = [t for t, v in tokens.items() if v["kind"] == "other"]
    if unknown:
        print("Jetons non résolus (affichés tels quels) :", unknown)


if __name__ == "__main__":
    main()
