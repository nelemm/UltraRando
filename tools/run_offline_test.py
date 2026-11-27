r"""One bounded Celeste launch for the CMR self-test.
usage: run_offline_test.py <slot_data.json> [play|playall|walk] [areas,comma,separated] [key=value ...]
       (walk: e.g. `... walk areas=3a shots=D:\out\walk_3a from=0 count=60`, see mod TESTING.md)
Rules: refuse if a Celeste is already running; hard timeout 115 s; the Celeste we started is ALWAYS killed
and every CMR_* flag file removed in a finally block."""
import glob, os, subprocess, sys, time
GAME = r"D:\Games\Celeste"
FLAG = os.path.join(GAME, "CMR_OFFLINE_TEST")
RESULT = FLAG + ".result"
HARD_TIMEOUT = 115


def running():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Celeste.exe", "/NH"], capture_output=True, text=True).stdout
    return "Celeste.exe" in out


slot = sys.argv[1]
mode = sys.argv[2] if len(sys.argv) > 2 else ""
extra = sys.argv[3:]
if running():
    sys.exit("Celeste already running (not started by us): not launching")
if os.path.exists(RESULT):
    os.remove(RESULT)
proc = None
try:
    with open(FLAG, "w", encoding="utf-8") as f:
        f.write("\n".join([slot, mode] + extra))
    proc = subprocess.Popen([os.path.join(GAME, "Celeste.exe")], cwd=GAME)
    import second_screen; second_screen.start(proc)  # on the 2nd monitor, focus given back to the user
    deadline = time.time() + HARD_TIMEOUT
    while time.time() < deadline:
        if proc.poll() is not None:
            break
        if os.path.exists(RESULT) and not mode:
            break
        time.sleep(1)
finally:
    result_text = None
    if proc is not None and proc.poll() is None:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
        try:
            proc.wait(20)
        except Exception:
            pass
    if os.path.exists(RESULT):
        result_text = open(RESULT, encoding="utf-8").read()
    for f in glob.glob(os.path.join(GAME, "CMR_*")):
        if os.path.isdir(f):
            continue  # CMR_export folder stays
        if os.path.exists(f):
            os.remove(f)
print("exit code:", proc.returncode if proc else None, "| celeste still running:", running())
print(result_text if result_text is not None else "no result")
