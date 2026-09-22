# Changelog

All notable changes to `blastlands` will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [26.9.47] - 2026-09-22

### Features

- feat(netcode): interpolate remote players and age bombs and flames by server tick (#282)

## [26.9.45] - 2026-09-22

### Features

- feat(lobby): report one match and whether it has started (#274)

## [26.9.44] - 2026-09-22

### Features

- feat(lobby): fill the seats nobody joined with bots (#265)

## [26.9.43] - 2026-09-22

### Features

- feat(client): tactical match HUD with roster, clock and action prompts (#271)

## [26.9.42] - 2026-09-22

### Bug Fixes

- fix(client): create the telegraph property block on first use (#273)

## [26.9.41] - 2026-09-22

### Features

- feat(server): give a reconnecting player their own seat back (#267)

## [26.9.40] - 2026-09-22

### Features

- feat(server): let a bot play any seat whose player is not connected (#264)

## [26.9.38] - 2026-09-22

### Features

- feat(server): admit only connections with a valid game ticket (#261)

## [26.9.37] - 2026-09-22

### Features

- feat(lobby): sign game tickets with an expiry for every admitted player (#260)
- feat(server): cap input packets per seat and disconnect flooders (#259)

## [26.9.34] - 2026-09-22

### Bug Fixes

- fix(client): scale flame tiles up so the blast area reads (#249)

## [26.9.33] - 2026-09-22

### Features

- feat(client): grow and pulse the wall regrowth warning as the wall comes back (#251)
- feat(client): draw a ring in the player's colour under each character (#253)

## [26.9.29] - 2026-09-21

### Features

- feat(server): wait for the seats to fill before ticking the match (#239)

## [26.9.28] - 2026-09-20

### Features

- feat(server): wait for the seats to fill before ticking the match (#239)

## [26.9.26] - 2026-09-20

### Features

- feat(lobby): tell an instance which match its port is serving (#237)

## [26.9.20] - 2026-09-19

### Features

- feat(client): update a Windows build below the minimum from the lobby's release (#234)

## [26.9.19] - 2026-09-19

### Features

- feat(lobby): take the latest client release from GitHub and serve its download (#227)

### Bug Fixes

- fix(server): run the Linux binary under the name game-ci gives it (#229)

## [26.9.18] - 2026-09-18

### Features

- feat(client): ask the lobby which build a player should be running (#207)

## [26.9.6] - 2026-09-06

### Features

- feat(build): publie le client et son manifeste a chaque release (#206)

### Bug Fixes

- fix(client): give every camera branch the seat, and say why nothing moves (#201)

## [26.9.5] - 2026-09-05

### Features

- feat(client): decide whether a build may play before it tries to (#202)
- feat(client): derive each round's seed so one number replays a series (#200)
- feat(client): show the series score on the HUD (#196)
- feat(client): play a series of rounds and keep the score (#195)

### Bug Fixes

- fix(client): separate the Easy and Normal bot skill levels (#198)

## [26.9.4] - 2026-09-04

### Features

- feat(client): sound the fuse burning down and the walls coming apart (#193)
- feat(client): shake the view when a blast lands near it (#182)
- feat(client): show how long is left before the coast closes (#188)

### Bug Fixes

- fix(server): stop synthesising sound a server build cannot play (#191)
- fix(core): spell the exception delegate so both NUnit versions agree (#190)
- fix(server): keep heartbeating when the lobby call throws (#189)

## [26.9.3] - 2026-09-03

### Bug Fixes

- fix(lobby): only the host can start a match (#185)

## [26.9.2] - 2026-09-02

### Features

- feat(server): package the dedicated server build as an image (#181)
- feat(server): start a dedicated server build headless and tick a match (#177)

### Bug Fixes

- fix(client): put each player's HUD panel inside their own viewport (#171)

### Refactoring

- refactor(core): name the match settings and copy them in one place (#172)

## [26.9.1] - 2026-09-01

### Features

- feat(client): animate players from what the simulation says they did (#164)

### Bug Fixes

- fix(client): cut classic soft block density so a spawn has somewhere to go (#166)
- fix(client): render classic pillars as a lattice rather than as scattered rock (#162)
- fix(client): keep simulating when the window loses focus (#160)

## [26.8.29] - 2026-08-29

### Features

- feat(sim): add a blinded classic mode and pick boards by name (#155)
- feat(lobby): throttle abuse per address and reap matches nobody is minding (#152)

## [26.8.28] - 2026-08-28

### Features

- feat(lobby): talk to the lobby over http with typed results (#148)

### Bug Fixes

- fix(lobby): rename Start so it stops colliding with Unity's lifecycle message (#150)

## [26.8.27] - 2026-08-27

### Features

- feat(sim): add a classic bomberman mode beside the arena (#146)
- feat(lobby): model the lobby protocol and every failure it can return (#147)

## [26.8.21] - 2026-08-21

### Features

- feat(client): hang cliffs under the island so the coast stops being a staircase (#126)
- feat(client): build the hud from synty stat boxes instead of a hand-made plate (#128)
- feat(arena): drop the pillar lattice and grow cover in clumps (#121)

### Bug Fixes

- fix(ci): fetch lfs objects after checkout so .lfsconfig applies (#113)

## [26.8.19] - 2026-08-19

### Features

- feat(sim): close the island from the coast so a round ends (#114)
- feat(art): move the players to the polygon apocalypse characters (#107)
- feat(sim): make bots shove opponents into blasts and seal them into traps (#104)
- feat(sim): make the arena a floating island (#103)
- feat(client): vision fog and four arena themes (#100)
- feat(arena): add bush tiles as cover you stand in (#95)
- feat(sim): grow the arena to 25x21 and scale the bomb supply to it (#90)
- feat(client): global, following and split-screen camera modes (#88)
- feat(sim): shove players and stun them on impact (#84)
- feat(sim): radial blasts occluded by walls (#83)
- feat(sim): free movement with sliding collision (#82)
- feat(sim): dash on a cooldown (#81)
- feat(sim): walls grow back, telegraphed on the floor (#79)
- feat(sim): bombs are picked up off the ground, not owned (#77)
- feat(hud): rebuild the player panels on the synty apocalypse hud pack (#63)
- feat(game): power-ups, synthesised sfx and a per-player hud (#60)
- feat(client): break up the grid reading of the arena (#56)
- feat(sim): server-side bots that fill every empty seat (#54)
- feat(client): gamepad controls and local multiplayer (#53)
- feat(client): roll a fresh arena seed for every match (#51)
- feat(client): Apocalypse art direction, tile-scale FX and a client version gate (#47)
- feat(game): playable local match rendered with Synty art (#44)
- feat(sim): add pierce and cluster bomb kinds (#36)

### Bug Fixes

- fix(sim): crush on the body, not the tile under the centre (#116)
- fix(ci): hold the rust workflow at the last pin that starts (#101)
- fix(client): stop ground patches z-fighting and mark where players die (#58)
- fix(client): make the Unity project compile in CI (#34)

### Refactoring

- refactor(sim): drop the unused line-of-sight overload, pin the diagonal case (#85)
