use std::net::SocketAddr;
use std::sync::Arc;
use std::time::Instant;

use blastlands_lobby::config::{Config, Limits};
use blastlands_lobby::matches::{Lifetimes, MatchDirectory, ReapReason};
use blastlands_lobby::ports::PortPool;
use blastlands_lobby::routes::{app, AppState, ClientAddress};
use blastlands_lobby::throttle::RateLimiter;
use tokio::net::TcpListener;
use tokio::signal;
use tracing_subscriber::EnvFilter;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing_subscriber::fmt()
        .with_env_filter(
            EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new("info")),
        )
        .init();

    let config = Config::from_env()?;
    let limits = config.limits;

    let directory = Arc::new(MatchDirectory::new(
        config.game_server_host.clone(),
        PortPool::new(config.port_range.clone()),
    ));

    let creates = Arc::new(RateLimiter::new(limits.creates_per_window, limits.window));
    let joins = Arc::new(RateLimiter::new(limits.joins_per_window, limits.window));

    let state = AppState {
        directory: Arc::clone(&directory),
        instance_token: Arc::from(config.instance_token.as_str()),
        release: Arc::new(config.release.clone()),
        creates: Arc::clone(&creates),
        joins: Arc::clone(&joins),
        matches_per_address: limits.matches_per_address,
        address_source: if limits.trust_forwarded_for {
            ClientAddress::Forwarded
        } else {
            ClientAddress::Peer
        },
    };

    tokio::spawn(sweep(
        Arc::clone(&directory),
        Arc::clone(&creates),
        Arc::clone(&joins),
        limits,
    ));

    let listener = TcpListener::bind(config.bind).await?;
    tracing::info!(
        address = %config.bind,
        latest = %config.release.latest,
        minimum = %config.release.minimum,
        creates_per_window = limits.creates_per_window,
        joins_per_window = limits.joins_per_window,
        matches_per_address = limits.matches_per_address,
        trust_forwarded_for = limits.trust_forwarded_for,
        "lobby listening"
    );

    // Connect info, so the per-address limits see the caller rather than nothing. Without
    // it every request shares one bucket, and a flood from one machine throttles the
    // whole lobby instead of only itself.
    axum::serve(
        listener,
        app(state).into_make_service_with_connect_info::<SocketAddr>(),
    )
    .with_graceful_shutdown(shutdown())
    .await?;

    Ok(())
}

/// Takes away matches nobody is looking after, and forgets addresses that went quiet.
async fn sweep(
    directory: Arc<MatchDirectory>,
    creates: Arc<RateLimiter>,
    joins: Arc<RateLimiter>,
    limits: Limits,
) {
    let lifetimes = Lifetimes {
        unjoined: limits.unjoined_ttl,
        silent: limits.silent_ttl,
    };

    let mut ticker = tokio::time::interval(limits.sweep_every);
    loop {
        ticker.tick().await;
        let now = Instant::now();

        for reaped in directory.reap(now, lifetimes) {
            match reaped.reason {
                // Loud on purpose. A silent instance means a process died holding a
                // match, and that is worth knowing rather than quietly tidying away.
                ReapReason::InstanceWentSilent => tracing::warn!(
                    port = reaped.port,
                    "instance stopped heartbeating, match reaped and port released"
                ),
                ReapReason::NobodyJoined => tracing::info!(
                    port = reaped.port,
                    "match expired with nobody in it, port released"
                ),
            }
        }

        creates.prune(now);
        joins.prune(now);
    }
}

async fn shutdown() {
    if let Err(error) = signal::ctrl_c().await {
        tracing::error!(%error, "failed to listen for shutdown signal");
    }
}
