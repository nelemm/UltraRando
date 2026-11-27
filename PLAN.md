# Plan : V1 (tout, d'un coup) et V2 (le mod final, brique par brique)

## Ce que j'ai découvert avant de coder

Ta demande initiale avait 7 priorités. Une bonne partie existe déjà dans **Celeste (Open World) 1.1.3** de PoryGone (sorti le 15 août 2026). Il est déjà installé chez toi.

| Ta demande | Existe déjà ? | Où |
|---|---|---|
| Dash par direction comme items | **Oui** : Dash Shuffle `unified`, `cardinal_loose`, `cardinal_restrictive`, `octal` | apworld + mod Open World |
| Grab / climb comme items | **Oui** : Climb Shuffle `unified` / `split` (gauche, droite) | idem |
| S'accroupir comme item | **Oui** : Crouch Shuffle | idem |
| Wall jump, saut, grab séparé du climb | Non | → V2, briques 6 et 7 |
| Logique de mouvement salle par salle | **Oui** : les 802 salles et environ 2 000 passages sont annotés avec les directions de dash, le climb et l'accroupi, sur 3 niveaux (Developer Intended, Vanilla Movement, Assist) | `CelesteLevelData.json` |
| Difficulté adaptée à ton niveau | Partiel : **Vanilla Movement** correspond à « tout ce que le jeu de base apprend, Farewell compris », donc à peu près ton niveau. Rien au-dessus (tech de Strawberry Jam) | → V2, brique 8 |
| Solvabilité garantie | **Oui** : la génération Archipelago vérifie que la seed est faisable. Testé : 64 combinaisons sur 64 | `tools/mass_gen.py` |
| Mélange des salles | Abandonné, à ta demande | - |
| Aide avec techniques nommées et sources | **Non** : c'est le vrai manque | → **mod Celeste Movement Helper** (V1) |

**Licences, important :**
- **apworld** Open World : « Modified MIT ». On peut le copier, le modifier et le distribuer, mais pas changer sa licence ni le vendre.
- **mod Everest** Open World : « All rights reserved ». **Interdit de redistribuer une version modifiée.** On ne le modifie donc pas : notre mod est **séparé** et lit seulement son état.

## V1 : ce qui est fait (cette session)

1. **Environnement de génération** : un clone d'Archipelago 0.6.9 avec l'apworld 1.1.3, et un Python 3.13 dans `tools/venv`.
   - `tools/gen.py <dossier_yaml>` : génère une seed dans `v1/output/`.
   - `tools/mass_gen.py` : génération en masse avec des options aléatoires.
   - `tools/serveur.ps1 <seed.zip>` : serveur local.
2. **Seeds de test** :
   - brique 1 : le dash est placé sur l'entrée de la salle 2 de Forsaken City ;
   - brique 2 : tous les mouvements séparés, donnés un par un avec `/send`.

   Mode d'emploi : [TESTS.md](TESTS.md).
3. **Recherche** : 79 techniques, dont 76 avec une source vérifiée (surtout celeste.ink et « The Celeste Dictionary »). Fichiers : [docs/TECHNIQUES.md](docs/TECHNIQUES.md) et `docs/techniques.json`.
4. **Mod Celeste Movement Helper 0.1.0** (C#, MIT, installé), touche **F6** :
   - **page salle** : ce que la logique exige pour chaque passage et chaque check de la salle, en vert, rouge ou jaune selon tes items ;
   - **page techniques** : nom, inputs, timing, explication et liens sources ;
   - **réglages** : la difficulté de logique (à mettre comme ton YAML), la taille du texte et les touches.

   Les données de logique sont exportées automatiquement depuis l'apworld (`tools/export_help_data.py`), donc l'aide montre **exactement** ce que le générateur utilise.

5. **Brouillon de l'item Wall Jump** (le contenu des briques 6 et 7 de la V2, d'un coup) :
   - **Fork de l'apworld** : `tools/make_fork.py` produit `v1/apworld/celeste_open_world.apworld`. Il ajoute l'option `wall_jump_shuffle` et l'item `Wall Jump` (ID `0xCA1F000`, loin de ceux de PoryGone).
     - Logique V1 prudente : sans Wall Jump, seuls le Prologue et l'Épilogue sont en logique.
     - Roomsanity est forcé, pour que le Prologue ait assez de checks.
     - Vérifié : sur 5 seeds, le Wall Jump tombe toujours dans le Prologue.
     - 64 combinaisons de génération en masse avec le fork passent, dont environ la moitié avec Wall Jump.
   - **Mod** : un hook sur `Player.WallJumpCheck` bloque le wall jump, le wallbounce et le kick depuis le mur tant que l'item n'est pas reçu. Grimper reste possible. L'item est lu dans la liste publique `ItemQueue` d'Open World, qui ignore sans planter les ID qu'il ne connaît pas.
   - **À activer à la main** dans les options du mod, comme le YAML.
   - `tools/use_world.py official|fork` choisit la version utilisée par le générateur local. C'est **le fork** qui est sélectionné actuellement : sans `wall_jump_shuffle`, il se comporte comme l'officiel.

**Non testé en jeu** : l'affichage F6 et les briques 1 à 3. Seul le chargement du mod a été vérifié, dans `log.txt` (versions 0.1.0 et 0.2.0).

## V2 : le mod final, brique par brique

Règle : **une brique = une chose testable en jeu**. On ne passe à la suivante qu'une fois la précédente validée.

| Brique | Contenu | Code nouveau ? | Test |
|---|---|---|---|
| 1 | Dash bloqué, débloqué en entrant en salle 2 | Non (seed plando) | TESTS.md, brique 1 |
| 2 | Chaque mouvement séparé (8 dashs, climb G/D, accroupi) | Non (`/send`) | TESTS.md, brique 2 |
| 3 | Aide F6 : page salle correcte (couleurs justes, lisible) | Mod Helper (fait) | Ouvrir F6 dans 5 à 6 salles variées, comparer au spoiler |
| 4 | Aide F6 : page techniques lisible, sources correctes | Mod Helper (fait) | Relire 10 techniques, ouvrir les liens |
| 5 | **Lien salle → technique** : quand une alternative demande par exemple « dash bas-droite + accroupi », proposer les techniques qui collent (hyper…) | Petit : une table de correspondance dans le mod | À valider par toi : c'est une suggestion, pas une preuve |
| 6 | **Nouveaux items Wall Jump (brouillon V1 fait) et Grab** (séparé du climb). Notre mod bloque le mouvement. Il lit l'item reçu depuis la session Archipelago d'Open World, en lecture seule | Oui : hooks `Player.WallJump` / `ClimbJump` dans notre mod, items ajoutés à un fork de l'apworld | Seed `/send Nelem Wall Jump` |
| 7 | Logique de ces nouveaux items : remplacer le verrou « chapitre entier » de la V1 par une annotation passage par passage | Données | `mass_gen.py` + jeu |
| 8 | **Niveau « Expert »** au-dessus de Vanilla Movement : des règles en plus, chacune liée à un `id` de technique, ce qui alimente aussi l'aide | Données + petit code dans le fork de l'apworld | `mass_gen.py` + jeu |
| 9 | Aide « solution » : quand une règle Expert est utilisée, la page salle affiche la technique et sa source | Petit | Jeu |

**Pour les briques 6 à 8, il faut décider** : les nouveaux items imposent de garder le même nom de jeu, « Celeste (Open World) », parce que le mod Open World vérifie ce nom. Notre apworld modifié devient alors **incompatible avec la version officielle dans une même multiworld**. Ça ne pose pas de problème en solo. Une autre option : proposer ces ajouts à PoryGone.

## Points à relire (signalés par la recherche)

- Le français de `docs/TECHNIQUES.md` a été accentué par script : une relecture rapide est utile.
- Deux durées diffèrent selon les sources : le buffer d'input (4 ou 5 frames) et le liftboost (9 ou 10 frames). Les deux valeurs sont notées.
- Trois techniques n'ont pas de source et sont marquées « non vérifiée » : *variable jump height*, *dash cancel*, *grabless*.
- `vanilla_where` du Dream Jump (2A) et du Fastbubbling (4A) vient de connaissances générales, pas d'une page ouverte.
- *Extended Super/Hyper* : la fiche dit que Farewell l'appelle « wavedash ». À vérifier, je n'en suis pas sûr.
