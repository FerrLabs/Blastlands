use std::collections::HashMap;
use std::net::IpAddr;
use std::sync::{Mutex, MutexGuard};
use std::time::{Duration, Instant};

/// Counts recent requests per address and refuses the ones over the line.
///
/// A fixed window of timestamps rather than a token bucket: the window is short and the
/// limits are small, so the memory is a handful of instants per caller and the behaviour
/// is something a person can predict from the numbers alone. A bucket buys smoothing that
/// nothing here needs.
///
/// The clock is passed in rather than read. Every decision this makes is about elapsed
/// time, and a test that has to sleep to exercise a limiter is a test that will one day
/// fail on a loaded machine for no reason.
pub struct RateLimiter {
    limit: u32,
    window: Duration,
    seen: Mutex<HashMap<IpAddr, Vec<Instant>>>,
}

impl RateLimiter {
    pub fn new(limit: u32, window: Duration) -> Self {
        Self {
            limit,
            window,
            seen: Mutex::new(HashMap::new()),
        }
    }

    /// Records a request and reports whether it is allowed.
    ///
    /// A limit of zero disables the limiter rather than refusing everything. Refusing
    /// everything is never what someone means when they set a limit to nothing, and the
    /// alternative is a service that answers 429 to its first caller after a typo in an
    /// environment variable.
    pub fn allow(&self, address: IpAddr, now: Instant) -> bool {
        if self.limit == 0 {
            return true;
        }

        let mut seen = self.lock();
        let hits = seen.entry(address).or_default();
        hits.retain(|hit| now.duration_since(*hit) < self.window);

        if hits.len() as u32 >= self.limit {
            return false;
        }

        hits.push(now);
        true
    }

    /// Drops addresses with nothing left in the window.
    ///
    /// Without this the map grows for the lifetime of the process, one entry per address
    /// that ever called, which is a slow memory leak on a public endpoint and exactly the
    /// thing a rate limiter is supposed to prevent someone doing to us.
    pub fn prune(&self, now: Instant) {
        let window = self.window;
        let mut seen = self.lock();

        for hits in seen.values_mut() {
            hits.retain(|hit| now.duration_since(*hit) < window);
        }

        seen.retain(|_, hits| !hits.is_empty());
    }

    #[cfg(test)]
    fn tracked(&self) -> usize {
        self.lock().len()
    }

    fn lock(&self) -> MutexGuard<'_, HashMap<IpAddr, Vec<Instant>>> {
        self.seen
            .lock()
            .unwrap_or_else(|poisoned| poisoned.into_inner())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::net::Ipv4Addr;

    fn address(last: u8) -> IpAddr {
        IpAddr::V4(Ipv4Addr::new(10, 0, 0, last))
    }

    #[test]
    fn allows_up_to_the_limit_then_refuses() {
        let limiter = RateLimiter::new(3, Duration::from_secs(60));
        let now = Instant::now();

        assert!(limiter.allow(address(1), now));
        assert!(limiter.allow(address(1), now));
        assert!(limiter.allow(address(1), now));
        assert!(!limiter.allow(address(1), now), "the fourth got through");
    }

    #[test]
    fn one_address_being_throttled_leaves_the_others_alone() {
        // The whole point of a per-address limit: a burst from one caller must not be
        // able to lock out everybody else, which is what a global limit would do.
        let limiter = RateLimiter::new(1, Duration::from_secs(60));
        let now = Instant::now();

        assert!(limiter.allow(address(1), now));
        assert!(!limiter.allow(address(1), now));
        assert!(
            limiter.allow(address(2), now),
            "a different caller was punished"
        );
    }

    #[test]
    fn the_window_moves_on() {
        let limiter = RateLimiter::new(2, Duration::from_secs(60));
        let start = Instant::now();

        assert!(limiter.allow(address(1), start));
        assert!(limiter.allow(address(1), start));
        assert!(!limiter.allow(address(1), start));

        let later = start + Duration::from_secs(61);
        assert!(limiter.allow(address(1), later), "the window never expired");
    }

    #[test]
    fn a_hit_at_the_edge_of_the_window_still_counts() {
        // Exactly one window later is outside; a hair inside it is not. Worth pinning,
        // because an off-by-one here is a limiter that lets through twice what it says.
        let limiter = RateLimiter::new(1, Duration::from_secs(60));
        let start = Instant::now();

        assert!(limiter.allow(address(1), start));
        assert!(!limiter.allow(address(1), start + Duration::from_millis(59_999)));
        assert!(limiter.allow(address(1), start + Duration::from_secs(60)));
    }

    #[test]
    fn a_limit_of_zero_lets_everything_through() {
        let limiter = RateLimiter::new(0, Duration::from_secs(60));
        let now = Instant::now();

        assert!(limiter.allow(address(1), now));
        assert!(limiter.allow(address(1), now));
    }

    #[test]
    fn pruning_forgets_addresses_that_went_quiet() {
        let limiter = RateLimiter::new(5, Duration::from_secs(60));
        let start = Instant::now();

        limiter.allow(address(1), start);
        limiter.allow(address(2), start);
        assert_eq!(limiter.tracked(), 2);

        limiter.prune(start + Duration::from_secs(61));
        assert_eq!(limiter.tracked(), 0, "the map kept growing");
    }
}
