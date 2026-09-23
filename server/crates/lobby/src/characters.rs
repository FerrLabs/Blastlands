use serde::Deserialize;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Character {
    Demolisher,
    Runner,
    Grenadier,
    Sapper,
}

impl Character {
    pub fn token(self) -> &'static str {
        match self {
            Character::Demolisher => "demolisher",
            Character::Runner => "runner",
            Character::Grenadier => "grenadier",
            Character::Sapper => "sapper",
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_the_tokens_the_game_server_parses() {
        for (json, character) in [
            ("\"demolisher\"", Character::Demolisher),
            ("\"runner\"", Character::Runner),
            ("\"grenadier\"", Character::Grenadier),
            ("\"sapper\"", Character::Sapper),
        ] {
            let read: Character = serde_json::from_str(json).expect(json);
            assert_eq!(read, character);
            assert_eq!(format!("\"{}\"", read.token()), json);
        }
    }

    #[test]
    fn refuses_a_character_that_does_not_exist() {
        assert!(serde_json::from_str::<Character>("\"hoarder\"").is_err());
        assert!(serde_json::from_str::<Character>("\"Demolisher\"").is_err());
    }
}
