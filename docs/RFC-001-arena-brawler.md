# RFC 001 — De la grille à l'arène

**Statut :** proposé
**Date :** 2026-08-15

Blastlands est aujourd'hui un bomberman classique : grille, déplacement aligné sur les
axes, explosions en croix, bombes illimitées. Ce RFC propose de le transformer en jeu
d'arène — déplacement libre, explosions radiales, bombes ramassées au sol, poussée,
dash, capacités par personnage.

Le but de ce document est de séparer ce qui est bon marché et réversible de ce qui
engage l'architecture, et de dire honnêtement ce que chaque changement casse.

---

## 1. Ce qui motive le changement

Trois constats, dont deux viennent du code existant plutôt que d'une envie de design.

**Les bombes illimitées cassent le jeu et l'IA.** Un bot laissé seul se fait exploser
dans 27 parties sur 40 (mesuré, cf. #59). La cause n'est pas une bêtise de
pathfinding : il pose plusieurs bombes dont les souffles se recouvrent, se scelle dans
une poche, et n'a plus aucune case où se tenir. Un humain fait exactement pareil, plus
lentement. Rendre les bombes rares transforme chaque pose en décision et fait
disparaître la classe de problème, chez le bot comme chez le joueur.

**La croix est une conséquence de la grille, pas un choix.** Elle existe parce qu'elle
est lisible sur un damier : on voit d'un coup d'œil quelles cases sont mortelles. Dès
que le déplacement devient libre, elle perd sa justification.

**Le plateau est statique.** Une fois les blocs détruits, l'arène ne bouge plus et deux
joueurs prudents tournent indéfiniment. C'est le problème que #27 (mort subite)
cherchait à résoudre par un remplissage en spirale. Des murs qui se reforment le
résolvent mieux, en continu, et donnent un rythme au lieu d'un couperet.

---

## 2. Ce qui change

### 2.1 Déplacement libre

Le joueur se déplace dans le plan, plus de case en case. Le point fixe reste : `SubPos`
garde ses 256 unités par tuile et sert d'unité de position continue. Ce qui disparaît,
c'est le verrouillage sur un axe et le recentrage dans le couloir.

La collision devient un test cercle contre boîtes de tuiles. C'est la partie la plus
délicate du lot : sans glissement le long des murs, on accroche à chaque angle et le
jeu paraît cassé — c'est exactement le problème que le recentrage automatique réglait
sur la grille, et il faudra le régler autrement.

### 2.2 Explosions radiales avec occlusion

Le souffle devient un rayon, **bloqué par les murs**. C'est le point le plus important
du RFC : sans occlusion, se mettre à couvert devient impossible et on perd la
compétence centrale du jeu. Un simple test de distance serait beaucoup moins cher à
écrire et beaucoup moins intéressant à jouer.

Conséquences :

- `ExplosionResolver` est réécrit. C'est la pièce la mieux testée du projet ; ses tests
  de chaînage restent valables sur le principe mais pas dans leur forme.
- La bombe **Pierce** change de sens : elle ignore le couvert au lieu de traverser les
  blocs.
- La bombe **Cluster** garde le sien.
- Le `BlastMap` des bots survit : il suffit de rastériser le rayon sur les tuiles pour
  retrouver un « dans combien de ticks cette case prend feu ». L'IA n'est pas à refaire.

### 2.3 Bombes ramassées au sol

Plus de stock personnel. Les bombes apparaissent dans l'arène, on les ramasse, on les
pose. La capacité devient « combien on peut en porter ».

C'est le changement qui modifie le plus le jeu pour le moins de code. Il crée une
raison de bouger, un objet de convoitise, et il supprime le spam.

### 2.4 Murs qui se reforment

Un mur détruit repousse après un délai, **avec un indicateur au sol pendant le
compte à rebours**. L'indicateur n'est pas un détail cosmétique : sans lui la
reconstruction est une mort arbitraire, avec lui c'est une zone à ne pas traîner. Un
joueur pris dans un mur qui se referme est tué, ou repoussé — à trancher (cf. §5).

Rend #27 (mort subite) caduque.

### 2.5 Poussée et stun

On peut projeter un adversaire. S'il percute un mur, il est étourdi quelques instants.

La poussée n'a aucun sens sur une grille — pousser d'une case, vers où ? — elle est
donc **couplée au déplacement libre**, pas indépendante. C'est aussi la mécanique la
plus délicate côté réseau : être poussé est un événement décidé par l'autorité
distante, ce qui entre en conflit avec la prédiction locale (cf. §4).

### 2.6 Dash

Une ruée directionnelle avec un temps de recharge. Bon marché, et c'est ce qui rend le
déplacement libre agréable plutôt que mou.

### 2.7 Capacités par personnage

Chaque personnage a sa capacité propre. C'est là que le périmètre explose : chaque
capacité est une règle de simulation, une surface réseau, une surface d'interface et un
problème d'équilibrage. À traiter **en dernier**, quand la base est bonne.

---

## 3. Caméra et modes de jeu

Décidé :

- **Quatre joueurs, c'est en ligne.** Chaque client a sa caméra qui suit son joueur.
- **En local, le mode est un choix du joueur** : écran splitté, ou caméra globale qui
  cadre tout le monde.

À assumer : avec une caméra rapprochée on ne voit plus tout le plateau. On perd la
lecture tactique du bomberman, on gagne une sensation de brawler. C'est un changement
de nature, pas un réglage.

La caméra globale rouvre le problème qu'elle est censée résoudre : elle dézoome quand
les joueurs s'écartent, et les personnages redeviennent petits. Il lui faut des bornes
de zoom, et une règle pour ce qui se passe quand un joueur sort du cadre.

---

## 4. Ce que ça coûte côté réseau

Le netcode n'est pas encore écrit (#14, #15, #16), ce qui est une chance : autant que
ces choix soient faits avant.

- Le déplacement libre demande de la prédiction et de la réconciliation, comme la
  grille, mais avec une erreur continue au lieu de discrète — les corrections se voient
  plus.
- **La poussée est le vrai point dur.** Le client prédit son propre déplacement ; il ne
  peut pas prédire qu'un adversaire va le projeter. Il faut donc soit accepter une
  correction visible, soit donner l'autorité de la poussée au serveur et encaisser la
  latence.
- Les explosions radiales avec occlusion coûtent plus cher à calculer côté serveur que
  la croix. À vérifier avec le nombre de joueurs et de bombes visé.

---

## 5. Questions ouvertes

1. Un joueur pris dans un mur qui se reforme : tué, ou poussé hors de la case ?
2. Les bombes au sol réapparaissent-elles à intervalle fixe, ou selon une réserve par
   arène ? Une arène qui se vide de bombes se termine en course-poursuite sans arme.
3. Combien de bombes un joueur peut-il porter, et est-ce que ça se ramasse aussi ?
4. Le dash traverse-t-il les autres joueurs ? Les bombes posées ?
5. Bornes de zoom de la caméra globale, et comportement quand un joueur sort du cadre.
6. La poussée est-elle une capacité de personnage ou une action universelle ?

---

## 6. Ce qui survit, ce qui meurt

**Survit :**

- La séparation `Core/` sans référence à Unity — donc les tests en CI sans licence
  Unity. C'est la décision structurante du projet et rien ici ne la remet en cause.
- Le point fixe (`SubPos`), le RNG déterministe, la boucle de tick, le déterminisme en
  général.
- Le générateur d'arène et sa seed.
- Le `BlastMap` des bots, moyennant la rastérisation du rayon.
- Le HUD, les power-ups, l'audio.

**Meurt ou est réécrit :**

- Le déplacement aligné sur les axes et le recentrage dans le couloir (`MatchSim.Move`).
- `ExplosionResolver` et ses tests.
- Le pathfinding BFS des bots dans sa forme actuelle — la logique de fuite reste, la
  granularité change.
- La sémantique de la bombe Pierce.
- #27 (mort subite), remplacée par les murs qui se reforment.

---

## 7. Séquence proposée

L'erreur serait de tout prendre d'un coup : six systèmes qui changent chacun la
sensation des cinq autres, et aucun moyen de savoir lequel marche.

**Étape 1 — sur la grille actuelle, sans rien casser.** Bombes ramassées au sol, murs
qui se reforment avec indicateur, dash. Quelques jours. À la fin de cette étape on sait
si le jeu est plus amusant, avant d'avoir payé quoi que ce soit d'irréversible.

**Étape 2 — le changement de nature.** Déplacement libre, collision avec glissement,
explosions radiales avec occlusion, poussée et stun. C'est le gros morceau, et il se
fait d'un bloc parce que ces quatre-là n'ont pas de sens séparément.

**Étape 3 — la caméra.** Suivi en ligne, puis les deux modes locaux.

**Étape 4 — l'identité.** Personnages POLYGON, capacités par personnage, équilibrage.

---

## 8. Personnages

Décidé : passage aux personnages **POLYGON**, pour le système modulaire (têtes, sacs,
attachements) qui donne à chaque personnage une identité liée à sa capacité, et parce
que la caméra rapprochée rentabilise le détail.

Deux choses à savoir, vérifiées :

- **Les animations ne dépendaient pas de ce choix.** SIMPLE et POLYGON sont tous les
  deux en rig Humanoid (`animationType: 3`), et Unity retargette n'importe quel clip
  Humanoid sur n'importe quel rig Humanoid. Les packs `ANIMATION_*` fonctionnaient déjà
  sur les SIMPLE. Ce n'est donc pas un argument pour POLYGON, le système modulaire en
  est un.
- **Le choix de personnages ne manquait pas non plus** : 51 SimplePeople et 22
  SimpleApocalypse riggés sont déjà dans le projet.

Le mélange décor SIMPLE et personnages POLYGON jure — proportions et densité de détail
différentes. Passer les personnages en POLYGON implique donc, à terme, de passer aussi
l'arène. À budgéter.

C'est la décision la moins chère et la plus réversible du lot : un champ dans
`MatchArt`. Elle peut être prise ou reprise à n'importe quel moment.
