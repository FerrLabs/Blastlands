use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum BotSkill {
    Easy,
    #[default]
    Normal,
    Hard,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn uses_the_tokens_the_game_server_parses() {
        for (json, skill) in [
            ("\"easy\"", BotSkill::Easy),
            ("\"normal\"", BotSkill::Normal),
            ("\"hard\"", BotSkill::Hard),
        ] {
            let read: BotSkill = serde_json::from_str(json).expect(json);
            assert_eq!(read, skill);
            assert_eq!(serde_json::to_string(&skill).unwrap(), json);
        }
    }

    #[test]
    fn a_skill_the_bots_do_not_have_is_refused() {
        assert!(serde_json::from_str::<BotSkill>("\"nightmare\"").is_err());
    }
}
