# HANDOFF : UltraRando (pour la prochaine conversation Claude)

Dernière mise à jour : 26/11/2026, vers 0 h. Utilisateur : Nelem. Il écrit en français, vite. **Écoute-le quand il dit que ça ne marche pas**, et ne défends pas ta lecture des logs contre ce qu'il voit en jeu.

## 0. Règles de travail (importantes)

- Le jeu est à **`D:\Games\Celeste`** (Everest installé). Olympus est **cassé** : `main.exe` a disparu. Lance `Celeste.exe` directement.
- **Ne lance jamais Celeste sans son accord.** Vérifie toujours avec `Get-Process Celeste` avant. Chaque lancement de test doit être **borné** : tuer ton propre PID dans un `finally`, timeout d'environ 115 s, et supprimer les fichiers drapeaux `D:\Games\Celeste\CMR_*` (à l'exception du dossier `CMR_export`). Un test a déjà laissé le jeu planté et ouvert, et il a dû prévenir deux fois.
- Mods désactivés dans `D:\Games\Celeste\Mods\blacklist.txt` : `Archipelago_Open_World.zip`, `CelesteMovementHelper.zip` et `Seekuhs.zip`. L'original est sauvegardé dans `blacklist.txt.avant_cmr`.
- Licences :
  - **Mod Everest Open World** (PoryGone) : « All rights reserved ». **Ne copie ni son code ni ses images**, lis-les seulement comme référence de comportement.
  - **Celeste Modded** (CDelc) : sans licence, même règle.
  - Utilisables : rhelmot CelesteRandomizer (MIT), apworld Open World (Modified MIT : copie et modification autorisées, sans changer de licence), Archipelago.MultiClient.Net (MIT).
- Pas de commit ni de push sans demande. Le projet n'est pas un dépôt git.
- Python : `tools\venv\Scripts\python.exe` (3.13). Pour Archipelago, lance `Generate.py` **sans** `-I` depuis `upstream\Archipelago` (`tools/gen.py` le fait).

## 1. But du projet

Un randomizer Archipelago **à nous**, nom de jeu **« UltraRando »** :
- les salles sont **mélangées à l'intérieur de chaque chapitre** ;
- les **mouvements** sont des items : Dash (unifié ou par direction), Climb, Crouch, Wall Jump ;
- les **~35 objets** du jeu de base sont des items : ressorts, plumes, bulles…
- **checks** : fraises, cœurs, cassettes, clés, checkpoints, entrée de salle, fin de chapitre ;
- logique **solvable**, calée sur un joueur fort (Farewell en environ 30 min).

Pas de Strawberry Jam.

Historique : une V1 « aide F6 par-dessus Open World » existe (`mod/CelesteMovementHelper`, voir `PLAN.md`), mais elle est **abandonnée au profit du projet ci-dessus**. Nelem voulait le mélange des salles depuis le début ; il avait écrit « roomsanity » par erreur.

## 2. Architecture (détails dans `ARCHITECTURE.md`, section « Contrat figé »)

```
data/rooms.json (géométrie exportée du jeu par le mod, commande cmr_export)
   -> apworld Python apworld/ultrarando : place les salles de chaque chapitre (portage de l'algo
      Pathway de rhelmot) + logique Open World par région -> slot_data (layout, location_ids, options)
   -> mod Everest mod/UltraRando (fork rhelmot, net8.0, Archipelago.MultiClient.Net 6.7.1) :
      reconstruit chaque chapitre depuis slot_data (LayoutBuilder), verrous, checks, connexion AP
```

- Clés de checks : `"{area}|{level}|strawberry|{entity_id}"`, `"…|room"`, `"…|checkpoint"`, `"{area}|clear"`.
- Les items sont désignés **par nom** (noms Open World).
- Nombres : 52 items (IDs à partir de `0xC3D0000`) et 1 101 locations dans la table statique (IDs à partir de `0xC3D1000`). Voir `apworld/ultrarando/ID_RANGES.md`.

## 3. Fichiers clés

| Chemin | Rôle |
|---|---|
| `ARCHITECTURE.md` | contrat apworld ↔ mod (à lire en premier) |
| `JOUER.md` | guide utilisateur pour lancer une partie |
| `apworld/ultrarando/` | apworld (`core/geometry.py`, `layout.py`, `static_data.py`, `rules.py`) |
| `tools/build_apworld.py`, `tools/mass_gen_cmr.py`, `tools/test_cmr_layout.py` | build, génération en masse, test des layouts |
| `mod/UltraRando/Source/AP/*.cs` | client AP (`ApClient`, `ApState`, `ApSaveData`, `SlotData`), `LayoutBuilder`, `ApHooks` (verrous et checks), `ApMenu`, `ApHud`, `SelfTest*.cs` |
| `mod/UltraRando/EXPORT_FORMAT.md`, `ENTITY_ITEMS.md`, `TESTING.md` | docs du mod |
| `tools/build_rando_mod.py --install` | compile et installe le zip dans `Mods` |
| `tools/run_offline_test.py <slot_data> playall <areas>` | autotest en jeu, borné |
| `tools/serveur.ps1 <seed.zip>` | serveur AP local, port 38281 |
| `v1/yamls/cmr_solo/Nelem.yaml` | YAML de test |
| `v1/output/cmr_solo_{42,7,1234}.zip` (+ `_slot_data.json`) | seeds générées |
| `upstream/` | clones de référence : Archipelago 0.6.9, rhelmot, mod Open World, ExtendedVariantMode, PoryAP |
| `docs/techniques.json`, `docs/TECHNIQUES.md` | 79 techniques sourcées, pour l'aide F6 future |

## 4. Tests faits et résultats

- **apworld** : 128 combinaisons d'options générées sans échec. Appariement porte ↔ trou automatique pour 95,1 % des portes.
- **Mod, test hors ligne par téléportation** : sur les seeds 42, 7 et 1234, toutes les salles des chapitres 0a à 7a se chargent sans exception, après correction du cache `levelsByName` de `MapData` dans `LayoutBuilder.RefreshNameCache`.
- **Test en ligne par Nelem** (seed 42) :
  - connexion OK, items reçus OK, objets verrouillés retirés OK ;
  - **BUG MAJEUR : en jeu, Forsaken City ressemble exactement au jeu de base.** La 2ᵉ salle affiche le ressort et la fraise de la salle 2 d'origine, alors que les checks loggés disent `1a|10`. **Non résolu au moment de l'écriture.**
- **BUG 2** : après un relancement de Celeste sans reconnexion, une sauvegarde liée joue la carte d'origine sans rien dire. Attendu : reconnexion automatique, ou `slot_data` en cache, ou à défaut un avertissement à l'écran.

## 4 bis. Mise à jour du 26/11, vers 0 h 30 : test de marche

- **Le mélange marche quand une session est active.** Le test de marche réelle (seed 42) traverse 29 sorties sur 29 au chapitre 1a, chacune vers la salle attendue.
- **Preuve en images** :
  - `tools/out/proof_1a_room2.png` : avec le mod, la sortie de la salle 1 mène à la salle 10 ;
  - `tools/out/proof_1a_room2_vanilla.png` : dans le jeu de base, la même sortie mène à la salle 2 ;
  - les séquences complètes sont dans `tools/out/walk_1a*`.
- **Ce que Nelem a vu venait du BUG 2.** Everest n'appelle plus `LoadSaveData(int)`, donc une sauvegarde liée relancée jouait la carte d'origine.
  - **Corrigé** par `ApState.PollSave()` : il recharge le `slot_data` en cache, se reconnecte tout seul, et affiche « ATTENTION … cartes VANILLA » s'il n'y a ni serveur ni cache.
  - **Non testé en ligne.**
- **Sorties suspectes à vérifier** (soit des sorties réellement bloquées, soit une limite du test) :
  - en 3a, 3 sorties sur 42 : `06-c→02-a`, `11-c→13-a`, `roof00→12-b` ;
  - en 5a, 9 sorties sur 61. Elles touchent surtout les salles « e-* », dont les trous sont de type `out`.

  À regarder côté apworld : est-ce qu'il relie un trou `out` comme une entrée ?
- Test de marche : mode `walk` du self-test (`Source/AP/SelfTest.Walk.cs`), voir `TESTING.md`.

## 4 ter. 26/11, vers 1 h 30 : Nelem joue à Open World d'origine pour faire des captures du journal

- `blacklist.txt` est temporairement modifié : Open World est **activé**, et UltraRando et CelesteMovementHelper sont **désactivés**. La version précédente est dans `blacklist.txt.avant_ow_test` : **la restaurer** après ses captures.
- Le serveur tourne dans une fenêtre PowerShell sur `v1/output/brique02_mouvements.zip` (Open World officiel 1.1.3).
- L'agent des sauvegardes séparées et du journal (dont le code est en cours) est **en pause pour les lancements du jeu**, en attente d'un message « RESUME ».

## 5. En cours au moment de l'écriture

Un agent travaille sur le mod (BUG MAJEUR et BUG 2) :
- test de **marche réelle** : traverser les sorties au lieu de se téléporter ;
- comparaison des hachages de tuiles avec la salle d'origine de même nom ;
- captures de chaque salle traversée dans `tools/out/walk_1a/` et `tools/out/walk_1a_vanilla/`.

Si son rapport n'est pas arrivé, regarde ces dossiers et `D:\Games\Celeste\log.txt`.

## 6. Objectifs restants (par priorité)

1. **Finir le BUG MAJEUR** : le mélange doit être visible en jeu, avec une preuve par captures.
2. **BUG 2** : reconnexion automatique ou cache, avec un avertissement visible.
3. **Saves séparées** (demande de Nelem du 26/11) :
   - « Climb » (Grimper) sur l'écran titre ouvre l'écran de connexion quand le mod est actif ;
   - après la connexion, on arrive sur une **liste de sauvegardes dédiée au randomizer** ;
   - ces sauvegardes sont **totalement séparées** des sauvegardes normales : jamais dans la même liste, aucune interférence possible ;
   - le passage de la connexion à la liste se fait sans accroc.

   Référence de comportement : Open World utilise un slot de sauvegarde à part (`SaveData.TryDelete(144)` dans `upstream/OpenWorldMod/Source/UI/OuiConnection.cs` et `modMainMenu.cs`). On peut s'en inspirer mais pas copier le code. **Vérifie en jeu que la liste de sauvegardes normale n'affiche jamais une sauvegarde du randomizer.**
4. **Journal (carnet) façon Open World** :
   - une page dans le journal du jeu qui montre les mouvements débloqués (8 directions de dash, climb gauche et droite, accroupi, wall jump), les objets et les clés ;
   - l'implémentation de référence est `upstream/OpenWorldMod/Source/UI/modJournal.cs` (lecture seule). Elle utilise `OuiJournalPage.Table`, l'API du journal du jeu de base ;
   - à nous de l'écrire avec l'événement Everest du journal et **sans leurs images** : texte ou icônes du jeu de base ;
   - si la doc ne suffit pas pour reproduire l'apparence, **mets en pause et attends les captures de Nelem**. N'invente pas l'interface.
5. ~~**Intégrer l'aide F6**~~ : **fait le 26/11** (voir « Point du 26/11 », point 4).
6. **Améliorations du générateur** :
   - un mélange plus « visible » qui évite les liaisons d'origine ;
   - 6a souvent laissé dans l'ordre d'origine ;
   - certaines salles non placées (60 à 100 % placées selon le chapitre) ;
   - la logique du Wall Jump reste grossière.
7. **Limites des verrous** :
   - un dash refusé n'est pas consommé, et la sortie de bulle n'est pas verrouillée ;
   - l'accroupi forcé après un dash diagonal vers le bas passe ;
   - ~~le mode du Core (9a) n'est pas forcé par salle~~ : **corrigé et vérifié en jeu le 26/11** (test de marche, 0 `COREMODE-WRONG`). rhelmot a deux variantes par salle du 9a, chaude (`a-00`) et froide (`a-00 copy`, `a-00#2` chez nous), et `MakeLevelData` pose `CMR_coreModes` sur nos salles aussi. Mais `mechanics.cs` n'appliquait le mode que sur les cartes rhelmot (`settings != null`). Maintenant, `OnLoadLevelHook` (apparition, sauf réapparition après une mort) et `OnTransition` (selon le côté d'entrée) l'appliquent aussi quand `InGeneratedLayout` est vrai. Sur nos cartes, `OnTransition` ne fait que ça : il ne vide pas les drapeaux de session, ne remet pas l'inventaire à zéro, etc.

## 7. Décisions techniques

- **Le Python décide du layout, le mod ne fait que l'appliquer.** C'est la seule façon de garantir la solvabilité.
- Le mod garde les **noms de salle d'origine** dans la carte construite (`PostProcess`) et décale tout le layout pour que la salle de départ reste à sa position d'origine.
- Clés : objets du pool avec `keysanity` (par défaut), sinon celles du jeu. Theo, les graines, les torches jaunes et le disjoncteur de Farewell ne sont jamais retirés. Le désordre du 3A et le disjoncteur restent faits d'une session à l'autre (`KeptState`).
- Un fork modifié de l'apworld Open World (item Wall Jump) existe dans `v1/apworld`. Il ne sert qu'à la V1 et n'est plus prioritaire.

## Mise à jour 26/11/2026 : saves séparées / journal

Fait (code, `mod/UltraRando/Source/AP/`) :
- `ApMenuHooks.cs` : écran titre façon Open World (`docs/ref_openworld_ui/`) : « Grimper » devient « Se connecter »
  (icône montagne du jeu) et ouvre `OuiApConnect` ; nouvelle entrée « Celeste (sans Archipelago) » = liste normale.
  Réglage `ClimbOpensArchipelago` (défaut oui). Attention : le mode debug d'Everest de ce PC met
  `PlayMode = Debug`, il ne doit pas couper le bouton (bug corrigé). Retour au menu principal = session AP fermée.
- `OuiApConnect.cs` : plein écran Adresse / Nom du slot / Mot de passe (gros titres, valeur dessous), « Se connecter »,
  statut, « Sauvegardes randomizer (hors connexion) », « Jouer sans Archipelago ».
- `OuiRandoSaves.cs` + `RandoSaves.cs` : liste dédiée (nouvelle, charger, mode suppression avec confirmation,
  copie d'une ancienne sauvegarde normale liée). Les sauvegardes randomizer sont aux **numéros négatifs -1000…-1099**
  (`-1000.celeste`…) : Everest ignore les numéros négatifs pour dimensionner la liste normale, donc elles n'y
  apparaissent jamais (même mod désactivé). Vérifié dans le code d'Everest (IL de `OuiFileSelect.Enter`/`LoadThread`,
  `SaveData.GetFilename`, `UserIO.Load`).
- `ApState` : une sauvegarde normale (numéro ≥ 0) n'est plus jamais liée ni jouée en AP (ni reprise de cache,
  ni reconnexion auto, ni DeathLink). **Conséquence** : l'ancienne sauvegarde `3.celeste` (seed 0205…/Nelem) ne
  joue plus la seed ; la copier depuis la liste randomizer (« Copier l'ancienne sauvegarde (fichier 3.celeste) »).
- `ApJournal.cs` : pages façon Open World après la couverture (nom du slot) : LIEUX (tableau par chapitre +
  TOTAUX + MORTS), CLÉS (clés nommées par chapitre + gemmes), MOUVEMENTS (sprites de Madeline, sombres si non
  reçus, Wall Jump compris), OBJETS (grille de 36 objets). API du journal du jeu + textures du jeu chargées à
  l'exécution (`GFX.Journal/Gui/Game`), rien d'Open World. Textes FR/EN (`Dialog/French.txt` créé, clés `CMR_*`
  dans `English.txt` ; les paramètres sont `@0`, `@1` car `[..]` et `{..}` sont des commandes de dialogue).
- Autotest `saves` (`SelfTest.Saves.cs`, `tools/run_saves_test.py`, option `offline=` pour l'aperçu du journal),
  voir TESTING.md.

Testé en jeu le 26/11 (lancements bornés, sauvegarde de `Saves` dans `backup_saves\` avant chaque lancement) :
liste normale identique avant/après (5 emplacements, 0 à 3), sauvegarde randomizer créée en `-1000`, liste randomizer
OK, reprise au 2e lancement avec la carte AP de 1a, captures de chaque écran et page du journal dans `tools\out\`
(détails et chemins : TESTING.md, « Résultats du 26/11/2026 »).
Reste / non vérifié :
- la saisie au clavier dans les champs de connexion (le test règle les valeurs par code) ;
- le mode suppression et la copie de `3.celeste` (pas cliqués par le test) ; le mode hors connexion (cache) via
  le nouvel écran ;
- « grisé » = teinte sombre, les sprites tournés du dash ne sont pas parfaitement centrés ; le bouton « Movement
  Rando » de rhelmot est toujours dans le menu principal (hérité, à retirer si Nelem veut) ;
- la sauvegarde de test `-1000*.celeste` (seed 42, slot Nelem) reste dans `Saves` : la supprimer depuis la liste
  randomizer (mode suppression) si besoin.

## Point du 26/11 (agent principal) : prochaines étapes, par priorité

1. ~~**Sorties suspectes du test de marche**~~ : **fait**, voir la section « Sorties du test de marche (26/11, après-midi) » plus bas.
2. ~~**Ménage dans le mod**~~ : **fait le 26/11**.
   - Ancien bouton « Movement Rando » de rhelmot retiré : l'abonnement `OnCreateButtons += CreateMainMenuButton` est commenté dans `Source/Patches/menuLifecycle.cs`. Vérifié sur la capture `tools/out/saves3/00_title_menu.png`.
   - Icônes du journal : `ApJournalPageBase` dessine maintenant le rectangle découpé (`ClipRect`) centré sur sa propre moitié, au lieu de `MTexture.DrawCentered`. Avant, le pivot était le cadre 32×32 avec sa marge, et `DrawOffset` n'était pas inversé avec le retournement. Comparaison avant/après : `tools/out/journal_preview/journal_3_Movement.png` contre `tools/out/journal_preview2/journal_3_Movement.png`.
   - Le test en ligne (`tools/out/saves3`) a créé la sauvegarde randomizer **-1001**, neuve et liée à la seed 42 actuelle. Le serveur la voit sous le même nom de seed que la sauvegarde -1000.
3. ~~**Non testé**~~ : **testé le 26/11** par des tests automatiques en jeu (`SelfTest.Saves.cs`, `tools/run_saves_test.py`).
   - **Copie de l'ancienne sauvegarde 3** (`manage=1`) : la copie devient la sauvegarde -1002. Elle est identique fichier par fichier (SHA-256) et l'original n'est pas modifié. Elle apparaît dans la liste (« 3 fraises, 22 checks ») et on peut la jouer. Le test l'a ensuite supprimée, et `3.celeste` est toujours là : Nelem peut encore l'importer depuis la liste.
   - **Mode suppression** (`manage=1`) : au 1er appui, le jeu demande une confirmation et le fichier reste. Au 2e appui, tous les fichiers `-1002*.celeste` sont supprimés et l'entrée disparaît de la liste. `RandoSaves.Delete` appelé sur un emplacement vanilla (3) ne fait rien.
   - **Jeu hors ligne depuis la liste** (`offlinelist=1 noserver=1 level=1a`), sans aucun serveur : la liste « Hors connexion » montre les 2 sauvegardes, jouables grâce à la seed en cache. Le chargement passe en mode `Cached`, l'emplacement -1000 est bien lié à sa seed, et le chapitre 1A a bien les salles mélangées (capture `tools/out/saves5/09_offline_list.png`).
   - **Saisie au clavier** : pas testée par code. Les champs ouvrent `OuiModOptionString` d'Everest, le même écran que les champs texte des options de mod, avec lesquels Nelem s'est déjà connecté.
   - Reste : une partie en ligne complète jouée par Nelem avec la nouvelle interface.
4. ~~**Intégrer l'aide F6**~~ : **fait le 26/11**.
   - Fichiers : `Source/AP/HelpData.cs` et `Source/AP/HelpOverlay.cs`. Les données sont dans `mod/UltraRando/HelpData/` : `logic.json` est réécrit aussi par `tools/export_help_data.py`, et `techniques.json` est une copie de `docs/techniques.json`. Les deux sont intégrés au DLL comme `CMR.help_logic.json` et `CMR.techniques.json`.
   - L'aide lit `ApState`, avec la même sémantique que les verrous : les dashs passent par `ApHooks.DashAllowed` (modes 0 à 4), puis le climb selon `climb_shuffle`, l'accroupi, les objets si `interactable_shuffle`, et les clés et gemmes par leur nom d'item. Les événements (désordre de l'hôtel, disjoncteur) s'affichent en jaune.
   - La logique vient de `slot_data.options.logic_difficulty`. Il n'y a plus de réglage manuel comme en V1.
   - Les touches et la taille du texte sont dans `RandoModuleSettings` (`HelpKey`, `HelpNextKey`, `HelpPreviousKey`, `HelpTextScale` ; voir « Touches » ci-dessous). Les textes `CMH_*` sont ajoutés à `Dialog/French.txt` et `Dialog/English.txt`.
   - Test : `run_saves_test.py … level=1a help=1 helprooms=3,6,s0`. Il capture `06_help_room.png` et `07_help_techniques.png`, et le rapport contient le texte de la page avec sa couleur. Vérifié le 26/11 sur la seed 42 en ligne : dashs absents en rouge, Left Climb en vert, Traffic Blocks manquants en rouge.
   - **Touches (26/11)** : par défaut **Inser** pour afficher, **O** pour suivant et **U** pour précédent. O et U n'agissent que si le panneau est ouvert. Le choix vient d'un scan Cecil des appels avec une touche fixe et des `DefaultButtonBinding`, dans `Celeste.dll` (jeu de base et Everest) et CelesteNet.Client. Touches prises : F1/F2/F3 (debug du jeu de base), F5/F6 (carte de debug), Home/End/PageUp/PageDown/Delete (chat CelesteNet, champs texte). F4 et F7–F12 ne sont pris par aucun code, mais les réglages d'un ancien SpeedrunTool (désinstallé) les utilisaient. I était la touche de dash de Nelem : une valeur F6 ou I déjà enregistrée est remplacée par Inser au chargement (`HelpOverlay.MigrateHelpKey`). En jeu, `CreateModMenuSection` ajoute maintenant `CreateModMenuSectionKeyBindings`, ce qui donne la configuration du clavier et de la manette en pause. Vérifié par le test : `help keys: Insert next O previous U`, et la liste des entrées du menu, en jeu et hors jeu, est dans le rapport.
   - **Destinations dans la seed (26/11)** : `HelpOverlay.AddSeedExits` ajoute la section « Où mènent les ouvertures dans cette seed ». Pour chaque liaison de `slot_data` touchant la salle, elle affiche le côté (`StaticRoom.Holes[i].Side`), la salle d'arrivée et le sens logique (`dir`, et `no_enter` = « sortie seulement »). Le résultat est mis en cache par salle. L'ancienne ligne s'appelle maintenant « Portes de la logique » (noms des régions d'Open World).
5. **Générateur** : mélange plus visible, 6a souvent laissé dans l'ordre d'origine, salles non placées, logique du Wall Jump.
   - **Mesure du 26/11** (`tools/layout_stats.py --seeds 6`, logique VM, salles « hard ») : en moyenne, 83 % des salles sont placées et 18 % des liaisons sont celles du jeu de base. Les chapitres faibles : **6a** (3 seeds sur 6 retombent sur la carte d'origine ; sinon 33 % des salles placées), **3a** (65 %, minimum 38 %), 5a (76 %), 1a (78 %), 5b (79 %), 10a (81 %). Les faces C (3 salles) gardent toujours leurs liaisons d'origine.
   - **Cause (6a)** : le placement est glouton, par emboîtement des ouvertures. Les salles du 6a ont des ouvertures de hauteurs et de tailles très différentes. Les sorties droites ne trouvent aucune salle compatible, et l'emplacement du voisin d'origine est déjà occupé par une branche mélangée. Sur la seed 0, la carte s'arrête à 12 salles sur 64 et la fin (`after-01`) n'est pas atteignable.
   - **Essais sans effet (retirés)** : (1) placer le voisin d'origine quand rien d'autre ne rentre : 0 gain, sa place est toujours prise ; (2) essayer ce voisin d'origine en premier avec une probabilité de 0,2 à 0,8 : pas de gain net sur 6a, 3a et 5a (30 seeds chacun).
   - **Pistes sérieuses** : (a) des « transitions-téléport » dans le mod, pour qu'une sortie mène à une salle posée ailleurs (comme `place_anywhere` le fait déjà pour les warps) ; c'est un gros chantier côté mod ; (b) une recherche avec retour arrière dans `_Placer` à la place du glouton.
   - **Relecture de rhelmot (26/11)** :
     - `RandoLogic/LogicPathway.cs`, `LogicLabyrinth.cs`, `RandoTask.cs` et `Receipt.cs`. rhelmot **ne cherche jamais à placer toutes les salles d'un chapitre**. Le mode Pathway construit un chemin d'une longueur visée (`Worth` 40 à 180), et le mode Labyrinth s'arrête entre 20 et 90 salles. Dans les deux cas, les salles sont tirées d'un réservoir (souvent plusieurs chapitres), et celles qui ne rentrent pas ne sont pas utilisées.
     - rhelmot revient en arrière : chaque tâche garde des « reçus » qu'il peut annuler, avec 100 à 1000 retours avant de tout recommencer.
     - Les warps (`CustomWarp`) ne servent qu'à quelques transitions de cinématique.
     - Notre exigence (toutes les salles, donc tous les checks) est plus forte que la sienne.
     - Notre `compatible()` est une copie exacte de `Hole.Compatible`.
   - **Diagnostic mesuré** :
     - Compatibilité : chaque sortie a de nombreuses salles compatibles (6a : 27 en moyenne, aucune sortie sans candidate).
     - Placement : **100 % des rejets viennent du chevauchement** (6a, 3a, 4a).
     - **6a** : la salle racine `start` est une colonne de 320×3160 px sans aucune sortie ni liaison d'origine (cinématique d'intro, quittée par un warp). Placée en premier à sa position d'origine, elle bloque la croissance vers la droite, alors que le 6a a 48 sorties droites contre 24 gauches.
     - **3a** : le placeur traversait les portes à clé de façon optimiste, puis `prune_unreachable` retirait tout ce qui se trouvait derrière une clé posée derrière sa propre porte (par exemple 37 salles posées, 3 gardées).
   - **Corrections (26/11, dans `core/layout.py`)** :
     1. Une racine sans sortie ni liaison d'origine est posée **en dernier**, à un endroit libre (`_detached`, `place_free`). La carte est ensuite **translatée** pour que la salle de départ garde sa position d'origine (`_keep_start_in_place`). Exception : si c'est la seule racine (10a `intro-00-past`), on garde l'ancien comportement.
     2. Le placeur ne pousse plus que depuis les sorties **vraiment atteignables** avec la logique complète, clés et événements compris (`_Placer.STRICT`, `evaluate(build_graph(...))` à chaque tour, environ 2 fois plus lent : 0,7 s par tentative pour 7a).
     3. Un placement est refusé si les deux ouvertures ne se touchent pas (`hole_touch_span`). Ce bug existait déjà : l'ouverture 2 de 7a `e-11` déclare un `launch` (34) hors de son ouverture (58-60).
   - **Résultat** (`layout_stats.py --seeds 6`) : **88 % des salles placées (83 % avant), 0 repli** sur la carte d'origine (3 avant). 6a passe de 33 % à 88 %, 3a de 65 % à 82 % (minimum 38 % → 71 %), 5a de 76 % à 83 %, 5b de 79 % à 89 %. `test_cmr_layout.py --seeds 4` : 312 cartes, tous les contrôles OK.
   - Les racines détachées sont **exclues des salles qu'on peut brancher** (`self.detached` dans `candidates`). Sans ça, le test de marche de la seed 42 a montré `b-02b` → `start` (6a) : la transition ne se termine jamais, parce qu'on n'entre dans `start` que par l'apparition au début du chapitre. `test_cmr_layout.py` vérifie maintenant qu'aucune racine détachée n'est reliée.
   - **Nombre de tentatives** : `make_layout` fait maintenant `max(24, 8000 // nb_salles)` tentatives (s'arrête dès 90 %). Le nombre dépend de la taille du chapitre et jamais du temps : une seed donne toujours la même carte. Mesuré (`layout_stats.py --seeds 4`) : **91 % des salles placées** en moyenne, le pire chapitre est 10a à 83 %. Temps : environ 10 s par joueur au total (5a 2,8 s, 7a 1,8 s, 10a 1,6 s, 3a 1,5 s, 1a 1,2 s).
   - 1a plafonne à 33 salles sur 38, même sur 120 tentatives. Les salles qui manquent sont surtout des culs-de-sac à une seule ouverture (9c, s1, 12z, 10zb), qui arrivent quand plus aucune sortie n'est libre.
6. **Sauvegarde de test** : `-1000.celeste` (seed 42, slot Nelem) est toujours dans `D:\Games\Celeste\Saves`. On peut la supprimer depuis la liste du randomizer.

Les captures (`tools/out/…`) servent **à notre autovérification**. Nelem ne veut pas qu'on les lui envoie, pour économiser les tokens.

## Notifications et nom de salle (26/11, nuit) : voir `docs/TEST_CHECKLIST.md` §14

- **`ApHud`** :
  - le grand bandeau (`Toast`, une notification à la fois, la file accélère quand elle s'allonge) ;
  - `ItemIcon` et `LocationIcon`, qui réutilisent les icônes du journal ;
  - le nom de salle pendant 4 s après la transition.
- **`ApClient`** : `MessageLog.OnMessageReceived` traite les `ItemSendLogMessage` (reçu, trouvé, envoyé), mais pas les indices.
- **`tools/logic_coverage.py`** et `docs/LOGIC_COVERAGE.md` : couverture de la logique.
- **`v1/yamls/coop`** : MM, reconstruit depuis le spoiler de la seed MM de Nelem, plus Celeste pour l'ami. L'apworld MM est copié dans `upstream/Archipelago/custom_worlds`.

## Clés = objets et état de session gardé (26/11, soir) : voir `docs/TEST_CHECKLIST.md` §12

- **apworld** :
  - option `keysanity` et `Items.KEY_ITEMS` (ID `0xC3D0200+`) ;
  - les événements de clé sont remplacés par des objets du pool ;
  - `_lock_keys` produit `slot_data.locks` ;
  - `teleport_in_logic` est vrai avec `keysanity`.
- **mod** :
  - `ApHooks.OnLoadLevel` ne charge plus les `lockBlock` dont la clé est possédée ;
  - `ApState.KeepState` et `RestoreState` (hook `Session.SetFlag`, gemmes dans `OnGemSmash`, restauration dans `OnLevelLoaderCtor`) ;
  - champ `ApSaveData.KeptState`.

## Checkpoints = objets (26/11, soir) : voir `docs/TEST_CHECKLIST.md` §11

- **apworld** :
  - `Items.CHECKPOINT_ITEMS` (ID `0xC3D0100+`) ;
  - `__init__.create_regions` : arête `Menu -> <checkpoint>` avec `Has(objet)`, sauf dans les chapitres qui ont des événements de session (`g.events`) ;
  - plus aucun lieu `checkpoint` ;
  - `slot_data.checkpoints` = {objet : "zone|salle"}.
- **mod** :
  - `SlotData.Checkpoints` ;
  - `ApState.CheckpointLocked` et `ApplyCheckpoints` ;
  - hook `SaveData.SetCheckpoint`.
- **Testé** : en jeu (bloqué → objet → débloqué → départ → check, puis relance), 8 seeds solo et 3 seeds à deux, génération massive, non-régression.

## Audit final et corrections hors logique (26/11, soir) : voir `docs/TEST_CHECKLIST.md`, §9 et §10

- **Corrigé** :
  - `TrySendGoal` : l'objectif est envoyé à la fin du chapitre, à chaque fraise reçue, ou après une reconnexion ;
  - recharges doubles : attribut `twoDash` ;
  - `serveur.ps1` : options `-Password` et `-Local` ;
  - trace de débogage retirée.
- **Ajouté pour les tests** :
  - `tools/ap_test_client.py` : un deuxième joueur sans Celeste ;
  - seeds `cmr_goal_31` et `cmr_sync_41`, YAML `v1/yamls/cmr_goal` et `v1/yamls/cmr_sync` ;
  - groupes de test `interact`, `gems`, `scan` et `checkpoint` ;
  - scénarios `chain=goal` et `chain=sync`.
- **Différences avec le design voulu par Nelem, non traitées (logique, après ce soir)** :
  - les checkpoints sont des lieux en plus, sans objet « checkpoint » dans le pool, et la logique ne compte pas sur la téléportation (Open World : un lieu **et** un objet par checkpoint, plus une arête du menu vers le checkpoint quand on a l'objet) ;
  - Demo Dash + Farewell (demodashes voulus par le jeu, absents de la logique d'Open World) ;
  - sens de « Left Climb » à recouper avec Open World.
- **Sauvegardes randomizer de test** dans `Saves` : de -1002 à -1006 (créées par les tests du jour, liées aux seeds de test). On peut les supprimer depuis la liste du jeu.

## Tests en jeu des checks, des dashs et du Demo Dash (26/11, nuit) : voir `docs/TEST_CHECKLIST.md`

- **Infrastructure** :
  - `SelfTest.Mech.cs` injecte de vraies touches sous `MInput` (les réglages de Nelem) et mesure ce que fait Madeline. Groupes : `basic` (61 tests), `combos` (256 combinaisons × 8 directions), `shaftskip` / `shaftreal` (3A `04-d`), `probe` (carte ASCII) et `explore` (entités et captures).
  - `SelfTest.Chain.cs` : chaîne en ligne (check en jeu → objet du serveur → capacité utilisée → transition, puis relance du jeu). Seed `v1/output/cmr_chain_77.zip`, YAML `v1/yamls/cmr_chain` (salles d'origine, plando).
  - Les deux tournent avec `run_offline_test.py … mech …`, ou `run_saves_test.py … chain=1|2 noserver=1 server=127.0.0.1:38281`, avec un serveur `MultiServer.py --host 127.0.0.1` lancé à part.
- **Demo Dash (nouveau)** :
  - objet `0xC3D001E`, option `demo_dash_shuffle` ;
  - verrou dans `ApHooks.OnCorrectDashPrecision` : sans l'objet, un dash accroupi qui ne va pas vers le bas relève Madeline ;
  - logique ajoutée par `data/cmr_extra_logic.json`, lue par `static_data._apply_extra_logic` et par `export_help_data.py` : 3A `04-d` `south → south-west` = Demo Dash + Left Dash + Left Climb, en VM et en Assist seulement ;
  - prouvé en jeu : climb jump sur la face de la marche, puis Demo Dash à y = 124. Ça ne passe plus si le Demo Dash ou la grimpe est verrouillé, ni depuis un saut au sol.
- **Bugs corrigés** :
  - changement de direction pendant le gel (dash vers une direction verrouillée) ;
  - dash dans l'eau sans verrou ;
  - tweaks de rhelmot qui retiraient des cassettes et des cœurs (`StaticRoom.CMR_KeepCollectibles`) ;
  - clé du cœur du 1A (`birdForsakenCityGem`, avec un repli sur l'unique entité de la salle) ;
  - `gen.py` ne reste plus bloqué sur « Press enter ».
- **Pas encore validé en jeu** : 33 types d'objets interactifs, les gemmes du 7A, la sortie de bulle, le multijoueur en ligne et le DeathLink (§8 de la liste).

## Test de marche de tous les chapitres (26/11, soir)

- **Seed complète** `v1/output/cmr_full_99.zip` (+ `_slot_data.json`), YAML `v1/yamls/cmr_full/Nelem.yaml` (cmr_solo avec faces B et C, Core et Farewell), 26 chapitres actifs. Marche réelle de toutes les liaisons :
  - 1b 17/17, 1c 2/2, 2b 19/19, 2c 3/3, 3b 30/30, 3c 3/3, 4b 27/27, 4c 4/4, 5b 26/26, 5c 4/4, 6b 27/27, 6c 2/2, 7b 33/33, 7c 2/2, 9a 43/43, 9b 28/28, 9c 2/2, **10a 121/121**. Tout est OK.
  - Le seul échec (10a `j-00` → `c-02`) était une limite du test : `j-00` a un `CrumbleWallOnRumble` contre sa sortie est, qui tombe sur un événement de la salle (c'est la même sortie que vers `j-00b` dans le jeu de base). Le test le retire maintenant comme les autres obstacles (`Breakable` dans `SelfTest.Walk.cs`).
- **Mode du Core vérifié** : la marche compare maintenant `level.CoreMode` au mode attendu pour le côté d'entrée (`CMR_coreModes`), et écrit `core=<réel>/<attendu>` (`COREMODE-WRONG` en cas d'écart). Sur 9a, 9b et 9c : 68 arrivées, 27 froid, 39 chaud, 2 sans mode, toutes justes.
- **Multijoueur (génération)** : un multiworld à 3 joueurs (Nelem en CMR, Risk of Rain 2 et Celeste Open World), `--seed 5`, s'est généré en 10 s. Dans le spoiler, 229 objets CMR sont placés chez les autres et 227 objets des autres chez Nelem (par exemple, Dash de Nelem sur une fraise de Celeste Open World). La partie en ligne à plusieurs n'est pas testée en jeu.
- Seeds 42/7/1234 régénérées avec le placeur actuel (racines détachées, croissance stricte, `8000 // salles` tentatives) ; les anciennes sont dans `v1/output/avant_correctif_6a/`.

## Sorties du test de marche (26/11, après-midi) : verdicts et corrections

Verdict pour chaque sortie signalée (seed 42 d'avant, rejouée avec le test amélioré, captures `tools/out/walk42_*`) :
- **3a `06-c→02-a`** : **vrai problème de logique (a)**. Le trou est de 02-a est bouché par une serrure (`lockBlock`)
  juste derrière : la transition gèle (Madeline poussée dans le solide, écran bloqué). L'apworld comptait ce trou
  comme une entrée. Sens inverse `02-a→06-c` : sortie derrière la serrure (à ouvrir de l'intérieur avec la clé).
- **3a `11-c→13-a`** : **(a)**, même cas avec le désordre de 3a (`ClutterBlock`, `req_in` rhelmot = drapeau). Idem
  `10-c→12-x` dans la même seed.
- **3a `roof00→12-b`** : **limite du test (c)** : la cinématique d'Oshiro (roof00) garde Madeline en `StDummy`.
  Passe une fois la cinématique passée (idem 3a `08-a`).
- **5a `d-01→a-03`** : sortie derrière un bloc fissuré du temple (porte Open World `blocked`) : **(c)** dans ce sens
  (le test téléportait Madeline dans le bloc) ; le sens inverse `a-03→d-01` gèlerait : **(a)**, corrigé.
- **5a `e-02→a-02`, `e-08→b-02`, `e-07→a-00d`, `e-01→b-01b`, `e-05→e-09`, `e-09→c-08`, `e-00→b-17`** :
  **bug du mod (b) + limite du test (c)**. Le trou droit `out` des salles Theo n'est franchissable qu'en portant
  Theo (`Level.EnforceBounds`, vérifié dans l'IL) ; les correctifs de rhelmot (`DontBlockOnTheo`,
  `BeGracefulOnTransitions`, `TheoGateBW`) n'étaient actifs que pour SES cartes (`InRandomizer`), pas pour les
  nôtres : un Theo posé dans une salle quelconque bloquait sa sortie droite. Le test, lui, ne portait pas Theo.
  L'apworld, lui, était correct (ces trous ne sont jamais utilisés comme entrée).
- **5a `b-01→b-20` (Up)** : **(c)** : paire vanilla verrouillée, trou `b-01#1` (`in`) ↔ `b-20#2` (`out`) = sens
  unique vers le bas ; l'ancien test le marchait vers le haut.

Corrections :
- **apworld** (`core/layout.py`) : les trous `unknown` ne servent plus jamais à la logique ; nouvelle
  `entry_obstructed()` : un trou dont l'entrée est bouchée (porte Open World `blocked` ou `req_in` rhelmot avec
  clé/drapeau) est **sortie seulement** pour la logique (21 portes, ~30 trous). slot_data : champs de lien `dir`
  (sens logiques), `obstructed`, `no_enter` (liens non vanilla seulement). Taux de mélange inchangé (232/234).
- **mod** : `no_enter` → barrière (`blockHole`) du côté de la salle voisine (`LinkedRoom.CMR_NoEnterHoles`,
  `LayoutBuilder`, log « N closed one-way ») ; correctifs Theo de rhelmot actifs aussi sur les cartes AP
  (`InGeneratedLayout`, `qol.cs`, `mechanics.cs`).
- **test de marche** : voir `mod/.../TESTING.md` (chaque lien dans chaque sens logique, Theo porté, cinématiques
  passées, obstacles ouverts avant une sortie, captures `FAIL_*`). `tools/run_walk_all.py` enchaîne les lancements.
- `tools/test_cmr_layout.py` et `tools/mass_gen_cmr.py --output` vérifient les sens logiques / `no_enter`.

Résultats (seeds régénérées 42/7/1234, mêmes noms ; l'ancien `cmr_solo_42.apsave` est renommé
`cmr_solo_42.apsave.bak_avant_26-11`) : test_cmr_layout OK, mass gen 2 × 80 combinaisons, 0 échec.
Marche réelle, **toutes les transitions OK**, pas dirigés (sens à sens unique non marchés) :

| seed | 0a | 1a | 2a | 3a | 4a | 5a | 6a | 7a |
|---|---|---|---|---|---|---|---|---|
| 42 | 12/12 | 48/48 | 69/69 | 74/74 | 79/79 | 109/109 | 15/15 | 134/134 |
| 7 | 12/12 | 48/48 | 73/73 | 48/48 | 75/75 | 108/108 | 49/49 | 127/127 |
| 1234 | 12/12 | 49/49 | 67/67 | 59/59 | 77/77 | 101/101 | 9/9 | 131/131 |

Détails : `tools/out/walk_s{42,7,1234}/walk_<aire>.txt` (+ logs et captures d'échec). Limites restantes : le test
ne vérifie pas les mouvements à l'intérieur des salles (seulement que chaque trou relié mène à la bonne salle) ;
en jeu, un Theo emporté hors d'une salle `e-*` peut être lâché n'importe où (comme chez rhelmot). Un lien 2a
(`9:2 - 9b:6`, paire vanilla) est « loose » (géométrie vanilla différente de l'alignement rhelmot) mais se traverse.
