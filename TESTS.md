# Tests à faire en jeu

Tout est déjà installé :
- Everest ;
- le mod **Archipelago Open World 1.1.3** (déjà présent avant) ;
- le mod **Celeste Movement Helper 0.1.0** (nouveau, `D:\Games\Celeste\Mods\CelesteMovementHelper.zip`).

## Lancer une seed

1. Ouvre PowerShell dans `D:\Projets\CelesteMovementRando`.
2. Lance le serveur sur la seed voulue (la fenêtre doit rester ouverte) :

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\serveur.ps1 v1\output\brique01_dash.zip
   ```
   (le `-ExecutionPolicy Bypass` évite le blocage des scripts `.ps1` par Windows)
3. Lance Celeste (Olympus ou `D:\Games\Celeste\Celeste.exe`).
4. Sur l'écran titre, va dans le menu de connexion d'Archipelago Open World :
   - serveur `localhost:38281` ;
   - slot `Nelem` ;
   - pas de mot de passe.
5. Crée une **nouvelle sauvegarde** pour chaque seed.

## Brique 1 : le dash se débloque en changeant de salle

Seed : `v1\output\brique01_dash.zip`. Réglages : Dash Shuffle `unified` et Roomsanity. L'item **Dash** est forcé sur la location **Forsaken City A - Room 2**.

| # | Action | Résultat attendu |
|---|---|---|
| 1 | Entrer dans Forsaken City A, salle 1, et essayer de dasher | Rien ne se passe, pas de dash |
| 2 | Passer à la salle 2 | Message « Dash » reçu |
| 3 | Dasher dans les 8 directions | Toutes marchent |
| 4 | F6 dans la salle 1, puis dans la salle 2 | La page salle s'affiche. Les règles qui demandent un dash sont rouges avant l'item, vertes après |
| 5 | F6 encore une fois | Page techniques. F7 et F8 changent de technique |
| 6 | F6 encore une fois | L'aide se ferme |

Si l'étape 1 échoue (le dash marche déjà), note-le : c'est le mod Open World qui gère ce blocage, pas notre mod.

## Brique 2 : chaque mouvement séparément

Seed : `v1\output\brique02_mouvements.zip`. Réglages : Dash Shuffle `octal` (8 items), Climb Shuffle `split` et Crouch Shuffle.

Lance le serveur sur cette seed. Dans la fenêtre du serveur, donne les items **un par un** et teste chacun en jeu avant de passer au suivant :

```
/send Nelem Up Dash
/send Nelem Up-Right Dash
/send Nelem Right Dash
/send Nelem Down-Right Dash
/send Nelem Down Dash
/send Nelem Down-Left Dash
/send Nelem Left Dash
/send Nelem Up-Left Dash
/send Nelem Right Climb
/send Nelem Left Climb
/send Nelem Crouch
```

À vérifier pour chaque item :
- **avant** l'item, le mouvement est impossible ;
- **après**, il marche ;
- les autres directions restent bloquées.

Avec la page salle de l'aide (F6), la règle correspondante doit passer du rouge au vert.

## Brique 3 : item Wall Jump (brouillon, fork de l'apworld)

Seed : `v1\output\brique03_walljump.zip`, générée avec le **fork** `v1\apworld\celeste_open_world.apworld`. Wall Jump Shuffle est activé. Les dashs, le climb et l'accroupi ne sont pas mélangés.

1. Dans Celeste : **Options des mods > Celeste Movement Helper > Wall Jump Shuffle = ON**.
2. Lance le serveur sur `brique03_walljump.zip`, puis connecte-toi avec une nouvelle sauvegarde.

| # | Action | Résultat attendu |
|---|---|---|
| 1 | Contre un mur, sauter en s'éloignant (wall jump) | Rien, Madeline ne rebondit pas |
| 2 | Wallbounce (dash haut contre un mur puis saut) | Rien |
| 3 | S'agripper au mur et grimper (Grab + haut) | **Marche** : seul le wall jump est bloqué |
| 4 | F6 | La ligne « Wall Jump : bloqué » est en rouge |
| 5 | Faire les checks du Prologue jusqu'à recevoir « Wall Jump » (il y est toujours) | Message reçu, avec un nom vide : normal, le mod Open World ne connaît pas cet item |
| 6 | Refaire 1 et 2 | Les deux marchent, et F6 affiche « débloqué » en vert |

Raccourci : `/send Nelem Wall Jump` dans la fenêtre du serveur.

**Attention :** remets **Wall Jump Shuffle = OFF** pour les seeds sans ce réglage, sinon le wall jump reste bloqué pour rien.

## Commande de débogage

1. Active le mode debug d'Everest : **Options des mods > Everest > Debug Mode**.
2. Ouvre la console en jeu avec la touche `~` (ou `²` sur un clavier AZERTY).
3. Tape `cmh_items`. La commande affiche ce que le mod d'aide lit :
   - les 8 directions de dash, le climb et l'accroupi ;
   - le Wall Jump ;
   - le nombre d'items reçus.

Envoie-moi une capture de cette sortie quand quelque chose ne colle pas.

## Ce qu'il faut me rapporter

- Ce qui ne marche pas, avec une capture si possible (F6 ouvert).
- Les textes de l'aide qui débordent ou s'affichent mal (accents, taille). La taille se règle dans Options des mods > Celeste Movement Helper.
- Le fichier `D:\Games\Celeste\log.txt` en cas de crash.

## Tests automatiques (déjà passés)

- `python tools\mass_gen.py --runs 8 --players 8` : 64 combinaisons d'options aléatoires (difficulté de logique, les 5 modes de Dash Shuffle, Climb et Crouch Shuffle, sanities, B et C-sides, Farewell…). **64 sur 64 générées sans erreur.**
- Le mod d'aide se charge dans Everest : `Loaded logic for 29 levels`, `Loaded 79 techniques`, sans erreur dans `log.txt`.
