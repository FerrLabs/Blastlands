use std::sync::Arc;

use blastlands_lobby::config::Config;
use blastlands_lobby::matches::MatchDirectory;
use blastlands_lobby::ports::PortPool;
use blastlands_lobby::routes::{app, AppState};
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

    let state = AppState {
        directory: Arc::new(MatchDirectory::new(
            config.game_server_host.clone(),
            PortPool::new(config.port_range.clone()),
        )),
        instance_token: Arc::from(config.instance_token.as_str()),
    };

    let listener = TcpListener::bind(config.bind).await?;
    tracing::info!(address = %config.bind, "lobby listening");

    axum::serve(listener, app(state))
        .with_graceful_shutdown(shutdown())
        .await?;

    Ok(())
}

async fn shutdown() {
    if let Err(error) = signal::ctrl_c().await {
        tracing::error!(%error, "failed to listen for shutdown signal");
    }
}
