use std::fmt;

use serde::{Deserialize, Serialize};

use crate::error::LobbyError;

const MIN_LEN: usize = 2;
const MAX_LEN: usize = 16;

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(transparent)]
pub struct DisplayName(String);

impl DisplayName {
    pub fn new(raw: &str) -> Result<Self, LobbyError> {
        let trimmed = raw.trim();

        let len = trimmed.chars().count();
        if !(MIN_LEN..=MAX_LEN).contains(&len) {
            return Err(LobbyError::InvalidName(format!(
                "name must be between {MIN_LEN} and {MAX_LEN} characters"
            )));
        }

        if !trimmed
            .chars()
            .all(|c| c.is_alphanumeric() || matches!(c, ' ' | '-' | '_'))
        {
            return Err(LobbyError::InvalidName(
                "name may only contain letters, digits, spaces, hyphens and underscores".to_owned(),
            ));
        }

        Ok(Self(trimmed.to_owned()))
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for DisplayName {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.0)
    }
}

impl<'de> Deserialize<'de> for DisplayName {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let raw = String::deserialize(deserializer)?;
        Self::new(&raw).map_err(serde::de::Error::custom)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn accepts_a_reasonable_name() {
        assert_eq!(DisplayName::new("Bryan").unwrap().as_str(), "Bryan");
    }

    #[test]
    fn trims_surrounding_whitespace() {
        assert_eq!(DisplayName::new("  Bryan  ").unwrap().as_str(), "Bryan");
    }

    #[test]
    fn rejects_names_that_are_too_short_or_too_long() {
        assert!(DisplayName::new("a").is_err());
        assert!(DisplayName::new("   ").is_err());
        assert!(DisplayName::new(&"a".repeat(MAX_LEN + 1)).is_err());
    }

    #[test]
    fn rejects_control_and_markup_characters() {
        assert!(DisplayName::new("<script>").is_err());
        assert!(DisplayName::new("bad\nname").is_err());
        assert!(DisplayName::new("null\0byte").is_err());
    }

    #[test]
    fn counts_length_in_characters_not_bytes() {
        assert!(DisplayName::new("éé").is_ok());
        assert!(DisplayName::new(&"é".repeat(MAX_LEN)).is_ok());
        assert!(DisplayName::new(&"é".repeat(MAX_LEN + 1)).is_err());
    }
}
