# Tester le mod (phase B)

Installation : `python tools/build_rando_mod.py --install` (Celeste fermé). Le zip contient
`bin/UltraRando.dll` et `bin/Archipelago.MultiClient.Net.dll` (Newtonsoft.Json vient d'Everest).

Le journal du mod est dans `D:\Games\Celeste\log.txt`, lignes `[UltraRando/AP]`.
La console de Celeste s'ouvre avec `~` (mode debug d'Everest activé).

## 1. Test hors ligne (sans serveur)

1. Générer un slot_data « vanilla » (salles à leur place d'origine) :
   `python tools/make_vanilla_slot_data.py --areas 1a`
   → `tools/out/vanilla_slot_data.json`. Options utiles : `--areas 1a,2a`, `--owned "Dash,Climb"` (sinon tout est
   possédé), `--dash-shuffle 0..4`, `--climb-shuffle 0..2`, `--crouch-shuffle 1`, `--wall-jump-shuffle 1`,
   `--interactables`. Le fichier `<seed>_cmr_slot_data.json` écrit par l'apworld marche aussi.
2. Copier le fichier en `D:\Games\Celeste\CMR_offline_slot_data.json` (ou donner son chemin, **sans espace**).
3. Dans Celeste, ouvrir une sauvegarde, console `~` : `cmr_offline` (ou `cmr_offline D:\chemin\fichier.json`).
4. Lancer le chapitre 1 A depuis la sélection de chapitre (ou `load 1` dans la console).

À vérifier :
- `log.txt` : `layout 1a built: 38 rooms, 42 links (0 loose)` ;
- on joue la carte construite (en vanilla : identique au jeu, sans checkpoints perdus ni fraises manquantes) ;
- les trous non reliés sont fermés (barrière invisible sur les côtés, pas de transition vers le haut hors trou relié) ;
- en haut à gauche : « AP : hors ligne (test) » et un message « Check : 1a|… » à chaque salle, fraise, clé,
  checkpoint et fin de chapitre ; les mêmes lignes `[offline] check … -> id` dans `log.txt` ;
- avec `--owned` et un `--dash-shuffle` > 0 : un dash dans une direction non possédée ne part pas (son d'aide,
  dash non consommé) ; climb bloqué du côté non possédé ; pas d'accroupi au sol sans « Crouch » ; pas de wall jump
  sans « Wall Jump » ; avec `--interactables`, les objets non possédés n'apparaissent pas
  (`log.txt` : `N locked interactable(s) not loaded`).
- `cmr_status` affiche le mode, les aires actives et les items ; `cmr_disconnect` arrête la session et remet les
  cartes vanilla.

### Test de marche (transitions réelles)

Drapeau : ligne 1 = slot_data, ligne 2 = `walk`, puis `areas=1a`, `shots=<dossier PNG>`, `maxshots=10`, `vanilla=1`
(même parcours sur la carte vanilla), `from=N` / `count=N` (découper une aire longue). Madeline est placée dans le
trou de sortie et poussée hors de l'écran : c'est le code du jeu qui choisit la salle suivante. Pour chaque
transition : salle attendue / salle réelle, ressemblance des solids avec la salle vanilla du même nom, et
comparaison avec la grille de tuiles réellement chargée (`liveGrid`). Captures : `tools/out/walk_1a/`,
`tools/out/walk_1a_vanilla/`, `tools/out/proof_1a_room2*.png`.

Depuis le 26/11 (après midi) :
- **chaque lien est marché dans chaque sens logique** : champ `dir` du lien dans le slot_data (`ab`, `ba`,
  `both`) ; pour un lien sans sens logique (`none` = paire vanilla verrouillée, la logique passe par la connexion
  Open World) : sens permis par les types de trous (sortie `out`/`inout`, entrée `in`/`inout`/`unknown`) et jamais
  vers un trou `obstructed`. Les sens à sens unique sont comptés « not walked », pas en échec ;
- **Theo** (5a `e-*`) : Madeline prend Theo avant d'être poussée (comme un joueur ; sinon portes « Theo » fermées) ;
- **cinématiques** (roof00 Oshiro, 3a 08-a…) : passées une fois, après 1,5 s (passer celle de roof00 tout de
  suite ou deux fois plante le jeu) ; jamais de téléportation pendant une cinématique (rechargement complet) ;
- **sortie derrière un obstacle** (lien `obstructed` du côté de départ : serrure, mur cassable, bloc fissuré du
  temple) : les solides qui couvrent le trou sont retirés avant la poussée (le joueur les ouvre de l'intérieur) ;
- en échec : capture `FAIL_<n>_….png` (40 max) et diagnostic (position, cible de transition du jeu, solides
  autour, Theo, trous verticaux utilisés).

Lanceurs : `tools\run_offline_test.py <slot_data> walk areas=3a shots=<dossier> from=N count=N` (un lancement
borné), `tools\run_walk_all.py <slot_data> <dossier_sortie> [aires]` (enchaîne les lancements, reprend au pas
suivant d'après `log.txt`, écrit `walk_<aire>.txt` et `summary.txt`).

Résultats du 26/11 (seeds régénérées `cmr_solo_42/7/1234`, aires 0a..7a) : **toutes les transitions marchées sont
correctes** (42 : 540/540, 7 : 540/540, 1234 : 505/505 pas dirigés), voir `tools/out/walk_s<seed>/walk_<aire>.txt`
et HANDOFF.md (« Sorties du test de marche »). Le mod ferme d'une barrière l'accès à un trou `no_enter` (log :
`layout 3a built: … (0 loose, 5 closed one-way)`).

### Test automatique (sans jouer)

Créer `D:\Games\Celeste\CMR_OFFLINE_TEST` contenant le chemin du slot_data (1re ligne), et en 2e ligne `play`
(premier chapitre actif) ou `playall` (tous les chapitres actifs) pour aussi charger les cartes et visiter chaque salle.
Chaque salle est vérifiée (exception attrapée et notée par salle, puis le test continue) ; à la fin Celeste se
ferme tout seul. Au démarrage, le mod construit chaque chapitre,
(avec `play`) lance le premier chapitre actif **sur la sauvegarde debug** (les vraies sauvegardes ne sont pas
touchées), téléporte le joueur dans chaque salle (rechargement complet après un échec ou une fin de chapitre), puis écrit `CMR_OFFLINE_TEST.result` (SUCCESS/FAILURE).
Fermer Celeste et **supprimer le fichier drapeau** ensuite.
Seeds mélangées cmr_solo_42 / 7 / 1234 (0a..7a) : toutes les salles se chargent sans exception (après correction du
cache `levelsByName` d'Everest, voir `LayoutBuilder.RefreshNameCache`). Une ligne 3 optionnelle limite les aires
(`0a,1a,2a`) pour garder chaque lancement court.
Résultat obtenu (25/11/2026, layout vanilla) : 1a 38 salles, 42 liens, 38 salles visitées sans exception, 40 checks envoyés
(38 salles + 2 checkpoints) ; 0a, 2a, 3a, 1b, 9a construits ; 9a avec verrous visité sans exception.

## 2. Avec un serveur Archipelago local

1. Générer une partie avec l'apworld (`UltraRando`) puis lancer le serveur
   (`tools/serveur.ps1` ou `ArchipelagoServer.exe <partie>.archipelago`), port 38281 par défaut.
2. Celeste, écran titre (comme Open World, `docs/ref_openworld_ui/01.webp`) : « Grimper » est remplacé par
   **« Se connecter »** (icône montagne du jeu, pas le logo d'Open World) et une entrée **« Celeste (sans
   Archipelago) »** juste en dessous ouvre la liste de sauvegardes normale. Réglage « Grimper ouvre Archipelago »
   dans les options du mod (activé par défaut ; désactivé = écran titre vanilla).
   « Se connecter » ouvre l'écran plein écran (réf. `02.webp`) : **Adresse** / **Nom du slot** / **Mot de passe**
   en gros titres avec la valeur dessous (valider une valeur ouvre le clavier d'Everest), puis « Se connecter » et
   le statut ; en dessous « Sauvegardes randomizer (hors connexion) » (seed en cache) et « Jouer sans
   Archipelago ». Les champs existent aussi dans Options des mods (sans bouton de connexion : revenir au menu
   principal ferme la session AP). Console : `cmr_connect` (inchangé).
3. Connexion réussie → **liste des sauvegardes randomizer** : « Nouvelle sauvegarde » (créée et liée à la seed +
   slot), les sauvegardes existantes (`#n  slot (seed …123456) - fraises reçues, checks, temps` ; celles d'une
   autre seed sont grisées), « Mode suppression » (choisir puis valider deux fois), et « Copier l'ancienne
   sauvegarde N » si une sauvegarde normale avait été liée par une ancienne version (copie, l'originale n'est
   pas modifiée). Choisir une sauvegarde → sélection des chapitres (ou reprise dans le chapitre si on avait
   quitté en cours). Les chapitres actifs de la seed sont débloqués à la création (faces B/C si la seed en a).
4. Lancer un chapitre actif.

### Sauvegardes séparées (à quoi s'attendre)

- Une sauvegarde randomizer est un fichier Celeste normal à un **numéro négatif** : `-1000.celeste`,
  `-1000-modsave-UltraRando.celeste`… (`RandoSaves.cs`, jusqu'à 100 : -1000 à -1099), dans
  `D:\Games\Celeste\Saves`. Everest calcule la taille de la liste normale avec le plus grand numéro
  `<n>.celeste` / `<n>-mod….celeste` trouvé (+2) : un numéro négatif ne compte jamais, donc ces fichiers
  n'apparaissent pas dans la liste normale et ne l'agrandissent pas, **même mod désactivé**.
- Une sauvegarde normale (numéro ≥ 0) n'est **jamais** liée ni modifiée par le mod : pas de carte mélangée, pas de
  verrou, pas de check, pas de DeathLink, pas de reprise de cache ni de reconnexion auto, même si une ancienne
  version l'avait liée (ex. `3.celeste` de Nelem, seed 0205…/Nelem : la copier depuis la liste randomizer).
- Revenir au menu principal ferme la session Archipelago (comme Open World). Le test hors ligne `cmr_offline`
  s'applique toujours à n'importe quelle sauvegarde (outil de dev).
- Journal (réf. `04`–`08.webp`, `ApJournal.cs`) : dans une sauvegarde randomizer avec une seed active, après la
  couverture du jeu (qui affiche le nom de la sauvegarde = nom du slot) viennent 4 pages, puis les pages vanilla :
  - **LIEUX** : une ligne par chapitre actif (nom traduit), 3 points cœur / cassette / fin (icône du journal si
    fait), fraises x/y, checkpoints x/y, clés x/y, salles x/y (« ● » si aucun check de ce type), puis **TOTAUX**
    (checks faits / checks de la seed) et **MORTS** (`SaveData.TotalDeaths`). Compté depuis `location_ids` et les
    checks de la sauvegarde ;
  - **CLÉS** : par chapitre, les clés de la seed avec leur nom (« Front Door Key »…, table tirée des données de
    l'apworld Open World, Modified MIT) au-dessus de l'icône de clé du jeu, sombre tant que le check n'est pas
    fait ; ligne **Gemmes** (6 gemmes du sommet, « - » si la gemme n'est pas un check de la seed) ;
  - **MOUVEMENTS** : sprites de Madeline du jeu (dash orienté dans la direction, climb gauche/droite, accroupi,
    **Wall Jump**), sombres si l'item n'est pas reçu, avec le nom de l'item dessous. Seuls les mouvements mélangés
    par la seed apparaissent (Dash unifié = un seul sprite) ;
  - **OBJETS** : grille de 36 objets (nom au-dessus, sprite du jeu dessous, sombre si non reçu) ; si
    `interactable_shuffle` est désactivé, tout est en couleur avec une note.
  Différences avec Open World : pas de colonne « jumelles » (pas de check jumelles chez nous), « grisé » = teinte
  sombre (pas de shader de désaturation), sprites du jeu au lieu de leurs icônes, noms d'items en anglais (noms AP).

### Test automatique des sauvegardes et du journal

`tools\venv\Scripts\python.exe tools\run_saves_test.py v1\output\cmr_solo_42.zip tools\out\saves vanillafs=1 newsave=1 journal=1`
puis (2e lancement, reprise) `… tools\out\saves2 vanillafs=1 journal=1 journaldir=D:\Projets\CelesteMovementRando\tools\out level=1a`.
Aperçu du journal avec toutes les options (8 dashs, climb G/D, accroupi, wall jump, objets, quelques items
possédés) sans serveur, sur la sauvegarde **debug** (jamais écrite) :
`python tools\make_vanilla_slot_data.py --areas 1a,2a,3a,5a,7a --dash-shuffle 4 --climb-shuffle 2 --crouch-shuffle 1 --wall-jump-shuffle 1 --interactables --owned "Right Dash,Up Dash,Up-Right Dash,Left Climb,Wall Jump,Springs,Dash Refills,Feathers,Blue Boosters,Traffic Blocks" --out tools/out/journal_preview_slot_data.json`
puis `… tools\out\journal_preview offline=D:\Projets\CelesteMovementRando\tools\out\journal_preview_slot_data.json journal=1`.
Le script refuse si Celeste tourne déjà, sauvegarde `Saves` dans `backup_saves\<date>`, lance un serveur local sur
une copie de la seed (`<out>\server`), écrit le drapeau `CMR_OFFLINE_TEST` (ligne 2 `saves`), lance Celeste
(115 s max, Celeste et serveur tués dans un `finally`, drapeaux et `.result` supprimés, résultat copié dans
`<out>\result.txt`). Le mod pilote les menus par code (`SelfTest.Saves.cs`) : liste normale (capture + slots
relevés), menu principal (capture), `OnBegin` (= « Se connecter »), connexion, liste randomizer, nouvelle sauvegarde
ou reprise, sélection des chapitres, chaque page du journal (`journal_<n>_<page>.png`), chapitre (carte AP ?).
Captures = back buffer, une par fichier.

Résultats du 26/11/2026 (seed `cmr_solo_42`, serveur local, slot Nelem) :
- liste normale **identique** avant et après la création de la sauvegarde randomizer : 5 emplacements, existants
  0, 1, 2, 3 (`tools\out\saves\01_vanilla_fileselect.png`, `tools\out\saves2\01_vanilla_fileselect.png`) ;
  fichiers créés : seulement `-1000*.celeste` ; `0/1/2/3.celeste` non modifiés ;
- écran titre `tools\out\saves2\00_title_menu.png`, connexion `02_connect.png`, liste randomizer `03_rando_saves.png`
  (#1 Nelem + « Copier l'ancienne sauvegarde (fichier 3.celeste) »), sélection des chapitres `04_chapter_select.png`
  (sauvegarde -1000, liée, 7 chapitres débloqués) ;
- reprise au 2e lancement : sauvegarde -1000 rechargée, 1a = carte AP (`LEVEL 1a: AP layout YES`, `05_level_1a.png`) ;
- journal : `tools\out\journal_0_Cover.png` … `journal_4_Items.png` (seed 42) et `tools\out\journal_preview\journal_*.png`.
- Note : le mode debug d'Everest de ce PC met `Celeste.PlayMode = Debug` ; il ne doit pas désactiver le bouton.

À vérifier :
- items reçus : message « Reçu : … » ; le verrou correspondant se lève (au prochain chargement de salle pour les
  objets) ;
- checks : chaque salle / fraise / clé / gemme / cœur / cassette / checkpoint / fin de chapitre apparaît côté
  serveur (client texte ou web) ;
- hors ligne : couper le serveur, ramasser une fraise (statut « déconnecté – checks mis en file »), relancer le
  serveur et se reconnecter : le check est renvoyé (synchronisation complète à chaque connexion) ;
- items reçus pendant l'absence : rejoués à la connexion suivante (index stocké dans la sauvegarde) ;
- au chargement d'une sauvegarde liée, le mod se reconnecte tout seul (serveur et mot de passe des options, slot
  de la sauvegarde) ; sans serveur, une sauvegarde liée reprend la seed depuis le cache
  `<dossier Saves d'Everest>/UltraRando/<seed>__<slot>.slot_data.json` (normalement `D:\Games\Celeste\Saves\...`, non vérifié en jeu) (items déjà reçus, checks mis en file) ; si ni serveur ni cache : message « ATTENTION … cartes VANILLA » à l'écran ;
- DeathLink (option `death_link` du slot_data) : mourir envoie une mort ; une mort reçue tue Madeline.

## Clés des checks envoyés (contrat phase B)

- `"{aire}|{salle}|strawberry|{id}"`, `golden` (fraise dorée), `key`, `gem` (gemme du sommet, `summitgem`),
  `heart` (`blackGem`, cœur de cristal ; les faux cœurs ne comptent pas), `cassette` ;
  `id` = ID d'entité vanilla (`EntityID`, ou entité la plus proche pour cœur/cassette) ;
- `"{aire}|{salle}|room"` à chaque chargement de salle, `"{aire}|{salle}|checkpoint"` si la salle a un checkpoint
  vanilla ;
- `"{aire}|clear"` à la fin du chapitre. L'objectif (N fraises + fin de `goal_area`) est envoyé au serveur si atteint.

## Limites connues

- Direction du dash lue à l'appui : changer de direction pendant le gel d'une frame n'est pas revérifié ; la
  sortie d'une bulle (booster) n'est pas verrouillée. Un dash refusé ne consomme pas le dash.
- Crouch : seul l'accroupi volontaire au sol et le bouton crouch-dash sont bloqués ; l'accroupi forcé par le jeu
  (après un dash bas-diagonal au sol) reste possible.
- Le mode core (9a) n'est pas forcé par salle comme chez rhelmot ; il suit la sauvegarde/les interrupteurs.
- L'objectif n'est envoyé que si on est connecté au moment de finir le chapitre objectif.
- `cmr_offline` : le chemin ne doit pas contenir d'espace (parseur de commandes Everest).
- Everest affiche « CONFLICT for asset path bin/Archipelago.MultiClient.Net.dll » si Open World est aussi
  installé : sans conséquence, chaque mod charge sa copie.
- Aide F6 (portée de la V1 le 26/11) : `tools\venv\Scripts\python.exe tools\run_saves_test.py v1\output\cmr_solo_42.zip tools\out\saves3 shots=<dir> level=1a help=1 helprooms=3,6,s0`. Le test ouvre la page de la salle, puis le glossaire, et fait une capture de chacun. Le texte de la page, avec ses couleurs, est dans le rapport, pour la salle courante et pour chaque salle de `helprooms`.
un_saves_test.py v1\output\cmr_solo_42.zip tools\out\saves3 shots=<dir> level=1a help=1 helprooms=3,6,s0`. Le test ouvre la page de la salle, puis le glossaire, et fait une capture de chacun. Le texte de la page, avec ses couleurs, est dans le rapport, pour la salle courante et pour chaque salle de `helprooms`.
