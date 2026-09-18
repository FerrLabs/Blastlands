use std::time::Duration;

use reqwest::header::{ACCEPT, LOCATION};
use reqwest::redirect::Policy;
use reqwest::StatusCode;
use serde::Deserialize;
use thiserror::Error;

use crate::version::ClientVersion;

const RELEASES: &str = "https://api.github.com/repos/FerrLabs/Blastlands/releases?per_page=30";
const API_VERSION: &str = "2022-11-28";

#[derive(Debug, Deserialize)]
pub struct Release {
    tag_name: String,
    draft: bool,
    prerelease: bool,
    assets: Vec<Asset>,
}

#[derive(Debug, Deserialize)]
pub struct Asset {
    name: String,
    url: String,
    digest: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PublishedClient {
    pub version: ClientVersion,
    pub asset_url: String,
    pub sha256: String,
}

#[derive(Debug, Error)]
pub enum GithubError {
    #[error(transparent)]
    Http(#[from] reqwest::Error),

    #[error("GitHub answered {0} instead of redirecting to the asset")]
    NoRedirect(StatusCode),
}

pub fn newest_client(releases: &[Release]) -> Option<PublishedClient> {
    releases
        .iter()
        .filter(|release| !release.draft && !release.prerelease)
        .filter_map(published_client)
        .max_by_key(|client| client.version)
}

fn published_client(release: &Release) -> Option<PublishedClient> {
    let version = release.tag_name.strip_prefix('v')?.parse().ok()?;
    let archive = format!("Blastlands-{}-windows.zip", release.tag_name);
    let asset = release.assets.iter().find(|asset| asset.name == archive)?;
    let sha256 = asset.digest.as_deref()?.strip_prefix("sha256:")?;

    is_sha256(sha256).then(|| PublishedClient {
        version,
        asset_url: asset.url.clone(),
        sha256: sha256.to_owned(),
    })
}

fn is_sha256(hex: &str) -> bool {
    hex.len() == 64
        && hex
            .bytes()
            .all(|byte| matches!(byte, b'0'..=b'9' | b'a'..=b'f'))
}

pub struct Github {
    http: reqwest::Client,
    token: String,
}

impl Github {
    pub fn new(token: String) -> Result<Self, reqwest::Error> {
        let http = reqwest::Client::builder()
            .user_agent("blastlands-lobby")
            .redirect(Policy::none())
            .timeout(Duration::from_secs(10))
            .build()?;

        Ok(Self { http, token })
    }

    pub async fn newest_client(&self) -> Result<Option<PublishedClient>, GithubError> {
        let releases: Vec<Release> = self
            .http
            .get(RELEASES)
            .bearer_auth(&self.token)
            .header(ACCEPT, "application/vnd.github+json")
            .header("X-GitHub-Api-Version", API_VERSION)
            .send()
            .await?
            .error_for_status()?
            .json()
            .await?;

        Ok(newest_client(&releases))
    }

    pub async fn download_location(&self, asset_url: &str) -> Result<String, GithubError> {
        let response = self
            .http
            .get(asset_url)
            .bearer_auth(&self.token)
            .header(ACCEPT, "application/octet-stream")
            .header("X-GitHub-Api-Version", API_VERSION)
            .send()
            .await?;

        response
            .headers()
            .get(LOCATION)
            .filter(|_| response.status().is_redirection())
            .and_then(|location| location.to_str().ok())
            .map(str::to_owned)
            .ok_or(GithubError::NoRedirect(response.status()))
    }
}

#[cfg(test)]
mod tests {
    use serde_json::{json, Value};

    use super::*;

    const DIGEST: &str = "5f70bf18a086007016e948b04aed3b82103a36bea41755b6cddfaf10ace3c6ef";

    fn release(tag: &str, assets: Value) -> Value {
        json!({ "tag_name": tag, "draft": false, "prerelease": false, "assets": assets })
    }

    fn windows_asset(tag: &str, digest: Option<&str>) -> Value {
        json!({
            "name": format!("Blastlands-{tag}-windows.zip"),
            "url": format!("https://api.github.com/repos/FerrLabs/Blastlands/releases/assets/{tag}"),
            "digest": digest,
        })
    }

    fn newest(releases: Value) -> Option<PublishedClient> {
        let releases: Vec<Release> = serde_json::from_value(releases).expect("fixture parses");
        newest_client(&releases)
    }

    fn sha(digest: &str) -> String {
        format!("sha256:{digest}")
    }

    #[test]
    fn picks_the_release_carrying_the_windows_archive() {
        let client = newest(json!([release(
            "v26.9.18",
            json!([windows_asset("v26.9.18", Some(&sha(DIGEST)))])
        )]))
        .expect("the release is usable");

        assert_eq!(client.version, "26.9.18".parse().unwrap());
        assert_eq!(client.sha256, DIGEST);
        assert!(client.asset_url.ends_with("/assets/v26.9.18"));
    }

    #[test]
    fn a_release_still_waiting_for_its_build_is_skipped() {
        let client = newest(json!([
            release("v26.9.19", json!([])),
            release(
                "v26.9.18",
                json!([windows_asset("v26.9.18", Some(&sha(DIGEST)))])
            ),
        ]))
        .expect("the older release is usable");

        assert_eq!(client.version, "26.9.18".parse().unwrap());
    }

    #[test]
    fn the_highest_version_wins_whatever_the_listing_order() {
        let client = newest(json!([
            release(
                "v26.9.2",
                json!([windows_asset("v26.9.2", Some(&sha(DIGEST)))])
            ),
            release(
                "v26.10.0",
                json!([windows_asset("v26.10.0", Some(&sha(DIGEST)))])
            ),
        ]))
        .unwrap();

        assert_eq!(client.version, "26.10.0".parse().unwrap());
    }

    #[test]
    fn drafts_and_prereleases_are_never_offered() {
        let mut draft = release(
            "v26.9.20",
            json!([windows_asset("v26.9.20", Some(&sha(DIGEST)))]),
        );
        draft["draft"] = json!(true);
        let mut prerelease = release(
            "v26.9.21",
            json!([windows_asset("v26.9.21", Some(&sha(DIGEST)))]),
        );
        prerelease["prerelease"] = json!(true);

        assert_eq!(newest(json!([draft, prerelease])), None);
    }

    #[test]
    fn an_archive_without_a_usable_digest_is_not_offered() {
        for digest in [
            None,
            Some("md5:abc".to_owned()),
            Some(sha("abc")),
            Some(sha(&DIGEST.to_uppercase())),
        ] {
            let listing = json!([release(
                "v26.9.18",
                json!([windows_asset("v26.9.18", digest.as_deref())])
            )]);

            assert_eq!(newest(listing), None, "{digest:?} should be refused");
        }
    }

    #[test]
    fn an_archive_named_for_another_release_is_ignored() {
        let listing = json!([release(
            "v26.9.18",
            json!([windows_asset("v26.9.17", Some(&sha(DIGEST)))])
        )]);

        assert_eq!(newest(listing), None);
    }

    #[test]
    fn a_tag_that_is_not_a_client_version_is_ignored() {
        let listing = json!([release(
            "nightly",
            json!([windows_asset("nightly", Some(&sha(DIGEST)))])
        )]);

        assert_eq!(newest(listing), None);
    }
}
