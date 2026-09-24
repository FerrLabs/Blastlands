use crate::release::ReleaseInfo;

const TEMPLATE: &str = include_str!("landing.html");
const ACTION_SLOT: &str = "{{action}}";

pub fn page(release: Option<&ReleaseInfo>) -> String {
    TEMPLATE.replace(ACTION_SLOT, &action(release))
}

fn action(release: Option<&ReleaseInfo>) -> String {
    match release {
        Some(release) => format!(
            "<a class=\"button\" href=\"{}\">Install Blastlands</a>\n    \
             <p class=\"meta\">Version {} for Windows<code>SHA-256 {}</code></p>",
            escape(&release.download_url),
            release.latest,
            escape(&release.sha256),
        ),
        None => {
            "<span class=\"button disabled\" aria-disabled=\"true\">Install Blastlands</span>\n    \
                 <p class=\"meta\">The first build is not published yet.</p>"
                .to_owned()
        }
    }
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

    #[test]
    fn a_published_build_links_straight_to_its_download() {
        let page = page(Some(&release(
            "https://blastlands.ferrlabs.com/v1/client/26.9.83/download",
        )));

        assert!(page.contains(
            "href=\"https://blastlands.ferrlabs.com/v1/client/26.9.83/download\">Install Blastlands</a>"
        ));
        assert!(page.contains("Version 26.9.83"));
        assert!(page.contains(&"ab".repeat(32)));
        assert!(!page.contains(ACTION_SLOT));
    }

    #[test]
    fn before_any_build_exists_the_button_goes_nowhere() {
        let page = page(None);

        assert!(page.contains("class=\"button disabled\""));
        assert!(page.contains("not published yet"));
        assert!(!page.contains("/v1/client/"));
        assert!(!page.contains(ACTION_SLOT));
    }

    #[test]
    fn a_url_cannot_break_out_of_its_attribute() {
        let page = page(Some(&release(
            "https://x.test/\"><script>alert(1)</script>",
        )));

        assert!(!page.contains("<script>alert(1)"));
        assert!(page.contains("&quot;&gt;&lt;script&gt;"));
    }
}
