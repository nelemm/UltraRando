"""Shared schema of the YAML options, read by the web YAML generator and by the in-game menu.

usage: python tools/export_options_schema.py
Reads the options straight from the apworld (Options.py: classes, bounds, defaults, groups, presets) through
Archipelago's own world registry, adds the French texts of apworld/.../data/options_fr.json, and writes:
  web/options_schema.json                                 (web generator data)
  web/yaml-generator.html                                 (web page with the schema inlined: works offline)
  mod/UltraRando/CMRConfig/options_schema.json  (in-game menu)
One source (Options.py), so the generators cannot drift apart: run it after any option change.
"""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AP = os.path.join(ROOT, "upstream", "Archipelago")
sys.path.insert(0, AP)
os.chdir(AP)

import ModuleUpdate  # noqa: E402,F401  (Archipelago environment)
from Options import Choice, Range, Toggle  # noqa: E402
from worlds import AutoWorldRegister  # noqa: E402

GAME = "UltraRando"
KEEP_COMMON = {"death_link"}


def clean(doc):
    return " ".join(l.strip() for l in (doc or "").strip().splitlines() if l.strip())


def main():
    world = AutoWorldRegister.world_types[GAME]
    fr = json.load(open(os.path.join(ROOT, "apworld", "ultrarando", "data", "options_fr.json"), encoding="utf-8"))
    hints = world.options_dataclass.type_hints
    own = [k for k in hints if k in KEEP_COMMON or hints[k].__module__.endswith("ultrarando.Options")]
    grouped = {}
    for g in world.web.option_groups:
        for cls in g.options:
            for k, c in hints.items():
                if c is cls:
                    grouped[k] = g.name
    options = []
    for key in own:
        cls = hints[key]
        o = {"key": key, "group": grouped.get(key, "Other"), "name": getattr(cls, "display_name", key),
             "doc": clean(cls.__doc__), "fr": fr["options"].get(key, [None, None])}
        if issubclass(cls, Toggle):
            o.update(type="toggle", default=bool(cls.default))
        elif issubclass(cls, Choice):
            names = {v: k for k, v in cls.options.items()}
            o.update(type="choice", default=names[cls.default],
                     choices=[{"value": k, "name": cls.get_option_name(v), "fr": fr["choices"].get(k)}
                              for k, v in sorted(cls.options.items(), key=lambda kv: kv[1]) if k not in cls.aliases])
        elif issubclass(cls, Range):
            o.update(type="range", default=cls.default, min=cls.range_start, max=cls.range_end)
        else:
            continue
        options.append(o)
    groups = []
    for g in [g.name for g in world.web.option_groups] + ["Other"]:
        if any(o["group"] == g for o in options) and g not in [x["name"] for x in groups]:
            groups.append({"name": g, "fr": fr["groups"].get(g, g)})
    presets = [{"name": n, "fr": fr["presets"].get(n, n), "values": v} for n, v in world.web.options_presets.items()]
    # every preset value must be a valid value of its option (the schema is the contract of both generators)
    byk = {o["key"]: o for o in options}
    for p in presets:
        for k, v in p["values"].items():
            o = byk[k]
            ok = (o["type"] == "toggle" and isinstance(v, bool)) or \
                 (o["type"] == "choice" and v in [c["value"] for c in o["choices"]]) or \
                 (o["type"] == "range" and o["min"] <= v <= o["max"])
            assert ok, f"preset {p['name']}: bad value {k}={v}"
    schema = {"game": GAME, "groups": groups, "options": options, "presets": presets}
    text = json.dumps(schema, ensure_ascii=False, indent=1)
    for out in (os.path.join(ROOT, "web", "options_schema.json"),
                os.path.join(ROOT, "mod", "UltraRando", "CMRConfig", "options_schema.json")):
        os.makedirs(os.path.dirname(out), exist_ok=True)
        open(out, "w", encoding="utf-8").write(text)
    tpl = os.path.join(ROOT, "web", "yaml-generator.template.html")
    if os.path.exists(tpl):
        html = open(tpl, encoding="utf-8").read().replace("/*SCHEMA*/null", json.dumps(schema, ensure_ascii=False))
        open(os.path.join(ROOT, "web", "yaml-generator.html"), "w", encoding="utf-8").write(html)
    print(f"{len(options)} options, {len(groups)} groups, {len(presets)} presets")


if __name__ == "__main__":
    main()
