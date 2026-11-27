"""Génère une seed à partir d'un dossier de YAML.

usage : python tools/gen.py <dossier_yaml> [--seed N] [--spoiler 2]
La seed (.zip) est écrite dans v1/output/.
"""
import argparse, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AP = os.path.join(ROOT, "upstream", "Archipelago")
PY = os.path.join(ROOT, "tools", "venv", "Scripts", "python.exe")

p = argparse.ArgumentParser()
p.add_argument("yaml_dir")
p.add_argument("--seed", default=None)
p.add_argument("--spoiler", default="2")
a = p.parse_args()

cmd = [PY, "Generate.py",
       "--player_files_path", os.path.abspath(a.yaml_dir),
       "--outputpath", os.path.join(ROOT, "v1", "output"),
       "--plando", "bosses, items, connections, texts",
       "--spoiler", a.spoiler]
if a.seed:
    cmd += ["--seed", a.seed]
sys.exit(subprocess.call(cmd, cwd=AP, stdin=subprocess.DEVNULL))  # never wait on "Press enter to close"
