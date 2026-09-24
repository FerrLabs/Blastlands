use crate::release::{InstallerInfo, ReleaseInfo};

const TEMPLATE: &str = include_str!("landing.html");
const ACTION_SLOT: &str = "{{action}}";
const NOTE_SLOT: &str = "{{note}}";

const INSTALLER_NOTE: &str = "Windows only for now. Run the installer, then start Blastlands from the Start menu. Windows may warn about an unknown publisher: the installer is not signed yet.";
const ARCHIVE_NOTE: &str =
    "Windows only for now. Unzip the archive anywhere and start the game from the folder.";

pub fn page(release: Option<&ReleaseInfo>, installer: Option<&InstallerInfo>) -> String {
    let (action, note) = match (installer, release) {
        (Some(installer), _) => (
            available(
                &installer.download_url,
                installer.version,
                &installer.sha256,
            ),
            INSTALLER_NOTE,
        ),
        (None, Some(release)) => (
            available(&release.download_url, release.latest, &release.sha256),
            ARCHIVE_NOTE,
        ),
        (None, None) => (unavailable(), ARCHIVE_NOTE),
    };

    TEMPLATE
        .replace(ACTION_SLOT, &action)
        .replace(NOTE_SLOT, note)
}

fn available(download_url: &str, version: impl std::fmt::Display, sha256: &str) -> String {
    format!(
        "<a class=\"button\" href=\"{}\">Install Blastlands</a>\n    \
         <p class=\"meta\">Version {} for Windows<code>SHA-256 {}</code></p>",
        escape(download_url),
        version,
        escape(sha256),
    )
}

fn unavailable() -> String {
    "<span class=\"button disabled\" aria-disabled=\"true\">Install Blastlands</span>\n    \
     <p class=\"meta\">The first build is not published yet.</p>"
        .to_owned()
}

fn escape(raw: &str) -> String {
    raw.chars()
        .fold(String::with_capacity(raw.len()), |mut out, c| {
            match c {
                '&' => out.push_str("&amp;"),
                '<' => out.push_str("&lt;"),
                '>' => out.push_str("&gt;"),
                '"' => out.push_str("&quot;"),
                '\'' => out.push_str("&#39;"),
                other => out.push(other),
            }
            out
        })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::version::ClientVersion;

    fn release(download_url: &str) -> ReleaseInfo {
        ReleaseInfo {
            latest: ClientVersion::new(26, 9, 83),
            minimum: ClientVersion::new(26, 9, 72),
            download_url: download_url.to_owned(),
            sha256: "ab".repeat(32),
        }
    }

    fn installer() -> InstallerInfo {
        InstallerInfo {
            version: ClientVersion::new(26, 9, 83),
            download_url: "https://blastlands.ferrlabs.com/v1/client/26.9.83/installer".to_owned(),
            sha256: "cd".repeat(32),
        }
    }

    #[test]
    fn a_published_build_links_straight_to_its_download() {
        let page = page(
            Some(&release(
                "https://blastlands.ferrlabs.com/v1/client/26.9.83/download",
            )),
            None,
        );

        assert!(page.contains(
            "href=\"https://blastlands.ferrlabs.com/v1/client/26.9.83/download\">Install Blastlands</a>"
        ));
        assert!(page.contains("Version 26.9.83"));
        assert!(page.contains(&"ab".repeat(32)));
        assert!(page.contains("Unzip the archive"));
        assert!(!page.contains(ACTION_SLOT));
        assert!(!page.contains(NOTE_SLOT));
    }

    #[test]
    fn an_installer_wins_over_the_archive_and_says_how_to_use_it() {
        let page = page(
            Some(&release(
                "https://blastlands.ferrlabs.com/v1/client/26.9.83/download",
            )),
            Some(&installer()),
        );

        assert!(page.contains(
            "href=\"https://blastlands.ferrlabs.com/v1/client/26.9.83/installer\">Install Blastlands</a>"
        ));
        assert!(!page.contains("/download"));
        assert!(page.contains(&"cd".repeat(32)));
        assert!(page.contains("Run the installer"));
        assert!(page.contains("not signed yet"));
        assert!(!page.contains("Unzip"));
    }

    #[test]
    fn before_any_build_exists_the_button_goes_nowhere() {
        let page = page(None, None);

        assert!(page.contains("class=\"button disabled\""));
        assert!(page.contains("not published yet"));
        assert!(!page.contains("/v1/client/"));
        assert!(!page.contains(ACTION_SLOT));
    }

    #[test]
    fn a_url_cannot_break_out_of_its_attribute() {
        let page = page(
            Some(&release("https://x.test/\"><script>alert(1)</script>")),
            None,
        );

        assert!(!page.contains("<script>alert(1)"));
        assert!(page.contains("&quot;&gt;&lt;script&gt;"));
    }
}
