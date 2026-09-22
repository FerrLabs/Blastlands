# Blastlands

Multiplayer arena bomber: low-poly 3D, dedicated servers, up to 8 players per match.

Drop bombs, blow up soft blocks, grab power-ups, be the last one standing. Anyone can
open a match; it shows up in the public lobby list and fills with players (or bots) until
the host starts it. No Steam, no accounts required to play.

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

Pre-alpha. Nothing is playable yet, see the [milestones](https://github.com/FerrLabs/Blastlands/milestones)
for what is being built and in what order.

## Licence

The code is Apache-2.0, see [LICENSE](LICENSE).

The art is not. The Synty packs the game draws with are licensed to FerrLabs and live in a
private repository, mounted at `client/Assets/Synty` as a submodule that only FerrLabs can
clone, so a clone of this repository carries no licensed asset. The game opens and runs
without them, with missing meshes where the art would be. [docs/assets.md](docs/assets.md)
explains how that is put together and what has to be bought to see the game as it looks.

---

A FerrLabs project.
