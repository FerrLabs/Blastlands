use axum::http::{header, HeaderMap};

use crate::error::LobbyError;

pub fn require_instance_token(headers: &HeaderMap, expected: &str) -> Result<(), LobbyError> {
    let provided = headers
        .get(header::AUTHORIZATION)
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.strip_prefix("Bearer "))
        .unwrap_or_default();

    if constant_time_eq(provided.as_bytes(), expected.as_bytes()) {
        Ok(())
    } else {
        Err(LobbyError::Unauthorized)
    }
}

fn constant_time_eq(left: &[u8], right: &[u8]) -> bool {
    left.len() == right.len()
        && left
            .iter()
            .zip(right)
            .fold(0u8, |accumulator, (a, b)| accumulator | (a ^ b))
            == 0
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
    fn accepts_the_expected_bearer_token() {
        assert!(require_instance_token(&headers_with("Bearer secret"), "secret").is_ok());
    }

    #[test]
    fn rejects_a_wrong_token() {
        assert_eq!(
            require_instance_token(&headers_with("Bearer wrong"), "secret").err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn rejects_a_token_that_is_only_a_prefix_of_the_expected_one() {
        assert_eq!(
            require_instance_token(&headers_with("Bearer sec"), "secret").err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn rejects_a_missing_header() {
        assert_eq!(
            require_instance_token(&HeaderMap::new(), "secret").err(),
            Some(LobbyError::Unauthorized)
        );
    }

    #[test]
    fn rejects_another_authorization_scheme() {
        assert_eq!(
            require_instance_token(&headers_with("Basic secret"), "secret").err(),
            Some(LobbyError::Unauthorized)
        );
    }
}
