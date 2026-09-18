use std::sync::Arc;
use std::time::{Duration, Instant};

use tokio::sync::Mutex;

use crate::github::{Github, GithubError};

pub const LINK_LIFETIME: Duration = Duration::from_secs(60);

#[derive(Debug, Clone)]
struct CachedLink {
    asset_url: String,
    location: String,
    fetched: Instant,
}

impl CachedLink {
    fn fresh_for(&self, asset_url: &str, now: Instant, lifetime: Duration) -> Option<&str> {
        (self.asset_url == asset_url && now.duration_since(self.fetched) < lifetime)
            .then_some(self.location.as_str())
    }
}

pub struct DownloadLinks {
    github: Arc<Github>,
    lifetime: Duration,
    cached: Mutex<Option<CachedLink>>,
}

impl DownloadLinks {
    pub fn new(github: Arc<Github>, lifetime: Duration) -> Self {
        Self {
            github,
            lifetime,
            cached: Mutex::new(None),
        }
    }

    pub async fn location(&self, asset_url: &str) -> Result<String, GithubError> {
        let mut cached = self.cached.lock().await;

        if let Some(location) = cached
            .as_ref()
            .and_then(|link| link.fresh_for(asset_url, Instant::now(), self.lifetime))
        {
            return Ok(location.to_owned());
        }

        let location = self.github.download_location(asset_url).await?;
        *cached = Some(CachedLink {
            asset_url: asset_url.to_owned(),
            location: location.clone(),
            fetched: Instant::now(),
        });

        Ok(location)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn link(fetched: Instant) -> CachedLink {
        CachedLink {
            asset_url: "https://api.github.com/assets/1".to_owned(),
            location: "https://release-assets.githubusercontent.com/signed".to_owned(),
            fetched,
        }
    }

    #[test]
    fn a_recent_link_for_the_same_asset_is_reused() {
        let fetched = Instant::now();
        let later = fetched + Duration::from_secs(59);

        assert_eq!(
            link(fetched).fresh_for("https://api.github.com/assets/1", later, LINK_LIFETIME),
            Some("https://release-assets.githubusercontent.com/signed")
        );
    }

    #[test]
    fn a_link_past_its_lifetime_is_fetched_again() {
        let fetched = Instant::now();
        let later = fetched + LINK_LIFETIME;

        assert_eq!(
            link(fetched).fresh_for("https://api.github.com/assets/1", later, LINK_LIFETIME),
            None
        );
    }

    #[test]
    fn a_link_for_another_asset_is_never_reused() {
        let fetched = Instant::now();

        assert_eq!(
            link(fetched).fresh_for("https://api.github.com/assets/2", fetched, LINK_LIFETIME),
            None
        );
    }
}
