"""Compile le mod UltraRando (fork de rhelmot) et crée le zip Everest.

usage : python tools/build_rando_mod.py [--install] [--export]
Sortie : mod/UltraRando.zip
--install  copie le zip dans D:\\Games\\Celeste\\Mods (ferme Celeste avant).
--export   (avec --install) crée le drapeau CMR_EXPORT_ON_LOAD, lance Celeste, attend
           l'écriture de CMR_export/rooms.json (2 min max), ferme Celeste, retire le
           drapeau et copie le résultat dans data/rooms.json.
           Refuse de faire quoi que ce soit si Celeste tourne déjà.
"""
import os, shutil, subprocess, sys, time, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD = os.path.join(ROOT, "mod", "UltraRando")
ZIP = os.path.join(ROOT, "mod", "UltraRando.zip")
GAME = r"D:\Games\Celeste"
CELESTE_MODS = os.path.join(GAME, "Mods")
FLAG = os.path.join(GAME, "CMR_EXPORT_ON_LOAD")
EXPORT = os.path.join(GAME, "CMR_export", "rooms.json")
DATA_OUT = os.path.join(ROOT, "data", "rooms.json")

FILES = ["everest.yaml", "bin/UltraRando.dll", "bin/UltraRando.pdb",
         "bin/Archipelago.MultiClient.Net.dll",
         "LICENSE", "THIRD_PARTY_NOTICES.md", "EXPORT_FORMAT.md", "ENTITY_ITEMS.md", "TESTING.md"]
DIRS = ["CMRConfig", "Dialog", "Graphics"]


def celeste_running():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Celeste.exe", "/NH"],
                         capture_output=True, text=True).stdout
    return "Celeste.exe" in out


def build():
    r = subprocess.call(["dotnet", "build", "-c", "Release", "-nologo", "-v", "q"], cwd=os.path.join(MOD, "Source"))
    if r != 0:
        sys.exit(r)
    with zipfile.ZipFile(ZIP, "w", zipfile.ZIP_DEFLATED) as z:
        for rel in FILES:
            path = os.path.join(MOD, rel)
            if os.path.exists(path):
                z.write(path, rel)
            else:
                print("absent :", rel)
        for d in DIRS:
            for dirpath, _, names in os.walk(os.path.join(MOD, d)):
                for n in sorted(names):
                    full = os.path.join(dirpath, n)
                    z.write(full, os.path.relpath(full, MOD).replace(os.sep, "/"))
    print("zip :", ZIP)


def export():
    if celeste_running():
        sys.exit("Celeste tourne déjà : export annulé (ferme le jeu et relance).")
    if os.path.exists(EXPORT):
        os.remove(EXPORT)
    open(FLAG, "w").close()
    try:
        proc = subprocess.Popen([os.path.join(GAME, "Celeste.exe")], cwd=GAME)
        deadline = time.time() + 120
        while time.time() < deadline and not os.path.exists(EXPORT):
            if proc.poll() is not None:
                print("Celeste s'est arrêté avant l'export (voir log.txt)")
                break
            time.sleep(2)
        ok = os.path.exists(EXPORT)
        if proc.poll() is None:
            proc.kill()
            proc.wait(30)
    finally:
        if os.path.exists(FLAG):
            os.remove(FLAG)
    if not ok:
        sys.exit("export non écrit en 2 min (voir D:\\Games\\Celeste\\log.txt)")
    os.makedirs(os.path.dirname(DATA_OUT), exist_ok=True)
    shutil.copy2(EXPORT, DATA_OUT)
    print("export :", DATA_OUT)


if __name__ == "__main__":
    if "--install" in sys.argv and celeste_running():
        sys.exit("Celeste tourne : ferme-le avant --install.")
    build()
    if "--install" in sys.argv:
        shutil.copy2(ZIP, os.path.join(CELESTE_MODS, "UltraRando.zip"))
        print("installé dans", CELESTE_MODS)
        if "--export" in sys.argv:
            export()
