use std::sync::{PoisonError, RwLock};

use axum::http::HeaderMap;
use serde::Serialize;

use crate::error::LobbyError;
use crate::github::PublishedClient;
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

pub struct Releases {
    minimum: ClientVersion,
    public_url: String,
    published: RwLock<Option<PublishedClient>>,
}

impl Releases {
    pub fn new(minimum: ClientVersion, public_url: String) -> Self {
        Self {
            minimum,
            public_url,
            published: RwLock::new(None),
        }
    }

    pub fn minimum(&self) -> ClientVersion {
        self.minimum
    }

    pub fn publish(&self, client: PublishedClient) -> bool {
        let mut published = self
            .published
            .write()
            .unwrap_or_else(PoisonError::into_inner);

        if published.as_ref() == Some(&client) {
            return false;
        }

        *published = Some(client);
        true
    }

    pub fn current(&self) -> Option<ReleaseInfo> {
        self.published().map(|client| ReleaseInfo {
            latest: client.version,
            minimum: self.minimum,
            download_url: format!("{}/v1/client/{}/download", self.public_url, client.version),
            sha256: client.sha256,
        })
    }

    pub fn asset_for(&self, version: ClientVersion) -> Result<String, LobbyError> {
        match self.published() {
            None => Err(LobbyError::ReleaseUnknown),
            Some(client) if client.version == version => Ok(client.asset_url),
            Some(_) => Err(LobbyError::ReleaseNotFound),
        }
    }

    fn published(&self) -> Option<PublishedClient> {
        self.published
            .read()
            .unwrap_or_else(PoisonError::into_inner)
            .clone()
    }

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

    fn published(version: &str) -> PublishedClient {
        PublishedClient {
            version: version.parse().unwrap(),
            asset_url: format!("https://api.github.com/assets/{version}"),
            sha256: "ab".repeat(32),
        }
    }

    fn releases() -> Releases {
        Releases::new(
            "26.8.0".parse().unwrap(),
            "https://api.blastlands.test".to_owned(),
        )
    }

    fn headers(value: &str) -> HeaderMap {
        let mut map = HeaderMap::new();
        map.insert(VERSION_HEADER, HeaderValue::from_str(value).unwrap());
        map
    }

    #[test]
    fn a_current_client_is_accepted() {
        assert!(releases().require_supported(&headers("26.9.0")).is_ok());
    }

    #[test]
    fn a_client_exactly_at_the_minimum_is_accepted() {
        assert!(releases().require_supported(&headers("26.8.0")).is_ok());
    }

    #[test]
    fn a_client_newer_than_latest_is_accepted() {
        // A developer build must not be locked out by its own lobby.
        let releases = releases();
        releases.publish(published("26.9.0"));

        assert!(releases.require_supported(&headers("27.0.0")).is_ok());
    }

    #[test]
    fn a_client_below_the_minimum_is_refused() {
        let error = releases()
            .require_supported(&headers("26.7.9"))
            .unwrap_err();

        assert!(matches!(error, LobbyError::ClientTooOld { .. }));
    }

    #[test]
    fn a_missing_header_is_refused() {
        assert!(releases().require_supported(&HeaderMap::new()).is_err());
    }

    #[test]
    fn a_malformed_header_is_refused() {
        assert!(releases().require_supported(&headers("banana")).is_err());
    }

    #[test]
    fn nothing_is_announced_before_a_release_is_known() {
        assert!(releases().current().is_none());
    }

    #[test]
    fn the_announced_download_names_the_version_it_describes() {
        let releases = releases();
        releases.publish(published("26.9.18"));

        let info = releases.current().unwrap();

        assert_eq!(info.latest, "26.9.18".parse().unwrap());
        assert_eq!(info.minimum, "26.8.0".parse().unwrap());
        assert_eq!(info.sha256, "ab".repeat(32));
        assert_eq!(
            info.download_url,
            "https://api.blastlands.test/v1/client/26.9.18/download"
        );
    }

    #[test]
    fn publishing_the_same_release_again_is_not_a_change() {
        let releases = releases();

        assert!(releases.publish(published("26.9.18")));
        assert!(!releases.publish(published("26.9.18")));
        assert!(releases.publish(published("26.9.19")));
    }

    #[test]
    fn only_the_announced_version_can_be_downloaded() {
        let releases = releases();

        assert_eq!(
            releases.asset_for("26.9.18".parse().unwrap()),
            Err(LobbyError::ReleaseUnknown)
        );

        releases.publish(published("26.9.19"));

        assert_eq!(
            releases.asset_for("26.9.18".parse().unwrap()),
            Err(LobbyError::ReleaseNotFound)
        );
        assert_eq!(
            releases.asset_for("26.9.19".parse().unwrap()),
            Ok("https://api.github.com/assets/26.9.19".to_owned())
        );
    }
}
