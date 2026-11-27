# Couverture de la logique (26/11, soir)

Outil : `tools/logic_coverage.py` (`--difficulty`, `--areas`, `--details`).

## Ce qui est mesuré

Un **élément de logique** est une connexion entre deux régions d'une salle, ou un lieu. Il vient de la logique Open World, à laquelle s'ajoute `cmr_extra_logic.json`. On prend la règle de la difficulté choisie (`vm_rule`, `assist_rule` ou `rule`).

**Validé** veut dire que l'élément passe trois vérifications automatiques :
1. **Jetons** : chaque objet demandé par la règle existe dans le générateur et est appliqué par le mod. Ça couvre les mouvements, les 36 objets, les clés, les gemmes et `any_dash_*`.
2. **Entités** : chaque type d'objet que le mod retire et qui est présent dans la salle (`data/rooms.json`, exporté du jeu) est cité par au moins une règle de la salle. Trois exceptions justifiées :
   - les torches : elles éclairent, sans collision ;
   - les Seekers et les boules de feu ou de glace : ce sont des dangers, les retirer ne fait que faciliter.
3. **Portes** : les régions existent, et chaque porte de la logique correspond à une ouverture de la salle du jeu.

**Classé** veut dire que l'élément a une règle `cannot_access`, ou que sa salle n'est jamais placée par le mod (3b `back`, 5a `void`).

**Non vérifié** couvre tout le reste, avec la raison.

**Limite de la mesure** : c'est une mesure de **cohérence** entre la logique, le jeu et le mod. Elle ne prouve pas qu'un humain réussit chaque saut. Les règles viennent de la logique Open World, et ne sont jouées en jeu que là où passent nos tests (voir plus bas).

## Résultat

| | Éléments | % |
|---|---|---|
| Validés | 1787 | 81,6 % |
| Classés | 215 | 9,8 % |
| **Couverture (validés + classés)** | **2002 / 2189** | **91,5 %** |
| Non vérifiés | 187 | 8,5 % |

Résultats selon le périmètre :
- `vanilla_movement` et `developer_intended`, sur les 26 chapitres : 91,5 % ;
- faces A seulement (0a à 7a) : 90,6 %.

**Avant** : aucune mesure n'existait. Le « 60 % » annoncé plus tôt était une estimation de ma part, pas une mesure.

Deux corrections ont fermé de vrais écarts entre la logique et le mod :
- les **Pufferfish** (`eyebomb`) sont maintenant retirés comme la logique le suppose. Avant, le mod attendait un nom d'entité qui n'existe pas (`fish`) ;
- la documentation des entités (`ENTITY_ITEMS.md` : `eyebomb`, `twoDash`) correspond maintenant au code.

## Les 187 éléments non vérifiés

Dans ces salles, un objet retirable n'est cité par aucune règle. La logique Open World suppose alors qu'on peut passer sans lui. Il faudrait le vérifier en jeu.

| Chapitre | Salles (objet en cause) |
|---|---|
| 1a | 5, 5z, s0 (ressort) ; 7a (Traffic Block) |
| 2a | 11, 12 (Dream Block) ; 12b, end_3b (ressort) |
| 2b, 2c | 07, 01 (ressort) |
| 3a | roof00, roof06, s2 (ressort) |
| 3b | 01, 04, 11 (ressort) |
| 4a | d-04 (bulle) |
| 5a | a-05 (bulle) ; b-08 (recharge) ; c-08, d-01, d-13, d-20, e-03 (ressort) ; e-07 (Swap Block) |
| 5b, 5c | b-05, start (bulle) ; 02 (ressort mural) |
| 6a | 09, 20 (bumper) ; boss-03, -06, -07, -13, -17, -19 (ressort ou bumper) |
| 7a | b-09, c-06b, d-04 (recharge) ; c-01, c-03b, c-05, d-06, e-01c (ressort) ; e-08, f-06 (bulle) |
| 7b | c-00, c-03 (ressort) |
| 9a | a-03, b-01, b-04 (recharge) ; b-07b (bumper) ; c-00 (ressort) ; d-00, d-04, d-06 (Core Toggle : le mod force déjà le mode du Core selon le côté d'entrée) |
| 10a | a-00, c-00 (ressort) ; e-08 (Move Block) ; intro-00-past (bulle) |

## Ce qui est réellement joué en jeu

Ces tests font jouer le jeu, mais ils ne valident pas une règle à eux seuls.

| Test | Résultat |
|---|---|
| Transitions (test de marche) | seeds 42/7/1234 : 540/540, 540/540, 505/505 ; Core 66 transitions, 0 erreur |
| Checks présents dans la carte construite (recensement) | tous |
| Mécaniques (verrous de dash, Climb, Crouch, Wall Jump, Demo Dash) | 61/61 |
| Objets interactifs (absents puis présents) | 32 types, plus 4 jamais retirés par choix |
| Chaîne en ligne, checkpoints, clés, gemmes, DeathLink, synchro à deux | voir `TEST_CHECKLIST.md` §11 à §14 |

**Wall Jump** : avec `wall_jump_shuffle`, la logique demande le Wall Jump pour entrer dans tout chapitre autre que le Prologue. Ça ne peut rien bloquer à tort, mais c'est grossier : la règle n'est pas précisée salle par salle.
