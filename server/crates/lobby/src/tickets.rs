use std::fmt;
use std::time::Duration;

use hmac::{Hmac, Mac};
use serde::Serialize;
use sha2::Sha256;
use uuid::Uuid;

use crate::matches::MatchId;
use crate::names::DisplayName;

const FORMAT: &str = "v1";
pub const MIN_KEY_BYTES: usize = 32;
pub const CONNECT_GRACE: Duration = Duration::from_secs(120);

#[derive(Clone)]
pub struct TicketKey(Vec<u8>);

impl TicketKey {
    pub fn new(bytes: Vec<u8>) -> Option<Self> {
        (bytes.len() >= MIN_KEY_BYTES).then_some(Self(bytes))
    }
}

impl fmt::Debug for TicketKey {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str("TicketKey(..)")
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(transparent)]
pub struct GameTicket(String);

impl GameTicket {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

#[derive(Debug, Clone)]
pub struct TicketSigner {
    key: TicketKey,
    lifetime: Duration,
}

impl TicketSigner {
    pub fn new(key: TicketKey, lifetime: Duration) -> Self {
        Self { key, lifetime }
    }

    pub fn issue(&self, id: MatchId, player: &DisplayName, since_epoch: Duration) -> GameTicket {
        let expires = (since_epoch + self.lifetime).as_secs();
        self.sign(id, player, expires, Uuid::new_v4())
    }

    fn sign(&self, id: MatchId, player: &DisplayName, expires: u64, nonce: Uuid) -> GameTicket {
        let payload = format!(
            "{FORMAT}.{id}.{}.{expires}.{}",
            hex(player.as_str().as_bytes()),
            nonce.simple()
        );

        let mut mac =
            Hmac::<Sha256>::new_from_slice(&self.key.0).expect("HMAC accepts a key of any length");
        mac.update(payload.as_bytes());
        let signature = hex(&mac.finalize().into_bytes());

        GameTicket(format!("{payload}.{signature}"))
    }
}

fn hex(bytes: &[u8]) -> String {
    bytes.iter().map(|byte| format!("{byte:02x}")).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    const KEY: &[u8] = b"blastlands-test-ticket-key-0123456789";
    const MATCH: &str = "0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b";

    fn signer(lifetime: Duration) -> TicketSigner {
        TicketSigner::new(TicketKey::new(KEY.to_vec()).expect("long enough"), lifetime)
    }

    fn match_id() -> MatchId {
        serde_json::from_str(&format!("\"{MATCH}\"")).expect("a match id")
    }

    fn player(name: &str) -> DisplayName {
        DisplayName::new(name).expect("a display name")
    }

    #[test]
    fn signs_the_vector_the_game_server_tests_verify() {
        let nonce: Uuid = "00112233-4455-6677-8899-aabbccddeeff".parse().unwrap();

        let ticket =
            signer(Duration::ZERO).sign(match_id(), &player("Bryan"), 1_790_000_000, nonce);

        assert_eq!(
            ticket.as_str(),
            "v1.0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b.427279616e.1790000000.\
             00112233445566778899aabbccddeeff.\
             da14ccf2b112adda120a715ca50537cba93eae34638d2fa6d62782b01784c7a7"
        );
    }

    #[test]
    fn expires_one_lifetime_after_it_is_issued() {
        let ticket = signer(Duration::from_secs(300)).issue(
            match_id(),
            &player("Bryan"),
            Duration::from_secs(1_000),
        );

        let expires = ticket.as_str().split('.').nth(3).expect("an expiry field");
        assert_eq!(expires, "1300");
    }

    #[test]
    fn two_tickets_for_the_same_player_differ() {
        let signer = signer(Duration::from_secs(300));
        let now = Duration::from_secs(1_000);

        let first = signer.issue(match_id(), &player("Bryan"), now);
        let second = signer.issue(match_id(), &player("Bryan"), now);

        assert_ne!(
            first, second,
            "a reused nonce would let one ticket stand in for another"
        );
    }

    #[test]
    fn a_short_key_is_refused() {
        assert!(TicketKey::new(vec![0; MIN_KEY_BYTES - 1]).is_none());
        assert!(TicketKey::new(vec![0; MIN_KEY_BYTES]).is_some());
    }

    #[test]
    fn the_key_never_reaches_a_debug_print() {
        let key = TicketKey::new(KEY.to_vec()).unwrap();

        assert!(!format!("{key:?}").contains("blastlands-test"));
    }
}
