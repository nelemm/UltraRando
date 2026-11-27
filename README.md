# UltraRando

> **Nouveau (25/11/2026) : notre propre randomizer « UltraRando ».** Il mélange les salles dans chaque chapitre et transforme les mouvements et les objets en items, avec Archipelago.
> - Pour jouer : [JOUER.md](JOUER.md).
> - Pour comprendre comment c'est construit : [ARCHITECTURE.md](ARCHITECTURE.md).
> - Le code : `mod/UltraRando` (mod Everest, fork de Celeste Randomizer de rhelmot, licence MIT) et `apworld/ultrarando` (générateur).
>
> Le reste de ce README décrit la **V1** : l'aide F6 par-dessus Open World, sans mélange des salles.

Un randomizer Celeste fondé sur les mouvements. Il est construit **sur les projets existants** plutôt que de tout réinventer :

- **Celeste (Open World) 1.1.3** de PoryGone, apworld et mod Everest. Il gère déjà le mélange des dashs par direction, du climb et de l'accroupi, ainsi que la logique de mouvement des 802 salles.
- **Celeste Movement Helper** (ce dépôt, `mod/`) : l'aide en jeu (F6). Elle affiche la logique de la salle et un glossaire de 79 techniques avec leurs sources.

À lire :
- [PLAN.md](PLAN.md) : ce qui existe, ce qui a été fait (V1) et les briques de la V2 ;
- [TESTS.md](TESTS.md) : comment tester en jeu ;
- [docs/TECHNIQUES.md](docs/TECHNIQUES.md) : le glossaire des techniques.

## Arborescence

```
mod/CelesteMovementHelper/   mod Everest (C#) : Source/, Dialog/, Data/ (logique et techniques, embarqués dans la DLL)
mod/CelesteMovementHelper.zip  paquet installable (déjà copié dans D:\Games\Celeste\Mods)
v1/yamls/                    YAML des seeds de test
v1/output/                   seeds générées (brique01_dash.zip, brique02_mouvements.zip, brique03_walljump.zip)
v1/apworld/                  fork de l'apworld avec l'item Wall Jump (généré par tools/make_fork.py)
v2/                          le mod final, brique par brique (voir v2/README.md)
docs/                        recherche sur les techniques (JSON + Markdown)
tools/                       scripts : gen.py, mass_gen.py, export_help_data.py, make_dialog.py, build_mod.py,
                             make_fork.py, use_world.py, serveur.ps1
upstream/                    clones des sources d'origine (Archipelago, mod Open World, ExtendedVariantMode, branche celeste-v1.1)
downloads/                   apworld 1.1.3 officiel
```

## Commandes utiles

Génère une seed depuis un dossier de YAML :

```powershell
tools\venv\Scripts\python.exe tools\gen.py v1\yamls\brique01_dash
```

Génération en masse (64 combinaisons d'options) :

```powershell
tools\venv\Scripts\python.exe tools\mass_gen.py --runs 8 --players 8
```

Recompile le mod d'aide et l'installe (ferme Celeste avant) :

```powershell
python tools\build_mod.py --install
```

Après une mise à jour de l'apworld, réexporte la logique pour l'aide, puis recompile :

```powershell
tools\venv\Scripts\python.exe tools\export_help_data.py
```

## Licences

- **Notre code** (`mod/`, `tools/`) : MIT.
- **Logique exportée** (`Data/logic.json`) : licence « Modified MIT » de PoryGone, voir `mod/CelesteMovementHelper/THIRD_PARTY_NOTICES.md`.
- **Mod Everest Open World** : « All rights reserved ». Il n'est **ni copié ni modifié** ici, seulement lu en jeu.
- `upstream/` : des clones pour référence. Ne pas les republier.
