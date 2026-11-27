# Architecture — « UltraRando » (notre randomizer)

Fusion de trois projets :

| Brique | Vient de | Licence |
|---|---|---|
| Géométrie des salles (trous/sorties, bornes, collectables, exigences en dashs et difficulté) et code qui fabrique une carte à partir de salles placées | **Celeste Randomizer** de rhelmot (`upstream/CelesteRandomizer`) | MIT : on peut copier et modifier, en gardant la notice |
| Logique fine par région (directions de dash, climb, accroupi, ~35 objets, clés) et noms des checks | données de l'apworld **Celeste (Open World)** 1.1.3 (`downloads/openworld-1.1.3/extracted/celeste_open_world/data/CelesteLevelData.json`) | « Modified MIT » : copie et modification autorisées, sans changer de licence ni vendre |
| Client Archipelago | `Archipelago.MultiClient.Net` (NuGet) | MIT |
| Aide F6 et techniques | notre V1 (`mod/CelesteMovementHelper`) | MIT |

**Ce qu'on ne fait pas :** on ne copie rien du mod Everest d'Open World (« All rights reserved ») ni de Celeste Modded (sans licence).

Nom du jeu côté Archipelago : **`UltraRando`**. C'est un jeu à part, qui ne remplace ni Open World ni Celeste Modded.

## Vue d'ensemble

```
        (1) une seule fois                     (2) à chaque seed                      (3) en jeu
Celeste + mod ──export──> rooms.json ──> apworld Python ──slot_data──> serveur AP ──> mod Celeste
 (commande cmr_export)     (géométrie)     - place les salles             (layout +       - reconstruit chaque
                                            de chaque chapitre             options)          chapitre dans l'ordre reçu
                                           - logique = Open World                          - bloque mouvements/objets
                                             + exigences rhelmot                           - envoie les checks
                                           - vérifie la solvabilité                         - reçoit les items
```

**Principe clé :** c'est le **générateur Python** qui choisit l'ordre et la position des salles. Il est le seul à connaître la logique et les items, donc le seul capable de garantir que la seed est faisable. Le mod se contente d'**appliquer** le placement reçu, sans tirage aléatoire de son côté.

## (1) Export de la géométrie : `data/rooms.json`

Le mod fournit la commande console `cmr_export`. Elle peut aussi s'exécuter au démarrage si le fichier `Mods/UltraRando/EXPORT_ON_LOAD` existe. Elle écrit `rooms.json` à partir des `StaticRoom` chargées par le code de rhelmot (`ProcessAreas`), pour les chapitres du jeu de base uniquement.

```jsonc
{
  "version": 1,
  "areas": {
    "1a": {                                  // "<ID de l'AreaKey><a|b|c>" (même clé que l'aide V1)
      "sid": "Celeste/1-ForsakenCity", "mode": 0,
      "rooms": {
        "3": {
          "bounds": {"x": 0, "y": -184, "w": 320, "h": 184},   // pixels, position vanilla
          "holes": [   // dans l'ordre de StaticRoom.Holes ; "index" = position dans cette liste
            {"index": 0, "side": "Up", "low": 2, "high": 6, "high_open": false, "kind": "inout",
             "launch": null},
            ...
          ],
          "nodes": {
            "main": {
              "edges": [ {"to_node": "x", "req_out": <req>, "req_in": <req>} ],          // arêtes internes
              "hole_edges": [ {"hole": 0, "req_out": <req>, "req_in": <req>} ],          // nœud <-> trou
              "collectables": [ {"index": 0, "kind": "strawberry|key|gem|cassette|heart|...",
                                 "x": 120, "y": 64, "req": <req>} ]
            }
          },
          "entities": ["spring", "zipMover", "refill", ...],  // noms d'entités présents (pour les objets-items)
          "is_start": true, "is_end": false, "checkpoint": false,
          "warps": []                       // salles reliées par cinématique/téléport (on les garde vanilla)
        }
      }
    }
  }
}
```

`<req>` sérialise `Requirement` de rhelmot :
- `{"dashes": "zero|one|two|infinite", "difficulty": "easy|normal|hard|expert|master|perfect"}` ;
- `{"or": [...]}` ou `{"and": [...]}` ;
- `{"key": n}` ;
- `{"flag": "name", "state": "set|unset"}` ;
- `null` = libre, `"impossible"` = impossible.

## (2) apworld `worlds/ultrarando`

**Entrées :**
- `data/rooms.json` (export du mod) ;
- `data/openworld_logic.json` : copie de `CelesteLevelData.json` d'Open World, avec la notice de licence.

**Correspondance porte ↔ trou :** Open World nomme les portes de chaque salle (`west`, `east`…) avec une direction. rhelmot numérote les trous par côté. Quand une salle n'a qu'**une** porte et qu'**un** trou ouvert d'un côté donné, la correspondance est automatique. Sinon, si les directions ne suffisent pas, on compare les positions. En dernier recours, la salle **garde ses voisins vanilla** pour ces sorties : on ne la mélange pas. Un rapport liste ces cas (`tools/door_mapping_report.txt`).

**Placement, par chapitre** (portage Python de l'algorithme « Pathway » de rhelmot, `LogicPathway.cs` et `LinkedRoom.cs`) :
- la salle de départ et la salle de fin du chapitre restent celles du jeu ;
- les autres salles sont placées une à une, en reliant un trou libre d'une salle placée à un trou **compatible** d'une salle restante. Compatible veut dire : côtés opposés, tailles compatibles comme dans `Hole.Compatible`, et position calculée comme rhelmot. Pas de chevauchement de rectangles (`LinkedMap.AreaFree`) ;
- les trous non reliés sont fermés, comme le fait rhelmot ;
- tirage avec `world.random` et backtracking. Si le placement échoue, on réessaie ; après N échecs, `OptionError` avec un message clair.

**Logique :**
- une région AP par région Open World de chaque salle placée ;
- connexions internes = règles Open World selon `logic_difficulty` (`rule`, `vm_rule`, `assist_rule`, même sémantique que l'apworld Open World) ;
- connexions entre salles = liens de trous du placement ;
- exigence de dashs rhelmot **en plus**, convertie en items : `one` = au moins un item de dash utilisable, `two` = l'item « Double Dash » quand on en fera un, ignoré en V1 ;
- difficulté rhelmot comparée à l'option `room_difficulty`.

**Items** (IDs à partir de `0xC3D0000`, **stables**, ne jamais renuméroter) :
- mouvements : `Dash` ou les 8 directions (option `dash_shuffle`, comme Open World), `Climb` ou `Left Climb`/`Right Climb`, `Crouch`, `Wall Jump` ;
- ~35 objets (mêmes noms qu'Open World : `Springs`, `Traffic Blocks`…) ;
- clés par chapitre, `Strawberry` (objectif), remplissage.

**Checks** : fraises, cœurs, cassettes, fin de chapitre, entrée de salle, clés, checkpoints, voitures, jumelles, gemmes. Mêmes noms qu'Open World quand ils existent, préfixés par le chapitre.

**Objectif** : N fraises plus la fin du chapitre choisi (même idée qu'Open World).

**slot_data :**

```jsonc
{
  "version": 1,
  "options": { "dash_shuffle": 1, "climb_shuffle": 0, "crouch_shuffle": 0, "wall_jump_shuffle": 0,
               "logic_difficulty": 1, "room_difficulty": 2, "goal_area": "7a", "strawberries_required": 20,
               "death_link": 0 },
  "chapters": {
    "1a": {
      "rooms": [ {"room": "1", "x": 0, "y": 0}, {"room": "6a", "x": 320, "y": -40}, ... ],   // positions en pixels
      "links": [ {"a": "1", "a_hole": 0, "b": "6a", "b_hole": 2}, ... ],
      "closed_holes": [ {"room": "6a", "hole": 1}, ... ]
    }
  },
  "location_ids": { "<clé interne mod>": 123456, ... },  // ex. "1a|6a|strawberry|0" -> id
  "item_ids": { ... }                                   // facultatif, pour le débogage
}
```

## (3) Mod Everest `mod/UltraRando`

C'est un fork de rhelmot, porté en SDK-style `net8.0` comme `mod/CelesteMovementHelper`, avec des références vers `D:\Games\Celeste`.

- **Connexion** : réglages du mod (serveur, slot, mot de passe), puis une commande et une entrée de menu « Connect ». On utilise `Archipelago.MultiClient.Net`, embarqué dans le zip.
- **Construction** : à l'entrée d'un chapitre du jeu de base pendant une session AP, on charge la carte reconstruite à partir de `slot_data.chapters[clé]`, avec le code de rhelmot : `StaticRoom.MakeLevelData(position)`, `LinkedMap.FillMap`, sans génération aléatoire. Les trous de `closed_holes` sont bouchés.
- **Verrous** : directions de dash, climb, accroupi et wall jump bloqués par des hooks, selon les items reçus. Les entités dont l'objet n'est pas reçu sont retirées au chargement de la salle. Un nouvel item prend effet au prochain chargement de salle ou au prochain respawn.
- **Checks** : collecte de fraise, cœur, cassette, clé ; fin de chapitre ; entrée dans une salle ; checkpoint atteint. On envoie l'ID `location_ids[clé]`.
- **Sécurité** : noms de joueurs et d'items assainis avant affichage, longueur bornée. ID inconnu : on l'ignore et on le journalise. On ne télécharge jamais rien et on n'exécute rien qui vienne du serveur.
- **Sauvegarde** : un fichier de sauvegarde Celeste par seed et par slot. L'index des items reçus est sauvegardé pour rejouer les items reçus hors ligne.
- **Aide F6** : reprise de `CelesteMovementHelper`, adaptée pour lire l'état de notre propre client au lieu d'Open World.

## Contrat figé (phase B) — à respecter à la lettre par l'apworld ET le mod

- **Export réel** : `data/rooms.json`, schéma exact dans `mod/UltraRando/EXPORT_FORMAT.md` (prime sur la section (1) ci-dessus).
- **Clés de checks** (`slot_data.location_ids` : clé -> id AP) :
  - collectable ou pickup : `"{area}|{level}|{kind}|{entity_id}"`, avec kind = `strawberry|key|gem|heart|cassette|golden` (ex. `"1a|2|strawberry|11"`) ;
  - entrée de salle : `"{area}|{level}|room"` ;
  - checkpoint atteint : `"{area}|{level}|checkpoint"` ;
  - fin du chapitre : `"{area}|clear"`.
  `area` = clé de rooms.json (`"1a"`), `level` = nom Celeste de la salle (champ `level`, pas la variante `#2`).
- **Items** : le mod les traite **par nom** (noms Archipelago). Les noms sont ceux d'Open World :
  `Dash`, `Up Dash`, `Up-Right Dash`, `Right Dash`, `Down-Right Dash`, `Down Dash`, `Down-Left Dash`, `Left Dash`, `Up-Left Dash`,
  `Climb`, `Left Climb`, `Right Climb`, `Crouch`, `Wall Jump`,
  les objets = valeurs de `ItemName` d'Open World (`Springs`, `Traffic Blocks`, `Dash Refills`, `Double Dash Refills`, `Dream Blocks`, `Feathers`, `Blue Boosters`, `Red Boosters`, …),
  `Strawberry`, et le remplissage (`Raspberry`). Un item au nom inconnu est ignoré et journalisé.
  Les clés du jeu restent vanilla en V1 (une clé ramassée ouvre sa porte ET envoie un check).
- **slot_data** : comme la section (2), avec en plus `"chapters"[area]["rooms"][i]["variant"]` (clé exacte dans rooms.json, ex. `"6a"` ou `"6a#2"`)
  et `"active_areas": ["0a","1a",...]`. Champs facultatifs d'un lien (26/11) : `"dir"` (`ab`, `ba`, `both`, `none`) =
  sens que la logique utilise ; `"obstructed"` (`a`, `b`, `ab`) = trou de cette salle dont l'entrée est bouchée tant
  que la salle n'a pas été ouverte de l'intérieur (porte Open World `blocked` ou `req_in` rhelmot avec clé/drapeau :
  serrure, mur cassable, désordre de 3a) ; `"no_enter"` = idem pour un lien non vanilla : le mod ferme l'autre côté
  (barrière, comme rhelmot pour un trou `out`), sinon la transition gèlerait (Madeline poussée dans un solide).
  La logique n'entre jamais par un trou `unknown`, `out` ou bouché, ni ne sort par `unknown`/`in`. Les positions `x`,`y` sont en pixels, coin haut-gauche de la salle.
- **Test hors ligne** : le mod accepte un fichier `slot_data` JSON local (commande `cmr_offline <chemin>`) pour tester la construction des cartes sans serveur. L'apworld écrit ce fichier dans le dossier de sortie de la génération (`<seed>_cmr_slot_data.json`).
