use axum::http::HeaderMap;
use serde::Serialize;

use crate::error::LobbyError;
use crate::version::ClientVersion;

pub const VERSION_HEADER: &str = "x-blastlands-version";

// What the client needs to decide whether it may play and what to download.
#[derive(Debug, Clone, Serialize)]
pub struct ReleaseInfo {
    pub latest: ClientVersion,
    // Anything below this cannot join: an outdated client desyncs the simulation
    // rather than failing loudly, which is far worse than refusing it up front.
    pub minimum: ClientVersion,
    pub download_url: String,
    pub sha256: String,
}

impl ReleaseInfo {
    pub fn accepts(&self, client: ClientVersion) -> bool {
        client >= self.minimum
    }

    // The header is required rather than optional: a client that does not send it
    // is either ancient or not our client at all.
    pub fn require_supported(&self, headers: &HeaderMap) -> Result<ClientVersion, LobbyError> {
        let raw = headers
            .get(VERSION_HEADER)
            .and_then(|value| value.to_str().ok())
            .ok_or_else(|| LobbyError::InvalidVersion("missing version header".to_owned()))?;

        let client: ClientVersion = raw.parse()?;

        if !self.accepts(client) {
            return Err(LobbyError::ClientTooOld {
                client,
                minimum: self.minimum,
            });
        }

        Ok(client)
    }
}

#[cfg(test)]
mod tests {
    use axum::http::HeaderValue;

    use super::*;

    fn release() -> ReleaseInfo {
        ReleaseInfo {
            latest: "26.9.0".parse().unwrap(),
            minimum: "26.8.0".parse().unwrap(),
            download_url: "https://example.test/blastlands.zip".to_owned(),
            sha256: "abc".to_owned(),
        }
    }

    fn headers(value: &str) -> HeaderMap {
        let mut map = HeaderMap::new();
        map.insert(VERSION_HEADER, HeaderValue::from_str(value).unwrap());
        map
    }

    #[test]
    fn a_current_client_is_accepted() {
        assert!(release().require_supported(&headers("26.9.0")).is_ok());
    }

    #[test]
    fn a_client_exactly_at_the_minimum_is_accepted() {
        assert!(release().require_supported(&headers("26.8.0")).is_ok());
    }

    #[test]
    fn a_client_newer_than_latest_is_accepted() {
        // A developer build must not be locked out by its own lobby.
        assert!(release().require_supported(&headers("27.0.0")).is_ok());
    }

    #[test]
    fn a_client_below_the_minimum_is_refused() {
        let error = release().require_supported(&headers("26.7.9")).unwrap_err();

        assert!(matches!(error, LobbyError::ClientTooOld { .. }));
    }

    #[test]
    fn a_missing_header_is_refused() {
        assert!(release().require_supported(&HeaderMap::new()).is_err());
    }

    #[test]
    fn a_malformed_header_is_refused() {
        assert!(release().require_supported(&headers("banana")).is_err());
    }
}
