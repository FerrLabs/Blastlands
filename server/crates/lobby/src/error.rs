use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Serialize;
use thiserror::Error;

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
}

impl LobbyError {
    fn status(&self) -> StatusCode {
        match self {
            Self::MatchNotFound => StatusCode::NOT_FOUND,
            Self::MatchFull | Self::MatchAlreadyStarted => StatusCode::CONFLICT,
            Self::NoCapacity => StatusCode::SERVICE_UNAVAILABLE,
            Self::Unauthorized => StatusCode::UNAUTHORIZED,
            Self::InvalidName(_) | Self::InvalidPlayerCount { .. } => StatusCode::BAD_REQUEST,
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
