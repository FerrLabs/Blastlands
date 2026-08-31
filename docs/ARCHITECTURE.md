# Architecture

## Why dedicated servers

The alternative was host-client with relay (one player hosts, everyone else connects
through a relay). It was rejected:

- The host's upload bandwidth and CPU become everyone's ceiling. A bomber game is
  latency-sensitive — a bad host ruins the match for seven other people.
- The host is authoritative, so the host can trivially cheat.
- The host leaving kills the match, and host migration in a simulation-heavy game means
  re-syncing full state under time pressure.

We have a VPS, and a bomber match is cheap to simulate: an 8-player match on a 15×13 grid
at 30 Hz is a rounding error of CPU. Running the simulation server-side costs little and
removes all three problems.

## Components

### Lobby service (`server/`, Rust + axum)

Long-lived HTTP service. Owns the match directory: which matches exist, who is waiting,
and where a match's game server lives. It is deliberately dumb about gameplay — it never
sees a bomb.

Responsibilities:

- `GET /v1/matches` — public list of matches accepting players.
- `POST /v1/matches` — create a match, allocate a game server instance, return its endpoint.
- `POST /v1/matches/{id}/join` — reserve a slot, return the endpoint and a join ticket.
- Reap matches whose game server stopped heartbeating.

Match state is in-memory for now. It is intentionally not in Postgres: a match's lifetime
is minutes, nothing about it is worth surviving a restart, and a restart during a match
only costs the lobby listing — running matches keep running because clients already hold
their server endpoint. If persistence is ever needed (stats, history), that is a separate
store, not this one.

### Game server (`client/`, Unity Dedicated Server build)

The same Unity project as the client, built for the Dedicated Server platform (headless
Linux, no rendering). One process per match, one container per process.

It runs the authoritative simulation and is the only thing allowed to decide that a player
died. Clients send inputs; the server sends state. Bots are just players whose inputs come
from `BotBrain` instead of a socket — the simulation cannot tell the difference, which is
what keeps them honest.

Building the server from the same project as the client is the point: gameplay code exists
once. A separate Rust game server was considered and rejected — it would mean writing every
bomb, blast and power-up rule twice, in two languages, and keeping them bit-identical
forever.

### Client (`client/`, Unity Standalone build)

Renders, reads input, predicts local movement, reconciles against server state.

Presentation reads the simulation, never the other way round. Character animation is the
case where that is easiest to get wrong, so it is worth stating: the animator is told what
the player did last tick, and is never allowed to move anyone. Root motion is off on every
character, and the in-place clips (`Walk_Static`, `Run_Static`) are the ones selected, so a
stride cannot displace a transform the simulation owns down to the sub-tile unit. A player
who animates their way off their own position is a player the server will disagree with.

The stride rate is a ratio of the ground the simulation actually moved someone over
against the speed the clip was authored at, which is why `MatchView.runClipSpeed` exists.
Swapping animation packs means measuring that number again rather than accepting whatever
skating falls out.

## The simulation core

`client/Assets/_Game/Core/` is plain C# with **no `UnityEngine` dependency**. This is a hard
rule, enforced by the assembly definition and by the fact that `tests/` compiles the same
sources under plain .NET.

Two things fall out of it:

- CI runs the gameplay tests with `dotnet test`, in seconds, with no Unity licence and no
  10 GB editor image. Only builds and PlayMode tests need the licence.
- The simulation is deterministic and engine-independent, which is what makes
  client-side prediction and server reconciliation tractable: both sides run the same code
  on the same inputs and must agree.

Determinism rules for anything under `Core/`:

- Integer grid coordinates and fixed-point sub-tile positions. No `float`, no `Vector3`.
- No `System.Random` without an explicit seed carried in the match state.
- No wall-clock time, no `DateTime.Now`. Time is a tick counter.
- No iteration over unordered collections where order affects outcome.

## Match lifecycle

```
client                    lobby                     game server
  │                         │                            │
  ├─ POST /v1/matches ─────▶│                            │
  │                         ├─ allocate instance ───────▶│ (container starts)
  │                         │◀─ ready, host:port ────────┤
  │◀─ match + endpoint ─────┤                            │
  │                                                      │
  ├─ connect (UDP, join ticket) ────────────────────────▶│
  │◀════════ authoritative state, 30 Hz ════════════════▶│
  │                         │◀─ heartbeat / final score ─┤
  │                         │◀─ instance exits ──────────┤
```

Other clients see the match via `GET /v1/matches` between creation and start, and join the
same way.

## Updating the client

There is no launcher, so nothing else can update the game. That makes the update path part
of the architecture rather than a packaging detail.

The failure it exists to prevent is not "the player misses a feature". It is an outdated
client joining a current match: the simulation is deterministic and lockstep-ish, so a client
running different rules **desyncs silently** rather than failing loudly. Everyone's match is
quietly wrong. Refusing that client up front is the whole point.

**The lobby is the gate.** `GET /v1/version` returns:

```json
{ "latest": "26.9.0", "minimum": "26.8.0",
  "download_url": "https://…/blastlands-26.9.0.zip", "sha256": "…" }
```

Every client sends `x-blastlands-version` on match create and join. Below `minimum` the lobby
answers **426 Upgrade Required** — a status that says "this would work on a newer build",
which is exactly the signal an updater needs. A missing or malformed header is a 400: a client
that does not identify itself is either ancient or not ours.

Two deliberate asymmetries:

- `GET /v1/version` is **ungated**. A client too old to play still has to be able to ask what
  to upgrade to, or it is stuck with no way out.
- A client **newer** than `latest` is allowed in. A developer build must not be locked out by
  its own lobby.

`minimum` moves only when the wire format or the simulation rules change. Bumping `latest`
alone offers an update; bumping `minimum` forces one.

**Applying the update** is the client's half and is not built yet (see the tracking issue).
The mechanic worth writing down: on Windows a running executable cannot replace itself. The
download therefore lands in a temporary directory, is verified against the published SHA-256,
and a small updater process is launched that waits for the game to exit, swaps the files, and
relaunches. Verifying the hash before swapping is not optional — an update path that installs
whatever it downloaded is a remote code execution vector.

## Deployment

Single VPS to start. Both images run under Docker on the same host:

- `ghcr.io/ferrlabs/blastlands-lobby` — one container, behind TLS, public HTTP.
- `ghcr.io/ferrlabs/blastlands-server` — N containers, one per match, UDP ports from a
  pool the lobby allocates from.

Instance allocation starts as "the lobby runs a container and tracks the port". That is
enough for a single host and does not need Kubernetes. If concurrency ever outgrows one
VPS, the allocation seam is the only thing that changes — the client already treats the
game server endpoint as opaque.

Nothing here touches `FerrLabs-Cloud/api` or the `Kit` crates. Like FerrGames, Blastlands is
standalone: its own service, its own state, no shared back-office, no accounts. It is a
FerrLabs project at the brand level.
