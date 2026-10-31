#![no_main]

use blastlands_lobby::github::{newest_client, PublishedClient, Release, ReleaseAsset};
use blastlands_lobby::release::Releases;
use blastlands_lobby::version::ClientVersion;
use libfuzzer_sys::fuzz_target;

fn is_lowercase_sha256(hex: &str) -> bool {
    hex.len() == 64 && hex.bytes().all(|b| matches!(b, b'0'..=b'9' | b'a'..=b'f'))
}

fn assert_checked(asset: &ReleaseAsset) {
    assert!(is_lowercase_sha256(&asset.sha256));
}

fn assert_published(client: &PublishedClient) {
    assert!(is_lowercase_sha256(&client.sha256));
    client.installer.iter().for_each(assert_checked);
    client.server.iter().for_each(assert_checked);
}

fuzz_target!(|data: &[u8]| {
    let Ok(releases) = serde_json::from_slice::<Vec<Release>>(data) else {
        return;
    };
    let Some(client) = newest_client(&releases) else {
        return;
    };
    assert_published(&client);

    let releases = Releases::new(
        ClientVersion::new(0, 0, 0),
        "https://lobby.example".to_owned(),
    );
    assert!(releases.publish(client.clone()));
    assert!(!releases.publish(client.clone()));
    let current = releases.current().expect("a published release is current");
    assert_eq!(current.latest, client.version);
    assert_eq!(current.sha256, client.sha256);
    assert_eq!(
        releases.asset_for(client.version).as_deref(),
        Ok(client.asset_url.as_str())
    );
});
