"""Test launches: put the Celeste window on a secondary monitor and give the focus back to the window the user had.

start(proc) runs in a thread: it waits (30 s max) for the first visible window of the process, moves it onto the
first non-primary monitor (fitted to it, no activation), then restores the foreground window that was active before
the launch. Without a second monitor it only restores the focus. Windows only; does nothing elsewhere.
"""
import ctypes
import sys
import threading
import time


def _monitors():
    user32 = ctypes.windll.user32
    out = []

    class RECT(ctypes.Structure):
        _fields_ = [("l", ctypes.c_long), ("t", ctypes.c_long), ("r", ctypes.c_long), ("b", ctypes.c_long)]

    class MONITORINFO(ctypes.Structure):
        _fields_ = [("cbSize", ctypes.c_ulong), ("rcMonitor", RECT), ("rcWork", RECT), ("dwFlags", ctypes.c_ulong)]

    proc_t = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(RECT), ctypes.c_void_p)

    def cb(hmon, hdc, rect, data):
        mi = MONITORINFO()
        mi.cbSize = ctypes.sizeof(MONITORINFO)
        user32.GetMonitorInfoW(ctypes.c_void_p(hmon), ctypes.byref(mi))
        w = mi.rcWork
        out.append((bool(mi.dwFlags & 1), w.l, w.t, w.r - w.l, w.b - w.t))
        return 1

    user32.EnumDisplayMonitors(None, None, proc_t(cb), None)
    return out


def _window_of(pid):
    user32 = ctypes.windll.user32
    found = []
    enum_t = ctypes.WINFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p)

    def cb(hwnd, _):
        p = ctypes.c_ulong()
        user32.GetWindowThreadProcessId(ctypes.c_void_p(hwnd), ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(ctypes.c_void_p(hwnd)):
            found.append(hwnd)
        return 1

    user32.EnumWindows(enum_t(cb), None)
    return found[0] if found else None


def start(proc):
    if sys.platform != "win32":
        return
    user32 = ctypes.windll.user32
    prev = user32.GetForegroundWindow()

    def run():
        end = time.time() + 30
        hwnd = None
        while time.time() < end and proc.poll() is None:
            hwnd = _window_of(proc.pid)
            if hwnd:
                break
            time.sleep(0.2)
        if hwnd:
            others = [m for m in _monitors() if not m[0]]
            if others:
                _, x, y, w, h = others[0]
                SWP_NOACTIVATE, SWP_NOZORDER = 0x10, 0x4
                user32.SetWindowPos(ctypes.c_void_p(hwnd), None, x, y, w, h, SWP_NOACTIVATE | SWP_NOZORDER)
        if prev:
            for _ in range(10):  # the game may grab the focus a little after its window appears
                user32.SetForegroundWindow(ctypes.c_void_p(prev))
                time.sleep(0.5)

    threading.Thread(target=run, daemon=True).start()
