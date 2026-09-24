use std::collections::{BTreeSet, HashMap};
use std::ops::RangeInclusive;
use std::time::{Duration, Instant};

use crate::error::LobbyError;

pub const INSTANCE_READY_TTL: Duration = Duration::from_secs(15);

#[derive(Debug)]
pub struct PortPool {
    range: RangeInclusive<u16>,
    available: BTreeSet<u16>,
    last_polled: HashMap<u16, Instant>,
}

impl PortPool {
    pub fn new(range: RangeInclusive<u16>) -> Self {
        Self {
            available: range.clone().collect(),
            range,
            last_polled: HashMap::new(),
        }
    }

    pub fn observe_instance(&mut self, port: u16, now: Instant) {
        if self.range.contains(&port) {
            self.last_polled.insert(port, now);
        }
    }

    pub fn acquire(&mut self, now: Instant) -> Result<u16, LobbyError> {
        let port = self
            .available
            .iter()
            .copied()
            .find(|port| self.has_ready_instance(*port, now))
            .ok_or(LobbyError::NoCapacity)?;

        self.available.remove(&port);
        Ok(port)
    }

    pub fn release(&mut self, port: u16) {
        if self.range.contains(&port) {
            self.available.insert(port);
        }
    }

    fn has_ready_instance(&self, port: u16, now: Instant) -> bool {
        self.last_polled
            .get(&port)
            .is_some_and(|polled| now.duration_since(*polled) <= INSTANCE_READY_TTL)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn pool_with_ready_instances(range: RangeInclusive<u16>, now: Instant) -> PortPool {
        let mut pool = PortPool::new(range.clone());
        for port in range {
            pool.observe_instance(port, now);
        }
        pool
    }

    #[test]
    fn hands_out_every_port_in_the_range_then_reports_no_capacity() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7002, now);

        assert_eq!(pool.acquire(now), Ok(7000));
        assert_eq!(pool.acquire(now), Ok(7001));
        assert_eq!(pool.acquire(now), Ok(7002));
        assert_eq!(pool.acquire(now), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn a_released_port_is_handed_out_again() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7001, now);

        let first = pool.acquire(now).unwrap();
        pool.acquire(now).unwrap();
        assert_eq!(pool.acquire(now), Err(LobbyError::NoCapacity));

        pool.release(first);

        assert_eq!(pool.acquire(now), Ok(first));
    }

    #[test]
    fn releasing_a_port_from_outside_the_range_does_not_widen_the_pool() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7000, now);

        pool.acquire(now).unwrap();
        pool.release(9999);

        assert_eq!(pool.acquire(now), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn releasing_the_same_port_twice_does_not_double_allocate_it() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7000, now);

        let port = pool.acquire(now).unwrap();
        pool.release(port);
        pool.release(port);

        assert_eq!(pool.acquire(now), Ok(port));
        assert_eq!(pool.acquire(now), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn a_port_no_instance_ever_polled_is_not_handed_out() {
        let mut pool = PortPool::new(7000..=7002);

        assert_eq!(pool.acquire(Instant::now()), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn a_port_whose_instance_went_quiet_is_not_handed_out() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7000, now);

        let later = now + INSTANCE_READY_TTL + Duration::from_secs(1);

        assert_eq!(pool.acquire(later), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn a_quiet_instance_becomes_eligible_again_once_it_polls() {
        let now = Instant::now();
        let mut pool = pool_with_ready_instances(7000..=7000, now);

        let later = now + INSTANCE_READY_TTL + Duration::from_secs(1);
        assert_eq!(pool.acquire(later), Err(LobbyError::NoCapacity));

        pool.observe_instance(7000, later);

        assert_eq!(pool.acquire(later), Ok(7000));
    }

    #[test]
    fn skips_a_quiet_port_for_the_next_one_that_is_ready() {
        let now = Instant::now();
        let mut pool = PortPool::new(7000..=7002);

        pool.observe_instance(7002, now);

        assert_eq!(pool.acquire(now), Ok(7002));
    }

    #[test]
    fn a_port_outside_the_range_never_becomes_eligible() {
        let now = Instant::now();
        let mut pool = PortPool::new(7000..=7000);

        pool.observe_instance(9999, now);

        assert_eq!(pool.acquire(now), Err(LobbyError::NoCapacity));
    }
}
