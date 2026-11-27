"""Walk self-test of whole areas, in as many bounded Celeste launches as needed.

usage : python tools/run_walk_all.py <slot_data.json> <out_dir> [areas,comma,separated]
Each launch goes through tools/run_offline_test.py (refuses if Celeste already runs, 115 s cap, kills its own
Celeste, removes the CMR_* flag files) with `walk areas=<area> from=<next step>`; the step lines are read back
from D:\\Games\\Celeste\\log.txt after each launch, so a launch cut by the time cap is simply continued.
Writes <out_dir>/walk_<area>.txt (every step line) and prints a summary per area. Screenshots of failed steps:
<out_dir>/<area>/FAIL_*.png (self-verification only).
"""
import json
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PY = os.path.join(ROOT, "tools", "venv", "Scripts", "python.exe")
LOG = r"D:\Games\Celeste\log.txt"
STEP = re.compile(r"self-test walk (\d{3}) (\S+) (.*)$")
TOTAL = re.compile(r"walk (\S+): (\d+) directed steps \((\d+) links, (\d+) one-way")


def main() -> int:
    slot, out = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    sd = json.load(open(slot, encoding="utf-8"))
    areas = sys.argv[3].split(",") if len(sys.argv) > 3 else sd["active_areas"]
    os.makedirs(out, exist_ok=True)
    summary = []
    for area in areas:
        steps: dict[int, str] = {}
        total = links = skipped = None
        nxt, launches, stalls = 0, 0, 0
        notes = []
        while True:
            launches += 1
            r = subprocess.run([PY, os.path.join(ROOT, "tools", "run_offline_test.py"), slot, "walk", f"areas={area}",
                                f"shots={os.path.join(out, area)}", "maxshots=0", f"from={nxt}", "count=1000"],
                               capture_output=True, text=True)
            if "already running" in (r.stdout + r.stderr):
                print("Celeste already running (not ours): stop")
                return 2
            text = open(LOG, encoding="utf-8", errors="replace").read()
            shutil.copy2(LOG, os.path.join(out, f"log_{area}_{launches}.txt"))
            for m in TOTAL.finditer(text):
                if m.group(1) == area:
                    total, links, skipped = int(m.group(2)), int(m.group(3)), int(m.group(4))
            new = 0
            for line in text.splitlines():
                m = STEP.search(line)
                if m and m.group(2) == area:
                    i = int(m.group(1))
                    if i not in steps:
                        new += 1
                    steps[i] = m.group(0)
            for k in ("CRITICAL ERROR", "HANG", "critical error screen"):
                if k in text:
                    notes.append(f"launch {launches}: {k}")
            if total is None:  # should not happen (the mod logs the step count at setup)
                notes.append(f"launch {launches}: step count unknown")
                break
            if len(steps) >= total or launches >= 12:
                break
            missing = [i for i in range(total) if i not in steps]
            if new == 0:
                stalls += 1
                # a step that freezes the game every time: record it and go on with the next one
                steps[missing[0]] = f"self-test walk {missing[0]:03d} {area} NOT RUN (launch stalled on this step)"
                if stalls >= 3:
                    break
            nxt = min(i for i in range(total) if i not in steps) if len(steps) < total else total
        bad = [s for s in steps.values() if " OK " not in s]
        with open(os.path.join(out, f"walk_{area}.txt"), "w", encoding="utf-8") as f:
            f.write(f"{area}: {len(steps)}/{total} directed steps run, {len(bad)} not OK, {links} links, "
                    f"{skipped} one-way directions not walked, {launches} launches {notes}\n")
            for i in sorted(steps):
                f.write(steps[i] + "\n")
        line = (f"{area}: {len(steps) - len(bad)}/{total} OK ({links} links, {skipped} one-way directions skipped, "
                f"{launches} launches){' ' + str(notes) if notes else ''}")
        summary.append(line)
        print(line, flush=True)
        for s in bad:
            print("   " + s[:400], flush=True)
    with open(os.path.join(out, "summary.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(summary) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
