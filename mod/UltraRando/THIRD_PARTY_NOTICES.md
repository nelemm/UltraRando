# Third-party notices

## Celeste Randomizer (rhelmot)

This mod is a fork of **Celeste Randomizer** by rhelmot
(https://github.com/rhelmot/CelesteRandomizer), used under the MIT License.
The C# sources in `Source/` (except `Source/Export/` and the lines marked `CMR`),
the room configuration files in `CMRConfig/Celeste/` (originally `Config/Celeste/`),
the dialog files in `Dialog/` and the graphics in `Graphics/` come from that project,
with renames so that this mod can be installed next to the original "Randomizer" mod:

- namespace `Celeste.Mod.Randomizer` -> `Celeste.Mod.UltraRando`, assembly `UltraRando`;
- room configs moved from `Config/` to `CMRConfig/`, metadata file `rando.yaml` -> `cmr_rando.yaml`;
- dialog keys `MODOPTIONS_RANDOMIZER_*` -> `MODOPTIONS_CMRANDO_*`;
- custom entities `randomizer/*` -> `cmrando/*`, generated map SIDs `randomizer/*` -> `cmrando/*`;
- DynData keys prefixed with `CMR_`, ModInterop names `Randomizer.*` -> `UltraRando.*`,
  console commands `rando_skeleton`/`madlibs_stats` -> `cmr_rando_skeleton`/`cmr_madlibs_stats`,
  overworld start mode 55 -> 3133;
- port from .NET Framework 4.8 to .NET 8 (Everest core): `IDetour` -> `IDisposable`, `Thread.Abort` removed.

Original license (also in `LICENSE`):

    The MIT License (MIT)

    Copyright (c) 2020 rhelmot

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

## Archipelago.MultiClient.Net

`bin/Archipelago.MultiClient.Net.dll` (NuGet package 6.7.1, https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net)
is shipped unmodified, under the MIT License (Copyright (c) Archipelago.MultiClient.Net contributors).
License text (MIT, as declared by the package):

    Copyright (c) Archipelago.MultiClient.Net authors

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

Its Newtonsoft.Json dependency is not shipped: Everest provides Newtonsoft.Json 13.0.x, which the library uses at runtime.

## ExtendedVariantMode

Not included. It was only read as a reference for which `Player` methods to hook (MIT); the lock hooks in
`Source/AP/ApHooks.cs` are our own code.

## Archipelago icon

`Graphics/Atlases/Gui/cmr/archipelago.png` is `data/icon.png` of Archipelago (MIT License, Copyright (c) 2017 LLCoolDave
and contributors). It is shown in item notifications for items of other games.
