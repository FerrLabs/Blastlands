#![no_main]

use axum::http::{HeaderMap, HeaderValue};
use blastlands_lobby::release::{Releases, VERSION_HEADER};
use blastlands_lobby::version::ClientVersion;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|data: &[u8]| {
    let minimum = ClientVersion::new(26, 8, 0);
    let releases = Releases::new(minimum, "https://lobby.example".to_owned());

    let mut headers = HeaderMap::new();
    if let Ok(value) = HeaderValue::from_bytes(data) {
        headers.insert(VERSION_HEADER, value);
    }

    if let Ok(version) = releases.require_supported(&headers) {
        assert!(version >= minimum);
        assert!(releases.accepts(version));
    }
});
