#![no_main]

use blastlands_lobby::version::ClientVersion;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|text: &str| {
    if let Ok(version) = text.parse::<ClientVersion>() {
        let canonical = version.to_string();
        let back: ClientVersion = canonical.parse().expect("a rendered version parses");
        assert_eq!(back, version);
        assert_eq!(back.to_string(), canonical);
        assert_eq!(version.cmp(&back), std::cmp::Ordering::Equal);
    }
});
