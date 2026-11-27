"""Minimal Archipelago client for tests (a second player without a second Celeste).

usage: ap_test_client.py <host:port> <slot> [--password PW] [--game "UltraRando"]
                         [--wait SECONDS] [--check ID ...] [--listen SECONDS]
Connects, prints the result (Connected / ConnectionRefused + errors), waits --wait seconds, sends the given location
checks, then prints every item received during --listen seconds ("RECEIVED <item id> from player <n>").
Exit code 0 if connected, 2 if refused, 3 on error.
"""
import argparse
import asyncio
import json
import sys
import uuid

import websockets


async def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("server")
    ap.add_argument("slot")
    ap.add_argument("--password", default="")
    ap.add_argument("--game", default="UltraRando")
    ap.add_argument("--wait", type=float, default=0)
    ap.add_argument("--check", type=int, nargs="*", default=[])
    ap.add_argument("--listen", type=float, default=2)
    ap.add_argument("--deathlink-at", type=float, default=-1, help="DeathLink tag on; send a death this many s after connecting")
    a = ap.parse_args()
    async with websockets.connect("ws://" + a.server, max_size=None) as ws:
        json.loads(await ws.recv())  # RoomInfo
        await ws.send(json.dumps([{
            "cmd": "Connect", "password": a.password, "name": a.slot, "game": a.game,
            "version": {"major": 0, "minor": 6, "build": 9, "class": "Version"},
            "tags": ["DeathLink"] if a.deathlink_at >= 0 else [], "items_handling": 0b111, "uuid": str(uuid.uuid4()), "slot_data": False,
        }]))
        connected = False
        async def pump(seconds: float) -> None:
            nonlocal connected
            try:
                end = asyncio.get_event_loop().time() + seconds
                while True:
                    left = end - asyncio.get_event_loop().time()
                    if left <= 0:
                        return
                    msg = await asyncio.wait_for(ws.recv(), left)
                    for p in json.loads(msg):
                        cmd = p.get("cmd")
                        if cmd == "Connected":
                            connected = True
                            print(f"CONNECTED as {a.slot} (slot {p.get('slot')}), {len(p.get('checked_locations', []))} locations already checked", flush=True)
                        elif cmd == "ConnectionRefused":
                            print("REFUSED", p.get("errors"), flush=True)
                            raise SystemExit(2)
                        elif cmd == "Bounced" and "DeathLink" in p.get("tags", []):
                            print(f"DEATHLINK RECEIVED from {p.get('data', {}).get('source')}", flush=True)
                        elif cmd == "ReceivedItems":
                            for it in p.get("items", []):
                                print(f"RECEIVED {it['item']} from player {it['player']} (location {it['location']})", flush=True)
            except asyncio.TimeoutError:
                return
        await pump(3)
        if not connected:
            print("NOT CONNECTED", flush=True)
            return 3
        if a.deathlink_at >= 0:
            await pump(a.deathlink_at)
            import time
            await ws.send(json.dumps([{"cmd": "Bounce", "tags": ["DeathLink"],
                                       "data": {"time": time.time(), "source": a.slot, "cause": a.slot + " test death"}}]))
            print("SENT deathlink", flush=True)
        await pump(a.wait)
        if a.check:
            await ws.send(json.dumps([{"cmd": "LocationChecks", "locations": a.check}]))
            print(f"SENT checks {a.check}", flush=True)
        await pump(a.listen)
        return 0


if __name__ == "__main__":
    try:
        sys.exit(asyncio.run(main()))
    except SystemExit as e:
        sys.exit(e.code)
