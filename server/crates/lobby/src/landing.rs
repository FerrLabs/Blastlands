use crate::release::{InstallerInfo, ReleaseInfo};

const TEMPLATE: &str = include_str!("landing.html");
pub const LOGO: &[u8] = include_bytes!("site/logo.png");
const ACTION_SLOT: &str = "{{action}}";
const NOTE_SLOT: &str = "{{note}}";

const INSTALLER_NOTE: &str = "Windows only for now. Run the installer, then start Blastlands from the Start menu. Windows may warn about an unknown publisher: the installer is not signed yet.";
const REPOSITORY: &str = "https://github.com/FerrLabs/Blastlands";
const GITHUB_MARK: &str = "M8 0c4.42 0 8 3.58 8 8a8.013 8.013 0 0 1-5.45 7.59c-.4.08-.55-.17-.55-.38 0-.27.01-1.13.01-2.2 0-.75-.25-1.23-.54-1.48 1.78-.2 3.65-.88 3.65-3.95 0-.88-.31-1.59-.82-2.15.08-.2.36-1.02-.08-2.12 0 0-.67-.22-2.2.82-.64-.18-1.32-.27-2-.27-.68 0-1.36.09-2 .27-1.53-1.03-2.2-.82-2.2-.82-.44 1.1-.16 1.92-.08 2.12-.51.56-.82 1.28-.82 2.15 0 3.06 1.86 3.75 3.64 3.95-.23.2-.44.55-.51 1.07-.46.21-1.61.55-2.33-.66-.15-.24-.6-.83-1.23-.82-.67.01-.27.38.01.53.34.19.73.9.82 1.13.16.45.68 1.31 2.69.94 0 .67.01 1.3.01 1.49 0 .21-.15.45-.55.38A7.995 7.995 0 0 1 0 8c0-4.42 3.58-8 8-8Z";
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
        .replace(NOTE_SLOT, note)
        .replace(ACTION_SLOT, &action)
}

fn available(download_url: &str, version: impl std::fmt::Display, sha256: &str) -> String {
    format!(
        "<div class=\"actions\"><a class=\"button\" href=\"{}\">Install Blastlands</a>{}</div>\n    \
         <p class=\"meta\">Version {} for Windows<code>SHA-256 {}</code></p>",
        escape(download_url),
        github(),
        version,
        escape(sha256),
    )
}

fn unavailable() -> String {
    format!(
        "<div class=\"actions\"><span class=\"button disabled\" aria-disabled=\"true\">Install Blastlands</span>{}</div>\n    \
         <p class=\"meta\">The first build is not published yet.</p>",
        github(),
    )
}

fn github() -> String {
    format!(
        "<a class=\"github\" href=\"{REPOSITORY}\" aria-label=\"Blastlands on GitHub\" title=\"Blastlands on GitHub\">\
         <svg viewBox=\"0 0 16 16\" aria-hidden=\"true\"><path fill=\"currentColor\" d=\"{GITHUB_MARK}\"/></svg></a>"
    )
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
    fn the_repository_is_linked_next_to_the_install_button_before_and_after_a_release() {
        for page in [
            page(Some(&release("https://x.test/d")), None),
            page(None, None),
        ] {
            assert!(page.contains(&format!("class=\"github\" href=\"{REPOSITORY}\"")));
            assert!(page.contains("aria-label=\"Blastlands on GitHub\""));
        }
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
