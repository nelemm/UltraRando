"""Choisit quelle version de Celeste (Open World) le générateur local utilise.

usage : python tools/use_world.py official|fork
Les deux ont le même nom de jeu, donc une seule peut être chargée à la fois.
"""
import os, shutil, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(ROOT, "upstream", "Archipelago", "worlds", "celeste_open_world")
SOURCES = {
    "official": os.path.join(ROOT, "downloads", "openworld-1.1.3", "extracted", "celeste_open_world"),
    "fork": os.path.join(ROOT, "v1", "apworld", "celeste_open_world"),
}

if len(sys.argv) != 2 or sys.argv[1] not in SOURCES:
    sys.exit(__doc__)
# Empty the folder without removing it (Windows refuses if a process uses it as its current folder).
os.makedirs(TARGET, exist_ok=True)
for entry in os.listdir(TARGET):
    path = os.path.join(TARGET, entry)
    shutil.rmtree(path) if os.path.isdir(path) else os.remove(path)
shutil.copytree(SOURCES[sys.argv[1]], TARGET, ignore=shutil.ignore_patterns("__pycache__"), dirs_exist_ok=True)
print(f"Générateur local : Celeste (Open World) {sys.argv[1]}")
