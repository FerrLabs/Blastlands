# Gameplay design

> This document describes the game as it is built today: a grid, axis-aligned movement
> and cross-shaped blasts. [RFC 001](./RFC-001-arena-brawler.md) proposes moving to free
> movement, radial blasts and scarce bombs. Read it before making design changes here —
> several sections below are what that RFC replaces.

## The match

2–8 players on a rectangular grid. Last player alive wins the round; first to N rounds wins
the match. Rounds are short on purpose (60–90 s) with a sudden-death timer, so a lobby of
friends plays a lot of them in a sitting.

## Game modes

Three, picked before the match starts.

**Arena** is the game this document otherwise describes: an eroded island with nothing
permanent on it, cover you stand in, bombs found on the ground and spent for good, dash and
shove, and vision you can hide from.

**Classic** is the board a bomberman inherits. A full rectangle with a border ring and an
indestructible pillar on every even/even coordinate, soft blocks sprinkled over the rest at
40%, no bushes and no drop. That share is the one thing tuned rather than inherited: at the
75% this board first shipped with, a player could walk to eight tiles before having to bomb
something, which is a spawn pocket rather than an opening. Forty gives them around forty
while still leaving 93% of spawn pairs walled off from each other, so the mode opens by
digging toward somebody rather than by finding them. Bombs are owned rather than found: you hold them, and each one
comes back once it has gone off. No dash, no shove, and the whole board is visible to
everyone, so the only verb is placing a bomb.

**Classic Blinded** is that same board and the same bombs, played without sight of anyone
you have no line to. Cover in Arena is a tile you stand in; here it is the lattice itself, so
a board made almost entirely of things to hide behind changes far more by switching sight off
than open ground would.

It draws far more often than the others: 6 of 12 four-bot matches end in a draw against
Classic's 3. Blind bots stop finding each other, nobody dies, and the last ring takes the
survivors together. Doubling the sudden-death cadence brings it to 4 of 12, which is an
improvement rather than a fix, so the shared cadence is kept. The honest caveat is that bots
are a weak instrument for a mode built on information denial: a person hears a fuse and reads
a board, where a bot has only the sightings it remembers. This number is worth revisiting
against people rather than tuned against bots.

**Movement is free in all three.** Locking Classic to four axes is the one piece of period
accuracy deliberately left out: it would mean a second movement system to maintain, and the
corner assist under **Movement** exists precisely so a one-tile corridor feels right without
it. The board is what makes a match read as classic, not the axis lock.

Sudden death runs in both, and Classic needs it more. Measured over twelve four-bot matches
with it switched off, Classic resolves 5 of 12 on its own against Arena's 11, and kills 23 of
48 against Arena's 35. The lattice is the whole difference: a pillar every other tile gives
far more to hide behind, and bots that can hide do.

Those two figures were taken while Classic still rolled soft blocks at 75%, and have not been
run again since it dropped to 40%. The direction is not in doubt, the lattice is still there
and still the difference, but treat the numbers as describing the old board. Density turned
out to move Classic's death count very little in a separate sweep, between 62 and 75 of 96
across the whole range from 25% to 75%, so they are unlikely to be far off.

The rules that differ live in a `RuleSet` the simulation asks by name, rather than an enum it
switches on, so a third mode answers the same questions instead of adding a third branch in a
dozen places.

## The arena

Grid of 25×21 by default, with an island carved out of it. Five tile kinds:

- **Soft block** — destructible cover. Hides power-ups.
- **Bush** — walkable cover that nothing sees through, and that burns like a soft block. You
  stand *in* it rather than behind it, which is the whole of the difference.
- **Floor** — walkable.
- **Void** — inside the grid and off the island. It stops movement and nothing else: sight
  and blasts both carry across a drop, so the only thing a gap costs you is the ground.
- **Hard block** — indestructible. Nothing generates one: the only source is sudden death
  closing the coast, so on a fresh board there are none at all.

**Nothing on the board is permanent, and the cover comes in clumps.** Generation used to put
an indestructible pillar on every even/even coordinate and then roll the dice per tile at 75%
for the rest. That is a bomberman board: a lattice for readability on a checkerboard, and an
even sprinkle over everything else. It produced a maze of one-tile corridors where a fight was
decided by who stood in the right slot.

Cover is now grown in clumps of three to nine tiles on open ground, to a target share of it,
and none of it is permanent. The result is ground open enough to fight across and ground dense
enough to hide in, and which of the two you are standing in is something you chose. Bots kill
each other more than twice as often on it: 29 deaths across twelve four-bot matches against 14
on the lattice.

It costs the old guarantee that the board never opens out completely. Sudden death is what
replaces it, which is why that had to land first.

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

Dropped on E, or on the south face button of a pad.

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

A blast shakes each view by how close it lands and how big it is. Shake is a common motion
sickness trigger, so `--no-shake` on the command line turns it off until a settings screen
can offer it.

Sound is placed the same way whenever a single viewport is listening. A cue is panned
towards the side of the view it came from and reaches its widest a screen width off centre,
so what you can see stays roughly in front of you and what you cannot is what the stereo
field is spent on. Past the edge of the view it fades with distance, down to a fifth of its
volume rather than to silence, because a blast across the board is still information. That
is what makes a fuse burning down just off screen tellable from one on the far side of the
arena. Split-screen drops back to a flat mix: four people watching four parts of the board
share one pair of speakers, and a cue placed for one of them is placed wrong for the rest.

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
every three seconds. Shoulder button on a pad, space on the keyboard, so it is reachable
without letting go of a direction: the thumb is the one finger not already holding one.

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

## Vision

The arena is not fully visible. Walls block sight, and a player standing in a bush cannot be
seen.

**Vision is simulated, not merely drawn.** That was the decision everything else here hung on.
The cheap version hides what the local player cannot see and leaves the simulation untouched,
but then every bot keeps perfect knowledge of the whole arena and hiding fools nobody who is
not human, which is most opponents until the netcode exists. Bots carry a knowledge model
instead: what they have seen, when they last saw it, and what they are still entitled to
believe.

**A bush hides its occupant without blinding them.** The asymmetry is the mechanic. Cover that
blinded you too would be a tile to avoid rather than one to use. Line of sight skips both
endpoints, so the bush you are standing in never blocks your own view out of it.

**Acting gives you away.** Dashing or shoving lights you up for about a second, wherever you
are standing. A bush that kept hiding someone while they dashed out of it or shoved you would
not be cover, it would be an ambush with no counterplay: the victim never had anything to
react to. The hider chooses between staying hidden and doing something.

**Only players are hidden.** Bombs, flames and the arena itself stay drawn. A blast nobody
could see coming is not a fair death, and a fog that hid the board would make the danger map
unreadable rather than tense.

**Bots forget, and they can be wrong.** A sighting decays, and harder bots hold one longer
than easy ones. A belief is dropped early when the bot can see the tile it remembers and finds
it empty, because a bot hunting a corner it can plainly see is bare reads as broken rather
than as fooled. Watching a bush tells it nothing about what is inside.

Rendering is per camera. Split-screen shows one arena to four people who are each entitled to
a different answer about who is visible, so a hidden player cannot simply be switched off: one
GameObject cannot be on for one viewport and off for the next. Renderers are toggled around
each camera's own render and restored afterwards. Someone stepping into a bush is held on
screen a moment longer, or the disappearance reads as a dropped frame rather than as somebody
taking cover.

Whether the HUD keeps showing an opponent's stats while they are out of sight is left to the
issue that builds the HUD.

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

## Characters

Each player is a character, and a character is a head start on one power-up the arena
already hands out, plus one ability of its own (see **Abilities** below). The kits came first,
the cheap half of #2, to find out whether choosing matters before paying for abilities that are
each a simulation rule, a netcode surface, a UI surface and a balance problem at once.

| Character | Starts with |
|---|---|
| Demolisher | one more tile of reach |
| Runner | two speed steps |
| Grenadier | bombs that flare around the tip of each arm |
| Sapper | bombs that go through soft blocks |

Every head start stops at the ceiling the pickups respect, so a kit is never a way past what a
player could reach by picking things up. Only Arena has characters. Classic is one verb and the
same tools for everybody, and a match built with characters in Classic ignores them.

**No character starts with room for a second bomb.** Carry capacity starts at one because two
live blasts are how a player walls themselves into their own fire, and `MatchSettings.Default`
withholds it on purpose so BombUp is the way to earn it. The first roster had a Hoarder that
started with it, which undid that guard for one seat in every match from the first tick. A test
now holds every kit to the plain player's capacity.

**Seats take the roster in turn**, so four players are one of each and nobody lands the strong
kit by chance. A local series turns the roster by one each round, so the same pad does not keep
the same kit for the whole series. A player who chose a character in the lobby gets it instead: the choice rides in the
signed game ticket and the instance applies it to that seat before kickoff (see the join
tickets in `ARCHITECTURE.md`). Clients do not need to know it up front, because every snapshot
carries each player's character along with their reach, speed and bomb kind.

**The pick** is made on the match list, above the matches, and remembered on that machine
between matches and launches. Online it goes with the join or create request. In a local match
it is seat 0's, the keyboard, for the whole series, while the other seats keep turning the
roster. No pick means the seat decides, exactly as before. Two players may pick the same
character: forbidding it would be a lobby rule with its own race, two players taking the last
free one at once, and nothing has shown it is needed.

**Balance, measured, and not guarded in CI.** Over 240 four-bot matches, every kit in every
seat on each board so a strong spawn cannot pass for a strong kit:

| | survived | won |
|---|---|---|
| Demolisher | 125 | 20 |
| Runner | 111 | 21 |
| Grenadier | 124 | 20 |
| Sapper | 127 | 16 |

Nineteen wins each would be even, over 77 decided matches. The roster with the Hoarder in it
read 19, 12, 20 and 27: two live bombs from the start pulled the match towards the kits that
shape a blast. Runner reads lowest on survival, and bots gain little from mobility, which this
project already measured for the dash, so it is likely no weaker against people.

There is no test that pins these. Survival saturates: a Demolisher given the maximum of
everything still survived 22 of 48 matches against 23 for the real one, and only its wins moved.
Wins are rare enough that telling a strong kit from an absurd one takes around 240 matches, ten
minutes, which is not a unit test. Measure it again after changing a kit or the bots.

## Abilities

One ability per character, on one shared button: right mouse, or north on a pad. Each has its
own cooldown, and a press that finds nothing to do spends none of it, the same courtesy the dash
gets. Like the kits, abilities exist only in Arena, and none of them kills: bombs stay the only
thing that does. Using one gives a hider away, exactly as dashing out of a bush does.

| Character | Ability | Cooldown |
|---|---|---|
| Demolisher | **Trigger**: sets off your oldest live bomb now | 6 s |
| Runner | **Vanish**: hidden for 2 s wherever you stand | 10 s |

The rest of the roster follows one ability per change: Grenadier throws (#36), Sapper raises a
wall (#37).

**Trigger** turns the fuse from a warning into a choice. A bomb you placed is a threat everybody
reads the same way, two and a half seconds and then fire; being able to cut that short is what
makes standing next to a Demolisher's bomb a different decision from standing next to anyone
else's. It needs a bomb of yours on the board, which in Arena is the scarce part.

**Vanish** is a bush you carry. It hides you exactly the way cover does, from sight and from the
bots' memory, and it breaks exactly the way cover does: dash, shove, drop a bomb or use the
ability and you are seen for the usual moment. It is the only ability that does not give you away
by being used, since giving you away would undo it, and it wipes out any giveaway still running
from what you did just before. It does not blind you and it does not hide your bombs, for the same
reason a bush does not.

Bots trigger when a rival they can see stands in the blast, chains included, and they do not.
They vanish when a rival they can see is within three tiles and they are not already hidden.

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
Difficulty never cheats with hidden information: a Hard bot sees exactly what an Easy one
does, tile for tile.

**Difficulty is how far ahead a bot plans, not how slowly it reacts.** A tile that is not on
fire when you arrive can still be one you cannot leave, because the way out burns first. The
escape window answers the second question: for each tile, the latest tick you can still be
standing on it and get away, computed through its neighbours rather than from the fuse alone.

The three levels differ in where they use it. Easy never does, and dies the way a beginner
does, walking back into its own blast on an errand it started while safe. Normal uses it when
fleeing but not when choosing where to go, so it still corners itself on the way to a
power-up. Hard uses it everywhere, including against you: it judges whether a bomb traps
somebody by the escape window the victim has, which is why it places bombs an arrival-safety
reading would have talked it out of.

Reaction delay, lookahead and memory still separate them, but they had stopped doing the
work: once every level escapes its own bombs properly, a slow bot and a quick one survive
about equally. Measured over sixty solo seeds, the ladder was Normal 56 against Hard 60
before this and reads as a real gap after it.

They have less than complete information, too. A bot reads its beliefs about the arena rather
than the arena itself: enemies it has seen, where they were, and how long ago. Cover works
against a bot for the same reason it works against a person, and memory length is part of what
separates the difficulties. See **Vision**.

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
