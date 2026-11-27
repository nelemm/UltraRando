# Liste des tests en jeu (checks, dashs, Demo Dash, transitions)

Règle : une mécanique n'est validée **que si elle a été démontrée dans le jeu en marche**. Un drapeau interne ou du code présent ne suffit pas.

Comment les tests agissent sur le jeu :
- les touches sont **injectées sous `MInput`**, avec les vraies touches de Nelem (dash I, saut J, grab P, demo `*`). Tout passe donc par le vrai code : `VirtualButton`, puis `Player`, puis nos verrous ;
- les objets arrivent par `ApState.ItemReceived`, le même chemin que les objets du serveur, ou directement du serveur Archipelago dans les tests en ligne ;
- les mesures portent sur ce que fait Madeline : son état (`StDash`, `StClimb`…), `DashDir`, sa vitesse, les dashs dépensés, `Ducking`, les contacts mortels (`Player.Die`).

Fichiers : `mod/UltraRando/Source/AP/SelfTest.Mech.cs` (mécaniques), `SelfTest.Chain.cs` (chaîne en ligne), `SelfTest.Walk.cs` (transitions), `SelfTest.cs` (construction des cartes et recensement des checks). Les résultats bruts sont dans `tools/out/mech`, `tools/out/chain3`, `tools/out/shaft`, `tools/out/walk*` et `tools/out/walk_full99*`.

Commandes (lancements bornés à 115 s, Celeste toujours fermé à la fin) :
- mécaniques : `tools\venv\Scripts\python.exe tools\run_offline_test.py <slot_data.json> mech area=1a [groups=basic|combos|shaftskip|explore] [combos=0-31]` ;
- chaîne en ligne : un serveur `MultiServer.py --host 127.0.0.1` reste lancé, puis `tools\run_saves_test.py v1\output\cmr_chain_77.zip <dossier> newsave=1 chain=1 noserver=1 server=127.0.0.1:38281`, puis `chain=2` (relance du jeu).

## 1. Verrous de mouvement (hors ligne, salle 1 du 1A) : 61 tests sur 61

| Test | Attendu | Obtenu | Résultat |
|---|---|---|---|
| Dash directionnel (mode 4), rien possédé : 8 directions | aucun dash, aucun dash dépensé | `StDash` jamais atteint, dashs 1 → 1 | 8 OK |
| Mode 4, une seule direction possédée, pour chacune des 8 | seule celle-ci dashe (bonne `DashDir`, dash dépensé), l'opposée est refusée | conforme pour les 8 | 16 OK |
| Mode 1 (Dash unique) : sans, puis avec Dash | refusé, puis droite et haut-gauche | conforme | 3 OK |
| Mode 2 (cardinaux souples), Up possédé | U et UR passent, R est refusé | conforme | 3 OK |
| Mode 3 (cardinaux stricts) : Up, puis Up + Right | UR refusé, puis UR accepté | conforme | 2 OK |
| Mode 0 (non mélangé) | dash libre | conforme | 1 OK |
| **Changement de direction pendant le gel** : appui vers R (possédé), puis visée vers U (verrouillé) | le dash reste vers R | `DashDir` (1,0) | OK (**bug corrigé**, voir §7) |
| Même manœuvre avec R et U possédés | le dash va vers U (comportement normal conservé) | `DashDir` (0,-1) | OK |
| **Dash dans l'eau** (eau ajoutée par le test) vers U verrouillé, puis vers R possédé | U ne bouge pas, R bouge | vitesse 0, puis 180 | OK (**bug corrigé**) |
| Grimpe séparée G/D, unique, non mélangée (mur ajouté par le test) | seul le côté possédé grimpe (`StClimb`) | conforme | 9 OK |
| Accroupi : sans, puis avec Crouch | `Ducking` faux, puis vrai | conforme | 2 OK |
| Wall jump (en l'air contre un mur, sans grab) : sans, avec, non mélangé | aucun saut, puis vitesse (∓130, -105) loin du mur | conforme | 5 OK |

## 2. Toutes les combinaisons de directions de dash : 256 sur 256

Pour chacun des 256 ensembles de directions possédées (mode 4), on essaie les 8 directions. Résultat : **2048 dashs réels, tous conformes**. Seules les directions possédées dashent, et aucune direction verrouillée ne passe (lots de 32 combinaisons, `tools/out/mech/combos_*.txt`).

## 3. Demo Dash (objet `Demo Dash`, option `demo_dash_shuffle`)

Le vrai fonctionnement, relu dans l'IL de `Player` :
- `StartDash`, `SwimUpdate` et `BoostUpdate` notent `demoDashed = Input.CrouchDashPressed` ;
- `DashBegin` accroupit Madeline si `demoDashed` est vrai **ou** si Bas est tenu ;
- la direction n'est fixée que dans `DashCoroutine` / `RedDashCoroutine`, via `CorrectDashPrecision`.

On peut donc faire un demodash **sans la touche Demo** : il suffit de tenir Bas à l'appui, puis de viser ailleurs. Le verrou agit dans `CorrectDashPrecision` : si l'objet manque, un dash accroupi qui ne va pas vers le bas relève Madeline.

| Test | Attendu | Obtenu | Résultat |
|---|---|---|---|
| Touche Demo vers R, verrouillé | dash normal, debout | `Ducking` faux après la fixation de la direction | OK |
| Bas + dash, puis R, verrouillé | debout | debout | OK |
| Touche Demo vers U, verrouillé | debout | debout | OK |
| Dash bas-droite au sol, verrouillé (glissade accroupie normale du jeu, base des hypers) | reste accroupi, `DashDir` (1,0) | conforme | OK |
| Touche Demo vers R / Bas + dash puis G / touche Demo vers U, objet possédé | accroupi | accroupi | 3 OK |
| Touche Demo, option désactivée | accroupi (libre) | accroupi | OK |
| **Shaftskip 3A `04-d`**, objet possédé : balayage des départs, mode invincible, contacts mortels comptés | au moins un départ traverse le mur de dust sans contact | **3 départs sur 81 passent**, en (168 à 172, y = 124) : un seul pixel de hauteur, conforme aux sources (« pixel-perfect ») | OK |
| Shaftskip, objet verrouillé, mêmes départs (x de 166 à 174, y de 122 à 126) | aucun ne passe | **0 sur 45**, 8 à 11 contacts mortels même aux bons départs | OK |
| **Shaftskip joué avec de vraies entrées** (idée de Nelem : « grab le mur de gauche ») : depuis le sol à droite de la marche, s'accrocher à sa face, grimper c frames, climb jump, puis Demo Dash vers la gauche k frames après | au moins un réglage passe | passe pour (c,k) = (10,14), (11,13), (12,13), (13,12), avec le dash qui part à y = 124 à chaque fois. **Rejoué deux fois, même résultat** | OK |
| Même séquence, Demo Dash verrouillé | rien ne passe | 0 | OK |
| Même séquence, grimpe verrouillée (`climb_shuffle` 1, sans Climb) | rien ne passe | 0 (Madeline ne s'accroche pas) | OK |
| Saut depuis le sol, puis Demo Dash (k de 0 à 24) | ne doit pas suffire | sommet à y = 125, 3 contacts au mieux, ne passe jamais | OK (**la grimpe est nécessaire**) |
| Logique : arête `3a_04-d_south → south-west` | absente en DI ; `Demo Dash + Left Dash + Left Climb` en VM et en Assist | conforme | OK |
| **Le skip ne supprime pas le détour** (remarque de Nelem) : moteur de logique sur le 3A d'origine, quatre cas : A sans Demo Dash, B avec, C avec mais clé impossible, D sans et clé impossible | C : côté ouest de `04-d` atteint sans la clé, et checks du détour toujours accessibles par le chemin normal. D : côté ouest inaccessible | A, B et C : fraises de `04-c` et `03-b`, lieu de la clé `02-c` et **cassette `01-c`** accessibles. C : ouest atteint. D : ouest et salles suivantes inaccessibles | OK |
| Archipelago : seed avec plando `Feathers` → « Celestial Resort A - Cassette » (salle du détour), `Demo Dash` dans le 1A, option activée | la seed se génère, et le parcours du spoiler passe quand même chercher la cassette | parcours : sphère « Celestial Resort A - Cassette: Feathers » ; génération OK | OK |
| Placement des salles | ne compte jamais sur le skip | 246 régions atteintes avec ou sans Demo Dash | OK |
| Génération avec l'option | 0 échec | 30 générations sur 30, 60 combinaisons | OK |
| Chaîne en ligne : fraise de la salle 5 (1A) | Demo Dash reçu, debout avant, accroupi après, conservé après relance | conforme | OK |

Sources du shaftskip : [celeste.ink, Celestial Resort](https://celeste.ink/wiki/Celestial_Resort) (section Elevator Shaft) et la [speedrunwiki (archive)](https://archive.speedrunwiki.com/w/index.php?title=Celestial_Resort). La config de rhelmot commente aussi `04-d` comme « demodash room ». Les autres usages connus du Demo Dash (6B, 4A, 5A, Farewell, d'après [celeste.ink Demodash](https://celeste.ink/wiki/Demodash)) ne sont **pas** ajoutés à la logique : aucune source ne parle de skip pour eux.

## 4. Chaîne en ligne (serveur local, seed `cmr_chain_77`, salles d'origine) : 29 sur 29, puis 30 sur 30

| Check (déclenché en jeu) | Objet attendu (spoiler) | Preuve d'utilisation | Résultat |
|---|---|---|---|
| Entrée de la salle 2 du 1A (vraie transition 1 → 2) | Dash | dash refusé avant, accepté après (R, UL) | OK |
| Fraise de la salle 2 | Climb | grimpe refusée avant, acceptée après, à droite et à gauche | OK |
| Entrée de la salle 3 (transition 2 → 3) | (check de salle) | dash et grimpe encore possibles après la transition | OK |
| Fraise de la salle 3 | Feathers | reçu (plume non utilisée en jeu, voir §8) | OK |
| Transitions 3 → 4 → 3b → 5 | — | réelles | OK |
| Fraise de la salle 5 | Demo Dash | voir §3 | OK |
| **Relance du jeu** (même sauvegarde, serveur toujours actif) | les objets restent | Dash, Climb, Feathers et Demo Dash possédés **et** utilisables | OK |
| Sauvegarde du jeu (`UserIO.SaveHandler`) | objets et checks écrits | 20 objets, 9 checks dans la sauvegarde randomizer | OK |
| Checkpoint Crossing (transition 5 → 6) | Crouch | accroupi refusé avant, accepté après | OK |
| Cœur de cristal du 1A (puzzle de l'oiseau résolu par le drapeau `unlocked_satellite`, puis vrai dash dans le cœur) | Springs | aucun ressort avant ; après, ressort chargé qui propulse Madeline (vitesse -185) | OK (**bug corrigé**) |
| Cassette du 1A (`11z`) | Raspberry | check envoyé | OK (**bug corrigé**) |
| Fin du 1A (`Level.RegisterAreaComplete`, la cinématique de fin n'est pas jouée) | Raspberry | check envoyé | OK |
| Clé Hallway Key 1 (3A `02-b`) | Traffic Blocks | bloc zip chargé dans la salle 4 du 1A | OK |

## 5. Transitions (marche réelle à travers chaque liaison, dans le sens logique)

| Carte | Résultat |
|---|---|
| Seeds 42, 7 et 1234, Prologue à 7A | toutes OK, par exemple seed 42 : 12, 57, 68, 93, 77, 107, 70 et 137 liaisons |
| Seed complète 99 : faces B et C, 9A/B/C, 10A | toutes OK (10A : 121/121) |
| Mode du Core sur 9A, 9B et 9C | 68 arrivées sur 68 avec le bon mode chaud/froid |

## 6. Recensement des objets à ramasser

Pour chaque lieu fraise, clé, gemme, cœur ou cassette du slot_data, le test vérifie que son entité (même ID, bon type) existe dans la salle construite (`SelfTest.cs`, « collectibles census »).

| Seed | Résultat |
|---|---|
| `cmr_chain_77` (salles d'origine, 0a à 7a) | 22, 20, 32, 31, 38, 1 et 56 sur autant : **tout est présent** (1A : 20 fraises, le cœur et la cassette) |
| `cmr_solo_42` (mélangée) | 17, 12, 23, 23, 17 et 41 sur autant : tout est présent |
| `cmr_full_99` (26 chapitres) | tout est présent. Les faces B et C, le 6A et le Prologue n'ont pas d'objet à ramasser comme lieu |

Avant la correction des tweaks (§7, point 3), la cassette du 1A manquait.

## 7. Bugs trouvés par ces tests, et corrigés

1. **Changement de direction pendant le gel** : le verrou lisait la direction au moment de l'appui, alors que Celeste la fixe quelques frames plus tard. On obtenait donc un dash dans une direction verrouillée. Corrigé dans `ApHooks.OnCorrectDashPrecision` : le dash garde la direction autorisée de l'appui.
2. **Dash dans l'eau** : `SwimUpdate` lance le dash sans passer par `StartDash`, donc sans verrou. Corrigé : dans une direction verrouillée, il ne bouge pas.
3. **Cassettes et cœurs supprimés** : les « Tweaks » de la config de rhelmot remplacent 14 cassettes par des cœurs et retirent des cœurs. Notre construction des salles les appliquait, et la cassette du 1A était impossible à ramasser. Corrigé : `StaticRoom.CMR_KeepCollectibles`, activé par `LayoutBuilder`.
4. **Cœur du 1A** : il est créé par le satellite (`unlocked_satellite`), loin de son marqueur `birdForsakenCityGem`, et le hook ne cherchait que `blackGem`. Le check `1a|s1|heart|14` était impossible. Corrigé dans `ApHooks.CollectedAt` : il accepte `birdForsakenCityGem`, et prend l'unique entité du type quand la distance ne suffit pas.

## 8. Pas encore validé en jeu (limites connues)

- **Objets interactifs** : seuls Springs (absent puis présent, et propulse), Traffic Blocks (absent puis présent) et la réception des Feathers sont prouvés. Les 33 autres types n'ont été vérifiés ni en présence ni en fonctionnement.
- **Gemmes du 7A et fraises dorées** : non testées. Les dorées ne sont pas des lieux dans nos seeds actuelles.
- **Fin de chapitre** : déclenchée par `RegisterAreaComplete`, pas en marchant jusqu'à la fin.
- **Cœur du 1A** : le code de l'oiseau n'a pas été tapé, le drapeau qu'il pose a été posé directement.
- **Sortie de bulle** : un dash depuis une bulle peut partir dans une direction verrouillée. C'est une limite connue : la bulle lance toujours Madeline.
- **Shaftskip** : prouvé avec un départ en l'air au pixel près. Atteindre ce pixel en jouant n'est pas démontré, c'est l'affaire du joueur (« pixel-perfect » d'après les sources).
- **Multijoueur en ligne et DeathLink** : non testés en jeu.

## 9. Audit final (26/11, soir)

**Checkpoints en jeu, sur la carte mélangée de la seed 42, 1A salle 6 : 6 sur 6**
- La salle du checkpoint est atteinte par une vraie transition, et envoie le check du checkpoint et celui de la salle (2 ID distincts).
- Le jeu débloque ensuite le checkpoint pour la sélection de chapitre.
- Démarrer depuis ce checkpoint marche, et renvoie les checks de la salle. Les ID sont distincts ; un renvoi est ignoré par le serveur.

**Différence avec le design voulu par Nelem (non corrigée : c'est un choix de design)**
- Nos checkpoints sont des **lieux en plus** (`checkpointsanity`, 23 par seed sur 0a–7a), remplis par des fraises ou des Raspberries.
- Il n'y a **aucun objet « checkpoint »** dans le pool.
- La logique ne compte jamais sur une téléportation par checkpoint : elle part toujours du début du chapitre. C'est prudent, donc sans blocage possible.
- Open World fait autrement : chaque checkpoint y est aussi un objet du pool, et la logique relie le menu au checkpoint quand on a cet objet.
- `roomsanity` ajoute de même environ 400 lieux, un par salle.

**Seeds avec les options du soir (`v1/yamls/cmr_solo`)**
- 8 seeds solo (101 à 108) :
  - génération OK ;
  - le parcours du spoiler atteint la victoire ;
  - les lieux réels du spoiler correspondent exactement aux `location_ids` du slot_data (557 à 576 par seed) : 0 omis, 0 ajouté, 0 doublon ;
  - `test_cmr_layout` est OK.
- 4 seeds à deux joueurs UltraRando (201 à 204) : victoire des deux joueurs dans le parcours, environ 300 objets échangés.
- Génération massive précédente : 30 + 40 générations sans échec.

**Sécurité**
- Noms des fichiers de cache réduits à `[A-Za-z0-9_-]`, donc pas de traversée de chemin.
- Lecture JSON limitée à 64 Mo et à une profondeur de 32.
- Aucun chargement de code ni désérialisation de types.
- `tools/serveur.ps1` lance MultiServer **sans mot de passe** et à l'écoute de toutes les interfaces (`0.0.0.0`).

## 10. Suites de l'audit (26/11, soir) : tout sauf la logique

| Point de l'audit | Changement | Preuve | Résultat |
|---|---|---|---|
| Serveur sans mot de passe, ouvert à tous | `tools/serveur.ps1` : `-Password` (→ `--password`) et `-Local` (→ `--host localhost`), avec un avertissement s'il n'y a pas de mot de passe | client de test `tools/ap_test_client.py` : sans mot de passe, ou avec un mauvais, `InvalidPassword` ; avec le bon, connexion. `-Local` écoute sur `127.0.0.1` et `::1`. Le mod se connecte avec le mot de passe (`run_saves_test … password=`) | OK |
| Objectif jamais envoyé si on finit la Summit déconnecté, ou avant d'avoir les fraises | `ApState.TrySendGoal` est appelé à la fin du chapitre, à chaque fraise reçue et après chaque (re)connexion (`FlushChecks`). La fin du chapitre est lue dans la sauvegarde | seed `cmr_goal_31` (1 fraise requise, placée sur Room 2) : Summit finie à 0/1 → pas d'envoi ; connexion coupée ; Room 2 hors connexion ; reconnexion → check renvoyé, fraise reçue, objectif envoyé. Serveur : « Nelem (Team #1) has completed their goal. » | OK (**bug corrigé**) |
| Synchro à deux joueurs non testée | — | seed `cmr_sync_41` (le Dash de chacun chez l'autre) : Ami (client de test) envoie son check, et le Dash arrive dans le jeu de Nelem, qui dashe vraiment. Nelem entre dans sa salle 2, et Ami reçoit son Dash (objet 205324304) | OK |
| 33 types d'objets interactifs non testés | — | seed complète `cmr_full_99` : pour chacun des 36 types, une salle qui en contient, rechargée objet non reçu puis reçu. Au contact : plume, bumper, recharges simple et double, bulles bleue et rouge | 31 types absents puis présents ; 5 types testés au contact OK |
| **Recharges doubles liées au mauvais objet** (trouvé par ce test) | `ItemNames.ItemForEntity` lit `twoDash` (attribut lu par `Refill(EntityData)` dans Celeste) au lieu de `twoDashes` | avant : les recharges doubles dépendaient de « Dash Refills ». Après : les 3 de `10a d-00` dépendent de « Double Dash Refills » et donnent 2 dashs | OK (**bug corrigé**, n'existe que dans le Farewell) |
| Gemmes du 7A non testées | — | seed 42 : vrai dash dans chacune des 4 gemmes (`a-06`, `b-02d`, `c-06c`, `f-02b`), le check part | 4 OK |
| Trace de débogage du verrou Demo Dash | retirée | — | fait |
| Non-régression | — | mécaniques 61/61 ; chaîne en ligne de zéro 29/29 puis 30/30 | OK |

**Limites restantes (hors logique)**
- ~~**Pufferfish** : jamais retirés~~ : **corrigé le 26/11** (`ItemNames` : l'entité `eyebomb` donne Pufferfish). En jeu, sur 10a `j-16` : les 10 sont absents tant que l'objet manque, puis présents une fois reçu. La logique d'Open World demande déjà Pufferfish (77 règles).
- **Strawberry Seeds, Yellow Torches, Theo Crystal** : jamais retirés, par choix.
- **Partie complète jouée de bout en bout** : pas faite (elle demande un vrai joueur).

## 11. Checkpoints = objets (26/11, soir), comme demandé par Nelem

**Design** (`checkpointsanity`, activé par défaut)
- Plus aucun lieu « checkpoint » : le nombre de checks redescend (seed 42 : 549 au lieu de 568).
- Chaque checkpoint de la carte est un **objet** (« Forsaken City A - Crossing », ID `0xC3D0100` + index, ordre fixe), placé dans le pool à la place d'un objet de remplissage.
- Le checkpoint n'apparaît dans la sélection de chapitre qu'une fois son objet reçu. L'atteindre ne le débloque plus (`ApHooks.OnSetCheckpoint` intercepte `SaveData.SetCheckpoint`, et `ApState.ApplyCheckpoints` aligne la sauvegarde sur les objets reçus).
- Y entrer envoie les checks de la salle d'arrivée (entrée de salle).
- **Logique** : avec l'objet, une arête « menu → région de départ du checkpoint » sur la carte mélangée (`checkpoint_region` d'Open World, ou le nœud principal d'une salle de rhelmot).
- **Exception de sûreté** : dans les chapitres dont la logique a des événements de session (clés, désordre, disjoncteur : 3A, 5A, 5B, 7A, 10A), la logique ne compte pas sur le départ au checkpoint. Un départ au checkpoint ouvre une nouvelle session, où les clés ne sont plus en main. L'objet débloque quand même le checkpoint dans la sélection de chapitre.
- **Option désactivée** : les checkpoints se débloquent en les atteignant, comme dans le jeu de base, et la logique part toujours du début.
- Les seeds générées avant ce changement (sans `checkpoints` dans le slot_data) gardent le comportement d'origine.

| Test | Résultat |
|---|---|
| En jeu, en ligne (`cmr_cp_51`, objet Crossing placé sur Room 2) : salle 6 atteinte sans l'objet → non débloquée ; Room 2 → objet reçu → débloquée ; départ depuis le checkpoint → salle 6, check de la salle envoyé ; aucun lieu checkpoint dans la seed | 10/10 |
| Relance du jeu, même serveur : le checkpoint reste débloqué | 1/1 |
| 8 seeds solo (options du soir) : 0 lieu omis ou en trop, 0 doublon, 0 lieu checkpoint, tous les objets checkpoint placés. Le parcours utilise des objets checkpoint dans 5 seeds. Victoire dans les 8 | OK |
| 3 seeds à deux joueurs : victoire des deux | OK |
| Génération massive 30/30 (60 combinaisons) ; `test_cmr_layout` 156 cartes OK ; seeds 42/7/1234 régénérées | OK |
| Non-régression : mécaniques 61/61, chaîne en ligne 29/29 puis 30/30 (ancienne seed : comportement d'origine) | OK |

## 12. Clés = objets, état de session gardé (26/11, soir) : l'exception des checkpoints est levée

- **`keysanity`** (activé par défaut) :
  - chaque clé de la logique est un objet du pool (ID `0xC3D0200+`), trouvable dans n'importe quel monde du multiworld ;
  - l'endroit de la clé dans le niveau reste un check ;
  - posséder la clé ouvre sa porte pour de bon : les `lockBlock` de la salle ne sont plus chargés. Au Farewell `d-00`, chaque clé reçue ouvre une des 5 portes ;
  - correspondance porte → clé tirée de la logique d'Open World : `slot_data.locks` ;
  - les clés physiques restent ramassables et ouvrent encore une porte comme dans le jeu de base. Ça ne peut que faciliter.
- **État gardé pour de bon** (`ApSaveData.KeptState`, remis à chaque nouvelle session) :
  - désordre de l'hôtel : `oshiro_clutter_cleared_*`, `oshiro_clutter_door_open` ;
  - disjoncteur : `disable_lightning` ;
  - gemmes du 7A.
- **Conséquence** : avec `keysanity`, la logique compte sur les départs depuis un checkpoint dans tous les chapitres. Sans `keysanity`, l'exception reste là où il y a des clés.

| Test | Résultat |
|---|---|
| En jeu, en ligne, à deux (`cmr_keys_61`). La Front Door Key de Nelem, trouvée **dans le monde d'Ami** (client de test), ouvre la porte de `3a s3`. La Hallway Key 1, trouvée dans la salle 2 de Nelem, ouvre `02-a`, qui reste ouverte dans une nouvelle session. Désordre rangé gardé dans une nouvelle session. Gemme brisée par un vrai dash gardée dans une nouvelle session | 16 + 4 tests, tous OK |
| 8 seeds solo : 0 écart de lieux, 0 événement de clé, de 4 à 8 clés dans le pool, victoire. Les checkpoints des chapitres à clés (3A, 5A, 7A) servent 15 fois dans les parcours | OK |
| 3 seeds à deux joueurs (victoire des deux) ; génération massive 25/25 ; mécaniques 61/61 ; seeds 42/7/1234 régénérées | OK |

## 13. Ce qui manquait (26/11, soir)

| Point | Résultat |
|---|---|
| Pufferfish jamais retirés | corrigé, 10/10 absents puis présents en jeu (10a `j-16`) |
| Chaîne en ligne sur le build actuel (checkpoints et clés en objets) | YAML `cmr_chain` mis à jour : Crouch passe sur `Room 6`, plus de lieu checkpoint, et Demo Dash est placé explicitement. Le test vise maintenant `1a|6|room`. Résultat de zéro : 29/29 puis 30/30 après relance |
| Mode du Core (9a/9b/9c) sur nos cartes | déjà vérifié par le test de marche (`walk_full99*`) : 66 transitions, 0 `COREMODE-WRONG` |

## 14. Notifications, nom de salle, DeathLink, partie à deux avec MM (26/11, nuit)

| Test | Résultat |
|---|---|
| **Grand bandeau** pour un objet reçu d'un autre joueur, en ligne, à deux (`cmr_sync_41`) : « Reçu Dash de Ami. », avec l'icône Celeste du dash | OK (capture vérifiée) |
| Bandeau pour un objet envoyé : « Envoyé Dash à Ami. », avec l'icône de l'objet Celeste | OK |
| Bandeau pour un objet trouvé pour soi : « Trouvé Raspberry. », avec l'icône de la framboise | vu en capture |
| Objet d'un autre jeu : icône du check (fraise, cœur, cassette, clé, gemme, fin, salle) quand c'est mon check, sinon icône Archipelago (`data/icon.png`, MIT) | code, non testé en jeu |
| **Nom de salle** : affiché à l'entrée (« Room: 2 »), encore là à 3,8 s, parti à 4,1 s ; pas réaffiché après une mort dans la même salle ; affiché après un départ par checkpoint (« Room: 6 ») | OK |
| **DeathLink** (`cmr_dl_43`) : la mort envoyée par Ami tue Madeline une fois ; la mort de Madeline arrive chez Ami (« DEATHLINK RECEIVED from Nelem ») | OK |
| Checkpoints (`cmr_cp_51`, régénérée) : 0 check « checkpoint » dans la seed ; checkpoint non débloqué sans son objet ; objet reçu → débloqué ; départ depuis le checkpoint, et le check de la salle part | 11/11 |
| **Seed Majora's Mask + Celeste** (`v1/yamls/coop`) : génération OK, victoire des deux dans le parcours du spoiler. Serveur : les deux slots se connectent, et le check d'Ami envoie « Bomb Refill 30 » au slot MM | OK (le client MM réel n'a pas été lancé) |
| Non-régression : mécaniques 61/61, chaîne en ligne 29/29 et 30/30 | OK |

**Panne trouvée** : 8 mécaniques échouaient. Une manette DualSense branchée (stick au repos qui dérive) ajoutait « haut » aux touches injectées. Les tests neutralisent maintenant la manette (`SelfTest.Mech.cs`, hook `GamePadData.Update`).

Couverture de la logique : voir `docs/LOGIC_COVERAGE.md` (91,5 %, méthode et liste des 187 éléments non vérifiés).

## 15. « Retour à la carte » dans le menu pause (26/11, nuit)

**Bug signalé par Nelem** : le bouton manquait sur les cartes du rando.

**Cause** : Celeste n'affiche ce bouton que si le Prologue est terminé dans la sauvegarde (ou en mode debug ou triche). Une sauvegarde rando n'a pas forcément fini le Prologue.

**Correctif** : `Patches/menuLifecycle.cs` ajoute le bouton sur nos cartes, après « Redémarrer le chapitre ». Il fait la même action que le bouton du jeu (`Level.GiveUp`, avec confirmation).

**Test** (`mech groups=pause`, sauvegarde mise dans l'état « Prologue non fini, sans debug »), 4/4 :
- le bouton est présent ;
- la confirmation « Oui / Annuler » s'affiche ;
- « Oui » ramène à la carte.

**Lancements de test** : ils ouvrent maintenant Celeste sur le 2ᵉ écran, puis rendent le focus à la fenêtre d'avant (`tools/second_screen.py`).

## 16. Pics accrochés, portes à cœurs, Épilogue (26/11, nuit), demandés par Nelem

- **Pics restés en l'air** quand leur bloc est retiré, signalé en jeu par Nelem :
  - les pics et les spinners accrochés à un bloc retiré partent avec lui (`ApHooks.AttachedTo`, selon le côté de leur base) ;
  - ils restent si un bloc encore chargé les porte aussi (blocs cassette de deux couleurs superposés).
  - Test `mech groups=attached`, 10 types de blocs : sans l'objet, 0 pic chargé ; avec l'objet, tous reviennent. 30/30.
- **Portes à cœurs ouvertes dès le départ** (Core A/B/C, Farewell `f-door`), car la logique ne compte jamais les cœurs. Test `mech groups=hearts` : 4/4.
- **Épilogue bloqué** sur les sauvegardes rando : une carte postale « L'Épilogue est désactivé dans le randomizer. », puis retour à la carte. Test `chain=epilogue` sur une vraie sauvegarde rando : 2/2.
- **Non-régression** : mécaniques 61/61, objets interactifs 50/50 et 58/58.

## 17. Retours de la partie de l'ami (26/11, nuit)

| Problème | Cause | Correctif | Test |
|---|---|---|---|
| **Crash** au miroir du 5A (`CS04_MirrorPortal` OnEnd, NullReference) | la cinématique envoie Madeline en salle `void`, absente des cartes construites | `ApHooks.PatchMirrorPortal` (ILHook) : sans `void`, direction `c-00`, comme le fait déjà la face B | `mech groups=portal` 3/3 ; passage automatique 2/2 |
| Autres salles visées en dur par des cinématiques (`end_0`, `00`/`04`, `intro-00-past`/`intro-01-future`, `c-00`) | — | toutes présentes dans la seed de l'ami, rien à corriger | vérifié dans le slot_data |
| **2A : salle où l'on entre sans pouvoir sortir** | le dream dash n'est donné qu'après la 1ʳᵉ cinématique du miroir du 2A ; sur une carte mélangée, les Dream Blocks restent solides | dream dash actif sur nos cartes (c'est l'objet « Dream Blocks » qui décide) | `mech groups=dream` 4/4, avec un vrai dash dans un Dream Block en 2A `12b`. Salle exacte vue par l'ami : non confirmée |
| **Fraises sans icône Archipelago** | aucun indicateur | petit badge Archipelago (11 px) au-dessus de chaque fraise dont le check n'est pas fait | `mech groups=badge` 2/2 |
| **Journal** : colonnes sans en-tête, 26 chapitres hors de la page, blocs cassette en « L », Dream Blocks invisibles | page unique ; texture des blocs cassette = feuille de 4×4 tuiles ; Dream Blocks = particules | en-têtes cœur, cassette et fin ; 12 chapitres par page ; bloc cassette fait de ses 4 coins ; Dream Block dessiné | captures du journal vérifiées |
| **Cinématiques à passer** | — | sur nos cartes, toute cinématique passable est passée automatiquement après 0,3 s (même action que « Passer la cinématique ») ; option `Passer les cinématiques automatiquement` | `mech groups=cutskip` 2/2 |
| **Fraises à graines** | — | rien à corriger | `mech groups=seeds` 4/4 (2A `d1` : 3 graines puis la fraise, le check part) |
| Notification au nom long | — | le texte reste dans le cadre | capture |

**Non-régression** : mécaniques 61/61, chaîne en ligne 29/29 et 30/30.

**Test de marche sur la seed de l’ami** (`v1/output/coop`), avec le passage automatique des cinématiques : 2A 69/69, 3A 85/85, 5A 115/115 transitions OK.

## 18. Fin de chapitre sans retour au menu (26/11, nuit), demandé par Nelem

**Problème** : la salle finale peut se trouver au milieu de la carte mélangée. L'atteindre joue la fin du chapitre et renvoie au menu, alors qu'il reste des salles à explorer.

**Correctif** (`ApHooks.OnCompleteArea`, hook de `Level.CompleteArea`) :
- sur nos cartes, la fin enregistre le chapitre terminé et envoie le check « Level Clear » (`RegisterAreaComplete`) ;
- puis la salle est rechargée et le joueur continue ;
- option : « Continuer à jouer après la fin du chapitre » (activée par défaut).

**Test** (`mech groups=clear`, vrais déclencheurs de fin, cinématique passée automatiquement), 6/6 :
- 1A `end`, 2A `end_6`, 7A `g-03` : le check part, Madeline reste dans la salle en état normal, plus de cinématique.

## 19. Chapitre final verrouillé par les fraises (27/11)

**Design** (comme Open World) :
- le chapitre objectif (`goal_area`) s'ouvre avec `strawberries_required` fraises reçues ;
- ses checks sont de vrais lieux, qui peuvent donner des objets pour les chapitres précédents ;
- la victoire = sa fin, avec les fraises requises.

**Mise en œuvre** :
- **apworld** (`__init__.py`, `entry()`) : l'entrée du chapitre (départ et checkpoints) demande les fraises, en plus du Wall Jump. La logique voit donc ses checks après les fraises, sans dépendance circulaire (le remplissage d'Archipelago le garantit).
- **slot_data** : `goal_area_locked`.
- **mod** (`ApHooks.GoalLockMessage`, `OnLevelEnterGo`) : entrée refusée par une carte postale « Ce chapitre est l'objectif : il s'ouvre avec N fraises reçues (x / N) ».
- Les anciennes seeds (sans `goal_area_locked`) ne changent pas.

**Seed de test** : `v1/yamls/cmr_final`, seed n° 7101 → `v1/output/cmr_final_7101.zip`.
- 1A : Dash en salle 2, 3 fraises sur les salles 3, 4 et 5 (5 fraises au total, 60 % requises).
- Sommet : Springs en `a-00`, Feathers en `a-00-intro`, Climb en `a-01`.

**Test en ligne** (`chain=final`), 21/21 :
1. 7A refusé avec 0 fraise ;
2. 3 fraises reçues ;
3-4. 7A ouvert et entré ;
5-6. Springs et Feathers reçus dans 7A ;
7-8. retour au 1A salle 2 : les Springs y sont (0 avant) ;
9. Climb reçu dans 7A ;
10-11. vraie fin de 7A : Level Clear et objectif envoyés. Le serveur affiche « Nelem has completed their goal ».

**Générations** :
- 4 seeds solo (301 à 304), 2 seeds à deux (311, 312) et la seed MM + Celeste (321) : victoire partout dans le parcours.
- Génération massive : 25/25 OK (100 combinaisons d'options).

## 20. Journal adapté à la seed (27/11)

- **Colonnes de « Lieux »** : seulement les types de checks présents dans la seed (cœur, cassette, fin, fraises, checkpoint, clés, gemmes, salles).
- **Lignes** : seulement les chapitres actifs qui ont au moins un check.
- **Pages** : « Clés » seulement s'il y a des clés ou des gemmes, « Mouvements » seulement si un mouvement est mélangé, « Objets » seulement avec `interactable_shuffle`. Le journal est reconstruit à chaque ouverture : jamais périmé.
- **Totaux** : exactement les `location_ids` du slot_data.

**Test** : journal ouvert sur 3 seeds.

| Seed | Colonnes | Pages | Total |
|---|---|---|---|
| `partie_19844` (26 chapitres, tout mélangé) | heart, cassette, clear, strawberry, key, gem, room | 3 pages de lieux + Clés, Mouvements, Objets | 910 |
| `cmr_final_7101` (faces A) | les mêmes | Lieux, Clés, Mouvements, Objets | 653 |
| `cmr_minimal_7201` (sans salles, sans mélange) | sans « Salles » | Lieux, Clés seulement | 208 |

**Limite** : le YAML ne permet pas de désactiver fraises, cassettes ou cœurs (ce sont toujours des checks), ni de choisir des chapitres un par un. Le journal suit ce que la seed contient.

## 21. Retours de Nelem (27/11)

| Point | Cause / décision | Correctif | Test |
|---|---|---|---|
| « Retour à la carte » en double | ma confirmation s'ouvrait par-dessus le menu pause, resté ouvert | même action que le bouton du jeu : `PauseMainMenuOpen = false`, fermeture du menu pause, puis `GiveUp(index, false, minimal, false)` | `mech groups=pause` 4/4 |
| Crash du menu au 5A (`TextMenu.get_Current`, non reproduit) | cause la plus probable : les deux menus ouverts en même temps | même correctif | non reproduit |
| Conseil d'escalade | oiseau tutoriel `ClimbingTutorial` du Prologue (`0a 1`) | retiré sur nos cartes | — |
| Icône des objets checkpoint (logo Archipelago à la place) | liste `checkpoints` absente des anciennes seeds | icône HD `checkpoint`, avec repli sur le nom « Chapitre - Nom » | galerie 14/14 |
| Badge des fraises qui contiennent une Raspberry | — | contenu des checks demandé au serveur (`ScoutLocationsAsync`, sans créer d'indice) ; objet sans importance = logo gris-bleu foncé `cmr/apbadge_filler` (même dessin) | rendu vérifié en image ; à voir en jeu |
| Chapitre 7 dans le sélecteur | — | sur le panneau du chapitre objectif : « Objectif du rando : x / N fraises reçues, chapitre ouvert / verrouillé » | à voir en jeu |
| Clés : animation de porte | — | la clé possédée te suit dans la salle de sa porte ; la porte s'ouvre avec l'animation du jeu et reste ouverte ; la clé ne quitte pas sa salle (sinon elle ouvrirait une autre porte) | `mech groups=doors` 9/9, 1er essai |
| 2A `d4`, fraise prise au wavedash | variante Open World `dr_dash + crouch + dash haut/haut-droite/droite`, mais ici l'accroupi forcé d'un dash diagonal bas n'est pas bloqué | variante confirmée ajoutée pour cette fraise seulement : `dr_dash + (u, ur ou r)` (`cmr_extra_logic.json`, section `locations`) ; visible dans l'aide | génération OK |
| 6A, deux plumes | les plumes sont des objets interactifs, pas des checks : un seul objet « Feathers » les fait toutes apparaître | rien à corriger | — |
| Blocs retirés qui exposent une fraise | retirer un bloc ne fait que faciliter : la logique, qui exige toujours l'objet, reste prudente (prise possible « hors logique ») | rien à corriger, pour ne pas créer de fausse accessibilité | — |
| Icône d'un objet Celeste reçu en jouant à Zelda | affichée par le mod MM (c'est lui qui cherche l'icône), pas par le nôtre | à faire dans le fork MM | — |

**Seed de test du chapitre 7** : `v1/yamls/cmr_ch7`, seed n° 7301 → `v1/output/cmr_ch7_7301.zip`.
- Dashs : `octal`, les 8 dashs placés par Archipelago parmi les 8 premiers lieux (Prologue, salles 1 et 2 du 1A).
- 4 fraises dans le 1A (3 suffisent).
- Au départ : Climb, Crouch, Wall Jump, Demo Dash, les 36 objets, la clé et les 6 checkpoints du Sommet (`start_inventory` : ces objets existent aussi en double dans la seed).
- `start_inventory_from_pool` casse la génération avec cet apworld : à étudier.

**Non-régression** : mécaniques 61/61, pause 4/4, fin de chapitre 6/6, objets 108/108, chaîne en ligne 29/29 et 30/30.

## 22. Panneau de chapitre et checkpoint automatique (27/11), à vérifier en jeu par Nelem

- **Checkpoint pré-sélectionné** : le jeu prend le **dernier** tant que le chapitre n'est pas terminé (`SwapRoutine` : `RealStats.Modes[mode].Completed` faux → `option = checkpoints.Count - 1`). Sur une sauvegarde rando, ILHook `ApHooks.PatchFirstCheckpoint` : on fait comme si le chapitre était terminé pour ce choix seulement, donc le premier est pré-sélectionné. Le choix manuel ne change pas.
- **Compteur du panneau** (référence : capture du panneau Open World) :
  - sur une sauvegarde rando, le compteur de fraises du jeu devient « logo Archipelago × checks faits / checks du chapitre », d'après la seed ;
  - pour l'icône, ILHook sur `StrawberriesCounter.Render`, texture `Gui/cmr/apcounter` ;
  - le chapitre objectif affiche « Fraises : x / N » dans la partie basse du panneau.
  - Tout vient de la seed active (`goal_area`, `strawberries_required`) : rien n'est figé sur le 7A.
- Compilé. **Pas encore vérifié en jeu** : la position du texte en bas du panneau est estimée.

## 23. Extension (27/11) : variété, difficulté, configuration en jeu et sur le web

### Variété des salles
- **Mesure (10 seeds)** : 91 % des salles placées, 83 % des liens mélangés, 312 liens sur 715 à double sens. Le parcours des salles dans l'autre sens existe déjà : ce sont les types d'ouverture de rhelmot (`in`, `out`, `inout`). Ni rhelmot ni Open World ne retournent la géométrie d'une salle.
- **Le seuil fixe de 90 %** (`make_layout` s'arrêtait dès 90 % de salles placées) devient l'option **`room_coverage`** (50 à 100, 90 par défaut : les YAML existants ne changent pas). À 100 % : 1B, 3B, 2B, 4B, 7B et 9B passent à 100 %, 2A et 4A à 96-97 %, 9A à 98 %.
- **Le vrai plafond** (1A, 3A, 5A, 7A, 10A à 85-90 %) : les salles restantes sont des culs-de-sac à une seule ouverture utilisable, ou des salles dont les autres ouvertures sont `unknown` chez rhelmot (aucun chemin interne décrit). Une seconde passe de placement n'a rien changé ; elle a été retirée. Lever ce plafond demande d'écrire et de vérifier en jeu la logique de chaque ouverture `unknown`. Non fait.
- **Doublons** : une salle n'est jamais placée deux fois, sous aucune variante (`room#2`). Vérifié sur 172 cartes et ajouté à `tools/test_cmr_layout.py`.

### Difficulté
- **Constat** : `room_difficulty` (Easy à Perfect, de rhelmot) n'agissait que sur les rares salles absentes de la logique Open World. Il était presque sans effet, et un joueur en « Hard » pouvait recevoir des passages Expert ou Master.
- **Correctif** (`_Placer.tier_ok`, `hole_difficulty`) : un lien mélangé n'utilise plus une ouverture que rhelmot classe au-dessus du niveau choisi. Les liens d'origine ne sont pas filtrés (le jeu de base est finissable).
- **Salles placées par niveau** (1A, 2A, 3A, 5A, 7A, 4 seeds) : Easy 59 % (le 7A ne peut pas être mélangé en Easy : ordre d'origine) ; Normal 87 % ; Hard 89 % ; Expert à Perfect 89-90 %.
- **Presets** (Archipelago `WebWorld.options_presets`) : Beginner, Normal, Advanced, Expert, Maximum Variety. Chacun relie `room_difficulty`, `logic_difficulty`, la couverture et les mouvements mélangés. Les 5 génèrent (2 seeds chacun, victoire atteignable).

### Configuration : une seule source
- **`tools/export_options_schema.py`** lit les options dans l'apworld (classes, bornes, valeurs par défaut, groupes, presets ; textes français dans `data/options_fr.json`) et écrit `web/options_schema.json`, `web/yaml-generator.html` et `mod/.../CMRConfig/options_schema.json`. Il vérifie aussi chaque valeur de preset contre le schéma.
- **Site** `web/yaml-generator.html` (une page, marche hors ligne, français et anglais) : presets, groupes, descriptions, curseurs, validation (nom vide, valeur hors bornes) et téléchargement. Testé dans le navigateur : les 5 presets, les erreurs bloquent le téléchargement.
- **Menu en jeu** « Créer un YAML » (écran de connexion Archipelago, `AP/OuiYamlGenerator.cs`) : même schéma, presets, toutes les options avec description, écriture dans `<Celeste>/UltraRando_yaml/<nom>.yaml`. Le YAML écrit est **identique octet pour octet** à celui du site, et il génère (seed 531, victoire atteignable).
- **Bug trouvé en test** : les descriptions sur une seule ligne élargissaient le menu (4551 px) et poussaient les lignes hors de l'écran. Les descriptions sont maintenant coupées en plusieurs lignes.

### Panneau de chapitre et checkpoint automatique
- Compteur « checks du chapitre » : 7A 0/153 sur la seed 7101. Le jeu ne l'affiche que pour un chapitre déjà visité ; le rendu de l'icône sur un chapitre visité n'a pas été capturé.
- « Fraises : x / N » sous les onglets du chapitre objectif.
- Checkpoint pré-sélectionné = le premier (`chain=panel` : option 0 sur 3).

### Non-régression
Mécaniques 61/61, pause 4/4, fin de chapitre 6/6, portes 9/9, chaîne en ligne 29/29 et 30/30. Génération massive 25/25 (100 combinaisons d'options), `test_cmr_layout` : 312 cartes mélangées, 0 repli, tout OK.

## 24. Nouveau nom : UltraRando

- Nom du jeu Archipelago, apworld (`apworld/ultrarando`, `ultrarando.apworld`), mod Everest (`mod/UltraRando`, `UltraRando.zip`, DLL, namespace `Celeste.Mod.UltraRando`), YAML, site et docs. Dossier des YAML créés en jeu : `<Celeste>/UltraRando_yaml`.
- Inchangés (identifiants internes) : préfixes `CMR`/`cmr` (clés de dialogue, images `cmr/`, `CMRConfig`, commandes `cmr_*`, noms des YAML de test) et le dossier du projet.
- Les anciennes seeds (jeu « Celeste Movement Rando ») ne se connectent plus. Les sauvegardes rando de l'ancien mod restent dans `Saves/*-modsave-CelesteMovementRando.celeste` ; les réglages ont été copiés vers `modsettings-UltraRando.celeste`. L'ancien zip est rangé dans `backup_saves/ancien_nom`.
- Vérifié : `test_cmr_layout` OK, génération 4/4 (16 combinaisons), test en ligne `chain=final` réussi (victoire confirmée par le serveur).
