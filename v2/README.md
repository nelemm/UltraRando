# V2 : le mod final, brique par brique

La V1 (`mod/`, `v1/`) contient **tout d'un coup** : l'aide F6, les techniques, le fork de l'apworld et le blocage du wall jump. La V2 reprend ce code **une brique à la fois**. Chaque brique est validée en jeu avant de passer à la suivante, pour savoir exactement ce qui casse quand quelque chose casse.

## Méthode

1. On part de zéro dans `v2/UltraRando/`, avec un mod vide qui se charge seulement.
2. Pour chaque brique :
   - copier ou réécrire depuis la V1 **uniquement** le code de cette brique ;
   - compiler, installer et tester en jeu avec la checklist de la brique ;
   - noter le résultat dans le tableau ci-dessous ;
   - passer à la suivante seulement si c'est OK.
3. La V1 reste intacte comme référence. Elle n'est pas modifiée pendant la V2.

## Briques

| # | Brique | Code V1 à reprendre | Test | État |
|---|---|---|---|---|
| 0 | Mod vide qui se charge (`everest.yaml`, module) | `CelesteMovementHelperModule.cs` sans hook | `log.txt` : « Registered code module » | à faire |
| 1 | Dash bloqué puis débloqué en salle 2 | aucun code, seed `brique01_dash` | TESTS.md, brique 1 | à faire |
| 2 | Chaque mouvement séparé | aucun code, seed `brique02_mouvements` | TESTS.md, brique 2 | à faire |
| 3 | Lecture de l'état d'Open World | `OpenWorldBridge.cs` + une commande console `cmh_items` qui liste les items possédés | console Everest (`~`) | à faire |
| 4 | Données de logique embarquées | `LogicData.cs` + `logic.json` | `log.txt` : « Loaded logic for 29 levels » | à faire |
| 5 | Page salle F6 | `HelpOverlay.cs` (page salle seulement) | 5 à 6 salles variées, comparées au spoiler | à faire |
| 6 | Page techniques F6 | `TechniqueData.cs` + page techniques | relire 10 techniques, ouvrir les liens | à faire |
| 7 | Item Wall Jump : fork de l'apworld | `tools/make_fork.py` | `mass_gen.py` + spoiler | à faire |
| 8 | Item Wall Jump : blocage dans le mod | hook `WallJumpCheck` | TESTS.md, brique 3 | à faire |
| 9 | Logique fine du Wall Jump (passage par passage) | nouveau : un fichier d'annotations en plus | `mass_gen.py` + jeu | à faire |
| 10 | Niveau Expert (tricks liés aux techniques) | nouveau | `mass_gen.py` + jeu | à faire |
| 11 | Lien salle → technique dans l'aide | nouveau | jeu | à faire |

## À décider avant la brique 7

Le fork garde le nom de jeu « Celeste (Open World) », obligatoire pour que le mod Open World accepte la connexion. Une seed générée avec le fork ne peut donc **pas** être mélangée à une seed générée avec l'apworld officiel dans la même multiworld. Il y a deux possibilités :
- jouer en solo, ou avec des amis qui utilisent le même fork ;
- proposer l'option à PoryGone pour qu'elle entre dans la version officielle.
