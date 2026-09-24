use std::collections::HashMap;
use std::fmt;
use std::net::IpAddr;
use std::sync::{RwLock, RwLockReadGuard, RwLockWriteGuard};
use std::time::{Duration, Instant};

use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::error::LobbyError;
use crate::modes::GameMode;
use crate::names::DisplayName;
use crate::ports::PortPool;

pub const MIN_PLAYERS: u8 = 2;
pub const MAX_PLAYERS: u8 = 8;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct MatchId(Uuid);

impl fmt::Display for MatchId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(transparent)]
pub struct HostTicket(Uuid);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum MatchState {
    WaitingForPlayers,
    InProgress,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct GameServerEndpoint {
    pub host: String,
    pub port: u16,
}

#[derive(Debug, Clone)]
pub struct Match {
    pub id: MatchId,
    pub name: DisplayName,
    pub host: DisplayName,
    pub players: Vec<DisplayName>,
    pub max_players: u8,
    pub mode: GameMode,
    /// Seats the host claimed for a bot ahead of start, so a human cannot join into
    /// one. The instance already fills every unclaimed seat with a bot regardless,
    /// so this only ever narrows who may still join, never who runs the match.
    pub bots: u8,
    pub state: MatchState,
    pub endpoint: GameServerEndpoint,
    /// Who asked for it, so one address cannot hold the whole port pool.
    pub host_address: IpAddr,
    pub created_at: Instant,
    /// Last sign of life from the instance running this match. Only meaningful once the
    /// match is in progress: before that there is no instance to hear from.
    pub last_seen: Instant,
    /// The ticket handed to whoever created this match, kept so that starting it can be
    /// proved to come from them. Never serialised: `MatchSummary` is what leaves here.
    pub host_ticket: HostTicket,
}

impl Match {
    pub fn is_full(&self) -> bool {
        self.players.len() + usize::from(self.bots) >= usize::from(self.max_players)
    }
}

#[derive(Debug)]
pub struct CreateMatch {
    pub name: DisplayName,
    pub host: DisplayName,
    pub max_players: u8,
    pub mode: GameMode,
    pub host_address: IpAddr,
}

/// How long a match may sit around without being looked after.
#[derive(Debug, Clone, Copy)]
pub struct Lifetimes {
    /// A match nobody has joined is a squatted port. The host counts as a player from
    /// the moment they create it, so "nobody" means the host and no one else.
    pub unjoined: Duration,
    /// How long a running match may go without a heartbeat before it is assumed dead.
    pub silent: Duration,
    pub waiting: Duration,
}

/// Why a match was taken away, so the log says something useful rather than "removed".
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ReapReason {
    NobodyJoined,
    NeverStarted,
    InstanceWentSilent,
}

#[derive(Debug, Clone)]
pub struct Reaped {
    pub id: MatchId,
    pub port: u16,
    pub reason: ReapReason,
}

/// Handed to the instance that owns a port so it can start the match it was given.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Assignment {
    pub match_id: MatchId,
    pub players: u8,
    pub humans: u8,
    pub mode: GameMode,
}

#[derive(Debug)]
pub struct Admitted {
    pub endpoint: GameServerEndpoint,
}

struct DirectoryState {
    matches: HashMap<MatchId, Match>,
    ports: PortPool,
}

pub struct MatchDirectory {
    game_server_host: String,
    state: RwLock<DirectoryState>,
}

impl MatchDirectory {
    pub fn new(game_server_host: String, ports: PortPool) -> Self {
        Self {
            game_server_host,
            state: RwLock::new(DirectoryState {
                matches: HashMap::new(),
                ports,
            }),
        }
    }

    pub fn create(
        &self,
        request: CreateMatch,
        now: Instant,
        max_per_address: usize,
    ) -> Result<(Match, HostTicket), LobbyError> {
        if !(MIN_PLAYERS..=MAX_PLAYERS).contains(&request.max_players) {
            return Err(LobbyError::InvalidPlayerCount {
                min: MIN_PLAYERS,
                max: MAX_PLAYERS,
            });
        }

        let mut state = self.write();

        // Checked before a port is taken. Acquiring first and releasing on refusal would
        // work, but it makes the pool briefly emptier than it is, which is the very thing
        // a flood is trying to achieve.
        if max_per_address > 0 {
            let hosted = state
                .matches
                .values()
                .filter(|entry| entry.host_address == request.host_address)
                .count();

            if hosted >= max_per_address {
                return Err(LobbyError::TooManyMatches {
                    max: max_per_address,
                });
            }
        }

        let port = state.ports.acquire(now)?;

        // Generated once and kept, rather than minted fresh on the way out. The copy the
        // host is handed is the only thing that can start this match later, so the lobby
        // has to remember which one it gave away.
        let host_ticket = HostTicket(Uuid::new_v4());

        let entry = Match {
            id: MatchId(Uuid::new_v4()),
            name: request.name,
            players: vec![request.host.clone()],
            host: request.host,
            max_players: request.max_players,
            mode: request.mode,
            bots: 0,
            state: MatchState::WaitingForPlayers,
            endpoint: GameServerEndpoint {
                host: self.game_server_host.clone(),
                port,
            },
            host_address: request.host_address,
            created_at: now,
            last_seen: now,
            host_ticket,
        };

        state.matches.insert(entry.id, entry.clone());

        Ok((entry, host_ticket))
    }

    pub fn get(&self, id: MatchId) -> Option<Match> {
        self.read().matches.get(&id).cloned()
    }

    pub fn open_matches(&self) -> Vec<Match> {
        let mut open: Vec<Match> = self
            .read()
            .matches
            .values()
            .filter(|entry| entry.state == MatchState::WaitingForPlayers && !entry.is_full())
            .cloned()
            .collect();

        open.sort_by(|left, right| left.name.as_str().cmp(right.name.as_str()));
        open
    }

    pub fn join(&self, id: MatchId, player: DisplayName) -> Result<Admitted, LobbyError> {
        let mut state = self.write();
        let entry = state
            .matches
            .get_mut(&id)
            .ok_or(LobbyError::MatchNotFound)?;

        if entry.state == MatchState::InProgress {
            return Err(LobbyError::MatchAlreadyStarted);
        }

        if entry.is_full() {
            return Err(LobbyError::MatchFull);
        }

        entry.players.push(player);

        Ok(Admitted {
            endpoint: entry.endpoint.clone(),
        })
    }

    /// Claims an open seat for a bot instead of waiting for someone to join it.
    ///
    /// Host-only for the same reason `start` is: match ids are public, so without the
    /// ticket a stranger could fill every open match with bots and lock everyone else
    /// out. Refused once the match is full, the same as a human trying to join it.
    pub fn add_bot(&self, id: MatchId, ticket: HostTicket) -> Result<Match, LobbyError> {
        let mut state = self.write();
        let entry = state
            .matches
            .get_mut(&id)
            .ok_or(LobbyError::MatchNotFound)?;

        if entry.host_ticket != ticket {
            return Err(LobbyError::Unauthorized);
        }

        if entry.state == MatchState::InProgress {
            return Err(LobbyError::MatchAlreadyStarted);
        }

        if entry.is_full() {
            return Err(LobbyError::MatchFull);
        }

        entry.bots += 1;
        Ok(entry.clone())
    }

    /// Starting is the host's call, so it has to be proved to come from the host.
    ///
    /// Match ids are public: `GET /v1/matches` hands them to anyone. Without this check
    /// a stranger could walk that list and start every open match in it, which takes each
    /// one out of the listing and refuses everybody still trying to join, one unauthorised
    /// request per match.
    pub fn start(&self, id: MatchId, ticket: HostTicket, now: Instant) -> Result<(), LobbyError> {
        let mut state = self.write();
        let entry = state
            .matches
            .get_mut(&id)
            .ok_or(LobbyError::MatchNotFound)?;

        // Before the already-started check, so a wrong ticket cannot tell the difference
        // between a match that is running and one that is waiting.
        if entry.host_ticket != ticket {
            return Err(LobbyError::Unauthorized);
        }

        if entry.state == MatchState::InProgress {
            return Err(LobbyError::MatchAlreadyStarted);
        }

        // The instance builds its arena from the seated count, so starting below the
        // minimum would hand it a degenerate match rather than a short one. A bot the
        // host added counts towards it same as a human: both are a seat the instance
        // will actually run.
        let seated = entry.players.len() + usize::from(entry.bots);
        if seated < usize::from(MIN_PLAYERS) {
            return Err(LobbyError::NotEnoughPlayers {
                min: MIN_PLAYERS,
                joined: seated,
            });
        }

        entry.state = MatchState::InProgress;

        // `last_seen` still holds the creation time, and the reaper measures the silence
        // of a running match from it. Without this, a lobby that sat in the list longer
        // than `silent` is reaped the instant it starts, before the instance picking it
        // up has had a chance to say anything.
        entry.last_seen = now;
        Ok(())
    }

    /// What the instance owning `port` should run, once there is something to run.
    ///
    /// Only a started match answers. Before that there is nobody to play against, and an
    /// instance booted early would spend its grace window waiting on an empty arena.
    pub fn observe_instance(&self, port: u16, now: Instant) {
        self.write().ports.observe_instance(port, now);
    }

    pub fn assignment(&self, port: u16) -> Option<Assignment> {
        self.read()
            .matches
            .values()
            .find(|entry| entry.endpoint.port == port && entry.state == MatchState::InProgress)
            .map(|entry| Assignment {
                match_id: entry.id,
                // Every seat the host opened, and how many of them will connect: `join`
                // refuses a started match, so this vec no longer moves. The instance waits
                // for the humans only and hands the other seats to bots.
                players: entry.max_players,
                humans: u8::try_from(entry.players.len()).unwrap_or(entry.max_players),
                mode: entry.mode,
            })
    }

    /// Records that the instance running a match is still alive.
    pub fn heartbeat(&self, id: MatchId, now: Instant) -> Result<(), LobbyError> {
        let mut state = self.write();
        let entry = state
            .matches
            .get_mut(&id)
            .ok_or(LobbyError::MatchNotFound)?;

        entry.last_seen = now;
        Ok(())
    }

    /// Removes matches nobody is looking after and gives their ports back.
    ///
    /// Only a match in progress is judged on its heartbeat. One still waiting for players
    /// has no instance yet, so there is nothing to hear from it, and reaping it for
    /// silence would delete every match on a lobby whose game servers do not exist. It is
    /// judged on whether anyone ever joined instead.
    pub fn reap(&self, now: Instant, lifetimes: Lifetimes) -> Vec<Reaped> {
        let mut state = self.write();
        let mut doomed = Vec::new();

        for entry in state.matches.values() {
            let reason = match entry.state {
                MatchState::InProgress => {
                    if now.duration_since(entry.last_seen) > lifetimes.silent {
                        Some(ReapReason::InstanceWentSilent)
                    } else {
                        None
                    }
                }
                MatchState::WaitingForPlayers => {
                    let waited = now.duration_since(entry.created_at);
                    if waited > lifetimes.waiting {
                        Some(ReapReason::NeverStarted)
                    } else if entry.players.len() <= 1 && waited > lifetimes.unjoined {
                        Some(ReapReason::NobodyJoined)
                    } else {
                        None
                    }
                }
            };

            if let Some(reason) = reason {
                doomed.push(Reaped {
                    id: entry.id,
                    port: entry.endpoint.port,
                    reason,
                });
            }
        }

        for reaped in &doomed {
            state.matches.remove(&reaped.id);
            state.ports.release(reaped.port);
        }

        doomed
    }

    pub fn finish(&self, id: MatchId) -> Result<(), LobbyError> {
        let mut state = self.write();
        let entry = state.matches.remove(&id).ok_or(LobbyError::MatchNotFound)?;
        state.ports.release(entry.endpoint.port);
        Ok(())
    }

    fn read(&self) -> RwLockReadGuard<'_, DirectoryState> {
        self.state
            .read()
            .unwrap_or_else(|poisoned| poisoned.into_inner())
    }

    fn write(&self) -> RwLockWriteGuard<'_, DirectoryState> {
        self.state
            .write()
            .unwrap_or_else(|poisoned| poisoned.into_inner())
    }
}

#[cfg(test)]
mod tests {
    use std::net::Ipv4Addr;

    /// A distinct address per caller, so the per-address cap can be exercised without
    /// every test pretending to be the same machine.
    fn caller(last: u8) -> IpAddr {
        IpAddr::V4(Ipv4Addr::new(203, 0, 113, last))
    }

    use super::*;

    fn directory_over(range: std::ops::RangeInclusive<u16>) -> MatchDirectory {
        let mut ports = PortPool::new(range.clone());
        let now = Instant::now();
        for port in range {
            ports.observe_instance(port, now);
        }
        MatchDirectory::new("game.blastlands.test".to_owned(), ports)
    }

    fn directory() -> MatchDirectory {
        directory_over(7000..=7001)
    }

    fn name(raw: &str) -> DisplayName {
        DisplayName::new(raw).expect("test name should be valid")
    }

    fn create(directory: &MatchDirectory, max_players: u8) -> Match {
        directory
            .create(
                CreateMatch {
                    name: name("Friday night"),
                    host: name("Bryan"),
                    max_players,
                    host_address: caller(1),
                    mode: GameMode::Arena,
                },
                Instant::now(),
                0,
            )
            .expect("create should succeed")
            .0
    }

    #[test]
    fn a_created_match_seats_its_host_and_gets_an_endpoint() {
        let directory = directory();
        let entry = create(&directory, 4);

        assert_eq!(entry.players, vec![name("Bryan")]);
        assert_eq!(entry.state, MatchState::WaitingForPlayers);
        assert_eq!(entry.endpoint.host, "game.blastlands.test");
        assert_eq!(entry.endpoint.port, 7000);
    }

    #[test]
    fn each_match_gets_its_own_port() {
        let directory = directory();

        let first = create(&directory, 2);
        let second = create(&directory, 2);

        assert_ne!(first.endpoint.port, second.endpoint.port);
    }

    #[test]
    fn creation_fails_once_the_port_pool_is_exhausted() {
        let directory = directory();
        create(&directory, 2);
        create(&directory, 2);

        let result = directory.create(
            CreateMatch {
                name: name("One too many"),
                host: name("Bryan"),
                max_players: 2,
                host_address: caller(1),
                mode: GameMode::Arena,
            },
            Instant::now(),
            0,
        );

        assert_eq!(result.err(), Some(LobbyError::NoCapacity));
    }

    #[test]
    fn player_count_outside_the_supported_range_is_rejected() {
        let directory = directory();

        for count in [0, 1, MAX_PLAYERS + 1] {
            let result = directory.create(
                CreateMatch {
                    name: name("Bad size"),
                    host: name("Bryan"),
                    max_players: count,
                    host_address: caller(1),
                    mode: GameMode::Arena,
                },
                Instant::now(),
                0,
            );

            assert_eq!(
                result.err(),
                Some(LobbyError::InvalidPlayerCount {
                    min: MIN_PLAYERS,
                    max: MAX_PLAYERS
                })
            );
        }
    }

    #[test]
    fn a_rejected_creation_does_not_leak_a_port() {
        let directory = directory();

        let _ = directory.create(
            CreateMatch {
                name: name("Bad size"),
                host: name("Bryan"),
                max_players: 99,
                host_address: caller(1),
                mode: GameMode::Arena,
            },
            Instant::now(),
            0,
        );

        assert_eq!(create(&directory, 2).endpoint.port, 7000);
    }

    #[test]
    fn joining_seats_the_player_and_returns_the_same_endpoint() {
        let directory = directory();
        let entry = create(&directory, 4);

        let admitted = directory
            .join(entry.id, name("Alex"))
            .expect("join should succeed");

        assert_eq!(admitted.endpoint, entry.endpoint);
        assert_eq!(directory.open_matches()[0].players.len(), 2);
    }

    #[test]
    fn a_full_match_rejects_further_players_and_leaves_the_lobby_list() {
        let directory = directory();
        let entry = create(&directory, 2);

        directory
            .join(entry.id, name("Alex"))
            .expect("second seat is free");

        assert_eq!(
            directory.join(entry.id, name("Sam")).err(),
            Some(LobbyError::MatchFull)
        );
        assert!(directory.open_matches().is_empty());
    }

    #[test]
    fn adding_a_bot_claims_a_seat_a_human_can_no_longer_join() {
        let directory = directory();
        let entry = create(&directory, 2);

        let updated = directory
            .add_bot(entry.id, entry.host_ticket)
            .expect("a seat is free");

        assert_eq!(updated.bots, 1);
        assert_eq!(
            directory.join(entry.id, name("Alex")).err(),
            Some(LobbyError::MatchFull)
        );
        assert!(directory.open_matches().is_empty());
    }

    #[test]
    fn a_bot_lets_a_solo_host_start() {
        let directory = directory();
        let entry = create(&directory, 4);

        assert_eq!(
            directory
                .start(entry.id, entry.host_ticket, Instant::now())
                .err(),
            Some(LobbyError::NotEnoughPlayers {
                min: MIN_PLAYERS,
                joined: 1
            })
        );

        directory
            .add_bot(entry.id, entry.host_ticket)
            .expect("a seat is free");

        directory
            .start(entry.id, entry.host_ticket, Instant::now())
            .expect("the host plus a bot meets the minimum");
    }

    #[test]
    fn a_full_match_refuses_another_bot() {
        let directory = directory();
        let entry = create(&directory, 2);
        directory
            .add_bot(entry.id, entry.host_ticket)
            .expect("a seat is free");

        assert_eq!(
            directory.add_bot(entry.id, entry.host_ticket).err(),
            Some(LobbyError::MatchFull)
        );
    }

    #[test]
    fn only_the_host_may_add_a_bot() {
        let directory = directory();
        let entry = create(&directory, 4);

        assert_eq!(
            directory.add_bot(entry.id, stranger_ticket()).err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn a_bot_cannot_be_added_once_the_match_has_started() {
        let directory = directory();
        let entry = create(&directory, 4);
        seat_a_guest(&directory, entry.id);
        directory
            .start(entry.id, entry.host_ticket, Instant::now())
            .expect("start should succeed");

        assert_eq!(
            directory.add_bot(entry.id, entry.host_ticket).err(),
            Some(LobbyError::MatchAlreadyStarted)
        );
    }

    /// `start` refuses a lobby below `MIN_PLAYERS`, so every test that wants a running
    /// match needs a guest in it. The host holds the first seat.
    fn seat_a_guest(directory: &MatchDirectory, id: MatchId) {
        directory.join(id, name("Alex")).expect("a seat is free");
    }

    #[test]
    fn a_started_match_leaves_the_lobby_list_and_refuses_joins() {
        let directory = directory();
        let entry = create(&directory, 4);
        seat_a_guest(&directory, entry.id);

        directory
            .start(entry.id, entry.host_ticket, Instant::now())
            .expect("start should succeed");

        assert!(directory.open_matches().is_empty());
        assert_eq!(
            directory.join(entry.id, name("Alex")).err(),
            Some(LobbyError::MatchAlreadyStarted)
        );
    }

    #[test]
    fn starting_a_match_twice_is_rejected() {
        let directory = directory();
        let entry = create(&directory, 4);
        seat_a_guest(&directory, entry.id);

        directory
            .start(entry.id, entry.host_ticket, Instant::now())
            .unwrap();

        assert_eq!(
            directory
                .start(entry.id, entry.host_ticket, Instant::now())
                .err(),
            Some(LobbyError::MatchAlreadyStarted)
        );
    }

    #[test]
    fn a_host_cannot_start_a_match_nobody_joined() {
        let directory = directory();
        let entry = create(&directory, 4);

        assert_eq!(
            directory
                .start(entry.id, entry.host_ticket, Instant::now())
                .err(),
            Some(LobbyError::NotEnoughPlayers {
                min: MIN_PLAYERS,
                joined: 1
            })
        );
        assert_eq!(directory.assignment(entry.endpoint.port), None);
    }

    #[test]
    fn a_started_match_builds_every_seat_and_counts_the_humans() {
        let directory = directory();
        let entry = create(&directory, 4);
        seat_a_guest(&directory, entry.id);
        directory
            .join(entry.id, name("Sam"))
            .expect("a third seat is free");

        directory
            .start(entry.id, entry.host_ticket, Instant::now())
            .expect("start should succeed");

        assert_eq!(
            directory.assignment(entry.endpoint.port),
            Some(Assignment {
                match_id: entry.id,
                players: 4,
                humans: 3,
                mode: GameMode::Arena,
            }),
            "the fourth seat nobody took is built and left to a bot, and not waited for"
        );
    }

    #[test]
    fn finishing_a_match_returns_its_port_to_the_pool() {
        let directory = directory();
        let first = create(&directory, 2);
        create(&directory, 2);

        directory.finish(first.id).expect("finish should succeed");

        assert_eq!(create(&directory, 2).endpoint.port, first.endpoint.port);
    }

    #[test]
    fn unknown_matches_are_reported_as_not_found() {
        let directory = directory();
        let unknown = MatchId(Uuid::new_v4());

        assert_eq!(
            directory.join(unknown, name("Alex")).err(),
            Some(LobbyError::MatchNotFound)
        );
        assert_eq!(
            directory
                .start(unknown, stranger_ticket(), Instant::now())
                .err(),
            Some(LobbyError::MatchNotFound)
        );
        assert_eq!(
            directory.finish(unknown).err(),
            Some(LobbyError::MatchNotFound)
        );
    }
    fn lifetimes() -> Lifetimes {
        Lifetimes {
            unjoined: Duration::from_secs(300),
            silent: Duration::from_secs(30),
            waiting: Duration::from_secs(900),
        }
    }

    // A ticket that belongs to nobody, for the cases where the call is expected to be
    // refused before the ticket is ever looked at.
    fn stranger_ticket() -> HostTicket {
        HostTicket(Uuid::new_v4())
    }

    fn make(directory: &MatchDirectory, address: IpAddr, now: Instant) -> MatchId {
        made(directory, address, now).0
    }

    fn made(directory: &MatchDirectory, address: IpAddr, now: Instant) -> (MatchId, HostTicket) {
        let (entry, ticket) = directory
            .create(
                CreateMatch {
                    name: name("Night raid"),
                    host: name("Bryan"),
                    max_players: 4,
                    host_address: address,
                    mode: GameMode::Arena,
                },
                now,
                0,
            )
            .expect("create should succeed");

        (entry.id, ticket)
    }

    #[test]
    fn a_match_nobody_joined_is_taken_away_and_its_port_returned() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        make(&directory, caller(1), start);

        // The pool holds exactly one port, so a second create can only succeed if the
        // first match really gave its port back.
        let reaped = directory.reap(start + Duration::from_secs(301), lifetimes());

        assert_eq!(reaped.len(), 1);
        assert_eq!(reaped[0].reason, ReapReason::NobodyJoined);
        assert!(directory.open_matches().is_empty());
        directory.observe_instance(7000, start + Duration::from_secs(302));
        make(&directory, caller(2), start + Duration::from_secs(302));
    }

    #[test]
    fn a_match_someone_joined_is_left_alone() {
        let directory = directory_over(7000..=7001);
        let start = Instant::now();
        let id = make(&directory, caller(1), start);
        directory
            .join(id, name("Sam"))
            .expect("join should succeed");

        let reaped = directory.reap(start + Duration::from_secs(301), lifetimes());

        assert!(reaped.is_empty(), "a match with players in it was reaped");
    }

    #[test]
    fn a_match_still_waiting_is_never_reaped_for_silence() {
        // The trap this design exists to avoid. A match nobody has started has no
        // instance, so it has nothing to heartbeat with. Judging it on silence would
        // delete every match on a lobby whose game servers do not exist yet, which is
        // exactly the state of this project.
        let directory = directory_over(7000..=7001);
        let start = Instant::now();
        let id = make(&directory, caller(1), start);
        directory
            .join(id, name("Sam"))
            .expect("join should succeed");

        let long_after = start + Duration::from_secs(899);
        assert!(directory.reap(long_after, lifetimes()).is_empty());
    }

    #[test]
    fn a_match_never_started_is_reaped_at_its_deadline_even_with_players_in_it() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let id = make(&directory, caller(1), start);
        directory
            .join(id, name("Sam"))
            .expect("join should succeed");

        assert!(directory
            .reap(start + Duration::from_secs(900), lifetimes())
            .is_empty());

        let reaped = directory.reap(start + Duration::from_secs(901), lifetimes());

        assert_eq!(reaped.len(), 1);
        assert_eq!(reaped[0].reason, ReapReason::NeverStarted);
        directory.observe_instance(7000, start + Duration::from_secs(902));
        make(&directory, caller(2), start + Duration::from_secs(902));
    }

    #[test]
    fn a_started_match_is_not_held_to_the_waiting_deadline() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);
        directory
            .start(id, ticket, start + Duration::from_secs(890))
            .expect("start should succeed");
        directory
            .heartbeat(id, start + Duration::from_secs(1_000))
            .expect("heartbeat should succeed");

        assert!(directory
            .reap(start + Duration::from_secs(1_001), lifetimes())
            .is_empty());
    }

    #[test]
    fn a_running_match_whose_instance_went_quiet_is_reaped() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);
        directory
            .start(id, ticket, start)
            .expect("start should succeed");

        let reaped = directory.reap(start + Duration::from_secs(31), lifetimes());

        assert_eq!(reaped.len(), 1);
        assert_eq!(reaped[0].reason, ReapReason::InstanceWentSilent);
        directory.observe_instance(7000, start + Duration::from_secs(32));
        make(&directory, caller(2), start + Duration::from_secs(32));
    }

    #[test]
    fn a_port_has_no_assignment_until_its_match_starts() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);

        assert_eq!(directory.assignment(7000), None);

        directory
            .start(id, ticket, start)
            .expect("start should succeed");

        assert_eq!(
            directory.assignment(7000),
            Some(Assignment {
                match_id: id,
                players: 4,
                humans: 2,
                mode: GameMode::Arena,
            }),
            "the instance builds every seat the host opened and waits for the joined players only"
        );
    }

    #[test]
    fn a_port_nobody_was_given_has_no_assignment() {
        let directory = directory_over(7000..=7001);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);
        directory
            .start(id, ticket, start)
            .expect("start should succeed");

        assert_eq!(directory.assignment(7001), None);
        assert_eq!(directory.assignment(9999), None);
    }

    #[test]
    fn a_finished_match_releases_its_assignment() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);
        directory
            .start(id, ticket, start)
            .expect("start should succeed");
        directory.finish(id).expect("finish should succeed");

        assert_eq!(directory.assignment(7000), None);
    }

    #[test]
    fn starting_late_still_leaves_the_instance_its_grace_window() {
        let directory = directory_over(7000..=7000);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);

        // Longer than `silent`, which is what a lobby that waited for players looks
        // like. Measured from creation it is already over; measured from the start it
        // has not begun.
        let started_at = start + Duration::from_secs(120);
        directory
            .start(id, ticket, started_at)
            .expect("start should succeed");

        assert!(directory.reap(started_at, lifetimes()).is_empty());
        assert!(directory
            .reap(started_at + Duration::from_secs(29), lifetimes())
            .is_empty());
        assert_eq!(
            directory
                .reap(started_at + Duration::from_secs(31), lifetimes())
                .len(),
            1
        );
    }

    #[test]
    fn a_heartbeat_keeps_a_running_match_alive() {
        let directory = directory_over(7000..=7001);
        let start = Instant::now();
        let (id, ticket) = made(&directory, caller(1), start);
        seat_a_guest(&directory, id);
        directory
            .start(id, ticket, start)
            .expect("start should succeed");

        directory
            .heartbeat(id, start + Duration::from_secs(25))
            .expect("heartbeat should succeed");

        assert!(
            directory
                .reap(start + Duration::from_secs(31), lifetimes())
                .is_empty(),
            "a match that reported in was reaped anyway"
        );
    }

    #[test]
    fn one_address_may_only_hold_so_many_matches() {
        let directory = directory_over(7000..=7010);
        let now = Instant::now();

        for _ in 0..2 {
            directory
                .create(
                    CreateMatch {
                        name: name("Mine"),
                        host: name("Bryan"),
                        max_players: 4,
                        host_address: caller(1),
                        mode: GameMode::Arena,
                    },
                    now,
                    2,
                )
                .expect("under the cap");
        }

        let refused = directory.create(
            CreateMatch {
                name: name("One too many"),
                host: name("Bryan"),
                max_players: 4,
                host_address: caller(1),
                mode: GameMode::Arena,
            },
            now,
            2,
        );
        assert!(matches!(refused, Err(LobbyError::TooManyMatches { .. })));

        directory
            .create(
                CreateMatch {
                    name: name("Someone else"),
                    host: name("Sam"),
                    max_players: 4,
                    host_address: caller(2),
                    mode: GameMode::Arena,
                },
                now,
                2,
            )
            .expect("a different address was punished for the first one");
    }
}
