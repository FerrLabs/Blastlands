use std::io;
use std::net::{IpAddr, Ipv4Addr, Ipv6Addr, SocketAddr};

use tokio::net::UdpSocket;

use crate::servers::Probe;

pub const PREFIX: &str = "blastlands-probe:";

pub struct Prober {
    socket: UdpSocket,
}

impl Prober {
    pub fn bind() -> io::Result<Self> {
        let socket = std::net::UdpSocket::bind((Ipv6Addr::UNSPECIFIED, 0))
            .or_else(|_| std::net::UdpSocket::bind((Ipv4Addr::UNSPECIFIED, 0)))?;
        socket.set_nonblocking(true)?;
        Ok(Self {
            socket: UdpSocket::from_std(socket)?,
        })
    }

    pub async fn send(&self, probe: &Probe) -> io::Result<()> {
        let datagram = format!("{PREFIX}{}", probe.nonce);
        self.socket
            .send_to(datagram.as_bytes(), self.reachable_form(probe.target)?)
            .await
            .map(|_| ())
    }

    fn reachable_form(&self, target: SocketAddr) -> io::Result<SocketAddr> {
        Ok(match (self.socket.local_addr()?, target.ip()) {
            (SocketAddr::V6(_), IpAddr::V4(v4)) => {
                SocketAddr::new(IpAddr::V6(v4.to_ipv6_mapped()), target.port())
            }
            _ => target,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn the_nonce_reaches_the_slot_port_over_udp() {
        let slot = UdpSocket::bind((Ipv4Addr::LOCALHOST, 0)).await.unwrap();
        let prober = Prober::bind().unwrap();

        prober
            .send(&Probe {
                target: slot.local_addr().unwrap(),
                nonce: "00112233445566778899aabbccddeeff".to_owned(),
            })
            .await
            .unwrap();

        let mut buffer = [0u8; 128];
        let (read, _) = slot.recv_from(&mut buffer).await.unwrap();
        assert_eq!(
            &buffer[..read],
            b"blastlands-probe:00112233445566778899aabbccddeeff"
        );
    }
}
