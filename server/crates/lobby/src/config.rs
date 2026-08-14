use std::env;
use std::net::SocketAddr;
use std::ops::RangeInclusive;

use thiserror::Error;

use crate::release::ReleaseInfo;
use crate::version::ClientVersion;

const BIND: &str = "BLASTLANDS_BIND";
const GAME_SERVER_HOST: &str = "BLASTLANDS_GAME_SERVER_HOST";
const PORT_RANGE: &str = "BLASTLANDS_PORT_RANGE";
const INSTANCE_TOKEN: &str = "BLASTLANDS_INSTANCE_TOKEN";
const CLIENT_LATEST: &str = "BLASTLANDS_CLIENT_LATEST";
const CLIENT_MINIMUM: &str = "BLASTLANDS_CLIENT_MINIMUM";
const CLIENT_URL: &str = "BLASTLANDS_CLIENT_URL";
const CLIENT_SHA256: &str = "BLASTLANDS_CLIENT_SHA256";

const DEFAULT_BIND: &str = "0.0.0.0:8080";
const DEFAULT_PORT_RANGE: &str = "7000-7099";

#[derive(Debug, Error, PartialEq, Eq)]
pub enum ConfigError {
    #[error("{0} is required")]
    Missing(&'static str),

    #[error("{name} is invalid: {reason}")]
    Invalid { name: &'static str, reason: String },
}

#[derive(Debug, Clone)]
pub struct Config {
    pub bind: SocketAddr,
    pub game_server_host: String,
    pub port_range: RangeInclusive<u16>,
    pub instance_token: String,
    pub release: ReleaseInfo,
}

impl Config {
    pub fn from_env() -> Result<Self, ConfigError> {
        let bind = required_or(BIND, DEFAULT_BIND)
            .parse::<SocketAddr>()
            .map_err(|error| ConfigError::Invalid {
                name: BIND,
                reason: error.to_string(),
            })?;

        let game_server_host = non_empty(GAME_SERVER_HOST)?;
        let instance_token = non_empty(INSTANCE_TOKEN)?;
        let port_range = parse_port_range(&required_or(PORT_RANGE, DEFAULT_PORT_RANGE))?;

        let latest = parse_version(CLIENT_LATEST, &non_empty(CLIENT_LATEST)?)?;
        let minimum = parse_version(CLIENT_MINIMUM, &non_empty(CLIENT_MINIMUM)?)?;

        if minimum > latest {
            return Err(ConfigError::Invalid {
                name: CLIENT_MINIMUM,
                reason: format!("minimum {minimum} is newer than latest {latest}"),
            });
        }

        Ok(Self {
            bind,
            game_server_host,
            port_range,
            instance_token,
            release: ReleaseInfo {
                latest,
                minimum,
                download_url: non_empty(CLIENT_URL)?,
                sha256: non_empty(CLIENT_SHA256)?,
            },
        })
    }
}

fn parse_version(name: &'static str, raw: &str) -> Result<ClientVersion, ConfigError> {
    raw.parse::<ClientVersion>()
        .map_err(|_| ConfigError::Invalid {
            name,
            reason: format!("{raw} is not a three-part version"),
        })
}

fn required_or(name: &'static str, fallback: &str) -> String {
    env::var(name)
        .ok()
        .filter(|value| !value.trim().is_empty())
        .unwrap_or_else(|| fallback.to_owned())
}

fn non_empty(name: &'static str) -> Result<String, ConfigError> {
    env::var(name)
        .ok()
        .map(|value| value.trim().to_owned())
        .filter(|value| !value.is_empty())
        .ok_or(ConfigError::Missing(name))
}

fn parse_port_range(raw: &str) -> Result<RangeInclusive<u16>, ConfigError> {
    let invalid = |reason: &str| ConfigError::Invalid {
        name: PORT_RANGE,
        reason: reason.to_owned(),
    };

    let (start, end) = raw
        .split_once('-')
        .ok_or_else(|| invalid("expected the form START-END, for example 7000-7099"))?;

    let start: u16 = start
        .trim()
        .parse()
        .map_err(|_| invalid("start is not a valid port"))?;
    let end: u16 = end
        .trim()
        .parse()
        .map_err(|_| invalid("end is not a valid port"))?;

    if start == 0 {
        return Err(invalid("start must be above 0"));
    }

    if end < start {
        return Err(invalid("end must be greater than or equal to start"));
    }

    Ok(start..=end)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_a_well_formed_range() {
        assert_eq!(parse_port_range("7000-7099"), Ok(7000..=7099));
    }

    #[test]
    fn tolerates_surrounding_whitespace() {
        assert_eq!(parse_port_range(" 7000 - 7001 "), Ok(7000..=7001));
    }

    #[test]
    fn accepts_a_single_port_range() {
        assert_eq!(parse_port_range("7000-7000"), Ok(7000..=7000));
    }

    #[test]
    fn rejects_a_reversed_range() {
        assert!(parse_port_range("7100-7000").is_err());
    }

    #[test]
    fn rejects_port_zero() {
        assert!(parse_port_range("0-7000").is_err());
    }

    #[test]
    fn rejects_a_range_without_a_separator() {
        assert!(parse_port_range("7000").is_err());
    }

    #[test]
    fn rejects_a_port_above_the_u16_range() {
        assert!(parse_port_range("7000-70000").is_err());
    }
}
