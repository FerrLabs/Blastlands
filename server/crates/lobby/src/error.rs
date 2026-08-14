use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Serialize;
use thiserror::Error;

use crate::version::ClientVersion;

#[derive(Debug, Error, PartialEq, Eq)]
pub enum LobbyError {
    #[error("match not found")]
    MatchNotFound,

    #[error("match is full")]
    MatchFull,

    #[error("match has already started")]
    MatchAlreadyStarted,

    #[error("no game server capacity available")]
    NoCapacity,

    #[error("unauthorized")]
    Unauthorized,

    #[error("{0}")]
    InvalidName(String),

    #[error("player count must be between {min} and {max}")]
    InvalidPlayerCount { min: u8, max: u8 },

    #[error("invalid client version: {0}")]
    InvalidVersion(String),

    #[error("client {client} is older than the minimum supported {minimum}")]
    ClientTooOld {
        client: ClientVersion,
        minimum: ClientVersion,
    },
}

impl LobbyError {
    fn status(&self) -> StatusCode {
        match self {
            Self::MatchNotFound => StatusCode::NOT_FOUND,
            Self::MatchFull | Self::MatchAlreadyStarted => StatusCode::CONFLICT,
            Self::NoCapacity => StatusCode::SERVICE_UNAVAILABLE,
            Self::Unauthorized => StatusCode::UNAUTHORIZED,
            Self::InvalidName(_) | Self::InvalidPlayerCount { .. } | Self::InvalidVersion(_) => {
                StatusCode::BAD_REQUEST
            }
            // 426 tells the client the request would succeed on a newer build, which
            // is exactly the signal the updater needs.
            Self::ClientTooOld { .. } => StatusCode::UPGRADE_REQUIRED,
        }
    }

    fn code(&self) -> &'static str {
        match self {
            Self::MatchNotFound => "match_not_found",
            Self::MatchFull => "match_full",
            Self::MatchAlreadyStarted => "match_already_started",
            Self::NoCapacity => "no_capacity",
            Self::Unauthorized => "unauthorized",
            Self::InvalidName(_) => "invalid_name",
            Self::InvalidPlayerCount { .. } => "invalid_player_count",
            Self::InvalidVersion(_) => "invalid_version",
            Self::ClientTooOld { .. } => "client_too_old",
        }
    }
}

#[derive(Serialize)]
struct ErrorBody {
    code: &'static str,
    message: String,
}

impl IntoResponse for LobbyError {
    fn into_response(self) -> Response {
        let body = ErrorBody {
            code: self.code(),
            message: self.to_string(),
        };

        (self.status(), Json(body)).into_response()
    }
}
