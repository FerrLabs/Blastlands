use axum::extract::rejection::JsonRejection;
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

    #[error("a match needs at least {min} players to start, {joined} joined")]
    NotEnoughPlayers { min: u8, joined: usize },

    #[error("no game server capacity available")]
    NoCapacity,

    #[error("unauthorized")]
    Unauthorized,

    #[error("too many requests, slow down")]
    RateLimited,

    #[error("one address may host at most {max} matches at a time")]
    TooManyMatches { max: usize },

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

    #[error("the lobby has not read a published client release yet")]
    ReleaseUnknown,

    #[error("that client release is not the one currently published")]
    ReleaseNotFound,

    #[error("the client download could not be obtained, try again shortly")]
    DownloadUnavailable,

    /// A request body axum refused before any handler saw it: malformed JSON, a
    /// wrong content type, or a field whose value does not match its type (an
    /// out-of-roster `character`, a display name outside the length or the
    /// character set `DisplayName::deserialize` enforces). Without this,
    /// `Json<T>` extraction failures skip `LobbyError` entirely and axum answers
    /// with its own plain-text body, which breaks the `{"code": ...}` contract
    /// every other error here keeps. A client reading for a code and finding
    /// none has no way to tell that case apart from a body it genuinely cannot
    /// parse.
    ///
    /// The status is axum's own for the rejection (422 for a body that parses as
    /// JSON but not into the target type, 400 for one that is not JSON at all),
    /// kept rather than flattened to one code so a syntax error and a value a
    /// validator refused stay tellable apart the way the rest of this API keeps
    /// client and server mistakes apart.
    #[error("{message}")]
    InvalidRequest { status: StatusCode, message: String },
}

impl LobbyError {
    fn status(&self) -> StatusCode {
        match self {
            Self::MatchNotFound | Self::ReleaseNotFound => StatusCode::NOT_FOUND,
            Self::MatchFull | Self::MatchAlreadyStarted | Self::NotEnoughPlayers { .. } => {
                StatusCode::CONFLICT
            }
            Self::NoCapacity | Self::ReleaseUnknown => StatusCode::SERVICE_UNAVAILABLE,
            Self::DownloadUnavailable => StatusCode::BAD_GATEWAY,
            Self::Unauthorized => StatusCode::UNAUTHORIZED,
            Self::RateLimited | Self::TooManyMatches { .. } => StatusCode::TOO_MANY_REQUESTS,
            Self::InvalidName(_) | Self::InvalidPlayerCount { .. } | Self::InvalidVersion(_) => {
                StatusCode::BAD_REQUEST
            }
            // 426 tells the client the request would succeed on a newer build, which
            // is exactly the signal the updater needs.
            Self::ClientTooOld { .. } => StatusCode::UPGRADE_REQUIRED,
            Self::InvalidRequest { status, .. } => *status,
        }
    }

    fn code(&self) -> &'static str {
        match self {
            Self::MatchNotFound => "match_not_found",
            Self::MatchFull => "match_full",
            Self::MatchAlreadyStarted => "match_already_started",
            Self::NotEnoughPlayers { .. } => "not_enough_players",
            Self::NoCapacity => "no_capacity",
            Self::Unauthorized => "unauthorized",
            Self::RateLimited => "rate_limited",
            Self::TooManyMatches { .. } => "too_many_matches",
            Self::InvalidName(_) => "invalid_name",
            Self::InvalidPlayerCount { .. } => "invalid_player_count",
            Self::InvalidVersion(_) => "invalid_version",
            Self::ClientTooOld { .. } => "client_too_old",
            Self::ReleaseUnknown => "release_unknown",
            Self::ReleaseNotFound => "release_not_found",
            Self::DownloadUnavailable => "download_unavailable",
            Self::InvalidRequest { .. } => "invalid_request",
        }
    }
}

#[derive(Serialize)]
struct ErrorBody {
    code: &'static str,
    message: String,
}

impl From<JsonRejection> for LobbyError {
    fn from(rejection: JsonRejection) -> Self {
        Self::InvalidRequest {
            status: rejection.status(),
            message: rejection.body_text(),
        }
    }
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
