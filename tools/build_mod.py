"""Compile le mod d'aide et crée le zip Everest (et l'installe si --install).

usage : python tools/build_mod.py [--install]
Sortie : mod/CelesteMovementHelper.zip
--install copie le zip dans D:\\Games\\Celeste\\Mods (ferme Celeste avant).
"""
import os, shutil, subprocess, sys, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD = os.path.join(ROOT, "mod", "CelesteMovementHelper")
ZIP = os.path.join(ROOT, "mod", "CelesteMovementHelper.zip")
CELESTE_MODS = r"D:\Games\Celeste\Mods"

INCLUDE = ["everest.yaml", "bin/CelesteMovementHelper.dll", "bin/CelesteMovementHelper.pdb",
           "Dialog/English.txt", "Dialog/French.txt", "README.md", "LICENSE", "THIRD_PARTY_NOTICES.md"]

r = subprocess.call(["dotnet", "build", "-c", "Release", "-nologo", "-v", "q"], cwd=os.path.join(MOD, "Source"))
if r != 0:
    sys.exit(r)

with zipfile.ZipFile(ZIP, "w", zipfile.ZIP_DEFLATED) as z:
    for rel in INCLUDE:
        path = os.path.join(MOD, rel)
        if os.path.exists(path):
            z.write(path, rel)
        else:
            print("absent :", rel)
print("zip :", ZIP)

if "--install" in sys.argv:
    shutil.copy2(ZIP, os.path.join(CELESTE_MODS, "CelesteMovementHelper.zip"))
    print("installé dans", CELESTE_MODS)
