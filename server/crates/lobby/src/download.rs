use std::collections::HashMap;
use std::future::Future;
use std::sync::{Arc, Mutex as StdMutex, PoisonError};
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

#[derive(Default)]
struct LinkCache {
    slots: StdMutex<HashMap<String, Arc<Mutex<Option<CachedLink>>>>>,
}

impl LinkCache {
    fn slot(&self, asset_url: &str) -> Arc<Mutex<Option<CachedLink>>> {
        let mut slots = self.slots.lock().unwrap_or_else(PoisonError::into_inner);
        Arc::clone(slots.entry(asset_url.to_owned()).or_default())
    }

    async fn get_or_fetch<F, Fut, E>(
        &self,
        asset_url: &str,
        lifetime: Duration,
        fetch: F,
    ) -> Result<String, E>
    where
        F: FnOnce() -> Fut,
        Fut: Future<Output = Result<String, E>>,
    {
        let slot = self.slot(asset_url);
        let mut cached = slot.lock().await;

        if let Some(location) = cached
            .as_ref()
            .and_then(|link| link.fresh_for(asset_url, Instant::now(), lifetime))
        {
            return Ok(location.to_owned());
        }

        let location = fetch().await?;
        *cached = Some(CachedLink {
            asset_url: asset_url.to_owned(),
            location: location.clone(),
            fetched: Instant::now(),
        });

        Ok(location)
    }
}

pub struct DownloadLinks {
    github: Arc<Github>,
    lifetime: Duration,
    cache: LinkCache,
}

impl DownloadLinks {
    pub fn new(github: Arc<Github>, lifetime: Duration) -> Self {
        Self {
            github,
            lifetime,
            cache: LinkCache::default(),
        }
    }

    pub async fn location(&self, asset_url: &str) -> Result<String, GithubError> {
        self.cache
            .get_or_fetch(asset_url, self.lifetime, || {
                self.github.download_location(asset_url)
            })
            .await
    }
}

#[cfg(test)]
mod tests {
    use std::sync::atomic::{AtomicUsize, Ordering};

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

    const FIRST: &str = "https://api.github.com/assets/1";
    const SECOND: &str = "https://api.github.com/assets/2";

    async fn fetch_counting(
        cache: &LinkCache,
        asset_url: &str,
        calls: &AtomicUsize,
    ) -> Result<String, ()> {
        cache
            .get_or_fetch(asset_url, LINK_LIFETIME, || async {
                calls.fetch_add(1, Ordering::SeqCst);
                tokio::task::yield_now().await;
                Ok(format!("signed:{asset_url}"))
            })
            .await
    }

    #[tokio::test]
    async fn alternating_between_assets_fetches_each_only_once() {
        let cache = LinkCache::default();
        let calls = AtomicUsize::new(0);

        for asset_url in [FIRST, SECOND, FIRST, SECOND, FIRST] {
            let location = fetch_counting(&cache, asset_url, &calls).await.unwrap();
            assert_eq!(location, format!("signed:{asset_url}"));
        }

        assert_eq!(calls.load(Ordering::SeqCst), 2);
    }

    #[tokio::test]
    async fn concurrent_requests_for_one_asset_share_a_single_fetch() {
        let cache = LinkCache::default();
        let calls = AtomicUsize::new(0);

        let results = tokio::join!(
            fetch_counting(&cache, FIRST, &calls),
            fetch_counting(&cache, FIRST, &calls),
            fetch_counting(&cache, FIRST, &calls),
        );

        assert!(results.0.is_ok() && results.1.is_ok() && results.2.is_ok());
        assert_eq!(calls.load(Ordering::SeqCst), 1);
    }

    #[tokio::test]
    async fn a_failed_fetch_is_not_cached() {
        let cache = LinkCache::default();

        let failed: Result<String, &str> = cache
            .get_or_fetch(FIRST, LINK_LIFETIME, || async { Err("down") })
            .await;
        let calls = AtomicUsize::new(0);
        let retried = fetch_counting(&cache, FIRST, &calls).await;

        assert_eq!(failed, Err("down"));
        assert!(retried.is_ok());
        assert_eq!(calls.load(Ordering::SeqCst), 1);
    }
}
