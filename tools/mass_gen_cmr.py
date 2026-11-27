"""Mass generation for UltraRando: random option combinations, several players per run.

usage : python tools/mass_gen_cmr.py [--runs 16] [--players 4] [--seed 0] [--output]

Each run writes N YAMLs with random options and calls Generate.py (with --skip_output unless --output).
Failures are copied to tools/mass_gen_failures/cmr_*/ (YAMLs + log) to be replayed.
Run tools/build_apworld.py first (it installs the world into upstream/Archipelago/worlds).
"""
import argparse
import glob
import json
import os
import random
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AP = os.path.join(ROOT, "upstream", "Archipelago")
PY = os.path.join(ROOT, "tools", "venv", "Scripts", "python.exe")
FAIL_DIR = os.path.join(ROOT, "tools", "mass_gen_failures")
GAME = "UltraRando"

sys.path.insert(0, os.path.join(ROOT, "tools"))
import test_cmr_layout as tcl  # noqa: E402  (layout checks, pure Python)
tcl_data = tcl.get_data()

CHOICES = {
    "logic_difficulty": ["developer_intended", "vanilla_movement", "assist_mode"],
    "room_shuffle": ["true", "true", "false"],
    "room_difficulty": ["normal", "hard", "expert"],
    "dash_shuffle": ["none", "unified", "cardinal_loose", "cardinal_restrictive", "octal"],
    "climb_shuffle": ["none", "unified", "split"],
    "crouch_shuffle": ["true", "false"],
    "wall_jump_shuffle": ["true", "false"],
    "demo_dash_shuffle": ["true", "false"],
    "interactable_shuffle": ["true", "false"],
    "include_b_sides": ["true", "false"],
    "include_c_sides": ["true", "false"],
    "include_core": ["true", "false"],
    "include_farewell": ["true", "false"],
    "goal_area": ["the_summit_a", "the_summit_b", "the_summit_c", "core_a", "core_b", "core_c", "farewell"],
    "total_strawberries": ["0", "30", "50", "120", "202"],
    "strawberries_required_percentage": ["0", "50", "80", "100"],
    "roomsanity": ["true", "false"],
    "checkpointsanity": ["true", "false"],
    "death_link": ["true", "false"],
}


def roll(rng: random.Random) -> dict:
    return {k: rng.choice(v) for k, v in CHOICES.items()}


def write_yaml(path: str, name: str, opts: dict) -> None:
    lines = [f"name: {name}", f"game: {GAME}", f"{GAME}:"]
    lines += [f"  {k}: {v}" for k, v in opts.items()]
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--runs", type=int, default=16)
    p.add_argument("--players", type=int, default=4)
    p.add_argument("--seed", type=int, default=0)
    p.add_argument("--output", action="store_true", help="full generation (no --skip_output), checks slot_data")
    a = p.parse_args()
    rng = random.Random(a.seed)
    ok = fail = 0
    os.makedirs(FAIL_DIR, exist_ok=True)
    for run in range(a.runs):
        d = tempfile.mkdtemp(prefix="cmr_mass_")
        for i in range(a.players):
            write_yaml(os.path.join(d, f"P{i}.yaml"), f"P{i}", roll(rng))
        seed = str(rng.randrange(1 << 30))
        out = os.path.join(d, "out")
        cmd = [PY, "Generate.py", "--player_files_path", d, "--seed", seed, "--outputpath", out]
        if not a.output:
            cmd.append("--skip_output")
        r = subprocess.run(cmd, cwd=AP, capture_output=True, text=True, input="\n",
                           encoding="utf-8", errors="replace")
        log = r.stdout + r.stderr
        good = r.returncode == 0 and "Traceback" not in log
        note = ""
        if good and a.output:
            files = glob.glob(os.path.join(out, "*_cmr_slot_data.json"))
            good = len(files) == a.players
            if good:
                for f in files:
                    sd = json.load(open(f, encoding="utf-8"))
                    good &= bool(sd["location_ids"]) and set(sd["chapters"]) == set(sd["active_areas"])
                    # same layout checks as tools/test_cmr_layout.py (geometry, logical directions vs hole kinds,
                    # obstructed entries closed by "no_enter")
                    del tcl.errors[:]
                    for ak, ch in sd["chapters"].items():
                        tcl.check_geometry(tcl_data.areas[ak], ch, f"{os.path.basename(f)} {ak}")
                        tcl.check_dirs(tcl_data.areas[ak], ch, f"{os.path.basename(f)} {ak}")
                    if tcl.errors:
                        good = False
                        log += "\nlayout check errors:\n" + "\n".join(tcl.errors[:20])
                note = f", {len(files)} slot_data files"
            else:
                note = f", {len(files)} slot_data files (expected {a.players})"
        if good:
            ok += 1
            print(f"run {run}: OK (seed {seed}{note})")
            shutil.rmtree(d, ignore_errors=True)
        else:
            fail += 1
            dest = os.path.join(FAIL_DIR, f"cmr_run{run}_seed{seed}")
            shutil.copytree(d, dest, dirs_exist_ok=True)
            with open(os.path.join(dest, "log.txt"), "w", encoding="utf-8") as f:
                f.write(log)
            last = [l for l in log.splitlines() if l.strip()][-4:]
            print(f"run {run}: FAILED (seed {seed}{note}) -> {dest}\n    " + "\n    ".join(last))
        sys.stdout.flush()
    print(f"\n{ok} runs OK, {fail} failed ({a.runs * a.players} option combinations)")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
