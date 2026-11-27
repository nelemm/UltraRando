"""Crée le fork de l'apworld Celeste (Open World) avec l'item « Wall Jump » (V1, brique 6 en brouillon).

usage : python tools/make_fork.py
Lit   : downloads/openworld-1.1.3/extracted/celeste_open_world (apworld officiel 1.1.3)
Écrit : v1/apworld/celeste_open_world/  et  v1/apworld/celeste_open_world.apworld

Chaque modification est un remplacement exact. Si un point d'ancrage n'existe plus
(nouvelle version de l'apworld), le script s'arrête au lieu de produire un fork cassé.
Toutes les lignes ajoutées sont marquées « # CMR ».

Logique V1 (volontairement prudente) : sans « Wall Jump », seuls le Prologue et l'Épilogue sont accessibles.
C'est toujours juste (la logique ne demande jamais de se passer du wall jump), mais grossier :
l'annotation passage par passage est la brique 7 de la V2.
"""
import json, os, shutil, sys, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "downloads", "openworld-1.1.3", "extracted", "celeste_open_world")
DST_DIR = os.path.join(ROOT, "v1", "apworld")
DST = os.path.join(DST_DIR, "celeste_open_world")

PATCHES = [
    ("Names/ItemName.py",
     'crouch = "Crouch"\n',
     'crouch = "Crouch"\n'
     'wall_jump = "Wall Jump"  # CMR\n'),

    ("Items.py",
     "    ItemName.crouch: CelesteItemData(celeste_base_id + 0x8C, ItemClassification.progression),\n",
     "    ItemName.crouch: CelesteItemData(celeste_base_id + 0x8C, ItemClassification.progression),\n"
     "    ItemName.wall_jump: CelesteItemData(celeste_base_id + 0xF000, ItemClassification.progression),  # CMR: 0xCA1F000, far from PoryGone's IDs\n"),

    ("Options.py",
     "class SplitInteractables(Choice):\n",
     "class WallJumpShuffle(Toggle):  # CMR\n"
     '    """\n'
     "    [UltraRando] Whether the ability to wall jump (and wallbounce) is shuffled into the item pool.\n"
     "    Needs the Celeste Movement Helper mod with its 'Wall Jump Shuffle' setting turned on.\n"
     "    V1 logic: every chapter except the Prologue and the Epilogue requires Wall Jump. Forces Roomsanity on.\n"
     '    """\n'
     '    display_name = "Wall Jump Shuffle"\n'
     "\n"
     "class SplitInteractables(Choice):\n"),

    ("Options.py",
     "        CrouchShuffle,\n    ]),\n",
     "        CrouchShuffle,\n        WallJumpShuffle,  # CMR\n    ]),\n"),

    ("Options.py",
     "    crouch_shuffle: CrouchShuffle\n",
     "    crouch_shuffle: CrouchShuffle\n    wall_jump_shuffle: WallJumpShuffle  # CMR\n"),

    ("Options.py",
     "    if world.options.logic_difficulty.value == 0:\n        world.options.climb_shuffle.value = 0\n",
     "    if world.options.logic_difficulty.value == 0:\n        world.options.climb_shuffle.value = 0\n"
     "\n"
     "    # CMR: the Prologue alone must hold Wall Jump, so it needs its room checks\n"
     "    if world.options.wall_jump_shuffle:\n"
     "        world.options.roomsanity.value = 1\n"),

    ("__init__.py",
     "        else:\n            self.multiworld.push_precollected(self.create_item(ItemName.crouch))\n",
     "        else:\n            self.multiworld.push_precollected(self.create_item(ItemName.crouch))\n"
     "\n"
     "        if self.options.wall_jump_shuffle:  # CMR\n"
     "            item_pool.append(self.create_item(ItemName.wall_jump))\n"),

    ("__init__.py",
     "    def set_rules(self) -> None:\n        self.set_completion_rule(Has(ItemName.victory))\n",
     "    def set_rules(self) -> None:\n        self.set_completion_rule(Has(ItemName.victory))\n"
     "\n"
     "        # CMR: V1 logic for Wall Jump Shuffle. Every way into a chapter goes through Menu,\n"
     "        # so gating Menu's exits gates the whole chapter (Prologue and Epilogue excepted).\n"
     "        if self.options.wall_jump_shuffle:\n"
     "            player = self.player\n"
     "            for exit_ in self.get_region(\"Menu\").exits:\n"
     "                target = exit_.connected_region\n"
     "                if target is None or target.name.startswith((\"0a_\", \"8a_\")):\n"
     "                    continue\n"
     "                old_rule = exit_.access_rule\n"
     "                exit_.access_rule = (lambda state, old_rule=old_rule:\n"
     "                                     state.has(ItemName.wall_jump, player) and old_rule(state))\n"),

    ("__init__.py",
     '            "crouch_shuffle": self.options.crouch_shuffle.value,\n',
     '            "crouch_shuffle": self.options.crouch_shuffle.value,\n'
     '            "wall_jump_shuffle": self.options.wall_jump_shuffle.value,  # CMR\n'),
]


def main() -> int:
    if os.path.exists(DST):
        shutil.rmtree(DST)
    shutil.copytree(SRC, DST, ignore=shutil.ignore_patterns("__pycache__"))

    for rel, anchor, replacement in PATCHES:
        path = os.path.join(DST, rel)
        with open(path, encoding="utf-8") as f:
            text = f.read()
        if text.count(anchor) != 1:
            print(f"ÉCHEC : ancrage introuvable ou ambigu dans {rel} :\n{anchor}")
            return 1
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(text.replace(anchor, replacement))

    meta_path = os.path.join(DST, "archipelago.json")
    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)
    # world_version must stay numeric (Archipelago parses it): the fork is tagged in a separate key.
    meta["cmr_fork_version"] = 1
    meta["authors"] = meta.get("authors", []) + ["UltraRando (fork)"]
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4)

    out = os.path.join(DST_DIR, "celeste_open_world.apworld")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for base, _, files in os.walk(DST):
            for name in files:
                full = os.path.join(base, name)
                z.write(full, os.path.relpath(full, DST_DIR))
    print(f"{len(PATCHES)} modifications appliquées -> {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
