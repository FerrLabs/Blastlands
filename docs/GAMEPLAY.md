# Gameplay design

> This document describes the game as it is built today: a grid, axis-aligned movement
> and cross-shaped blasts. [RFC 001](./RFC-001-arena-brawler.md) proposes moving to free
> movement, radial blasts and scarce bombs. Read it before making design changes here —
> several sections below are what that RFC replaces.

## The match

2–8 players on a rectangular grid. Last player alive wins the round; first to N rounds wins
the match. Rounds are short on purpose (60–90 s) with a sudden-death timer, so a lobby of
friends plays a lot of them in a sitting.

## The arena

Odd-sized grid (default 25×21). Three tile kinds:

- **Hard block** — indestructible, laid out on every even/even coordinate. This is the
  classic pillar lattice; it is what makes the arena readable and stops the map from
  collapsing into an open field.
- **Soft block** — destructible, scattered randomly on the remaining floor. Hides power-ups.
- **Floor** — walkable.

Spawn corners are cleared three tiles along each axis. The clearance has to exceed the
starting fire range: a player sealed into a smaller pocket cannot open it, because the only
way out is to bomb and their own blast covers the whole thing. One tile was enough while
blasts were crosses; a disc of radius two swallows a three-tile L, and the symptom was bots
standing armed and idle for a whole match, every bomb they considered correctly refused as
suicide.

**The arena grew from 15×13 with the camera work.** A camera that follows a player is
pointless on a board narrower than its own view. Nothing tuned before it survived the change
unexamined — bomb supply, wall timing and every bot figure were re-measured, not assumed.

Generation is seeded and deterministic: the same seed produces the same arena on the server
and every client.

## Bombs

Bombs are found, not owned. They lie around the arena, get picked up by walking over them,
and are **spent when placed** — a bomb going off does not hand it back. Running out is the
normal state of affairs, and it is what sends a player back into the open.

The arena tops itself up toward `LooseBombTarget` every `BombRespawnTicks`, on floor the
players can actually reach. Reachability is not a detail: a free tile can be sealed inside a
ring of soft blocks, and bombs dropped there make the count say the arena is stocked while
nobody can get to a single one.

Supply is expressed as floor per bomb rather than a flat count, so growing the arena does not quietly starve it: four bombs is generous on 15×13 and thin on 25×21. It also controls lethality more than it looks, through chains — the more bombs lying around, the more often a blast lights one and it takes whoever set it off.

**`BombRespawnTicks` must stay above `FuseTicks`.** Below it a player finds their next bomb
before the last has gone off, which is the only way to have two live at once — and two live
bombs is how you wall yourself into your own blast. Solo bot survival falls off a cliff at
exactly that boundary: 35 seeds of 40 at 90 ticks, 11 at 60. A test pins the invariant.

A bomb lying in fire goes off, so a stocked corner is worth a shot from a distance.

**The blast is a disc, and it is blocked by walls.** The cross was never a design choice: it
was legible on a checkerboard, which is the only reason it existed. Once a player can stand
between two tiles it stops answering "am I in it".

The occlusion is the load-bearing half. A plain distance check is far cheaper to write and
far worse to play — without walls stopping the blast, taking cover is impossible and the
central skill of the game goes with it. Line of sight is integer Bresenham, so the answer is
the same everywhere.

A disc grows with the square of its range while a cross grows linearly, so `MaxFireRange`
came down from 8 to 4 with this. Against a 15×13 arena:

| range | disc | share of arena | old cross |
|---|---|---|---|
| 2 | 13 | 7% | 9 |
| 4 | 49 | 25% | 17 |
| 8 | 197 | 101% | 33 |

Eight was fine as a cross. As a disc it is one bomb covering the whole map.

- A player drops a bomb on their current tile if they are carrying one.
- Fuse is a fixed tick count (~2.5 s).
- On detonation, every tile within fire range burns, unless a wall stands between it and
  the bomb.
- Flame never burns a hard block. It destroys a soft block it reaches, and that block
  shelters whatever is behind it.
- Flame reaching another bomb detonates it immediately — chains resolve in the same tick,
  which is why the resolver is breadth-first rather than recursive.
- Flame kills any player it touches, including the owner.

## Bomb kinds

Variety in a bomber game comes from **blast shape**, not from damage numbers — there is only one
kind of damage and it is lethal. The kinds differ in where the fire goes, which changes which
tiles are safe and therefore how players move.

| Kind | Behaviour | Why it is interesting |
|---|---|---|
| **Standard** | Cross blast. Destroys the first soft block in each arm and stops there. | The baseline, readable at a glance. |
| **Pierce** | Punches through soft blocks, destroying every one in the arm until a hard block stops it. | Turns a wall of blocks from cover into a liability, and opens the map fast. |
| **Cluster** | Normal cross, then each arm flares one tile in every direction around where it stopped. | Reaches past its own range and around corners, so the safe-tile maths a player does at a glance stops working. |

A cluster flare is deliberately not itself a cluster. That is what bounds the recursion, and it
keeps the shape readable instead of turning every cluster into an unpredictable chain.

Kinds are per-bomb rather than permanent: a pickup grants a kind, the player holds it until they
use it. That stops one lucky drop from deciding the whole round.

Kinds that change *when* a bomb goes off rather than *where* the fire lands — remote detonation,
proximity mines, sticky bombs — are deferred until the tick loop exists, because they need a
trigger the resolver has no concept of today.

## Power-ups

Dropped by destroyed soft blocks at a fixed drop rate. V1 set, kept deliberately small:

| Power-up | Effect |
|---|---|
| Bomb up | +1 carrying capacity |
| Fire up | +1 flame range |
| Speed up | +1 movement speed step |
| Pierce bomb | The next bomb dropped is a Pierce |
| Cluster bomb | The next bomb dropped is a Cluster |

Bomb count and fire range are capped (`MaxBombs`, `MaxFireRange`). Uncapped fire range ends
with a player who covers the arena from their spawn, which is not a fight.

Which blocks hide what is decided when the match is created, from the match seed, so the seed
describes the whole match rather than just its walls. Placement draws from its own stream, so
changing the arena generator does not reshuffle every pickup.

A pickup survives the blast that uncovered it and is destroyed by any later one. Flames burn
for `FlameTicks`, so "is this tile on fire" cannot tell those two apart — the comparison is
between when the fire was lit and when the pickup appeared. Getting this wrong is invisible in
a same-tick test and makes every pickup vanish one tick after it is revealed.

Kick, punch, remote detonators and pass-through are explicitly out of V1. They each change
the movement and collision model significantly and are worth their own issues once the base
game is solid.

## Camera

Three modes, because a following camera cannot serve four people on one screen.

- **Global** frames every living player and zooms to fit, bounded at both ends. Dead players
  drop out of the framing, or the survivors spend the round zoomed out around a corpse.
- **Follow** tracks one player, with lookahead in the direction they face and more of it
  while dashing — a camera that lags a dash makes the dash feel worse than not having one.
  This is what every client uses online.
- **Split** gives each local player their own following view. Two players split across, so
  each view stays wider than it is tall; three or four take quadrants, and a third player
  leaves an empty corner rather than handing someone a differently shaped view to read.

None of it shows much beyond the arena edge, past which the view is mostly scenery and the
player loses track of where the board ends.

**Following only pays off on a bigger arena, which the current one is not.** At 16:9 on 15×13
the view is wider than the board before it is close enough to feel like following, so the
clamp pins it near the centre. The mode works; the arena defeats it.

## Movement

Positions are continuous. A player is a body about 0.7 of a tile across that slides along
whatever it is pressed against, rather than a token that hops between tile centres.

**Input is a vector, not one of four directions.** This is not a nicety: the old movement
locked to an axis and pulled the player onto their corridor centre on every step, which is
what stopped them catching on pillars. Free positions remove that, and without diagonals
there is nothing to slide with — the result catches on *more* corners than the grid did, not
fewer. `CornerAssist` covers the rest, pushing the body onto an open lane when it walks into
the edge of one.

Diagonals are scaled by the vector's length, so going two ways at once is not faster than
going one. The integer square root in `MatchSim` is deliberate: a float there is the one
place determinism would leak.

**Bots are worse under this and it is not yet fixed.** A Hard bot left alone survives 23 of
40 unsupervised matches, down from 30 on the grid. The planner reasons in tiles while the
body no longer lives in one — a bot sitting on a tile boundary changes which tile it is in
every tick, and its decision flips with it. Several point fixes recovered part of it; the
rest needs the planner to think in positions.

## Shoving

Always available, on left click, and on the west face button of a pad. It finds the nearest
living player within reach **in front of the shover** and launches them.

**A shove never kills.** Bombs are the only source of damage, so the way to kill someone with
a shove is to put them where a bomb already is — the kill still belongs to the bomb. That
makes the interactions the point rather than a side effect: into a blast, into a wall that is
about to close (#67), off the ground they were holding.

Hitting a wall mid-flight stops the shove and stuns the target. A shove into open ground is a
reposition; a shove into a wall is the punish. A stunned player cannot move or place bombs,
but **can still be shoved** — someone helpless is exactly who you want to be able to move, and
a stun that made them immovable would turn the punish into protection.

Every shove on a tick is decided against the same starting positions, so two players shoving
each other both land rather than the loop order picking a winner.

Reach has to exceed a tile. Two players on adjacent tile centres are 256 sub-units apart, and
a shorter reach means a shove that only works when you are already inside somebody.

## Dash

A burst of speed on a recharge — about two and a half tiles in a third of a second, once
every three seconds. Shoulder button on a pad, shift on the keyboard, so it is reachable
without letting go of a direction.

**It is committed.** Once started it runs its length in the direction it began in, whatever
the player does next. That commitment is the risk that pays for the speed: it is entirely
possible to dash into a blast.

**It is speed and nothing else.** No invulnerability window, and it collides with everything
a walking player collides with, including placed bombs. Passing through bombs would make
laying one risk-free — seal yourself in, then dash out of your own trap — and an i-frame
through fire would change what a bomb means. Bombs are what kills; a dash does not argue
with that.

Dashing with no direction at all does nothing rather than spending the cooldown, or the
button reads as broken.

**Bots dash only if they have a reaction delay.** Measured over 40 seeds: giving it to
everyone took Hard from 30 survivors to 27 while lifting Normal from 16 to 20 and Easy from
19 to 22. The commitment costs an agent that would otherwise re-plan every tick, and pays for
one that cannot. Hard has `ReactionTicks` of zero, so it keeps walking.

## Walls grow back

A destroyed soft block returns after `WallRegrowTicks`, with a marker on the floor for the
last `WallTelegraphTicks`. The tile stays walkable for the whole countdown — a tile that is
already impassable while the marker shows tells the player nothing they can act on.

This is what stops the board going static. Once the soft blocks are gone two careful players
circle each other forever, which is the problem sudden death was working around; routes that
keep closing and reopening solve it continuously instead of with a guillotine at the end.

**A closing wall never kills.** Bombs are the only thing that does, so it shoves the player
onto a free neighbouring tile, and waits if there is nowhere to shove them. It also waits for
a live bomb or a fire rather than sealing over them. It does take back any pickup lying there:
postponing on one would let an unwanted bomb hold a corridor open all round.

Timing is measured, not guessed — solo Hard bot survival over 40 seeds against how much of the
arena stays walkable in a four-bot match:

| regrow | survive | open floor |
|---|---|---|
| 12 s | 21 | 46% |
| **20 s** | **30** | **48%** |
| 30 s | 29 | 53% |
| 40 s | 28 | 57% |

Faster than 20 s is the only setting that really bites, and it buys almost no extra pressure
for it. Bots treat a telegraphed tile as impassable, which is exactly the warning a player
gets: a gap that shuts on the way through is the same death as a blast, and the blast map
knows nothing about walls.

## Sudden death

Ninety seconds in, the island starts closing from the coast. Every five seconds the outermost
ring of remaining ground turns to hard block and kills anyone it touches, so the board is gone
within a minute of the first ring and no round outlives two minutes.

This is the anti-stalemate rule and it is not optional. Two survivors reading the same blast
map do not resolve on their own: measured over six four-bot matches with it switched off, one
was decided inside 200 s and the rest were still circling. With it on, twenty-four of
twenty-four end, median 95 s, slowest 109 s.

Rings, rather than the inward spiral this section used to describe. A spiral is defined on a
rectangle, and the arena is an eroded island where a tile near the middle of the grid can sit
on the shore of a bay; distance from the drop is the only ordering that means the same thing
whatever shape erosion left behind. One block per interval is also far too slow to end
anything at this size, the island being close to three hundred tiles.

Unlike a regrowing wall, a closing ring kills rather than shoves. A wall that shoves has to
find somewhere to shove to and postpones when it cannot, which is the stalemate over again.

It takes anyone whose **body** is in it, not whoever is centred on it. Movement came off the
grid, and a body 0.7 of a tile across can be two thirds inside the ring while still centred on
the tile next door. On the centre alone that player lives and then walks around with their body
inside the rock, which the movement code allows on purpose: it ignores the tiles you already
overlap, so that a wall growing back under you cannot lock you in place.

## Bots

Bots fill empty slots so a match starts without waiting for eight humans, and they replace
players who disconnect mid-round rather than leaving a corpse standing.

They are server-side and drive the same input struct a human does. No special-casing in the
simulation.

Behaviour, roughly in priority order:

1. If standing in a tile that a live bomb will reach, path to the nearest safe tile.
2. If a soft block is adjacent and dropping a bomb leaves an escape route, drop it.
3. If an enemy is reachable and trapped, drop a bomb.
4. Otherwise path toward the nearest power-up or the nearest enemy.

Danger evaluation is a flood fill over tiles reachable before each live bomb's fuse expires.
Difficulty is a reaction-delay knob plus how far ahead the danger map is computed — not
cheating with hidden information.

## Art direction

Low-poly 3D, Synty **SIMPLE Apocalypse**: dirt and rubble, rusted barrels, burnt-out vehicles
and building ruins. The setting earns the mechanics — a game about blowing things up reads
better in a wasteland than in a tidy park.

SIMPLE rather than POLYGON: a POLYGON pack is 130 MB and change against roughly 30 MB for a
SIMPLE one, and SIMPLE's flat chunky shapes read better at the size a tile occupies on screen.

**Variety in form, never in meaning.** Destructible blocks are always barrels — three colours,
one silhouette. An earlier pass mixed in medical crates and toolboxes, and they read as
pickups to grab rather than blocks to destroy. Indestructible blocks are always grey rock,
which is also why the ground is uniform dirt: a mixed concrete-and-dirt floor produced a grey
checkerboard that camouflaged the grey walls standing on it.

Fixed isometric-ish camera framing the whole arena — no camera control, the arena always fits
on screen. Readability beats spectacle: a player must be able to tell at a glance which tiles
are about to be on fire.

Two things bite when using Synty art on a grid, both handled in `TileFitter` and `MatchView`
rather than by hand-tuning numbers per prefab:

- Prefabs come in their own world scale with off-centre pivots, so they are measured from
  their renderer bounds and normalised into a tile-sized box. The ground sections are the
  exception: they are 15x15 unit slabs meant to be laid as-is, so the floor is one stretched
  slab rather than one shrunk copy per tile, which would squeeze a whole texture into each
  tile and turn the floor into noise.
- The FX prefabs are authored for set dressing: `FX_Fire` simulates in world space, ignores
  transform scale for particle size, and lives four seconds. Pooled flame objects move between
  tiles, so those defaults smear fire across the arena. The instances are retuned on spawn;
  the source assets are never modified.
