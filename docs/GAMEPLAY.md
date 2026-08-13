# Gameplay design

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

- A player drops a bomb on their current tile, up to their bomb capacity.
- Fuse is a fixed tick count (~2.5 s).
- On detonation, flame extends from the bomb tile in the four cardinal directions, up to the
  player's fire range.
- Flame stops at a hard block. Flame destroys the first soft block it hits and stops there.
- Flame reaching another bomb detonates it immediately — chains resolve in the same tick,
  which is why the resolver is breadth-first rather than recursive.
- Flame kills any player it touches, including the owner.

## Power-ups

Dropped by destroyed soft blocks at a fixed drop rate. V1 set, kept deliberately small:

| Power-up | Effect |
|---|---|
| Bomb up | +1 simultaneous bomb |
| Fire up | +1 flame range |
| Speed up | +1 movement speed step |

Kick, punch, remote detonators and pass-through are explicitly out of V1. They each change
the movement and collision model significantly and are worth their own issues once the base
game is solid.

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

Low-poly 3D, Synty POLYGON assets, same family as the other FerrLabs game projects. Fixed
isometric-ish camera framing the whole arena — no camera control, the arena always fits on
screen. Readability beats spectacle: a player must be able to tell at a glance which tiles
are about to be on fire.
