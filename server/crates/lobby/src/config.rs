use std::env;
use std::net::SocketAddr;
use std::ops::RangeInclusive;
use std::time::Duration;

use thiserror::Error;

use crate::version::ClientVersion;

const BIND: &str = "BLASTLANDS_BIND";
const GAME_SERVER_HOST: &str = "BLASTLANDS_GAME_SERVER_HOST";
const PORT_RANGE: &str = "BLASTLANDS_PORT_RANGE";
const INSTANCE_TOKEN: &str = "BLASTLANDS_INSTANCE_TOKEN";
const CLIENT_MINIMUM: &str = "BLASTLANDS_CLIENT_MINIMUM";
const PUBLIC_URL: &str = "BLASTLANDS_PUBLIC_URL";
const GITHUB_TOKEN: &str = "BLASTLANDS_GITHUB_TOKEN";
const RELEASE_POLL_SECONDS: &str = "BLASTLANDS_RELEASE_POLL_SECONDS";

const DEFAULT_BIND: &str = "0.0.0.0:8080";
const DEFAULT_PORT_RANGE: &str = "7000-7099";
const DEFAULT_RELEASE_POLL: Duration = Duration::from_secs(300);

#[derive(Debug, Error, PartialEq, Eq)]
pub enum ConfigError {
    #[error("{0} is required")]
    Missing(&'static str),

    #[error("{name} is invalid: {reason}")]
    Invalid { name: &'static str, reason: String },
}

const CREATES_PER_WINDOW: &str = "BLASTLANDS_LOBBY_CREATES_PER_WINDOW";
const JOINS_PER_WINDOW: &str = "BLASTLANDS_LOBBY_JOINS_PER_WINDOW";
const WINDOW_SECONDS: &str = "BLASTLANDS_LOBBY_RATE_WINDOW_SECONDS";
const MATCHES_PER_ADDRESS: &str = "BLASTLANDS_LOBBY_MATCHES_PER_ADDRESS";
const UNJOINED_TTL_SECONDS: &str = "BLASTLANDS_LOBBY_UNJOINED_TTL_SECONDS";
const SILENT_TTL_SECONDS: &str = "BLASTLANDS_LOBBY_SILENT_TTL_SECONDS";
const SWEEP_SECONDS: &str = "BLASTLANDS_LOBBY_SWEEP_SECONDS";
const TRUST_FORWARDED_FOR: &str = "BLASTLANDS_LOBBY_TRUST_FORWARDED_FOR";

#[derive(Debug, Clone)]
pub struct Config {
    pub bind: SocketAddr,
    pub game_server_host: String,
    pub port_range: RangeInclusive<u16>,
    pub instance_token: String,
    pub release: ReleaseSource,
    pub limits: Limits,
}

#[derive(Debug, Clone)]
pub struct ReleaseSource {
    pub minimum: ClientVersion,
    pub public_url: String,
    pub github_token: String,
    pub poll_every: Duration,
}

/// What one address may do, and how long a match may go unattended.
///
/// The defaults are deliberately generous for a person and tight for a script. Three
/// matches an hour is more than anyone hosts by hand; a thousand a minute is what a loop
/// does. The point is to make the flood expensive, not to police normal play.
#[derive(Debug, Clone, Copy)]
pub struct Limits {
    pub creates_per_window: u32,
    pub joins_per_window: u32,
    pub window: Duration,
    pub matches_per_address: usize,
    pub unjoined_ttl: Duration,
    pub silent_ttl: Duration,
    pub sweep_every: Duration,
    pub trust_forwarded_for: bool,
}

impl Default for Limits {
    fn default() -> Self {
        Self {
            creates_per_window: 5,
            joins_per_window: 30,
            window: Duration::from_secs(60),
            matches_per_address: 3,
            // A lobby full of matches nobody joined is the cheapest denial of service
            // there is, so an empty one does not get to sit on a port for long.
            unjoined_ttl: Duration::from_secs(300),
            // Three missed heartbeats at the ten-second cadence #17 will use. One missed
            // beat is a hiccup; three is a process that is not coming back.
            silent_ttl: Duration::from_secs(30),
            sweep_every: Duration::from_secs(10),
            trust_forwarded_for: false,
        }
    }
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

        let release = ReleaseSource {
            minimum: parse_version(CLIENT_MINIMUM, &non_empty(CLIENT_MINIMUM)?)?,
            public_url: parse_public_url(&non_empty(PUBLIC_URL)?)?,
            github_token: non_empty(GITHUB_TOKEN)?,
            poll_every: seconds(RELEASE_POLL_SECONDS, DEFAULT_RELEASE_POLL)?,
        };

        let defaults = Limits::default();
        let limits = Limits {
            creates_per_window: number(CREATES_PER_WINDOW, defaults.creates_per_window)?,
            joins_per_window: number(JOINS_PER_WINDOW, defaults.joins_per_window)?,
            window: seconds(WINDOW_SECONDS, defaults.window)?,
            matches_per_address: number::<u32>(
                MATCHES_PER_ADDRESS,
                defaults.matches_per_address as u32,
            )? as usize,
            unjoined_ttl: seconds(UNJOINED_TTL_SECONDS, defaults.unjoined_ttl)?,
            silent_ttl: seconds(SILENT_TTL_SECONDS, defaults.silent_ttl)?,
            sweep_every: seconds(SWEEP_SECONDS, defaults.sweep_every)?,
            trust_forwarded_for: flag(TRUST_FORWARDED_FOR)?,
        };

        Ok(Self {
            bind,
            game_server_host,
            port_range,
            instance_token,
            limits,
            release,
        })
    }
}

fn parse_public_url(raw: &str) -> Result<String, ConfigError> {
    let url = raw.trim_end_matches('/');

    if !url.starts_with("https://") || url.len() == "https://".len() {
        return Err(ConfigError::Invalid {
            name: PUBLIC_URL,
            reason: format!("{raw} is not an https:// URL, and clients refuse any other"),
        });
    }

    Ok(url.to_owned())
}

fn number<T>(name: &'static str, fallback: T) -> Result<T, ConfigError>
where
    T: std::str::FromStr + Copy,
{
    match env::var(name) {
        Err(_) => Ok(fallback),
        Ok(raw) if raw.trim().is_empty() => Ok(fallback),
        Ok(raw) => raw.trim().parse::<T>().map_err(|_| ConfigError::Invalid {
            name,
            reason: format!("{raw} is not a number"),
        }),
    }
}

fn seconds(name: &'static str, fallback: Duration) -> Result<Duration, ConfigError> {
    let value = number::<u64>(name, fallback.as_secs())?;
    Ok(Duration::from_secs(value))
}

/// Anything but a clear yes is a no.
///
/// This one decides whether a spoofable header is believed, so an unreadable value has
/// to land on the safe side rather than be guessed at.
fn flag(name: &'static str) -> Result<bool, ConfigError> {
    let raw = env::var(name).unwrap_or_default();
    Ok(matches!(
        raw.trim().to_ascii_lowercase().as_str(),
        "1" | "true" | "yes" | "on"
    ))
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

    #[test]
    fn a_public_url_loses_its_trailing_slash() {
        assert_eq!(
            parse_public_url("https://api.blastlands.ferrlabs.com/"),
            Ok("https://api.blastlands.ferrlabs.com".to_owned())
        );
    }

    #[test]
    fn a_public_url_must_be_https() {
        for raw in [
            "http://api.blastlands.ferrlabs.com",
            "api.blastlands.ferrlabs.com",
            "https://",
            "https:///",
        ] {
            assert!(parse_public_url(raw).is_err(), "{raw} should be rejected");
        }
    }
}
