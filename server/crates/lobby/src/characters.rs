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

    pub fn portrait(self) -> &'static [u8] {
        match self {
            Character::Demolisher => include_bytes!("site/demolisher.webp"),
            Character::Runner => include_bytes!("site/runner.webp"),
            Character::Grenadier => include_bytes!("site/grenadier.webp"),
            Character::Sapper => include_bytes!("site/sapper.webp"),
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
    fn every_character_has_its_own_webp_portrait() {
        let all = [
            Character::Demolisher,
            Character::Runner,
            Character::Grenadier,
            Character::Sapper,
        ];
        for character in all {
            let portrait = character.portrait();
            assert_eq!(&portrait[..4], b"RIFF", "{}", character.token());
            assert_eq!(&portrait[8..12], b"WEBP", "{}", character.token());
        }
        for (i, a) in all.iter().enumerate() {
            for b in &all[i + 1..] {
                assert_ne!(
                    a.portrait(),
                    b.portrait(),
                    "{} and {}",
                    a.token(),
                    b.token()
                );
            }
        }
    }

    #[test]
    fn refuses_a_character_that_does_not_exist() {
        assert!(serde_json::from_str::<Character>("\"hoarder\"").is_err());
        assert!(serde_json::from_str::<Character>("\"Demolisher\"").is_err());
    }
}
