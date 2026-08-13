use std::collections::BTreeSet;
use std::ops::RangeInclusive;

use crate::error::LobbyError;

#[derive(Debug)]
pub struct PortPool {
    range: RangeInclusive<u16>,
    available: BTreeSet<u16>,
}

impl PortPool {
    pub fn new(range: RangeInclusive<u16>) -> Self {
        Self {
            available: range.clone().collect(),
            range,
        }
    }

    pub fn acquire(&mut self) -> Result<u16, LobbyError> {
        self.available.pop_first().ok_or(LobbyError::NoCapacity)
    }

    pub fn release(&mut self, port: u16) {
        if self.range.contains(&port) {
            self.available.insert(port);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hands_out_every_port_in_the_range_then_reports_no_capacity() {
        let mut pool = PortPool::new(7000..=7002);

        assert_eq!(pool.acquire(), Ok(7000));
        assert_eq!(pool.acquire(), Ok(7001));
        assert_eq!(pool.acquire(), Ok(7002));
        assert_eq!(pool.acquire(), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn a_released_port_is_handed_out_again() {
        let mut pool = PortPool::new(7000..=7001);

        let first = pool.acquire().unwrap();
        pool.acquire().unwrap();
        assert_eq!(pool.acquire(), Err(LobbyError::NoCapacity));

        pool.release(first);

        assert_eq!(pool.acquire(), Ok(first));
    }

    #[test]
    fn releasing_a_port_from_outside_the_range_does_not_widen_the_pool() {
        let mut pool = PortPool::new(7000..=7000);

        pool.acquire().unwrap();
        pool.release(9999);

        assert_eq!(pool.acquire(), Err(LobbyError::NoCapacity));
    }

    #[test]
    fn releasing_the_same_port_twice_does_not_double_allocate_it() {
        let mut pool = PortPool::new(7000..=7000);

        let port = pool.acquire().unwrap();
        pool.release(port);
        pool.release(port);

        assert_eq!(pool.acquire(), Ok(port));
        assert_eq!(pool.acquire(), Err(LobbyError::NoCapacity));
    }
}
