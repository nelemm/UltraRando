# Interface de référence : captures d'Open World 1.1.3 faites par Nelem le 26/11/2026

Ces captures servent **de référence pour le comportement et la mise en page**. Les **images** d'Open World (ses icônes en pixel art, le logo) sont « All rights reserved » : **ne les copie pas**. Pour les icônes, utilise des textures **du jeu de base** chargées au moment de l'exécution (`GFX.Game` / `GFX.Gui` / `GFX.Journal` : `objects/spring/00`, `objects/refill/idle00`, `objects/booster/booster00`, `objects/flyFeather/idle00`, les sprites de Madeline…) ou du texte.

| Fichier | Écran | Ce qu'on reproduit |
|---|---|---|
| `01.webp` | Écran titre | Le bouton « Grimper » est **remplacé** par « Connect », avec un logo au-dessus (chez nous : texte ou icône du jeu, **pas** leur logo). Les autres entrées restent : Options, Options des mods, Crédits, Quitter. Nelem a demandé en plus qu'on puisse quand même jouer au jeu de base. |
| `02.webp` | Connexion | Plein écran sur le fond de la montagne. Trois champs en gros texte, **Address** (valeur en vert, par défaut `archipelago.gg:`), **Slot Name** et **Password**, puis un bouton **Connect**. |
| `03.webp` | Après la connexion | On arrive **directement** à la sélection des chapitres. Open World n'a pas de liste de sauvegardes et utilise un slot dédié. **Chez nous, Nelem veut une liste de sauvegardes randomizer entre les deux.** |
| `04.webp` | Journal, couverture | Le nom du slot (« Nelem ») sur la couverture. 8 pages (points en bas). |
| `05.webp` | Journal, LOCATIONS | Un tableau, une ligne par chapitre actif (noms traduits). Colonnes : 3 points (cœur / cassette / fin, gris si pas fait), puis **fraises x/y**, **checkpoints x/y**, **clés x/y** (« ● » si aucune), **jumelles** (« ● » si aucune), **salles x/y**. Puis une ligne **TOTAUX x/680** et **DEATHS**. |
| `06.webp` | Journal, KEYS | Par chapitre, la liste des clés avec leur nom au-dessus (Front Door Key…), une icône grisée tant que la clé n'est pas obtenue. Puis une ligne **Gems**, avec 6 gemmes de formes différentes. |
| `07.webp` | Journal, MOVEMENT | 4 rangées. Les icônes de Madeline sont **grisées si l'item n'est pas obtenu** et en couleur s'il l'est. Ici, une seule icône en couleur (un dash horizontal). Dernière rangée : 3 icônes grises (climb gauche, climb droite, accroupi). Chez nous, il faut aussi le **Wall Jump**. |
| `08.webp` | Journal, ITEMS | Une grille : nom de l'objet au-dessus, icône en dessous, **grisée tant que l'objet n'est pas obtenu**. Ordre : Springs, Traffic Blocks, Dash Refills, Pink/Blue Cassette Blocks, Dream Blocks, Coins, Strawberry Seeds / Sinking Platforms, Moving Platforms, Blue Clouds, Pink Clouds, Blue Boosters, Move Blocks, White Block / Swap Blocks, Red Boosters, Dash Switches, Seekers, Theo Crystal, Blue Torches, Yellow Torches / Feathers, Bumpers, Kevins, Badeline Boosters. |

Pour nous, la page LOCATIONS peut se baser sur `slot_data.location_ids` et les checks envoyés : compte par chapitre et par type (strawberry, checkpoint, key, room, heart, cassette, clear).
