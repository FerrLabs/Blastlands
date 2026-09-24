# Blastlands

Multiplayer arena bomber: low-poly 3D, dedicated servers, up to 8 players per match.

Drop bombs, blow up soft blocks, grab power-ups, be the last one standing. Anyone can
open a match; it shows up in the public lobby list and fills with players (or bots) until
the host starts it. No Steam, no accounts required to play.

## What's in the game

Download the Windows installer from [blastlands.ferrlabs.com](https://blastlands.ferrlabs.com).

### Playing online

- Pick a name, then a character, then a mode on the match list. All three are remembered on
  that machine, so the next launch goes straight to the match list; Change next to your name
  goes back to edit it.
- **Host a match** lists it publicly. Others join from the list, the host can **Add bot** to
  fill seats, and starts whenever they like, from 2 to 8 players.
- The lobby hands each player a dedicated server instance. The server runs the simulation,
  clients only send their inputs.
- A player who drops out mid-round is taken over by a bot.
- The game checks for a new version on launch. When one is out, an icon and a card appear at
  the bottom right of the lobby, and F5 installs it and restarts.

### Modes

| Mode | Board | Bombs | Moves | Sight |
|---|---|---|---|---|
| **Classic** | the bomberman board: 13x11 inside a border, a pillar on every other tile, corner starts | owned, each one comes back once it goes off | walk, place a bomb | the whole board |
| **Classic Blinded** | same as Classic | same as Classic | same as Classic | only what you have a line to |
| **Arena** | 25x21 eroded island, cover in clumps, bushes to hide in | found on the ground, spent for good | walk, dash, shove, ability | only what you have a line to |

In every mode, destroyed soft blocks grow back after 20 seconds (a marker shows where), and
after 90 seconds sudden death closes the board from the edge one ring every five seconds, so
no round outlasts two minutes. Last one standing wins.

### Bombs and pickups

The blast is a disc, stopped by walls, and a bomb caught in fire goes off at once, so chains
are part of the game. Soft blocks hide pickups:

| Pickup | Effect |
|---|---|
| Bomb up | carry one more bomb |
| Fire up | one more tile of blast reach |
| Speed up | one step faster |
| Pierce bomb | the next bomb burns through soft blocks |
| Cluster bomb | the next bomb flares one tile around the end of each arm |

### Characters

Each character starts with a kit and has one ability on a cooldown. Classic modes play
without characters.

| Character | Starts with | Ability |
|---|---|---|
| Demolisher | one more tile of reach | **Trigger**: sets off your oldest live bomb now (6 s) |
| Runner | two speed steps | **Vanish**: hidden for 2 s wherever you stand (10 s) |
| Grenadier | cluster bombs | **Throw**: your next bomb lands up to three tiles ahead (5 s) |
| Sapper | pierce bombs | **Wall**: raises a soft block in front of you for 8 s (10 s) |

### Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD, ZQSD or arrows | left stick or d-pad |
| Drop a bomb | E | south button (A / Cross) |
| Dash | Space | either shoulder |
| Shove | left click | west button (X / Square) |
| Ability | right click | north button (Y / Triangle) |

A shove never kills on its own: it pushes the nearest player in front of you, and stuns
them if they hit a wall. Xbox and PlayStation pads both work, and the on-screen prompts
follow whichever one is plugged in.

### Bots

Easy, Normal and Hard. They use the same inputs as a player and see only what a player in
their seat would see. A harder bot plans further ahead, reacts sooner and remembers where
it last saw you for longer. Every bot in an online match plays at Normal; the difficulty is
an editor setting for local play.

### Practice

**Practice vs bots** on the match list starts a local match in the mode selected there,
without a server or a connection: one player per gamepad (the first also gets the keyboard),
up to four, with bots in the other seats and the screen split between the people playing. The
series is first to three rounds, the other seats' characters rotate between rounds, R or Start
rerolls the board, and Escape or Select goes back to the lobby. Opening the `Match` scene
directly in the editor runs the same thing in Arena.

### Presentation

- Four arena themes (Wasteland, Desert, Scorched, Overgrown), one picked per match.
- Low-poly Synty art, with a 3D preview of your character in the lobby.
- A camera that follows your player, online and alone on a couch, and a split screen when
  several people play locally. `--no-shake` on the command line turns off the screen shake.
- Positional sound on a single screen: a fuse just off screen is heard from its side. Split
  screen falls back to a flat mix.

The design reasoning and the numbers behind the tuning are in
[docs/GAMEPLAY.md](docs/GAMEPLAY.md).

## Layout

| Path | What |
|---|---|
| `client/` | Unity 6 project. Builds the player client **and** the headless dedicated server (same codebase, `SERVER` define). |
| `client/Assets/_Game/Core/` | Engine-free deterministic simulation (grid, bombs, explosions, bots). No `UnityEngine` references, so it compiles under plain .NET. |
| `client/Assets/_Game/Runtime/` | Unity glue: presentation, input, Netcode for GameObjects wiring. |
| `server/` | Rust + axum lobby service. Holds the match directory and allocates game server instances. |
| `tests/` | .NET test project that compiles the sim core outside Unity so CI can run it without a Unity licence. |
| `docs/` | [Architecture](docs/ARCHITECTURE.md) and [gameplay design](docs/GAMEPLAY.md). |

## Topology

```
                   ┌──────────────────────┐
   Unity client ───▶│  lobby (Rust/axum)   │  list / create / join matches
                   │  api.blastlands.*    │
                   └──────────┬───────────┘
                              │ allocates
                              ▼
                   ┌──────────────────────┐
   Unity client ══▶│ game server instance │  authoritative simulation, UDP
        (UTP)      │ Unity Dedicated Srv  │  one container per match
                   └──────────────────────┘
```

The lobby never sees gameplay traffic: it hands the client a `host:port` and gets out of
the way. The game server is authoritative: clients send inputs, never positions.

## Running it

**Sim core tests** (no Unity needed):

```bash
dotnet test tests/Blastlands.Core.Tests
```

**Lobby**:

```bash
cargo run --manifest-path server/Cargo.toml
```

**Client**: Unity Hub, Add, Add project from disk, `client/`, with Unity `6000.4.2f1`.
The licensed art is a private submodule at `client/Assets/Synty`: clone with
`--recurse-submodules` and set up the LFS credentials described in [docs/assets.md](docs/assets.md).

The repo tracks `Packages/manifest.json`, `ProjectVersion.txt` and `Assets/`; Unity generates
the rest of `ProjectSettings/` on first open and resolves the packages from the manifest.
After the first open you still need to create the URP asset and assign it in Graphics and
Quality settings: the manifest pulls the package, not the configuration.

## Status

Pre-alpha and playable. The [milestones](https://github.com/FerrLabs/Blastlands/milestones)
show what is being built next.

## Licence

The code is Apache-2.0, see [LICENSE](LICENSE).

The art is not. The Synty packs the game draws with are licensed to FerrLabs and live in a
private repository, mounted at `client/Assets/Synty` as a submodule that only FerrLabs can
clone, so a clone of this repository carries no licensed asset. The game opens and runs
without them, with missing meshes where the art would be. [docs/assets.md](docs/assets.md)
explains how that is put together and what has to be bought to see the game as it looks.

---

A FerrLabs project.
