use std::cmp::Ordering;
use std::fmt;
use std::str::FromStr;

use serde::{Serialize, Serializer};

use crate::error::LobbyError;

// Versions follow FerrFlow's ShortCalVer (26.8.1), which is still three numeric
// components, so ordering is plain component-wise comparison.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ClientVersion {
    major: u32,
    minor: u32,
    patch: u32,
}

impl ClientVersion {
    pub fn new(major: u32, minor: u32, patch: u32) -> Self {
        Self {
            major,
            minor,
            patch,
        }
    }
}

impl PartialOrd for ClientVersion {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

impl Ord for ClientVersion {
    fn cmp(&self, other: &Self) -> Ordering {
        (self.major, self.minor, self.patch).cmp(&(other.major, other.minor, other.patch))
    }
}

impl fmt::Display for ClientVersion {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}.{}.{}", self.major, self.minor, self.patch)
    }
}

impl Serialize for ClientVersion {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.collect_str(self)
    }
}

impl FromStr for ClientVersion {
    type Err = LobbyError;

    fn from_str(raw: &str) -> Result<Self, Self::Err> {
        let invalid = || LobbyError::InvalidVersion(raw.to_owned());

        let mut parts = raw.trim().split('.');
        let mut next = || -> Result<u32, LobbyError> {
            parts
                .next()
                .ok_or_else(invalid)?
                .parse::<u32>()
                .map_err(|_| invalid())
        };

        let version = Self {
            major: next()?,
            minor: next()?,
            patch: next()?,
        };

        if parts.next().is_some() {
            return Err(invalid());
        }

        Ok(version)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_a_three_part_version() {
        assert_eq!(
            "26.8.1".parse::<ClientVersion>().unwrap(),
            ClientVersion::new(26, 8, 1)
        );
    }

    #[test]
    fn rejects_malformed_versions() {
        for raw in ["", "1", "1.2", "1.2.3.4", "1.2.x", "v1.2.3", "-1.2.3"] {
            assert!(
                raw.parse::<ClientVersion>().is_err(),
                "{raw} should be rejected"
            );
        }
    }

    #[test]
    fn orders_by_component_not_lexically() {
        // The bug this guards: string comparison puts "26.10.0" before "26.9.0".
        let older: ClientVersion = "26.9.0".parse().unwrap();
        let newer: ClientVersion = "26.10.0".parse().unwrap();

        assert!(newer > older);
        assert!(older < newer);
    }

    #[test]
    fn orders_patches_within_a_minor() {
        let a: ClientVersion = "1.2.3".parse().unwrap();
        let b: ClientVersion = "1.2.10".parse().unwrap();

        assert!(b > a);
    }

    #[test]
    fn renders_back_to_its_text_form() {
        assert_eq!(
            "26.8.1".parse::<ClientVersion>().unwrap().to_string(),
            "26.8.1"
        );
    }
}
