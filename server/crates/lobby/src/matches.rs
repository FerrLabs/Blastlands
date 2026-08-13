use std::collections::HashMap;
use std::sync::{RwLock, RwLockReadGuard, RwLockWriteGuard};

use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::error::LobbyError;
use crate::names::DisplayName;
use crate::ports::PortPool;

pub const MIN_PLAYERS: u8 = 2;
pub const MAX_PLAYERS: u8 = 8;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct MatchId(Uuid);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(transparent)]
pub struct JoinTicket(Uuid);

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
    pub state: MatchState,
    pub endpoint: GameServerEndpoint,
}

impl Match {
    pub fn is_full(&self) -> bool {
        self.players.len() >= usize::from(self.max_players)
    }
}

#[derive(Debug)]
pub struct CreateMatch {
    pub name: DisplayName,
    pub host: DisplayName,
    pub max_players: u8,
}

#[derive(Debug)]
pub struct Admitted {
    pub endpoint: GameServerEndpoint,
    pub ticket: JoinTicket,
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

    pub fn create(&self, request: CreateMatch) -> Result<(Match, JoinTicket), LobbyError> {
        if !(MIN_PLAYERS..=MAX_PLAYERS).contains(&request.max_players) {
            return Err(LobbyError::InvalidPlayerCount {
                min: MIN_PLAYERS,
                max: MAX_PLAYERS,
            });
        }

        let mut state = self.write();
        let port = state.ports.acquire()?;

        let entry = Match {
            id: MatchId(Uuid::new_v4()),
            name: request.name,
            players: vec![request.host.clone()],
            host: request.host,
            max_players: request.max_players,
            state: MatchState::WaitingForPlayers,
            endpoint: GameServerEndpoint {
                host: self.game_server_host.clone(),
                port,
            },
        };

        state.matches.insert(entry.id, entry.clone());

        Ok((entry, JoinTicket(Uuid::new_v4())))
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
            ticket: JoinTicket(Uuid::new_v4()),
        })
    }

    pub fn start(&self, id: MatchId) -> Result<(), LobbyError> {
        let mut state = self.write();
        let entry = state
            .matches
            .get_mut(&id)
            .ok_or(LobbyError::MatchNotFound)?;

        if entry.state == MatchState::InProgress {
            return Err(LobbyError::MatchAlreadyStarted);
        }

        entry.state = MatchState::InProgress;
        Ok(())
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
    use super::*;

    fn directory() -> MatchDirectory {
        MatchDirectory::new(
            "game.blastlands.test".to_owned(),
            PortPool::new(7000..=7001),
        )
    }

    fn name(raw: &str) -> DisplayName {
        DisplayName::new(raw).expect("test name should be valid")
    }

    fn create(directory: &MatchDirectory, max_players: u8) -> Match {
        directory
            .create(CreateMatch {
                name: name("Friday night"),
                host: name("Bryan"),
                max_players,
            })
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

        let result = directory.create(CreateMatch {
            name: name("One too many"),
            host: name("Bryan"),
            max_players: 2,
        });

        assert_eq!(result.err(), Some(LobbyError::NoCapacity));
    }

    #[test]
    fn player_count_outside_the_supported_range_is_rejected() {
        let directory = directory();

        for count in [0, 1, MAX_PLAYERS + 1] {
            let result = directory.create(CreateMatch {
                name: name("Bad size"),
                host: name("Bryan"),
                max_players: count,
            });

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

        let _ = directory.create(CreateMatch {
            name: name("Bad size"),
            host: name("Bryan"),
            max_players: 99,
        });

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
    fn each_join_gets_a_distinct_ticket() {
        let directory = directory();
        let entry = create(&directory, 4);

        let first = directory.join(entry.id, name("Alex")).unwrap().ticket;
        let second = directory.join(entry.id, name("Sam")).unwrap().ticket;

        assert_ne!(first, second);
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
    fn a_started_match_leaves_the_lobby_list_and_refuses_joins() {
        let directory = directory();
        let entry = create(&directory, 4);

        directory.start(entry.id).expect("start should succeed");

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

        directory.start(entry.id).unwrap();

        assert_eq!(
            directory.start(entry.id).err(),
            Some(LobbyError::MatchAlreadyStarted)
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
            directory.start(unknown).err(),
            Some(LobbyError::MatchNotFound)
        );
        assert_eq!(
            directory.finish(unknown).err(),
            Some(LobbyError::MatchNotFound)
        );
    }
}
