"""Statistiques du mélange des salles, par chapitre (Python pur, sans Archipelago).

usage : python tools/layout_stats.py [--seeds 10] [--diff 1] [--room_diff 2] [--areas 1a,6a]
Pour chaque chapitre : nombre moyen de salles placées (%), part des liaisons identiques au jeu de base,
nombre de repli sur la carte d'origine (fallback) et temps moyen de génération.
"""
import argparse
import os
import random
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "apworld", "ultrarando"))

from core.layout import make_layout  # noqa: E402
from core.static_data import get_data  # noqa: E402


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--seeds", type=int, default=10)
    p.add_argument("--diff", type=int, default=1)
    p.add_argument("--room_diff", type=int, default=2)
    p.add_argument("--areas", default="")
    p.add_argument("--coverage", type=float, default=0.9)
    a = p.parse_args()
    data = get_data()
    keys = a.areas.split(",") if a.areas else list(data.areas)
    tot_p = tot_n = tot_v = tot_l = tot_fb = 0
    print(f"{'area':5} {'rooms':>5} {'placed%':>8} {'min%':>5} {'vanilla links%':>15} {'fallback':>8} {'s/layout':>8}")
    for ak in keys:
        area = data.areas[ak]
        n = len(area.levels)
        vp = {(x, i, y, j) for (x, i, y, j) in area.vanilla_links} | {(y, j, x, i) for (x, i, y, j) in area.vanilla_links}
        placed, van, links, fb, t = [], 0, 0, 0, 0.0
        for s in range(a.seeds):
            t0 = time.time()
            lay = make_layout(data, area, True, a.diff, a.room_diff, random.Random(1000 + s), coverage=a.coverage)
            t += time.time() - t0
            if lay.fallback:
                fb += 1
                continue
            placed.append(len(lay.placed) / n)
            links += len(lay.links)
            van += sum(1 for l in lay.links if tuple(l) in vp)
        avg = 100 * sum(placed) / len(placed) if placed else 0
        mn = 100 * min(placed) if placed else 0
        vl = 100 * van / links if links else 0
        print(f"{ak:5} {n:5} {avg:8.0f} {mn:5.0f} {vl:15.0f} {fb:8} {t / a.seeds:8.2f}")
        tot_p += sum(placed) * n
        tot_n += n * len(placed)
        tot_v += van
        tot_l += links
        tot_fb += fb
    print(f"TOTAL placed {100 * tot_p / max(1, tot_n):.0f}%, vanilla links {100 * tot_v / max(1, tot_l):.0f}%, fallbacks {tot_fb}")


if __name__ == "__main__":
    main()
