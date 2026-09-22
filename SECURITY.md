# Sécurité & anti-triche

## Modèle de menace — lis ça d'abord

Blastlands est **multijoueur sur serveur dédié**. C'est une position très différente d'un
jeu solo : l'état de jeu ne vit pas sur la machine du joueur, donc la triche sur l'état
(se donner 99 bombes, ignorer les flammes, traverser les murs) n'est pas « rendue
difficile », elle est **structurellement impossible**. Le client n'envoie que des inputs ;
il ne peut pas affirmer une position, un kill, ou un power-up.

Ce que ça déplace, ce n'est pas le problème : ça le change. Les vecteurs qui restent sont
l'abus du lobby, l'automatisation côté client, et le déni de service.

## Ce qui est protégé par l'architecture

| Vecteur | Pourquoi ça ne marche pas |
|---|---|
| Éditer sa position / sa vie / son inventaire | Le serveur ne lit jamais l'état envoyé par le client. La prédiction locale est cosmétique et se fait corriger au tick suivant. |
| Se déclarer vainqueur, forger un kill | Seul le serveur décide qu'un joueur meurt. Il n'existe pas de message client « j'ai tué X ». |
| Poser plus de bombes que sa capacité | La capacité est de l'état serveur. Un input « poser une bombe » invalide est ignoré, pas appliqué. |
| Rejouer / accélérer le temps | Le temps est un compteur de ticks côté serveur. Un client qui tourne trop vite envoie juste des inputs qui seront traités au rythme du serveur. |
| Save scumming, rollback | Il n'y a pas de save locale qui compte. |
| Flood d'inputs | Chaque siège a un budget de paquets (`InputRateGate`) : une rafale d'une seconde passe, un débit soutenu au-delà de deux paquets par tick est refusé, et un siège qui dépasse pendant une seconde entière est déconnecté. Les inputs pour un tick passé ou trop lointain sont refusés par `InputBuffer`. |

C'est la raison pour laquelle le serveur dédié a été choisi plutôt qu'un host-client relay
(voir [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)) : en host-client, l'hôte *est* le
serveur autoritaire, donc tout ce tableau tombe.

## Ce qui reste à protéger

Ces points sont réels et suivis comme des issues, pas comme des acquis.

- **Automatisation côté client (bots humains).** Un joueur peut scripter des inputs
  parfaits. C'est le seul cheat qui survit à un serveur autoritaire, dans tous les jeux.
  Mitigation réaliste : détection statistique côté serveur (temps de réaction, régularité
  inhumaine des timings), pas d'anti-cheat kernel.
- **Abus du lobby.** Création massive de parties, squat de slots, noms abusifs. **Traité**
  (#22, #23) : rate limit par adresse sur `POST /v1/matches` et sur les joins, plafond de
  parties hébergées simultanément par une adresse, TTL sur les parties que personne n'a
  rejointes, et moissonnage des parties dont l'instance ne donne plus signe de vie. Les
  pseudos étaient déjà contraints en longueur et en charset côté serveur.

  Une quatrième forme d'abus manquait à cette liste et a été corrigée depuis :
  `POST /v1/matches/{id}/start` ne vérifiait rien. Les identifiants de parties sont
  publics, `GET /v1/matches` les donne à qui les demande, donc n'importe qui pouvait
  parcourir la liste et démarrer chaque partie ouverte. Une partie démarrée sort du
  listing et refuse les joins, ce qui suffisait à rendre le lobby inutilisable, une
  requête non authentifiée par partie. Le lobby retient maintenant le ticket qu'il a
  remis à l'hôte et exige qu'il soit présenté pour démarrer.

  Une réserve à connaître avant d'exposer le service : l'adresse retenue est celle du
  socket, sauf si `BLASTLANDS_LOBBY_TRUST_FORWARDED_FOR` est activé. Derrière un reverse
  proxy sans ce réglage, tout le trafic partage un seul compteur ; avec ce réglage sur un
  lobby joignable en direct, n'importe qui peut écrire l'adresse de son choix et les
  limites ne valent plus rien. Le bon réglage dépend du déploiement, il n'y a pas de
  valeur juste par défaut.

  Quand ce réglage est actif, c'est la **dernière** entrée de `X-Forwarded-For` qui est
  lue, pas la première. La plupart des proxys ajoutent au lieu de remplacer (le
  `$proxy_add_x_forwarded_for` de nginx, les ALB), donc un client qui envoie lui-même
  l'en-tête arrive sous la forme `<ce qu'il a écrit>, <ce que le proxy a vu>` : lire le
  début de cette liste revient à croire l'attaquant, qui se fabriquerait une adresse
  neuve à chaque requête. Cela suppose **exactement un saut de confiance**. Avec
  plusieurs proxys, ou avec un proxy qui laisse passer tel quel un en-tête fourni par le
  client, même la dernière entrée n'est pas fiable sans retirer un nombre connu de sauts.
- **Forge de ticket de join.** Le ticket rendu par le lobby doit être signé et à durée de
  vie courte, sinon on peut se connecter à une instance sans passer par le lobby ou entrer
  dans une partie pleine.
- **DoS sur le lobby ou sur une instance.** Un seul VPS = une seule cible. Mitigation :
  limites de connexion, et le fait qu'une instance qui tombe ne fait tomber qu'une partie.

## Ce qui n'est explicitement pas couvert

- **Anti-cheat au niveau noyau.** Pas prévu, et pas souhaitable pour un jeu entre potes.
- **Bans persistants.** Il n'y a pas de comptes. Un ban ne peut porter que sur une IP, ce
  qui est faible et frappe à côté. Assumé tant que le jeu reste petit.
- **Chiffrement du trafic de jeu.** Le trafic UDP n'est pas chiffré. Il ne transporte que
  des positions de bombes ; il n'y a rien de sensible dedans. Le lobby, lui, est en TLS.

## Données personnelles

Le jeu ne demande pas de compte, pas d'e-mail, pas d'identifiant tiers. Ce qui transite :
un pseudo choisi à la volée, et l'IP nécessaire pour établir la connexion. Rien n'est
persisté après la fin d'une partie. Toute évolution qui ajouterait de la persistance
(stats, classements) est un changement de posture qui doit être documenté ici en même temps.

## Signaler une faille

Ouvrir une issue si c'est bénin. Pour quoi que ce soit d'exploitable à distance (RCE sur
le lobby ou sur une instance, forge de ticket, crash serveur déclenchable par un paquet),
passer par une [security advisory GitHub](https://github.com/FerrLabs/Blastlands/security/advisories/new)
plutôt qu'une issue publique.
