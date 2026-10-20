use axum::http::{header, HeaderMap};

use crate::error::LobbyError;
use crate::servers::ServerKey;

pub fn server_key(headers: &HeaderMap) -> Result<ServerKey, LobbyError> {
    headers
        .get(header::AUTHORIZATION)
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.strip_prefix("Bearer "))
        .filter(|key| !key.is_empty())
        .map(ServerKey::new)
        .ok_or(LobbyError::Unauthorized)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn headers_with(value: &str) -> HeaderMap {
        let mut headers = HeaderMap::new();
        headers.insert(
            header::AUTHORIZATION,
            value.parse().expect("valid header value"),
        );
        headers
    }

    #[test]
    fn reads_the_bearer_key() {
        assert_eq!(
            server_key(&headers_with("Bearer secret")).ok(),
            Some(ServerKey::new("secret"))
        );
    }

    #[test]
    fn rejects_a_missing_header() {
        assert_eq!(
            server_key(&HeaderMap::new()).err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn rejects_an_empty_bearer() {
        assert_eq!(
            server_key(&headers_with("Bearer ")).err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn rejects_another_authorization_scheme() {
        assert_eq!(
            server_key(&headers_with("Basic secret")).err(),
            Some(LobbyError::Unauthorized)
        );
    }
}
