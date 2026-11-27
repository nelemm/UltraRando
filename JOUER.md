# Jouer à « UltraRando » (salles mélangées + Archipelago)

## Une seule fois : préparer Olympus

Dans Olympus > **Gérer les mods installés** :
- **désactive « Archipelago Open World »**. Il embarque la même bibliothèque Archipelago que notre mod (Everest signale un conflit), et ses verrous se mêleraient aux nôtres ;
- **désactive « CelesteMovementHelper »** (l'aide F6 de la V1). Elle lit Open World, pas notre mod. L'aide est maintenant **dans notre mod**, sur la touche **Inser** ;
- laisse **« UltraRando »** activé (version 0.2.0, déjà installée).

## Lancer une partie

1. Ouvre PowerShell dans `D:\Projets\CelesteMovementRando` et lance le serveur sur une des seeds déjà prêtes (42, 7 ou 1234) :

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\serveur.ps1 v1\output\cmr_solo_42.zip
   ```
   La fenêtre du serveur doit rester ouverte pendant toute la partie.

   - **Partie avec un ami à distance** : ajoute un mot de passe, sinon n'importe qui qui atteint le port 38281 peut se connecter à un slot :
     ```powershell
     powershell -ExecutionPolicy Bypass -File .\tools\serveur.ps1 v1\output\cmr_solo_42.zip -Password tonmotdepasse
     ```
     Chaque joueur met ce mot de passe dans le champ « Mot de passe » de **Se connecter**. Ton ami se connecte avec ton adresse IP (`<ton IP>:38281`), et le port doit être joignable depuis chez lui (redirection de port sur ta box).
   - **Partie seul** : ajoute `-Local`, et le serveur n'écoute que sur ton PC.
2. Lance Celeste. Sur l'écran titre, choisis **Se connecter** :
   - Adresse : `localhost:38281`
   - Slot : `Nelem`
   - **Connecter**
3. La liste des **sauvegardes randomizer** s'ouvre. Elle est séparée de tes sauvegardes normales. Prends une sauvegarde existante de cette seed, ou crée-en une nouvelle.
4. Lance un chapitre : ses salles sont dans un ordre mélangé.

Pour jouer à Celeste sans Archipelago, prends **Celeste (sans Archipelago)** sur l'écran titre.

## Partie à deux : Majora's Mask (Nelem) + Celeste (ton ami)

Seed prête : `v1\output\coop\AP_52190015160706791300.zip`
- slot **Nelem** : Majora's Mask Recompiled, avec les mêmes réglages que ta seed MM du 25/11 ;
- slot **31459265393** : UltraRando, avec tout mélangé.

Ta partie MM en cours (`AP_72751055431831601676`) **ne peut pas être reprise** dans cette seed : un multiworld se génère en une fois. Tu recommences MM dans la nouvelle seed. L'ancienne sauvegarde n'est pas touchée.

1. Pour mettre le vrai pseudo de ton ami, change `name: Ami` dans `v1\yamls\coop\Ami.yaml`, puis régénère la seed :
   ```
   cd upstream\Archipelago
   ..\..\tools\venv\Scripts\python.exe Generate.py --player_files_path ..\..\v1\yamls\coop --outputpath ..\..\v1\output\coop
   ```
2. Lance le serveur, ouvert au réseau et avec un mot de passe :
   ```
   powershell -ExecutionPolicy Bypass -File .\tools\serveur.ps1 v1\output\coop\AP_....zip -Password <mot de passe>
   ```
   Le port 38281 doit être joignable par ton ami, par une redirection de port sur la box ou par Tailscale.
3. **Toi** : dans MM Recomp, connecte-toi au serveur (pas à la génération locale du mod) : adresse, slot `Nelem`, mot de passe.
4. **Ton ami** :
   - Celeste avec Everest et le mod `UltraRando.zip` (dossier `Mods`) ; supprime l'ancien `CelesteMovementRando.zip` et l'ancien `celeste_movement_rando.apworld` s'ils y sont encore ;
   - dans le menu du rando, « Se connecter » : `<ton IP>:38281`, slot `31459265393`, mot de passe.

En jeu Celeste, chaque objet reçu, trouvé ou envoyé s'affiche dans un grand bandeau avec l'icône Celeste de l'objet ou du check. Le nom de la salle s'affiche 4 s en bas à gauche.

**Manette** : si le stick de ta manette dérive, les dashs partent en diagonale. Les tests automatiques ignorent maintenant la manette.

## Aide en jeu (touche Inser)

- **Inser** : affiche ce que la logique attend dans la salle où tu es, région par région. En **vert**, tu as ce qu'il faut. En **rouge**, il te manque un objet, et il est nommé. En **jaune**, l'aide ne peut pas vérifier : un événement, ou pas de partie Archipelago en cours.
- **Inser** une 2e fois : le glossaire des 79 techniques (inputs, timing, sources). **O / U** passent à la technique suivante ou précédente.
- **Inser** une 3e fois : ferme l'aide.
- Sur la page de la salle, **O / U** font défiler le texte. Ces deux touches n'ont d'effet que si l'aide est ouverte.
- Pour changer les touches : **Options des mods > UltraRando > Configuration du clavier**, depuis le menu principal ou en pause. La taille du texte se règle depuis le menu principal.
- La logique affichée est celle de la seed (`logic_difficulty`). Il n'y a rien à régler.
- **F6** reste la carte de debug d'Everest (mode debug) : clic droit sur une salle pour s'y téléporter.

Réglages de ces seeds :
- logique Vanilla Movement, difficulté des salles « hard » ;
- un seul item Dash ;
- les ~35 objets (ressorts, plumes, bulles…) sont des items à trouver ;
- chapitres : Prologue à la 7A ;
- objectif : 25 fraises (la moitié des 50) + finir le Sommet.

## Créer une nouvelle seed

1. Modifie `v1\yamls\cmr_solo\Nelem.yaml` si tu veux d'autres options.
2. Génère la seed :

   ```powershell
   tools\venv\Scripts\python.exe -I tools\gen.py v1\yamls\cmr_solo --seed 123
   ```
3. Le fichier `AP_*.zip` créé dans `v1\output` est la seed à donner au serveur.

## Ce qui est testé, ce qui ne l'est pas

- **Testé automatiquement** (3 seeds) : chaque salle de chaque chapitre se charge sans erreur, y compris dans le nouvel ordre. 128 combinaisons d'options se génèrent sans échec.
- **Pas encore testé** :
  - la partie en ligne dans le jeu (connexion, réception d'items, envoi des checks) ;
  - les verrous de mouvements « à la main » ;
  - le ramassage des fraises, clés, cœurs et cassettes.

  C'est ton premier test.

## Ce qu'il faut me rapporter

- Le message d'erreur s'il y en a un.
- Une capture si la carte est bizarre : une salle mal raccordée, ou une sortie bloquée alors que la logique dit qu'elle est ouverte.
- Le fichier `D:\Games\Celeste\log.txt` en cas de crash.

## Limites connues

- **Dash :** un dash refusé ne se consomme pas, et sortir d'une bulle vers une direction bloquée reste possible.
- **Accroupi :** l'accroupi forcé par le jeu après un dash diagonal vers le bas passe quand même.
- **Salles laissées de côté :** dans certains chapitres, une partie des salles ne trouve pas sa place dans le mélange (de 60 à 100 % selon le chapitre). Leurs checks n'existent pas dans cette seed.
- **6a :** le chapitre est parfois laissé dans l'ordre d'origine (environ une seed sur trois).
- **Wall Jump :** sa logique est grossière (il ouvre tous les chapitres sauf le Prologue).
- **Fraises multiples :** quand une salle a plusieurs fraises, la logique exige de pouvoir atteindre toutes ces fraises avant de considérer l'une d'elles comme accessible. C'est prudent, mais ça rend parfois la seed plus linéaire.
- **Pas des checks :** les fraises dorées, les jumelles, les voitures, les cœurs des faces B et C, et l'Épilogue.
