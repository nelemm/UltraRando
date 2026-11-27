"""Génération en masse : vérifie que la logique tient pour des combinaisons d'options aléatoires.

usage : python tools/mass_gen.py [--runs 6] [--players 8] [--seed 0]

Chaque run crée une multiworld de N joueurs Celeste, chacun avec des options tirées au hasard,
et lance Generate.py --skip_output (la génération vérifie que tout est atteignable).
Les échecs sont écrits dans tools/mass_gen_failures/ (YAML + log) pour pouvoir les rejouer.
"""
import argparse, os, random, shutil, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AP = os.path.join(ROOT, "upstream", "Archipelago")
PY = os.path.join(ROOT, "tools", "venv", "Scripts", "python.exe")
FAIL_DIR = os.path.join(ROOT, "tools", "mass_gen_failures")

CHOICES = {
    "logic_difficulty": ["developer_intended", "vanilla_movement", "assist_mode"],
    "dash_shuffle": ["none", "unified", "cardinal_loose", "cardinal_restrictive", "octal"],
    "climb_shuffle": ["none", "unified", "split"],
    "crouch_shuffle": ["true", "false"],
    "split_interactables": ["none", "per_level", "per_side", "per_level_and_side"],
    "checkpointsanity": ["true", "false"],
    "binosanity": ["true", "false"],
    "keysanity": ["true", "false"],
    "gemsanity": ["true", "false"],
    "carsanity": ["true", "false"],
    "roomsanity": ["true", "false"],
    "include_goldens": ["true", "false"],
    "include_core": ["true", "false"],
    "include_farewell": ["none", "empty_space", "farewell"],
    "include_b_sides": ["true", "false"],
    "include_c_sides": ["true", "false"],
    "goal_area": ["the_summit_a", "the_summit_b", "the_summit_c", "core_a", "core_b", "core_c",
                  "empty_space", "farewell", "farewell_golden"],
    "lock_goal_area": ["true", "false"],
    "trap_fill_percentage": ["0", "20"],
    "wall_jump_shuffle": ["true", "false"],
}


def roll(rng: random.Random) -> dict:
    return {k: rng.choice(v) for k, v in CHOICES.items()}


def write_yaml(path: str, name: str, opts: dict) -> None:
    lines = [f"name: {name}", "game: Celeste (Open World)", "Celeste (Open World):"]
    lines += [f"  {k}: {v}" for k, v in opts.items()]
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--runs", type=int, default=6)
    p.add_argument("--players", type=int, default=8)
    p.add_argument("--seed", type=int, default=0)
    a = p.parse_args()
    rng = random.Random(a.seed)
    ok = fail = 0
    os.makedirs(FAIL_DIR, exist_ok=True)
    for run in range(a.runs):
        d = tempfile.mkdtemp(prefix="celeste_mass_")
        for i in range(a.players):
            write_yaml(os.path.join(d, f"P{i}.yaml"), f"P{i}", roll(rng))
        seed = str(rng.randrange(1 << 30))
        cmd = [PY, "Generate.py", "--player_files_path", d, "--skip_output", "--seed", seed,
               "--outputpath", os.path.join(d, "out")]
        r = subprocess.run(cmd, cwd=AP, capture_output=True, text=True, input="\n",
                           encoding="utf-8", errors="replace")
        log = r.stdout + r.stderr
        if r.returncode == 0 and "Traceback" not in log:
            ok += 1
            print(f"run {run}: OK (seed {seed})")
            shutil.rmtree(d, ignore_errors=True)
        else:
            fail += 1
            dest = os.path.join(FAIL_DIR, f"run{run}_seed{seed}")
            shutil.copytree(d, dest, dirs_exist_ok=True)
            with open(os.path.join(dest, "log.txt"), "w", encoding="utf-8") as f:
                f.write(log)
            last = [l for l in log.splitlines() if l.strip()][-3:]
            print(f"run {run}: ÉCHEC (seed {seed}) -> {dest}\n    " + "\n    ".join(last))
    print(f"\n{ok} runs OK, {fail} en échec ({a.runs * a.players} combinaisons testées)")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
