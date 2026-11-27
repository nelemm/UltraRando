# Glossaire des techniques de déplacement - Celeste

Version lisible de `techniques.json`. Explications reformulées; voir les sources pour le détail. Vitesses en pixels/seconde, frames à 60 fps.

Sources principales: Celeste Wiki (celeste.ink), The Celeste Dictionary (document communautaire), article "Celeste & Forgiveness" de Maddy Thorson.

## Bases

### Jump
*Requiert:* jump  
*En vanilla:* Prologue  

Le saut de base. Celeste est tolérant: on peut encore sauter quelques frames après avoir quitté une plateforme, et un appui juste avant l'atterrissage déclenche le saut dès que Madeline touche le sol. Maintenir Saut au sommet du saut réduit la gravité, ce qui donne plus de temps pour ajuster la réception.

**Entrées:**
1. Sur le sol (ou pendant le coyote time), appuyer sur Saut.

**Timing:** Utilisable jusqu'à 5 frames après avoir quitté un rebord (coyote time); un appui un peu avant l'atterrissage est mis en tampon.

**Sources:** [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Variable jump height (non vérifiée)
*Requiert:* jump  

La hauteur du saut dépend de la durée d'appui sur Saut: relâcher tôt donne un saut plus court. Les sources consultées ne détaillent pas ce mécanisme (seul le fait que maintenir Saut divise la gravité par deux au sommet est documenté), d'où le statut non vérifié.

**Entrées:**
1. Appuyer sur Saut et le relâcher tôt pour un petit saut, ou le maintenir pour un saut complet.

**Timing:** Non documenté précisément dans les sources consultées.

**Sources:** aucune source fiable trouvée.

### Grab / Climbing
*Aussi appelé:* climb, wall grab  
*Requiert:* grab, climb_left, climb_right  

Maintenir Grab colle Madeline au mur; elle peut grimper ou descendre. Cela consomme l'endurance (110 au maximum), qui ne se recharge qu'au sol ou avec un cristal. Quand Madeline clignote en rouge, il faut vite trouver un appui.

**Entrées:**
1. Près d'un mur, maintenir Grab pour s'y accrocher.
2. Haut/Bas pour grimper ou descendre.

**Timing:** Endurance (stamina) de 110: rester accroché coûte 10/s, grimper 45,45/s. À 20 Madeline clignote en rouge et ne peut plus attraper un nouveau mur; à 0 elle lâche. L'endurance se recharge au sol ou avec un cristal de dash.

**Sources:** [Celeste Wiki (celeste.ink) - Stamina](https://celeste.ink/wiki/Stamina)

### Climbjump
*Aussi appelé:* climb jump  
*Requiert:* jump, grab, climb_left, climb_right  

Saut vers le haut depuis un mur auquel on est accroché. Il fait monter vite mais consomme un quart de l'endurance à chaque fois. À ne pas confondre avec le wall jump (saut qui s'éloigne du mur), qui est gratuit.

**Entrées:**
1. Accroché à un mur (Grab maintenu), appuyer sur Saut en tenant Haut ou aucune direction (ou vers le mur).

**Timing:** Coûte 27,5 d'endurance (un quart du maximum).

**Sources:** [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Stamina](https://celeste.ink/wiki/Stamina) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Wall Jump
*Aussi appelé:* walljump, wallkick  
*Requiert:* jump  

Saut qui repousse Madeline loin du mur. Il ne coûte aucune endurance et marche même à quelques pixels du mur. C'est la base pour remonter un puits en zigzag entre deux murs.

**Entrées:**
1. Contre un mur (ou jusqu'à 2 pixels de lui), sans tenir Grab ou en tenant la direction opposée, appuyer sur Saut.

**Timing:** Fenêtre de détection du mur: 2 pixels (environ 5 pixels pendant un dash vers le haut).

**Sources:** [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Wallkick vs climbjump
*Requiert:* jump, grab, climb_left, climb_right  

Les deux partent d'un mur mais ne font pas la même chose. Le wallkick (wall jump) projette loin du mur sans endurance; le climbjump monte droit le long du mur et coûte de l'endurance. En pratique, si l'endurance est le facteur limitant, on privilégie wallkicks et neutral jumps.

**Entrées:**
1. Wallkick: Saut en s'éloignant du mur (ou sans Grab).
2. Climbjump: Saut en tenant Grab et Haut/neutre.

**Timing:** Climbjump = 27,5 d'endurance; wallkick = 0. Si on pousse à l'opposé du mur dans les 11 frames après un climbjump neutre, l'endurance est remboursée (wallboost).

**Sources:** [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Stamina](https://celeste.ink/wiki/Stamina) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Neutral Jump
*Aussi appelé:* Neutral, neutral climb  
*Requiert:* jump  
*En vanilla:* Non enseigné; recommandé pour la fraise ailée dorée de Forsaken City (1À); bon mur d'entraînement: première salle de Hollows (6À)  

Saut depuis un mur sans tenir de direction: Madeline s'écarte un peu puis on la ramène vers le mur pour resauter. Répété, cela permet de grimper sans aucune endurance. Le jeu ne l'enseigné pas, mais c'est très utile en mods.

**Entrées:**
1. Contre un mur, sans Grab et sans direction, appuyer sur Saut.
2. Revenir aussitôt vers le mur (tenir la direction du mur).
3. Recommencer en rythme pour grimper.

**Timing:** Vitesse horizontale fixée à 130; demande un rythme régulier, aucune fenêtre en frames donnée.

**Sources:** [Celeste Wiki (celeste.ink) - Neutrals](https://celeste.ink/wiki/Neutrals) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Crouch
*Aussi appelé:* duck  
*Requiert:* crouch  

Maintenir Bas au sol accroupit Madeline et réduit sa hitbox. Accroupie, elle n'est plus poussée par le vent. C'est aussi la base du crouch dash (demodash) et des crouch jumps.

**Entrées:**
1. Au sol, maintenir Bas.

**Timing:** Instantané.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing)

### Dash (8 directions)
*Aussi appelé:* air dash  
*Requiert:* dash_u, dash_ur, dash_r, dash_dr, dash_d, dash_dl, dash_l, dash_ul  
*En vanilla:* Fin du Prologue (obtention du dash)  

Madeline fonce dans l'une des huit directions. Le nombre de dashes disponibles est indique par la couleur des cheveux (rouge = 1, rose = 2, bleu = vide). C'est l'outil central de presque toutes les techniques avancées.

**Entrées:**
1. Tenir une des 8 directions (ou aucune: dash vers l'avant) et appuyer sur Dash.

**Timing:** Un dash dure 15 frames, dont 3 frames de gel (freeze frames). Suivi de 6 frames de 'dash attack'.

**Sources:** [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing)

### Dash refill on ground
*Requiert:* -  
*En vanilla:* Tout le jeu; exceptions dans Core et Farewell  

Toucher le sol recharge le dash (et l'endurance). Exceptions: dans Core le sol ne recharge pas, et dans Farewell le sol ne rend qu'un dash. Ce délai de recharge explique les versions 'extended' des supers et hypers.

**Entrées:**
1. Toucher le sol après un dash.

**Timing:** Le dash ne peut être recharge par le sol qu'après un délai de 10 frames depuis le début du dash.

**Sources:** [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Two dashes
*Aussi appelé:* double dash  
*Requiert:* two_dashes  
*En vanilla:* Fin de Reflection (6À), Summit, Core; double refill dans Farewell (Singular)  

Après la fusion avec Badeline, Madeline a deux dashes. On peut enchaîner deux directions en l'air, ou utiliser le second pour regagner de la vitesse après une technique.

**Entrées:**
1. Avec deux dashes (cheveux roses), dasher deux fois avant de retoucher le sol.

**Timing:** Aucune fenêtre; un nouveau dash ne peut pas commencer avant la fin des 15 frames du précédent.

**Sources:** [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing) - [Celeste Wiki (celeste.ink) - Farewell](https://celeste.ink/wiki/Farewell)

### Coyote Time
*Aussi appelé:* coyote jump, coyote frames  
*Requiert:* jump  

Mécanisme de tolérance: on peut encore sauter quelques frames après être tombée d'une plateforme. Il existe aussi des frames de coyote après certains objets (sortie de dream block, blocs de Core qui cassent), ce qui permet des techniques comme le dream jump ou le core hyper.

**Entrées:**
1. Quitter un rebord en marchant, puis appuyer sur Saut très vite.

**Timing:** 5 frames après avoir quitté le sol.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Input Buffering
*Aussi appelé:* buffer, jump buffering  
*Requiert:* jump  

Le jeu mémorise un appui pendant quelques frames et l'exécute dès que possible. Cela rend les sauts à l'atterrissage plus faciles. Attention: tamponner un saut peut casser certaines techniques (par ex. les ultras enchaînés).

**Entrées:**
1. Appuyer sur Saut (ou Dash) un peu avant que l'action soit possible, par ex. juste avant d'atterrir.

**Timing:** Le wiki indique 4 frames, le Celeste Dictionary 5 frames; sortir de la pause tamponné 6 frames.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Dash cancel (non vérifiée)
*Requiert:* jump  

Terme générique pour l'interruption d'un dash (le super et l'hyper sont des dashes interrompus par un saut). Aucune source consultée ne définit 'dash cancel' comme technique à part entière; voir plutôt Superdash, Hyperdash, Spring Cancel et Grounded Ultra Cancel.

**Entrées:**
1. (Usage communautaire non confirmé) Interrompre un dash avec un saut ou une autre action avant sa fin.

**Timing:** Inconnu.

**Sources:** aucune source fiable trouvée.

### Fastfall
*Aussi appelé:* Fast Fall  
*Requiert:* -  

Maintenir Bas en tombant augmente la vitesse de chute maximale (le sprite de Madeline s'affine). Utile pour gagner du temps ou atteindre une plateforme avant un obstacle.

**Entrées:**
1. En chute, maintenir Bas.

**Timing:** Vitesse de chute max passe de 160 à 240.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Climbhop
*Requiert:* grab, climb_left, climb_right  

Petit saut automatique quand on arrive en haut d'un mur en grimpant. Le contrôle horizontal est bloqué un instant, ce qui peut surprendre près d'obstacles.

**Entrées:**
1. Grimper jusqu'au sommet d'un mur: Madeline fait automatiquement un petit saut sur le rebord.

**Timing:** Automatique; contrôle horizontal désactivé pendant le hop.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Corner Correction
*Requiert:* -  

Si Madeline touche un coin de très peu (4 pixels max), le jeu la décale pour qu'elle passe. En saut elle glisse sur le côté du plafond; en dash horizontal elle est remontée sur le rebord.

**Entrées:**
1. Sauter ou dasher en frôlant un coin.

**Timing:** Jusqu'à 4 pixels de décalage.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

## Intermédiaire

### Crouch Jumps
*Aussi appelé:* crouch jump  
*Requiert:* crouch, jump  

Sauter en restant accroupi garde la petite hitbox pendant le saut. Cela permet de passer des passages étroits ou d'éviter un obstacle qui toucherait la hitbox normale.

**Entrées:**
1. Accroupi (Bas maintenu), appuyer sur Saut.

**Timing:** Pas de fenêtre particulière.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Superdash
*Aussi appelé:* Super, Dashjump  
*Requiert:* jump, dash_l, dash_r  
*En vanilla:* Pas requis en vanilla sauf une variante avec poisson-globe dans Farewell  

Un dash horizontal au sol interrompu par un saut: Madeline part loin et vite avec une hauteur de saut normale. C'est l'une des techniques les plus faciles et la porte d'entrée vers hyper et wavedash.

**Entrées:**
1. Au sol, dash horizontal (Gauche ou Droite).
2. Appuyer sur Saut pendant le dash, avant qu'il se termine.

**Timing:** Jump avant la fin du dash (15 frames). Vitesse initiale 260.

**Sources:** [Celeste Wiki (celeste.ink) - Superdashes](https://celeste.ink/wiki/Superdashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Hyperdash
*Aussi appelé:* Hyper  
*Requiert:* jump, dash_dl, dash_dr  
*En vanilla:* Introduit et requis dans Core C-Side  

Comme le super, mais avec un dash diagonal vers le bas: Madeline va encore plus vite mais saute deux fois moins haut. Idéal pour franchir de longues distances à plat.

**Entrées:**
1. Au sol (ou juste avant de toucher le sol), dash en diagonale bas (Bas-Gauche ou Bas-Droite).
2. Appuyer sur Saut pendant le dash.

**Timing:** Saut avant la fin du dash. Vitesse horizontale 325, hauteur de saut divisée par deux (52,5 au lieu de 105).

**Sources:** [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Extended Super/Hyper
*Aussi appelé:* Extended, extended dash, extended hyper, extended super  
*Requiert:* jump, dash_l, dash_r, dash_dl, dash_dr  
*En vanilla:* Utilisé partout dans Farewell (appelé 'wavedash' en jeu)  

En retardant le saut d'un super/hyper, le sol à le temps de recharger le dash. On garde la vitesse ET on récupère son dash pour la suite. Cela demande un peu plus de place au sol (souvent 2-3 tuiles).

**Entrées:**
1. Faire un super ou un hyper.
2. Attendre au moins 10 frames après le début du dash (en restant au sol) avant d'appuyer sur Saut, mais avant la fin du dash.

**Timing:** Saut entre la 10e frame et la fin du dash (15 frames).

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [Celeste Wiki (celeste.ink) - Superdashes](https://celeste.ink/wiki/Superdashes)

### Reverse Dashes
*Aussi appelé:* reverse hyper, reverse super, reverse wavedash  
*Requiert:* jump, dash_l, dash_r, dash_dl, dash_dr  
*En vanilla:* Exemple: Search Skip dans 5À  

On saute dans le sens opposé au dash: la vitesse du dash est renvoyée dans l'autre direction. Pratique quand un mur ou des pics bloquent un côté, et un reverse extended super peut se faire sans aucune piste en dashant contre un mur.

**Entrées:**
1. Dasher dans une direction (super, hyper ou wavedash).
2. Avant de sauter, tenir la direction opposée.
3. Appuyer sur Saut.

**Timing:** Même fenêtre que le super/hyper; toucher le sol à la frame 10 ou après rend le dash (version extended).

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [Celeste Wiki (celeste.ink) - Superdashes](https://celeste.ink/wiki/Superdashes)

### Wavedash
*Aussi appelé:* midair hyper, wave  
*Requiert:* jump, dash_dl, dash_dr  
*En vanilla:* Farewell - Event Horizon, 'Internet Café' (présentation wavedash.ppt) + zone d'entraînement  

Un hyperdash lance depuis les airs: on dash en diagonale vers le sol et on saute à l'impact. Madeline repart à grande vitesse et récupère son dash. C'est LA technique de Farewell.

**Entrées:**
1. En l'air, un peu au-dessus du sol, dash diagonal bas.
2. Appuyer sur Saut juste quand Madeline touche le sol (pendant le dash).

**Timing:** Le dash doit toucher le sol avant de finir; un saut à partir de la frame 10 du dash rend le dash.

**Sources:** [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Farewell](https://celeste.ink/wiki/Farewell)

### Wallbounce
*Aussi appelé:* Super Walljump, wounce, wb  
*Requiert:* jump, dash_u  
*En vanilla:* Première apparition dans The Summit B-Side (7B); entraîné dans Farewell (Internet Café)  

Un saut mural pendant un dash vers le haut propulse Madeline très haut. Il monte plus vite qu'un dash ou un wallkick seul. Variante: depuis une bulle (pas besoin de son propre dash).

**Entrées:**
1. À côté d'un mur, dash vers le Haut.
2. Pendant le dash, appuyer sur Saut (en s'éloignant du mur ou neutre).

**Timing:** Saut pendant le dash (ou ses 6 frames de dash attack); détection du mur jusqu'à environ 5 pixels. Vitesse 170 horizontal / 160 vertical.

**Sources:** [Celeste Wiki (celeste.ink) - Wallbounces](https://celeste.ink/wiki/Wallbounces) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Farewell](https://celeste.ink/wiki/Farewell) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Dash Attack
*Requiert:* -  

Pendant ces quelques frames, Madeline peut encore faire un wallbounce ou casser/activer des objets comme si elle dashait. Sauter ou attraper un bloc l'annule.

**Entrées:**
1. Après la fin d'un dash, les 6 frames suivantes comptent encore comme 'dash' pour certaines interactions.

**Timing:** 6 frames après le dash de 15 frames.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing)

### Bunnyhop
*Aussi appelé:* Bhop, Bunny Hop  
*Requiert:* jump  

Resauter immédiatement à l'atterrissage pour ne pas perdre la vitesse au sol. Chaque saut ajoute aussi un petit bonus de vitesse. Indispensable pour garder l'élan d'un hyper ou d'un wavedash.

**Entrées:**
1. Après une technique rapide, resauter dès l'atterrissage.

**Timing:** Saut dès la première frame au sol (ou presque) pour limiter la friction.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Demodash
*Aussi appelé:* Demo, crouch dash, Demo Dash  
*Requiert:* crouch, dash_l, dash_r  
*En vanilla:* Farewell - Event Horizon (3 demos voulus) et Remembered (1)  

Un dash horizontal fait en restant accroupi: la hitbox est beaucoup plus petite, ce qui permet de passer entre des spinners ou dans des tunnels très bas. Le jeu propose une touche dédiée 'Crouch Dash' depuis la version 1.4.

**Entrées:**
1. Tenir Bas (ou utiliser la touche Crouch Dash depuis la v1.4) et appuyer sur Dash.
2. Avant la 5e frame du dash, tenir la direction voulue (gauche/droite).

**Timing:** Le jeu vérifie l'accroupissement à la 1re frame du dash et fixe la direction vers la 5e. Hitbox de 4 px au lieu de 9.

**Sources:** [Celeste Wiki (celeste.ink) - Demodash](https://celeste.ink/wiki/Demodash) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Dashing](https://celeste.ink/wiki/Dashing)

### Wallboost
*Aussi appelé:* wall-boost  
*Requiert:* jump, grab, climb_left, climb_right  

En poussant loin du mur juste après un climbjump neutre, le jeu rembourse l'endurance du climbjump et transforme le saut en saut mural. Cela permet de gagner de la hauteur sans vider l'endurance.

**Entrées:**
1. Accroché à un mur, faire un climbjump neutre (sans direction).
2. Dans les 11 frames, tenir la direction opposée au mur.

**Timing:** 11 frames après le climbjump neutre. Rembourse l'endurance et fixe la vitesse à 130 à l'opposé du mur.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Cornerkick
*Aussi appelé:* Corner Kick  
*Requiert:* jump  
*En vanilla:* Ex.: First Blockless dans 4À (facilité par un demo)  

Un wall jump fait sur le coin d'un bloc qu'on vient de dépasser. Utile pour remonter sur un rebord sans grab ni dash. Un demodash diagonal haut peut le rendre plus facile.

**Entrées:**
1. Sauter vers le coin d'un bloc par-dessous.
2. Une fois juste à côté du coin, appuyer de nouveau sur Saut pour faire un wallkick sur ce coin.

**Timing:** Saut quand on est juste à côté du coin; neutre si aucune direction n'est tenue.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Demodash](https://celeste.ink/wiki/Demodash)

### Liftboost
*Aussi appelé:* Block Boost, lift momentum storage  
*Requiert:* jump, moving_block  

Sauter depuis un bloc qui bouge ajoute sa vitesse à celle de Madeline, et ce bonus reste disponible quelques frames après l'arrêt du bloc. C'est ce qui donne les grands sauts depuis les zip movers.

**Entrées:**
1. Se tenir sur (ou accroché à) un bloc en mouvement (zip mover, etc.).
2. Sauter pendant qu'il bouge ou juste après son arrêt.

**Timing:** Le wiki indique un minuteur de 10 frames, le Celeste Dictionary 9 frames. Plafonds: 250 horizontal, 130 vers le haut.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste & Forgiveness (Maddy Thorson)](https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html)

### Spring Cancel
*Requiert:* -  

Un dash juste après un ressort annule l'élan donné par celui-ci. Utile quand le ressort envoie dans une mauvaise direction.

**Entrées:**
1. Dasher juste après avoir touche un ressort.

**Timing:** Juste après le ressort.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Directional Spikes
*Requiert:* -  

Les pics ne tuent pas Madeline si elle se déplace dans le même sens que leur pointe. On peut ainsi traverser ou longer certains pics en dashant dans le bon sens.

**Entrées:**
1. Se déplacer (ou dasher) dans le sens où pointent les pics.

**Timing:** Aucune.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Waterboost
*Requiert:* jump  

Sauter à la surface de l'eau ajoute de la vitesse horizontale à chaque saut. Un humain en enchaîne en général deux ou trois.

**Entrées:**
1. À la surface de l'eau, sauter de façon répétée.

**Timing:** Chaque saut donne 40 de vitesse horizontale.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Fastbubbling
*Aussi appelé:* Fastbubble, fast bubble  
*Requiert:* bubble  
*En vanilla:* Golden Ridge (4À) - bulles  

Normalement la bulle attend un instant avant de partir. En appuyant sur Dash à l'intérieur, elle part immédiatement dans la direction choisie, ce qui fait gagner du temps sans utiliser de dash.

**Entrées:**
1. Dans une bulle (verte ou rouge) immobile, appuyer sur Dash (avec la direction voulue).

**Timing:** Dès l'entrée dans la bulle; ne consomme pas de dash.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Boosters](https://celeste.ink/wiki/Bubble)

### Dream Jump
*Requiert:* dream_block, jump  
*En vanilla:* Old Site (2À) - dream blocks  

Sauter en sortant d'un dream block garde l'élan et ajoute de la hauteur. La fenêtre est généreuse grâce au tampon et au coyote time.

**Entrées:**
1. Traverser un dream block en dashant.
2. Appuyer sur Saut à la sortie (tampon avant ou coyote après).

**Timing:** Fenêtre de 13 frames: 5 de tampon, la frame de sortie, 3 de gel, 5 de coyote. Sortie 240 de vitesse, +40 avec le saut.

**Sources:** [Celeste Wiki (celeste.ink) - Dream tech](https://celeste.ink/wiki/Dream_tech) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Neutral Drop
*Requiert:* holdable, grab  

Lâche l'objet sur place, sans vitesse horizontale, au lieu de le lancer. Base des techniques de jellyvator, stall et neutral jump avec objet.

**Entrées:**
1. En portant un objet, tenir Bas et relâcher Grab.

**Timing:** Aucune.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Throwable Backboost
*Aussi appelé:* Backboost  
*Requiert:* holdable  

Lancer un objet pousse Madeline dans la direction opposée. En lançant vers l'arrière, on gagne de la vitesse vers l'avant.

**Entrées:**
1. Se retourner brièvement.
2. Lancer l'objet vers l'arrière.

**Timing:** Donne 80 de vitesse à l'opposé du lancer.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Cloud Jump
*Requiert:* jump  

Sauter au bon moment sur un nuage ajoute son élan vertical au saut. On peut aussi faire un hyper ou un super depuis un nuage.

**Entrées:**
1. Sauter depuis un nuage au point le plus haut de son rebond.

**Timing:** Au sommet du mouvement du nuage.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

## Avancé

### Midair Super
*Requiert:* jump, dash_l, dash_r  

Version àérienne du superdash, l'équivalent 'horizontal' du wavedash. Donne plus de hauteur qu'un wavedash mais moins de vitesse.

**Entrées:**
1. Dash horizontal en l'air juste au-dessus d'une plateforme.
2. Sauter au moment où le dash touche le sol.

**Timing:** Le dash doit toucher le sol avant sa fin.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Demohyper
*Aussi appelé:* Demo Hyper  
*Requiert:* crouch, jump, dash_l, dash_r  

Un hyper peut être obtenu à partir d'un dash horizontal accroupi. Il peut se lancer en l'air à côté d'une plateforme sans faire de wavedash, et selon le wiki couvre plus de distance qu'un hyper normal à basse vitesse.

**Entrées:**
1. Faire un demodash (dash horizontal accroupi) au sol ou en touchant le sol.
2. Appuyer sur Saut pendant le dash.

**Timing:** Même fenêtre qu'un hyper.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Demodash](https://celeste.ink/wiki/Demodash) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Cornerjump
*Aussi appelé:* Corner Jump  
*Requiert:* jump  

Un wall jump fait tout en haut d'un mur pour gagner un maximum de hauteur et de distance. Les variantes '5jump', '6jump' etc. désignent le nombre de tuiles franchies sans dash.

**Entrées:**
1. Faire un wall jump depuis tout le haut d'un mur (hitbox au niveau du sommet du mur, à 3 px près).

**Timing:** Le haut de la hitbox doit être au niveau ou en dessous du sommet du mur, à 3 px près.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Cornerboost
*Aussi appelé:* cb  
*Requiert:* jump, grab, climb_left, climb_right  

En faisant un climbjump juste avant de percuter le coin supérieur d'un mur, Madeline récupère la vitesse horizontale qu'elle avait et passe par-dessus le mur avec élan. Très utilisé en speedrun et en mods.

**Entrées:**
1. Arriver avec de la vitesse horizontale vers le haut d'un mur.
2. Faire un climbjump avant de toucher le mur (sinon 'bad cb').

**Timing:** La vitesse conservée est rendue si on dépasse le sommet du mur dans les 5 frames.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Cornerslip
*Requiert:* -  

En passant tout près d'une plateforme sans atterrir, Madeline récupère son dash sans perdre sa vitesse verticale. Cela donne aussi des frames de coyote (le 'jasig jump').

**Entrées:**
1. Frôler le sol ou un rebord (à 1 pixel) sans vraiment atterrir.

**Timing:** À 1 pixel verticalement du sol.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Delayed Blockboost
*Aussi appelé:* Delayed Block Boost  
*Requiert:* jump, moving_block  

On conserve la vitesse d'un bloc mobile après l'avoir quitté, à condition de sauter (wall jump) dans une courte fenêtre. Utile quand on ne peut pas sauter directement depuis le bloc.

**Entrées:**
1. Quitter un bloc en mouvement.
2. Faire un wall jump dans les 9 frames qui suivent.

**Timing:** 9 frames.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Spike Jumps
*Aussi appelé:* Spike Jump  
*Requiert:* jump  

Lorsque quelque chose pousse Madeline dans des pics, il existe un court instant ou elle peut encore sauter depuis la surface derrière. Exige un timing précis.

**Entrées:**
1. Quand un bloc mobile (ou le vent) pousse Madeline dans des pics, sauter du mur derrière eux pendant la courte fenêtre.

**Timing:** Fenêtre brève, non chiffrée.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Ultradash
*Aussi appelé:* Ultra  
*Requiert:* dash_dl, dash_dr  
*En vanilla:* Non enseigné (bug conservé volontairement)  

Atterrir depuis un dash diagonal bas multiplie la vitesse horizontale par 1,2. On le fait en général après un wavedash ou un saut depuis un bloc mobile, et cela peut s'enchaîner sans limite.

**Entrées:**
1. Avec de la vitesse horizontale, dash diagonal bas en l'air.
2. Atterrir (pendant ou après le dash) puis repartir sans tamponner le saut.

**Timing:** Multiplicateur x1,2 de la vitesse horizontale à l'atterrissage; pas de plafond.

**Sources:** [Celeste Wiki (celeste.ink) - Ultradashes](https://celeste.ink/wiki/Ultradashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Delayed Ultra
*Requiert:* dash_dl, dash_dr  

Le bonus de l'ultra reste actif même si on atterrit longtemps après le dash diagonal bas. Souvent combiné à un cornerboost.

**Entrées:**
1. Dash diagonal bas.
2. Atterrir plus tard (par ex. après un cornerboost), sans dash horizontal ou diagonal haut entre-temps.

**Timing:** Aucune; casse si un autre dash non diagonal-bas intervient.

**Sources:** [Celeste Wiki (celeste.ink) - Ultradashes](https://celeste.ink/wiki/Ultradashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Superwave
*Requiert:* jump, dash_l, dash_r, dash_dl, dash_dr  

Combinaison d'un super étendu suivi d'un wavedash inverse. Sert à gagner de la hauteur puis repartir vite dans l'autre sens.

**Entrées:**
1. Faire un extended super.
2. Enchaîner avec un reverse wavedash.

**Timing:** Fenêtrès du super étendu puis du wavedash.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Stamina Cancel
*Aussi appelé:* Stamina Canceling  
*Requiert:* grab, climb_left, climb_right  

En relâchant et reprenant Grab rapidement pendant qu'on grimpe, on monte en consommant moins d'endurance.

**Entrées:**
1. Accroché au mur en tenant Haut, tapoter Grab.

**Timing:** Non précisé.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Bubsdrop
*Requiert:* jump  
*En vanilla:* Mirror Temple B-Side (5B)  

Après une transition d'écran verticale vers le haut, on annule l'élan pour ne pas atterrir sur une plateforme à sens unique et revenir à l'écran du dessous. Utilisé dans Mirror Temple B-Side.

**Entrées:**
1. En entrant dans un écran par le bas, faire un wallkick ou climbjump (ou un petit saut) pour casser l'élan vers le haut.
2. Retomber pour déclencher la transition vers le bas.

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Bubble Super/Hyper
*Aussi appelé:* bubble hyper, bubble super  
*Requiert:* bubble, jump  

La bulle remplace le dash: en entrant dans une bulle depuis le sol et en enchaînant un saut, on obtient un super ou un hyper. Cela permet ces techniques sans dépenser son propre dash.

**Entrées:**
1. Entrer dans une bulle depuis le sol en tamponnant les entrées du super/hyper.
2. À la sortie (moins de 5 frames après avoir quitté le sol), exécuter le saut.

**Timing:** Dans les 5 frames après avoir quitté le sol.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [Celeste Wiki (celeste.ink) - Boosters](https://celeste.ink/wiki/Bubble)

### Bubble wallbounce
*Requiert:* bubble, jump  
*En vanilla:* Golden Ridge C-Side (4C)  

Variante du wallbounce où l'élan vient de la bulle au lieu d'un dash. Introduite dans Golden Ridge C-Side.

**Entrées:**
1. Sortir d'une bulle vers le haut le long d'un mur.
2. Appuyer sur Saut pour un wallbounce utilisant l'élan de la bulle.

**Timing:** Comme un wallbounce.

**Sources:** [Celeste Wiki (celeste.ink) - Wallbounces](https://celeste.ink/wiki/Wallbounces)

### Archie
*Requiert:* bubble, crouch  
*En vanilla:* Golden Ridge (4À), Mirror Temple (5À)  

Entrer accroupi dans une bulle remonte la hitbox de Madeline de 2 pixels, ce qui peut permettre de passer un obstacle. Utilisé dans 4À et 5À.

**Entrées:**
1. Entrer dans une bulle en étant accroupi (par ex. via un demodash).

**Timing:** Aucune.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Boosters](https://celeste.ink/wiki/Bubble) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Demodash](https://celeste.ink/wiki/Demodash)

### Dream Double Jump
*Requiert:* dream_block, jump  

Deux sauts rapides à la sortie d'un dream block: grande vitesse et hauteur de saut normale, sans consommer de dash.

**Entrées:**
1. Tamponner un premier saut à la sortie du dream block.
2. Faire un second saut pendant les frames de coyote.

**Timing:** Second saut dans les 5 frames de coyote. Vitesse 320.

**Sources:** [Celeste Wiki (celeste.ink) - Dream tech](https://celeste.ink/wiki/Dream_tech) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Dream Hyper
*Requiert:* dream_block, jump, dash_dl, dash_dr  

Un hyperdash enchaîné juste à la sortie d'un dream block. Il donne la vitesse d'un hyper mais consomme le dash. Impossible si on est entre accroupi ou verticalement.

**Entrées:**
1. Sortir d'un dream block.
2. Pendant les frames de coyote, faire un hyper (dash diagonal bas + saut).

**Timing:** Le wiki donne une fenêtre de 4 à 8 frames après la sortie (8 pour un demohyper); le Celeste Dictionary parle de 5 frames de coyote. Vitesse 325.

**Sources:** [Celeste Wiki (celeste.ink) - Dream tech](https://celeste.ink/wiki/Dream_tech) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes)

### Dream Grab
*Requiert:* dream_block, grab  

Au lieu d'être éjectée, Madeline s'accroche au côté du dream block par lequel elle sort. Impossible si ce côté a des pics ou un mur de glace.

**Entrées:**
1. En sortant d'un dream block, tenir Grab et la direction opposée (tamponnable).

**Timing:** Peut être tamponné avant la sortie.

**Sources:** [Celeste Wiki (celeste.ink) - Dream tech](https://celeste.ink/wiki/Dream_tech) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Theo/Jelly Regrabs
*Aussi appelé:* Jelly Regrab, Theo regrab, regrab  
*Requiert:* holdable, grab  
*En vanilla:* Jellyfish: Farewell - Power Source  

On lance l'objet puis on dash dedans pour le rattraper: le dash est annulé mais sa vitesse est conservée. Cela donne hauteur et distance avec la méduse ou le cristal.

**Entrées:**
1. Lancer la méduse (ou le cristal de Theo) en l'air.
2. Dasher immédiatement dans l'objet (souvent en diagonale) en tenant Grab pour le rattraper.

**Timing:** Non chiffré; plus difficile avec Theo (objet plus lourd).

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/) - [Celeste Wiki (celeste.ink) - Farewell](https://celeste.ink/wiki/Farewell)

### Holdable Stall
*Aussi appelé:* Theo Stall, Jelly Ladder, jelly laddering  
*Requiert:* holdable, grab  

Lâcher puis reprendre rapidement l'objet pour rester plus longtemps en l'air. Avec deux méduses, on peut s'en servir comme une échelle.

**Entrées:**
1. Faire un neutral drop.
2. Rattraper l'objet très vite.
3. Avec deux méduses, alterner (jelly laddering).

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Jellyvator / Theovator
*Aussi appelé:* Theovator, Vator  
*Requiert:* holdable, grab, dash_u  

On lâche l'objet puis on dash vers le haut dedans pour le reprendre: le dash est annulé mais la vitesse est conservée, ce qui fait monter.

**Entrées:**
1. Tenir Bas et relâcher Grab (neutral drop).
2. Dasher vers le haut dans l'objet en tenant Grab pour le rattraper.

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Holdable Slash
*Requiert:* holdable, grab  

Reprendre un objet en dashant vers l'avant pour garder la vitesse du dash.

**Entrées:**
1. Lancer l'objet puis le rattraper avec un dash vers l'avant.

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Holdable Neutral Jump
*Aussi appelé:* Jelly Neutrals  
*Requiert:* holdable, grab, jump  

Grimper un mur en neutral jumps tout en gardant un objet. Avec la méduse il faut la lâcher et la reprendre à chaque cycle.

**Entrées:**
1. Theo: neutral jump normal en portant le cristal.
2. Méduse: neutral drop, aller au mur, neutral jump, reprendre la méduse.

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Featherboost
*Requiert:* feather  
*En vanilla:* Plumes: Reflection (6À)  

Entrer dans l'état plume en tenant une diagonale donne un supplément de vitesse.

**Entrées:**
1. Tenir une diagonale à la toute première frame de vol avec la plume.

**Timing:** 1re frame de l'état plume.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Feather](https://celeste.ink/wiki/Feather)

### Feather Super
*Aussi appelé:* Fuper  
*Requiert:* feather, jump  

Un saut fait pendant le vol avec la plume, le long d'un sol, donne un saut long et rapide.

**Entrées:**
1. En vol avec la plume, en se déplaçant horizontalement le long d'une surface, appuyer sur Saut.

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Reform Boost (Cassette Boost)
*Aussi appelé:* Cassette Boost, Reform Boost  
*Requiert:* jump  

Sauter au moment exact ou un bloc cassette réapparaît projette Madeline plus haut que la normale.

**Entrées:**
1. Se placer là où un bloc cassette va réapparaître.
2. Sauter à la frame où le bloc se reforme.

**Timing:** Première frame active du bloc; ne peut pas être tamponné.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Core Hyper/Super
*Requiert:* jump, dash_l, dash_r, dash_dl, dash_dr  

Un hyper ou super fait juste après la casse d'un bloc de Core cumule l'élan du bloc et celui de la technique, pour aller très loin.

**Entrées:**
1. Se tenir sur un bloc de Core (qui casse).
2. Pendant les frames de coyote après sa destruction, faire un hyper ou un super.

**Timing:** Frames de coyote du bloc de Core.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [Celeste Wiki (celeste.ink) - Hyperdashes](https://celeste.ink/wiki/Hyperdashes) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Grabless (non vérifiée)
*Requiert:* jump  

Terme communautaire pour jouer sans attraper les murs. Les sources consultées ne le décrivent pas comme une technique; seuls la variante d'assistance 'No Grabbing' et le 'Holdable Grabless Dream Hyper' y apparaissent.

**Entrées:**
1. (Non documenté) Progresser sans utiliser Grab: wallkicks, neutral jumps, cornerkicks, wallbounces.

**Timing:** Inconnu.

**Sources:** aucune source fiable trouvée.

## Expert

### Reverse Cornerboost
*Aussi appelé:* rcb  
*Requiert:* jump, grab, climb_left, climb_right  

Cornerboost fait sur un coin dans le dos de Madeline, ce qui la renvoie dans l'autre sens avec un petit bonus de vitesse. Technique pointue, surtout en speedrun.

**Entrées:**
1. Faire un climbjump sur un coin situé derrière Madeline.

**Timing:** Applique -40 de vitesse; variante neutre (nrcb) permettant un wallboost dans les 11 frames.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Multiboost
*Aussi appelé:* double block boost  
*Requiert:* jump, moving_block  

Technique avancée pour récupérer plusieurs fois l'élan d'un même bloc mobile. Rare et exigeante.

**Entrées:**
1. Obtenir plusieurs boosts successifs d'un même bloc en mouvement.

**Timing:** Non précisé.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Spiked Wallbounce
*Requiert:* jump, dash_u  

Un wallbounce reste possible sur un mur à pics si on le fait au bon moment. Rare, mais présent dans certains mods difficiles.

**Entrées:**
1. Wallbounce sur un mur couvert de pics, pendant les frames de dash attack.

**Timing:** Pendant la dash attack.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Spike Clip
*Requiert:* -  

À grande vitesse, on peut traverser des pics qui n'ont pas de bloc sous eux. Technique de speedrun.

**Entrées:**
1. Traverser à grande vitesse des pics sans tuile en dessous.

**Timing:** Non précisé.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Grounded Ultra
*Aussi appelé:* Gultra  
*Requiert:* jump, dash_dl, dash_dr  

Variante au sol de l'ultra: on dash en diagonale dans le sol puis on saute en fin de dash pour combiner bonus ultra et hyper. Le 'Grounded Ultra Cancel' interrompt le dash (souvent avec un objet) pour garder la vitesse.

**Entrées:**
1. Au sol avec de la vitesse, dash diagonal bas dans le sol.
2. Sauter (wavedash) pendant le dash.

**Timing:** Saut optimal à la 14e frame; vitesse optimale 390.

**Sources:** [Celeste Wiki (celeste.ink) - Ultradashes](https://celeste.ink/wiki/Ultradashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Chained Ultras
*Aussi appelé:* Chained Ultra  
*Requiert:* jump, dash_dl, dash_dr  

Enchaîner plusieurs ultras pour accumuler une vitesse énorme. Il faut passer le moins de temps possible au sol et éviter de tamponner le saut.

**Entrées:**
1. Faire un ultra.
2. Resauter très vite après l'atterrissage et re-dasher en diagonale bas immédiatement.
3. Répéter.

**Timing:** Sauter vite sans tamponner le saut (le tampon à ~3/8 de chances de casser la chaîne).

**Sources:** [Celeste Wiki (celeste.ink) - Ultradashes](https://celeste.ink/wiki/Ultradashes) - [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Hyper Bunnyhop
*Requiert:* jump, dash_dl, dash_dr  

Un hyper bref suivi d'un saut complet: on garde la vitesse de l'hyper tout en obtenant une hauteur de saut normale.

**Entrées:**
1. Faire un hyper court.
2. Enchaîner un saut complet (bunnyhop) dès l'atterrissage.

**Timing:** Dépend des sous-pixels pour les versions optimales.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Ceiling Pop
*Aussi appelé:* cpop  
*Requiert:* jump, grab, climb_left, climb_right  

Technique de très haut niveau qui exploite les sous-pixels pour passer sous un rebord. Réservée aux joueurs experts et aux TAS.

**Entrées:**
1. Faire un climbjump depuis le pixel du bas d'un mur, sous un rebord.

**Timing:** Précision sous-pixel, quasi frame-perfect.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Overhangs
*Aussi appelé:* 4tile, 5tile, 6tile  
*Requiert:* jump  

Franchir un surplomb (plafond qui déborde) sans dash. Les versions longues sont extrêmement précises.

**Entrées:**
1. Faire un wallkick ou wallboost sous le bord d'un plafond, puis remonter par-dessus.

**Timing:** Fenêtrès de l'ordre du dixième de pixel pour 5tile/6tile.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Pause Buffering
*Requiert:* -  

Mettre le jeu en pause pour préparer des entrées difficiles. Utile pour s'entraîner frame par frame, généralement mal vu en run normal.

**Entrées:**
1. Mettre pause, maintenir les entrées voulues, puis reprendre.

**Timing:** Entrées maintenues jusqu'à 6 frames avant la reprise exécutées à la 1re frame.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Theo/Jelly Ultras
*Requiert:* holdable, dash_dl, dash_dr  

Utiliser le regrab d'un objet pour annuler un grounded ultra et conserver beaucoup plus de vitesse.

**Entrées:**
1. Faire un grounded ultra.
2. L'annuler en reprenant un objet (regrab).

**Timing:** Non chiffré.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques)

### Feather Clip
*Requiert:* feather  

Technique de clip qui permet de traverser une plateforme en exploitant la hitbox de la plume. Surtout du speedrun.

**Entrées:**
1. Toucher une plateforme traversable (jumpthrough) en mode plume, vers la fin de la plume.

**Timing:** Vers la fin de la durée de la plume.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

### Cassette Raise
*Requiert:* -  

Chaque cycle fait monter le bloc cassette de 2 pixels. Technique de niche.

**Entrées:**
1. Rester à l'intérieur d'un bloc cassette pendant qu'il s'active puis se désactive.

**Timing:** Répétable à chaque cycle.

**Sources:** [Celeste Wiki (celeste.ink) - Techniques](https://celeste.ink/wiki/Techniques) - [The Celeste Dictionary (Google Doc, community)](https://docs.google.com/document/d/13K5XuV2u54lLdKR_oWLNxE6on4GerDJ4GjHy1sQJfDk/)

## Non vérifiées

Entrées conservées mais sans source fiable consultée (à confirmer avant de s'en servir dans la logique du randomizer):

- **Variable jump height** (`variable_jump_height`): La hauteur du saut dépend de la durée d'appui sur Saut: relâcher tôt donne un saut plus court. Les sources consultées ne détaillent pas ce mécanisme (seul le fait que maintenir Saut divise la gravité par deux au sommet est documenté), d'où le statut non vérifié.
- **Dash cancel** (`dash_cancel`): Terme générique pour l'interruption d'un dash (le super et l'hyper sont des dashes interrompus par un saut). Aucune source consultée ne définit 'dash cancel' comme technique à part entière; voir plutôt Superdash, Hyperdash, Spring Cancel et Grounded Ultra Cancel.
- **Grabless** (`grabless`): Terme communautaire pour jouer sans attraper les murs. Les sources consultées ne le décrivent pas comme une technique; seuls la variante d'assistance 'No Grabbing' et le 'Holdable Grabless Dream Hyper' y apparaissent.
