use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum GameMode {
    #[default]
    Arena,
    Classic,
    ClassicBlinded,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn uses_the_tokens_the_game_server_parses() {
        for (json, mode) in [
            ("\"arena\"", GameMode::Arena),
            ("\"classic\"", GameMode::Classic),
            ("\"classic_blinded\"", GameMode::ClassicBlinded),
        ] {
            let read: GameMode = serde_json::from_str(json).expect(json);
            assert_eq!(read, mode);
            assert_eq!(serde_json::to_string(&mode).unwrap(), json);
        }
    }

    #[test]
    fn a_mode_the_game_does_not_have_is_refused() {
        assert!(serde_json::from_str::<GameMode>("\"bomberman\"").is_err());
    }
}
