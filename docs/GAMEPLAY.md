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

Odd-sized grid (default 15×13). Three tile kinds:

- **Hard block** — indestructible, laid out on every even/even coordinate. This is the
  classic pillar lattice; it is what makes the arena readable and stops the map from
  collapsing into an open field.
- **Soft block** — destructible, scattered randomly on the remaining floor. Hides power-ups.
- **Floor** — walkable.

Spawn corners are cleared in an L shape (the spawn tile plus its two orthogonal neighbours)
so nobody is bomb-locked on tick zero.

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

**`BombRespawnTicks` must stay above `FuseTicks`.** Below it a player finds their next bomb
before the last has gone off, which is the only way to have two live at once — and two live
bombs is how you wall yourself into your own blast. Solo bot survival falls off a cliff at
exactly that boundary: 35 seeds of 40 at 90 ticks, 11 at 60. A test pins the invariant.

A bomb lying in fire goes off, so a stocked corner is worth a shot from a distance.

- A player drops a bomb on their current tile if they are carrying one.
- Fuse is a fixed tick count (~2.5 s).
- On detonation, flame extends from the bomb tile in the four cardinal directions, up to the
  player's fire range.
- Flame stops at a hard block. Flame destroys the first soft block it hits and stops there.
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

When the round timer expires, hard blocks start dropping in an inward spiral, one per tick
interval, crushing whatever is on the tile. This is the anti-stalemate rule and it is not
optional — two players with max fire range circling each other will otherwise never resolve.

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
