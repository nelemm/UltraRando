"""One bounded Celeste launch for the "saves" self-test (randomizer saves + journal), with a local AP server.
usage: run_saves_test.py <seed.zip> <out_dir> [key=value ...]
  keys passed to the mod (see SelfTest.Saves.cs): vanillafs=1 newsave=1 journal=1 journalshot=<png> level=1a
  server=localhost:38281 slot=Nelem  manage=1 (import + delete)  offlinelist=1 noserver=1 (offline play)
Rules (HANDOFF section 0): refuse if a Celeste is already running; backup of the Saves folder first
(backup_saves/<timestamp>); hard timeout 115 s; our Celeste and our server are ALWAYS killed and every CMR_* flag
file removed in a finally block. The seed zip is copied to <out_dir>/server so the server's .apsave stays there."""
import datetime, os, shutil, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = r"D:\Games\Celeste"
SAVES = os.path.join(GAME, "Saves")
FLAG = os.path.join(GAME, "CMR_OFFLINE_TEST")
RESULT = FLAG + ".result"
HARD_TIMEOUT = 115
PY = os.path.join(ROOT, "tools", "venv", "Scripts", "python.exe")


def running():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Celeste.exe", "/NH"], capture_output=True, text=True).stdout
    return "Celeste.exe" in out


seed, out_dir = sys.argv[1], os.path.abspath(sys.argv[2])
extra = sys.argv[3:]
if running():
    sys.exit("Celeste already running (not started by us): not launching")
os.makedirs(out_dir, exist_ok=True)
stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
backup = os.path.join(ROOT, "backup_saves", stamp)
shutil.copytree(SAVES, backup)
print("saves backup:", backup)
srv_dir = os.path.join(out_dir, "server")
os.makedirs(srv_dir, exist_ok=True)
seed_copy = os.path.join(srv_dir, os.path.basename(seed))
if not os.path.exists(seed_copy):
    shutil.copy2(seed, seed_copy)
if os.path.exists(RESULT):
    os.remove(RESULT)
server = proc = None
try:
    log = open(os.path.join(srv_dir, "server.log"), "w", encoding="utf-8")
    if "noserver=1" not in extra:  # offline tests: no server at all
        server = subprocess.Popen([PY, "MultiServer.py", "--port", "38281", seed_copy],
                                  cwd=os.path.join(ROOT, "upstream", "Archipelago"), stdout=log, stderr=subprocess.STDOUT)
        time.sleep(6)
    with open(FLAG, "w", encoding="utf-8") as f:
        f.write("\n".join(["", "saves", "shots=" + out_dir] + extra))
    proc = subprocess.Popen([os.path.join(GAME, "Celeste.exe")], cwd=GAME)
    import second_screen; second_screen.start(proc)  # on the 2nd monitor, focus given back to the user
    deadline = time.time() + HARD_TIMEOUT
    while time.time() < deadline and proc.poll() is None:
        time.sleep(1)
finally:
    if proc is not None and proc.poll() is None:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
        try:
            proc.wait(20)
        except Exception:
            pass
    if server is not None and server.poll() is None:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(server.pid)], capture_output=True)
    for f in [FLAG, os.path.join(GAME, "CMR_EXPORT_ON_LOAD")]:
        if os.path.exists(f):
            os.remove(f)
print("exit code:", proc.returncode if proc else None, "| celeste still running:", running())
if os.path.exists(RESULT):
    shutil.copy2(RESULT, os.path.join(out_dir, "result.txt"))
    print(open(RESULT, encoding="utf-8").read())
    os.remove(RESULT)
else:
    print("no result")
