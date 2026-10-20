use std::collections::HashMap;
use std::fmt;
use std::net::IpAddr;
use std::ops::RangeInclusive;
use std::time::{Duration, Instant};

use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::error::LobbyError;
use crate::ports::PortPool;
use crate::tickets::TicketKey;

pub const MAX_SLOTS: u8 = 16;
pub const SERVER_SILENT_TTL: Duration = Duration::from_secs(60);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ServerId(Uuid);

impl fmt::Display for ServerId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

#[derive(Clone, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ServerKey(String);

impl ServerKey {
    pub fn new(raw: &str) -> Self {
        Self(raw.to_owned())
    }

    fn generate() -> Self {
        Self(random_hex())
    }

    fn matches(&self, other: &Self) -> bool {
        let (left, right) = (self.0.as_bytes(), other.0.as_bytes());
        left.len() == right.len()
            && left
                .iter()
                .zip(right)
                .fold(0u8, |difference, (a, b)| difference | (a ^ b))
                == 0
    }
}

impl fmt::Debug for ServerKey {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str("ServerKey(..)")
    }
}

#[derive(Clone, Serialize)]
#[serde(transparent)]
pub struct TicketSecret(String);

impl fmt::Debug for TicketSecret {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str("TicketSecret(..)")
    }
}

fn random_hex() -> String {
    format!("{}{}", Uuid::new_v4().simple(), Uuid::new_v4().simple())
}

#[derive(Debug)]
pub struct RegisterServer {
    pub first_port: u16,
    pub slots: u8,
    pub listed: bool,
    pub address: IpAddr,
}

#[derive(Debug)]
pub struct Registered {
    pub id: ServerId,
    pub key: ServerKey,
    pub ticket_secret: TicketSecret,
}

#[derive(Debug)]
struct GameServer {
    address: IpAddr,
    listed: bool,
    key: ServerKey,
    ticket_key: TicketKey,
    ports: PortPool,
    last_seen: Instant,
}

#[derive(Debug)]
pub struct Slot {
    pub server: ServerId,
    pub address: IpAddr,
    pub port: u16,
}

#[derive(Debug, Default)]
pub struct ServerRegistry {
    servers: HashMap<ServerId, GameServer>,
    keys: HashMap<ServerKey, ServerId>,
}

impl ServerRegistry {
    pub fn register(
        &mut self,
        request: RegisterServer,
        now: Instant,
        max_per_address: usize,
    ) -> Result<Registered, LobbyError> {
        let ports = slot_ports(request.first_port, request.slots)?;

        if max_per_address > 0 {
            let registered = self
                .servers
                .values()
                .filter(|server| server.address == request.address)
                .count();

            if registered >= max_per_address {
                return Err(LobbyError::TooManyServers {
                    max: max_per_address,
                });
            }
        }

        let secret = random_hex();
        let ticket_key = TicketKey::new(secret.as_bytes().to_vec())
            .expect("two simple uuids are longer than the minimum key");

        let id = ServerId(Uuid::new_v4());
        let key = ServerKey::generate();

        self.keys.insert(key.clone(), id);
        self.servers.insert(
            id,
            GameServer {
                address: request.address,
                listed: request.listed,
                key: key.clone(),
                ticket_key,
                ports: PortPool::new(ports),
                last_seen: now,
            },
        );

        Ok(Registered {
            id,
            key,
            ticket_secret: TicketSecret(secret),
        })
    }

    pub fn authenticate(&self, key: &ServerKey) -> Result<ServerId, LobbyError> {
        self.keys.get(key).copied().ok_or(LobbyError::Unauthorized)
    }

    pub fn contains(&self, id: ServerId) -> bool {
        self.servers.contains_key(&id)
    }

    pub fn ticket_key(&self, id: ServerId) -> Option<&TicketKey> {
        self.servers.get(&id).map(|server| &server.ticket_key)
    }

    pub fn touch(&mut self, id: ServerId, now: Instant) {
        if let Some(server) = self.servers.get_mut(&id) {
            server.last_seen = now;
        }
    }

    pub fn observe_slot(&mut self, id: ServerId, port: u16, now: Instant) {
        if let Some(server) = self.servers.get_mut(&id) {
            server.last_seen = now;
            server.ports.observe_instance(port, now);
        }
    }

    pub fn acquire_on(
        &mut self,
        id: ServerId,
        key: Option<&ServerKey>,
        now: Instant,
    ) -> Result<Slot, LobbyError> {
        let server = self
            .servers
            .get_mut(&id)
            .ok_or(LobbyError::ServerNotFound)?;

        if !server.listed && !key.is_some_and(|key| key.matches(&server.key)) {
            return Err(LobbyError::Unauthorized);
        }

        let port = server.ports.acquire(now)?;
        Ok(Slot {
            server: id,
            address: server.address,
            port,
        })
    }

    pub fn acquire_any(&mut self, now: Instant) -> Result<Slot, LobbyError> {
        self.servers
            .iter_mut()
            .filter(|(_, server)| server.listed)
            .find_map(|(id, server)| {
                server.ports.acquire(now).ok().map(|port| Slot {
                    server: *id,
                    address: server.address,
                    port,
                })
            })
            .ok_or(LobbyError::NoCapacity)
    }

    pub fn release(&mut self, id: ServerId, port: u16) {
        if let Some(server) = self.servers.get_mut(&id) {
            server.ports.release(port);
        }
    }

    pub fn expire(&mut self, now: Instant) -> Vec<ServerId> {
        let gone: Vec<ServerId> = self
            .servers
            .iter()
            .filter(|(_, server)| now.duration_since(server.last_seen) > SERVER_SILENT_TTL)
            .map(|(id, _)| *id)
            .collect();

        for id in &gone {
            if let Some(server) = self.servers.remove(id) {
                self.keys.remove(&server.key);
            }
        }

        gone
    }
}

fn slot_ports(first_port: u16, slots: u8) -> Result<RangeInclusive<u16>, LobbyError> {
    let invalid = LobbyError::InvalidSlots { max: MAX_SLOTS };

    if first_port == 0 || !(1..=MAX_SLOTS).contains(&slots) {
        return Err(invalid);
    }

    let last = first_port
        .checked_add(u16::from(slots) - 1)
        .ok_or(invalid)?;

    Ok(first_port..=last)
}

#[cfg(test)]
mod tests {
    use std::net::Ipv4Addr;

    use super::*;

    fn address(last: u8) -> IpAddr {
        IpAddr::V4(Ipv4Addr::new(198, 51, 100, last))
    }

    fn polled(
        registry: &mut ServerRegistry,
        first_port: u16,
        slots: u8,
        listed: bool,
        now: Instant,
    ) -> Registered {
        let server = registry
            .register(request(first_port, slots, listed), now, 0)
            .unwrap();
        for port in slot_ports(first_port, slots).unwrap() {
            registry.observe_slot(server.id, port, now);
        }
        server
    }

    #[test]
    fn a_slot_is_not_offered_before_it_polls() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = registry.register(request(7777, 2, true), now, 0).unwrap();

        assert!(matches!(
            registry.acquire_any(now),
            Err(LobbyError::NoCapacity)
        ));

        registry.observe_slot(server.id, 7778, now);

        assert_eq!(registry.acquire_any(now).unwrap().port, 7778);
    }

    fn request(first_port: u16, slots: u8, listed: bool) -> RegisterServer {
        RegisterServer {
            first_port,
            slots,
            listed,
            address: address(1),
        }
    }

    #[test]
    fn a_registered_server_offers_each_of_its_slots_once() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = polled(&mut registry, 7777, 2, true, now);

        let first = registry.acquire_any(now).unwrap();
        let second = registry.acquire_any(now).unwrap();

        assert_eq!((first.server, first.port), (server.id, 7777));
        assert_eq!((second.server, second.port), (server.id, 7778));
        assert_eq!(first.address, address(1));
        assert!(matches!(
            registry.acquire_any(now),
            Err(LobbyError::NoCapacity)
        ));
    }

    #[test]
    fn an_unlisted_server_is_never_picked_for_a_stranger() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = polled(&mut registry, 7777, 1, false, now);

        assert!(matches!(
            registry.acquire_any(now),
            Err(LobbyError::NoCapacity)
        ));
        assert!(matches!(
            registry.acquire_on(server.id, None, now),
            Err(LobbyError::Unauthorized)
        ));
        assert!(matches!(
            registry.acquire_on(server.id, Some(&ServerKey::new("guess")), now),
            Err(LobbyError::Unauthorized)
        ));
        assert_eq!(
            registry
                .acquire_on(server.id, Some(&server.key), now)
                .unwrap()
                .port,
            7777
        );
    }

    #[test]
    fn a_listed_server_can_be_chosen_by_anyone() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = polled(&mut registry, 7777, 1, true, now);

        assert!(registry.acquire_on(server.id, None, now).is_ok());
    }

    #[test]
    fn the_key_identifies_its_server_and_nothing_else_does() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = registry.register(request(7777, 1, true), now, 0).unwrap();

        assert_eq!(registry.authenticate(&server.key).unwrap(), server.id);
        assert!(matches!(
            registry.authenticate(&ServerKey::new("")),
            Err(LobbyError::Unauthorized)
        ));
    }

    #[test]
    fn every_server_signs_with_its_own_secret() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let first = registry.register(request(7777, 1, true), now, 0).unwrap();
        let second = registry.register(request(7777, 1, true), now, 0).unwrap();

        assert_ne!(first.ticket_secret.0, second.ticket_secret.0);
        assert_ne!(first.key, second.key);
        assert!(first.ticket_secret.0.len() >= crate::tickets::MIN_KEY_BYTES);
    }

    #[test]
    fn slot_counts_and_ports_outside_the_range_are_refused() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();

        for (port, slots) in [(7777, 0), (7777, MAX_SLOTS + 1), (0, 1), (u16::MAX, 2)] {
            assert!(
                matches!(
                    registry.register(request(port, slots, true), now, 0),
                    Err(LobbyError::InvalidSlots { .. })
                ),
                "{port} with {slots} slots should be refused"
            );
        }

        assert!(registry
            .register(request(u16::MAX, 1, true), now, 0)
            .is_ok());
    }

    #[test]
    fn one_address_may_only_register_so_many_servers() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        registry.register(request(7777, 1, true), now, 1).unwrap();

        assert!(matches!(
            registry.register(request(7778, 1, true), now, 1),
            Err(LobbyError::TooManyServers { max: 1 })
        ));
    }

    #[test]
    fn a_silent_idle_server_is_forgotten_with_its_key() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = registry.register(request(7777, 1, true), now, 0).unwrap();
        let later = now + SERVER_SILENT_TTL + Duration::from_secs(1);

        assert_eq!(registry.expire(later), vec![server.id]);
        assert!(!registry.contains(server.id));
        assert!(registry.authenticate(&server.key).is_err());
    }

    #[test]
    fn a_match_heartbeat_keeps_its_server_alive() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = registry.register(request(7777, 1, true), now, 0).unwrap();
        let later = now + SERVER_SILENT_TTL;

        registry.touch(server.id, later);

        assert!(registry.expire(later + Duration::from_secs(1)).is_empty());
        assert!(registry.contains(server.id));
    }

    #[test]
    fn a_slot_poll_keeps_its_server_alive() {
        let mut registry = ServerRegistry::default();
        let now = Instant::now();
        let server = registry.register(request(7777, 1, true), now, 0).unwrap();
        let later = now + SERVER_SILENT_TTL;

        registry.observe_slot(server.id, 7777, later);

        assert!(registry.expire(later + Duration::from_secs(1)).is_empty());
    }
}
