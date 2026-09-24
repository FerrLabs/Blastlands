use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::sync::Arc;
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use axum::extract::{ConnectInfo, FromRequest, Path, Request, State};
use axum::http::{header, HeaderMap, StatusCode};
use axum::response::{Html, IntoResponse, Redirect, Response};
use axum::routing::{delete, get, post};
use axum::{Json, Router};
use serde::{Deserialize, Serialize};
use tower_http::trace::TraceLayer;

use crate::auth::require_instance_token;
use crate::characters::Character;
use crate::download::DownloadLinks;
use crate::error::LobbyError;
use crate::landing;
use crate::matches::{
    CreateMatch, GameServerEndpoint, HostTicket, Match, MatchDirectory, MatchId, MatchState,
};
use crate::names::DisplayName;
use crate::release::{ReleaseInfo, Releases};
use crate::throttle::RateLimiter;
use crate::tickets::{GameTicket, TicketSigner};
use crate::version::ClientVersion;

/// How the address a request came from is decided.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ClientAddress {
    /// The socket's own peer address. Correct when the lobby is reached directly.
    Peer,
    /// The entry your own proxy appended to `X-Forwarded-For`, for a lobby behind a
    /// reverse proxy that terminates TLS.
    ///
    /// The **last** entry, not the first. Most proxies append rather than overwrite
    /// (nginx's `$proxy_add_x_forwarded_for`, ALB, and most defaults), so a client that
    /// sends `X-Forwarded-For: 1.2.3.4` itself arrives as `1.2.3.4, <real address>`.
    /// Reading the front of that list means reading whatever the caller typed, which
    /// would let a flood mint a fresh address per request and walk straight through the
    /// limits this exists to enforce.
    ///
    /// This assumes exactly one trusted hop. More than one, or a proxy that passes a
    /// client-supplied header through untouched, and the last entry is not trustworthy
    /// either without stripping a known number of hops.
    ///
    /// Off unless turned on, because on a directly reachable lobby the header is simply
    /// whatever the caller wrote.
    Forwarded,
}

#[derive(Clone)]
pub struct AppState {
    pub directory: Arc<MatchDirectory>,
    pub instance_token: Arc<str>,
    pub tickets: Arc<TicketSigner>,
    pub release: Arc<Releases>,
    pub downloads: Arc<DownloadLinks>,
    pub creates: Arc<RateLimiter>,
    pub joins: Arc<RateLimiter>,
    pub matches_per_address: usize,
    pub address_source: ClientAddress,
}

/// `Json<T>`, except a body axum itself refuses (malformed JSON, the wrong content
/// type, an out-of-roster enum value) becomes a [`LobbyError::InvalidRequest`]
/// instead of axum's own plain-text response, so every refusal this service makes
/// carries the same `{"code": ...}` body a caller can act on.
pub struct ValidatedJson<T>(pub T);

impl<T, S> FromRequest<S> for ValidatedJson<T>
where
    T: serde::de::DeserializeOwned,
    S: Send + Sync,
{
    type Rejection = LobbyError;

    async fn from_request(req: Request, state: &S) -> Result<Self, Self::Rejection> {
        let Json(value) = Json::<T>::from_request(req, state)
            .await
            .map_err(LobbyError::from)?;
        Ok(Self(value))
    }
}

/// The address a request is attributed to for the per-address limits.
///
/// Written as an extractor rather than a helper so it can run before the body: axum
/// requires everything but the last extractor to work on the request parts alone, and
/// the last one is always the JSON payload.
#[derive(Debug, Clone, Copy)]
pub struct Caller(pub IpAddr);

impl axum::extract::FromRequestParts<AppState> for Caller {
    type Rejection = std::convert::Infallible;

    async fn from_request_parts(
        parts: &mut axum::http::request::Parts,
        state: &AppState,
    ) -> Result<Self, Self::Rejection> {
        if state.address_source == ClientAddress::Forwarded {
            if let Some(forwarded) = parts
                .headers
                .get("x-forwarded-for")
                .and_then(|value| value.to_str().ok())
                .and_then(|value| value.rsplit(',').next())
                .map(str::trim)
                .and_then(|value| value.parse::<IpAddr>().ok())
            {
                return Ok(Self(forwarded));
            }
        }

        // Falling back rather than refusing. A missing peer address means the router was
        // built without connect info, which is a wiring mistake on our side and not
        // something a caller should be told about. Every such request then shares one
        // bucket, so the limits still hold, they just hold together.
        let peer = parts
            .extensions
            .get::<ConnectInfo<SocketAddr>>()
            .map(|ConnectInfo(socket)| socket.ip())
            .unwrap_or(IpAddr::V4(Ipv4Addr::UNSPECIFIED));

        Ok(Self(peer))
    }
}

#[derive(Debug, Deserialize)]
pub struct CreateMatchRequest {
    pub name: DisplayName,
    pub host: DisplayName,
    pub max_players: u8,
    #[serde(default)]
    pub character: Option<Character>,
}

#[derive(Debug, Deserialize)]
pub struct JoinRequest {
    pub player: DisplayName,
    #[serde(default)]
    pub character: Option<Character>,
}

#[derive(Debug, Serialize)]
pub struct MatchSummary {
    pub id: MatchId,
    pub name: DisplayName,
    pub host: DisplayName,
    pub players: u8,
    pub bots: u8,
    pub max_players: u8,
}

#[derive(Debug, Serialize)]
pub struct MatchStatus {
    #[serde(flatten)]
    pub summary: MatchSummary,
    pub state: MatchState,
}

impl From<&Match> for MatchSummary {
    fn from(entry: &Match) -> Self {
        Self {
            id: entry.id,
            name: entry.name.clone(),
            host: entry.host.clone(),
            players: entry.players.len().min(usize::from(u8::MAX)) as u8,
            bots: entry.bots,
            max_players: entry.max_players,
        }
    }
}

/// Proof that the caller is the one who created this match. Match ids are public, so
/// without it anybody could start or reshape anybody else's.
#[derive(Debug, Deserialize)]
pub struct HostRequest {
    pub ticket: HostTicket,
}

#[derive(Debug, Serialize)]
pub struct MatchCreated {
    #[serde(flatten)]
    pub summary: MatchSummary,
    pub endpoint: GameServerEndpoint,
    pub ticket: HostTicket,
    pub game_ticket: GameTicket,
}

#[derive(Debug, Serialize)]
pub struct JoinAccepted {
    pub endpoint: GameServerEndpoint,
    pub ticket: GameTicket,
}

pub fn app(state: AppState) -> Router {
    Router::new()
        .route("/", get(landing_page))
        .route("/healthz", get(health))
        .route("/v1/version", get(version))
        .route("/v1/client/{version}/download", get(download_client))
        .route("/v1/client/{version}/installer", get(download_installer))
        .route("/v1/matches", get(list_matches).post(create_match))
        .route("/v1/matches/{id}", get(match_status))
        .route("/v1/matches/{id}/join", post(join_match))
        .route("/v1/matches/{id}/start", post(start_match))
        .route("/v1/matches/{id}/bots", post(add_bot_to_match))
        .route("/internal/instances/{port}", get(instance_assignment))
        .route("/internal/matches/{id}", delete(finish_match))
        .route("/internal/matches/{id}/heartbeat", post(heartbeat_match))
        .layer(TraceLayer::new_for_http())
        .with_state(state)
}

async fn health() -> StatusCode {
    StatusCode::NO_CONTENT
}

async fn landing_page(State(state): State<AppState>) -> impl IntoResponse {
    (
        [(header::CACHE_CONTROL, "public, max-age=60")],
        Html(landing::page(
            state.release.current().as_ref(),
            state.release.installer().as_ref(),
        )),
    )
}

// Deliberately ungated: a client too old to be allowed in still has to be able to
// ask what it should upgrade to.
async fn version(State(state): State<AppState>) -> Result<Json<ReleaseInfo>, LobbyError> {
    state
        .release
        .current()
        .map(Json)
        .ok_or(LobbyError::ReleaseUnknown)
}

async fn download_client(
    State(state): State<AppState>,
    Path(version): Path<String>,
) -> Result<Redirect, LobbyError> {
    let asset_url = state.release.asset_for(version.parse::<ClientVersion>()?)?;
    redirect_to_asset(&state, &version, &asset_url).await
}

async fn download_installer(
    State(state): State<AppState>,
    Path(version): Path<String>,
) -> Result<Redirect, LobbyError> {
    let asset_url = state
        .release
        .installer_for(version.parse::<ClientVersion>()?)?;
    redirect_to_asset(&state, &version, &asset_url).await
}

async fn redirect_to_asset(
    state: &AppState,
    version: &str,
    asset_url: &str,
) -> Result<Redirect, LobbyError> {
    let location = state.downloads.location(asset_url).await.map_err(|error| {
        tracing::warn!(%error, %version, "could not obtain a download link from GitHub");
        LobbyError::DownloadUnavailable
    })?;

    Ok(Redirect::temporary(&location))
}

async fn match_status(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
) -> Result<Json<MatchStatus>, LobbyError> {
    let entry = state.directory.get(id).ok_or(LobbyError::MatchNotFound)?;

    Ok(Json(MatchStatus {
        summary: MatchSummary::from(&entry),
        state: entry.state,
    }))
}

async fn list_matches(State(state): State<AppState>) -> Json<Vec<MatchSummary>> {
    Json(
        state
            .directory
            .open_matches()
            .iter()
            .map(MatchSummary::from)
            .collect(),
    )
}

async fn create_match(
    State(state): State<AppState>,
    Caller(address): Caller,
    headers: HeaderMap,
    ValidatedJson(request): ValidatedJson<CreateMatchRequest>,
) -> Result<(StatusCode, Json<MatchCreated>), LobbyError> {
    state.release.require_supported(&headers)?;

    let now = Instant::now();

    if !state.creates.allow(address, now) {
        return Err(LobbyError::RateLimited);
    }

    let (entry, ticket) = state.directory.create(
        CreateMatch {
            name: request.name,
            host: request.host,
            max_players: request.max_players,
            host_address: address,
        },
        now,
        state.matches_per_address,
    )?;

    let created = MatchCreated {
        summary: MatchSummary::from(&entry),
        game_ticket: state
            .tickets
            .issue(entry.id, &entry.host, request.character, since_epoch()),
        endpoint: entry.endpoint,
        ticket,
    };

    Ok((StatusCode::CREATED, Json(created)))
}

async fn join_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    Caller(address): Caller,
    headers: HeaderMap,
    ValidatedJson(request): ValidatedJson<JoinRequest>,
) -> Result<Json<JoinAccepted>, LobbyError> {
    state.release.require_supported(&headers)?;

    if !state.joins.allow(address, Instant::now()) {
        return Err(LobbyError::RateLimited);
    }

    let admitted = state.directory.join(id, request.player.clone())?;
    let ticket = state
        .tickets
        .issue(id, &request.player, request.character, since_epoch());

    Ok(Json(JoinAccepted {
        endpoint: admitted.endpoint,
        ticket,
    }))
}

fn since_epoch() -> Duration {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or(Duration::ZERO)
}

async fn start_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    ValidatedJson(request): ValidatedJson<HostRequest>,
) -> Result<StatusCode, LobbyError> {
    state.directory.start(id, request.ticket, Instant::now())?;
    Ok(StatusCode::NO_CONTENT)
}

async fn add_bot_to_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    ValidatedJson(request): ValidatedJson<HostRequest>,
) -> Result<Json<MatchStatus>, LobbyError> {
    let entry = state.directory.add_bot(id, request.ticket)?;

    Ok(Json(MatchStatus {
        summary: MatchSummary::from(&entry),
        state: entry.state,
    }))
}

// What an instance asks for on boot. It knows its own port and nothing else: the match
// it should run is whatever the lobby put on that port, so this is the only thing
// between a pod on a fixed port and the match it serves.
//
// 204 rather than 404 while nothing is assigned, because an idle instance polling an
// empty slot is the normal state, not a mistake to log.
async fn instance_assignment(
    State(state): State<AppState>,
    Path(port): Path<u16>,
    headers: HeaderMap,
) -> Result<Response, LobbyError> {
    require_instance_token(&headers, &state.instance_token)?;

    Ok(match state.directory.assignment(port) {
        Some(assignment) => Json(assignment).into_response(),
        None => StatusCode::NO_CONTENT.into_response(),
    })
}

async fn heartbeat_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    headers: HeaderMap,
) -> Result<StatusCode, LobbyError> {
    require_instance_token(&headers, &state.instance_token)?;
    state.directory.heartbeat(id, Instant::now())?;
    Ok(StatusCode::NO_CONTENT)
}

async fn finish_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    headers: HeaderMap,
) -> Result<StatusCode, LobbyError> {
    require_instance_token(&headers, &state.instance_token)?;
    state.directory.finish(id)?;
    Ok(StatusCode::NO_CONTENT)
}

#[cfg(test)]
mod tests {
    use axum::body::{to_bytes, Body};
    use axum::http::{header, Request};
    use axum::response::Response;
    use serde_json::{json, Value};
    use tower::ServiceExt;

    use super::*;
    use crate::download::LINK_LIFETIME;
    use crate::github::{Github, PublishedClient};
    use crate::ports::PortPool;

    use crate::release::VERSION_HEADER;

    const TOKEN: &str = "instance-token";

    fn signer() -> TicketSigner {
        let key = crate::tickets::TicketKey::new(b"route-tests-ticket-key-0123456789".to_vec())
            .expect("long enough");
        TicketSigner::new(key, Duration::from_secs(300))
    }
    const CURRENT: &str = "26.9.0";

    fn unpublished() -> Arc<Releases> {
        Arc::new(Releases::new(
            "26.8.0".parse().unwrap(),
            "https://api.blastlands.test".to_owned(),
        ))
    }

    fn release() -> Arc<Releases> {
        let releases = unpublished();
        releases.publish(PublishedClient {
            version: CURRENT.parse().unwrap(),
            asset_url: "https://api.github.com/assets/1".to_owned(),
            sha256: "ab".repeat(32),
            installer: None,
        });
        releases
    }

    fn downloads() -> Arc<DownloadLinks> {
        let github = Github::new("github-token".to_owned()).expect("client builds");
        Arc::new(DownloadLinks::new(Arc::new(github), LINK_LIFETIME))
    }

    fn router_with(release: Arc<Releases>) -> Router {
        app(AppState {
            directory: Arc::new(MatchDirectory::new(
                "game.blastlands.test".to_owned(),
                PortPool::new(7000..=7001),
            )),
            instance_token: Arc::from(TOKEN),
            tickets: Arc::new(signer()),
            release,
            downloads: downloads(),
            // Limits off by default in these tests: they are about routing and payloads,
            // and a limiter counting their requests would make them order-dependent.
            // The tests that are about the limits turn them on themselves.
            creates: Arc::new(RateLimiter::new(0, Duration::from_secs(60))),
            joins: Arc::new(RateLimiter::new(0, Duration::from_secs(60))),
            matches_per_address: 0,
            address_source: ClientAddress::Peer,
        })
    }

    use std::time::Duration;

    /// A router that believes `X-Forwarded-For`, as it would behind a reverse proxy.
    fn forwarded_router(creates: u32) -> Router {
        app(AppState {
            directory: Arc::new(MatchDirectory::new(
                "game.blastlands.test".to_owned(),
                PortPool::new(7000..=7010),
            )),
            instance_token: Arc::from(TOKEN),
            tickets: Arc::new(signer()),
            release: release(),
            downloads: downloads(),
            creates: Arc::new(RateLimiter::new(creates, Duration::from_secs(60))),
            joins: Arc::new(RateLimiter::new(0, Duration::from_secs(60))),
            matches_per_address: 0,
            address_source: ClientAddress::Forwarded,
        })
    }

    fn forwarded_post(forwarded: &str, name: &str) -> Request<Body> {
        Request::builder()
            .method("POST")
            .uri("/v1/matches")
            .header(header::CONTENT_TYPE, "application/json")
            .header(VERSION_HEADER, CURRENT)
            .header("x-forwarded-for", forwarded)
            .body(Body::from(
                json!({ "name": name, "host": "Bryan", "max_players": 4 }).to_string(),
            ))
            .expect("request should build")
    }

    /// A router whose limits are switched on, for the tests that are about the limits.
    fn throttled_router(creates: u32, per_address: usize) -> Router {
        app(AppState {
            directory: Arc::new(MatchDirectory::new(
                "game.blastlands.test".to_owned(),
                PortPool::new(7000..=7010),
            )),
            instance_token: Arc::from(TOKEN),
            tickets: Arc::new(signer()),
            release: release(),
            downloads: downloads(),
            creates: Arc::new(RateLimiter::new(creates, Duration::from_secs(60))),
            joins: Arc::new(RateLimiter::new(0, Duration::from_secs(60))),
            matches_per_address: per_address,
            address_source: ClientAddress::Peer,
        })
    }

    fn router() -> Router {
        router_with(release())
    }

    fn post_json(uri: &str, body: Value) -> Request<Body> {
        post_json_as(uri, body, Some(CURRENT))
    }

    fn post_json_as(uri: &str, body: Value, version: Option<&str>) -> Request<Body> {
        let mut builder = Request::builder()
            .method("POST")
            .uri(uri)
            .header(header::CONTENT_TYPE, "application/json");

        if let Some(v) = version {
            builder = builder.header(VERSION_HEADER, v);
        }

        builder
            .body(Body::from(body.to_string()))
            .expect("request should build")
    }

    async fn body_json(response: Response) -> Value {
        let bytes = to_bytes(response.into_body(), usize::MAX)
            .await
            .expect("body should be readable");
        serde_json::from_slice(&bytes).expect("body should be json")
    }

    fn instance_get(uri: &str) -> Request<Body> {
        Request::builder()
            .uri(uri)
            .header(header::AUTHORIZATION, format!("Bearer {TOKEN}"))
            .body(Body::empty())
            .expect("request should build")
    }

    async fn create_match_on(router: &Router, name: &str) -> Value {
        let response = router
            .clone()
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": name, "host": "Bryan", "max_players": 4 }),
            ))
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::CREATED);
        body_json(response).await
    }

    /// The lobby refuses to start a match below `MIN_PLAYERS`, so a test that wants a
    /// running one has to seat a guest next to the host first.
    async fn join_match_on(router: &Router, id: &str, player: &str) {
        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": player }),
            ))
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::OK);
    }

    #[tokio::test]
    async fn the_version_endpoint_is_reachable_without_a_version_header() {
        // A client too old to play still has to learn what to upgrade to.
        let response = router()
            .oneshot(
                Request::builder()
                    .uri("/v1/version")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);

        let body = body_json(response).await;
        assert_eq!(body["latest"], "26.9.0");
        assert_eq!(body["minimum"], "26.8.0");
        assert_eq!(
            body["download_url"],
            "https://api.blastlands.test/v1/client/26.9.0/download"
        );
        assert_eq!(body["sha256"], "ab".repeat(32));
    }

    fn get_request(uri: &str) -> Request<Body> {
        Request::builder()
            .uri(uri)
            .body(Body::empty())
            .expect("request should build")
    }

    #[tokio::test]
    async fn the_version_endpoint_is_unavailable_until_a_release_is_read() {
        let response = router_with(unpublished())
            .oneshot(get_request("/v1/version"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::SERVICE_UNAVAILABLE);
        assert_eq!(body_json(response).await["code"], "release_unknown");
    }

    async fn body_text(response: Response) -> String {
        let bytes = to_bytes(response.into_body(), usize::MAX)
            .await
            .expect("body should be readable");
        String::from_utf8(bytes.to_vec()).expect("body should be text")
    }

    #[tokio::test]
    async fn the_root_offers_the_published_build_as_html() {
        let response = router().oneshot(get_request("/")).await.unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        assert!(response
            .headers()
            .get(header::CONTENT_TYPE)
            .unwrap()
            .to_str()
            .unwrap()
            .starts_with("text/html"));

        let page = body_text(response).await;
        assert!(page.contains("Install Blastlands"));
        assert!(page.contains("/v1/client/26.9.0/download"));
    }

    #[tokio::test]
    async fn the_root_still_answers_before_any_release_is_known() {
        let response = router_with(unpublished())
            .oneshot(get_request("/"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        assert!(!body_text(response).await.contains("/v1/client/"));
    }

    fn release_with_installer() -> Arc<Releases> {
        let releases = unpublished();
        releases.publish(PublishedClient {
            version: CURRENT.parse().unwrap(),
            asset_url: "https://api.github.com/assets/1".to_owned(),
            sha256: "ab".repeat(32),
            installer: Some(crate::github::Installer {
                asset_url: "https://api.github.com/assets/2".to_owned(),
                sha256: "cd".repeat(32),
            }),
        });
        releases
    }

    #[tokio::test]
    async fn the_root_prefers_the_installer_when_one_is_published() {
        let response = router_with(release_with_installer())
            .oneshot(get_request("/"))
            .await
            .unwrap();

        let page = body_text(response).await;
        assert!(page.contains("/v1/client/26.9.0/installer"));
        assert!(!page.contains("/v1/client/26.9.0/download"));
        assert!(page.contains(&"cd".repeat(32)));
    }

    #[tokio::test]
    async fn a_release_without_an_installer_answers_not_found_for_it() {
        let response = router()
            .oneshot(get_request("/v1/client/26.9.0/installer"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::NOT_FOUND);
        assert_eq!(body_json(response).await["code"], "release_not_found");
    }

    #[tokio::test]
    async fn an_installer_before_any_release_is_read_is_unavailable() {
        let response = router_with(unpublished())
            .oneshot(get_request("/v1/client/26.9.0/installer"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::SERVICE_UNAVAILABLE);
    }

    #[tokio::test]
    async fn only_the_published_client_can_be_downloaded() {
        let response = router()
            .oneshot(get_request("/v1/client/26.8.5/download"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::NOT_FOUND);
        assert_eq!(body_json(response).await["code"], "release_not_found");
    }

    #[tokio::test]
    async fn a_download_before_any_release_is_read_is_unavailable() {
        let response = router_with(unpublished())
            .oneshot(get_request("/v1/client/26.9.0/download"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::SERVICE_UNAVAILABLE);
    }

    #[tokio::test]
    async fn a_malformed_download_version_is_refused() {
        let response = router()
            .oneshot(get_request("/v1/client/latest/download"))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::BAD_REQUEST);
    }

    #[tokio::test]
    async fn an_outdated_client_cannot_create_a_match() {
        let response = router()
            .oneshot(post_json_as(
                "/v1/matches",
                json!({ "name": "Old build", "host": "Bryan", "max_players": 4 }),
                Some("26.7.9"),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::UPGRADE_REQUIRED);
        assert_eq!(body_json(response).await["code"], "client_too_old");
    }

    #[tokio::test]
    async fn a_client_without_a_version_header_cannot_create_a_match() {
        let response = router()
            .oneshot(post_json_as(
                "/v1/matches",
                json!({ "name": "No header", "host": "Bryan", "max_players": 4 }),
                None,
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::BAD_REQUEST);
        assert_eq!(body_json(response).await["code"], "invalid_version");
    }

    #[tokio::test]
    async fn joining_hands_back_a_game_ticket_for_that_match_and_player() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().unwrap().to_owned();

        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        let ticket = body_json(response).await["ticket"]
            .as_str()
            .expect("a ticket")
            .to_owned();
        let fields: Vec<&str> = ticket.split('.').collect();
        assert_eq!(fields.len(), 6);
        assert_eq!(fields[1], id);
        assert_eq!(fields[2], "416c6578");
    }

    #[tokio::test]
    async fn the_host_gets_a_game_ticket_apart_from_the_key_that_starts_the_match() {
        let created = create_match_on(&router(), "Friday night").await;

        let game_ticket = created["game_ticket"].as_str().expect("a game ticket");
        assert!(game_ticket.starts_with(&format!("v1.{}.", created["id"].as_str().unwrap())));
        assert_ne!(created["ticket"], created["game_ticket"]);
    }

    #[tokio::test]
    async fn a_chosen_character_is_signed_into_the_join_ticket() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().unwrap().to_owned();

        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex", "character": "sapper" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        let ticket = body_json(response).await["ticket"]
            .as_str()
            .expect("a ticket")
            .to_owned();
        let fields: Vec<&str> = ticket.split('.').collect();
        assert_eq!(fields.len(), 7);
        assert_eq!(fields[0], "v2");
        assert_eq!(fields[3], "sapper");
    }

    #[tokio::test]
    async fn the_host_can_bring_a_character_too() {
        let response = router()
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": "Friday night", "host": "Bryan", "max_players": 4, "character": "runner" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::CREATED);
        let game_ticket = body_json(response).await["game_ticket"]
            .as_str()
            .expect("a game ticket")
            .to_owned();
        assert_eq!(game_ticket.split('.').nth(3), Some("runner"));
    }

    #[tokio::test]
    async fn a_character_the_lobby_does_not_know_is_refused() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().unwrap().to_owned();

        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex", "character": "hoarder" }),
            ))
            .await
            .unwrap();

        assert!(response.status().is_client_error());
    }

    #[tokio::test]
    async fn a_character_the_lobby_does_not_know_answers_with_the_usual_error_shape() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().unwrap().to_owned();

        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex", "character": "hoarder" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::UNPROCESSABLE_ENTITY);
        assert_eq!(
            response.headers().get(header::CONTENT_TYPE).unwrap(),
            "application/json"
        );
        assert_eq!(body_json(response).await["code"], "invalid_request");
    }

    #[tokio::test]
    async fn a_body_that_is_not_json_answers_with_the_usual_error_shape() {
        let request = Request::builder()
            .method("POST")
            .uri("/v1/matches")
            .header(header::CONTENT_TYPE, "application/json")
            .header(VERSION_HEADER, CURRENT)
            .body(Body::from("{not json"))
            .expect("request should build");

        let response = router().oneshot(request).await.unwrap();

        assert_eq!(response.status(), StatusCode::BAD_REQUEST);
        assert_eq!(
            response.headers().get(header::CONTENT_TYPE).unwrap(),
            "application/json"
        );
        assert_eq!(body_json(response).await["code"], "invalid_request");
    }

    #[tokio::test]
    async fn an_outdated_client_cannot_join_a_match() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().unwrap().to_owned();

        let response = router
            .clone()
            .oneshot(post_json_as(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex" }),
                Some("26.0.0"),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::UPGRADE_REQUIRED);
    }

    #[tokio::test]
    async fn a_build_newer_than_the_lobby_is_still_allowed_in() {
        let response = router()
            .oneshot(post_json_as(
                "/v1/matches",
                json!({ "name": "Dev build", "host": "Bryan", "max_players": 4 }),
                Some("99.0.0"),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::CREATED);
    }

    #[tokio::test]
    async fn health_reports_no_content() {
        let response = router()
            .oneshot(
                Request::builder()
                    .uri("/healthz")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::NO_CONTENT);
    }

    #[tokio::test]
    async fn creating_a_match_returns_its_endpoint_and_a_ticket() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;

        assert_eq!(created["endpoint"]["host"], "game.blastlands.test");
        assert_eq!(created["endpoint"]["port"], 7000);
        assert_eq!(created["players"], 1);
        assert!(created["ticket"].is_string());
    }

    #[tokio::test]
    async fn the_public_match_list_never_exposes_game_server_endpoints() {
        let router = router();
        create_match_on(&router, "Friday night").await;

        let response = router
            .clone()
            .oneshot(
                Request::builder()
                    .uri("/v1/matches")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();

        let listed = body_json(response).await;

        assert_eq!(listed.as_array().expect("a list").len(), 1);
        assert!(listed[0].get("endpoint").is_none());
        assert!(listed[0].get("ticket").is_none());
    }

    #[tokio::test]
    async fn a_rejected_name_does_not_reach_the_directory() {
        let response = router()
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": "<script>", "host": "Bryan", "max_players": 4 }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::UNPROCESSABLE_ENTITY);
    }

    #[tokio::test]
    async fn an_unsupported_player_count_is_rejected() {
        let response = router()
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": "Too big", "host": "Bryan", "max_players": 99 }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::BAD_REQUEST);
        assert_eq!(body_json(response).await["code"], "invalid_player_count");
    }

    #[tokio::test]
    async fn joining_an_unknown_match_is_a_not_found() {
        let unknown = uuid::Uuid::new_v4();

        let response = router()
            .oneshot(post_json(
                &format!("/v1/matches/{unknown}/join"),
                json!({ "player": "Alex" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::NOT_FOUND);
        assert_eq!(body_json(response).await["code"], "match_not_found");
    }

    #[tokio::test]
    async fn a_stranger_cannot_start_somebody_elses_match() {
        // Match ids are public: GET /v1/matches hands them to anyone. Before the ticket
        // was checked, this request answered 204, took the match out of the listing and
        // left everybody still trying to join with match_already_started. One
        // unauthorised request per match, walked straight off the public list.
        let router = router();
        let created = create_match_on(&router, "Alice game").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let refused = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": "0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b" }),
            ))
            .await
            .unwrap();

        assert_eq!(refused.status(), StatusCode::UNAUTHORIZED);

        // And the match is untouched: still joinable, which is the thing the attack took
        // away.
        let joined = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Bob" }),
            ))
            .await
            .unwrap();

        assert_eq!(joined.status(), StatusCode::OK);
    }

    #[tokio::test]
    async fn a_stranger_cannot_add_a_bot_to_somebody_elses_match() {
        let router = router();
        let created = create_match_on(&router, "Alice game").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let refused = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/bots"),
                json!({ "ticket": "0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b" }),
            ))
            .await
            .unwrap();

        assert_eq!(refused.status(), StatusCode::UNAUTHORIZED);
    }

    #[tokio::test]
    async fn a_bot_lets_a_solo_host_start_without_anybody_joining() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        let ticket = created["ticket"].as_str().expect("a ticket").to_owned();

        let added = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/bots"),
                json!({ "ticket": ticket }),
            ))
            .await
            .unwrap();

        assert_eq!(added.status(), StatusCode::OK);
        let body = body_json(added).await;
        assert_eq!(body["players"], 1);
        assert_eq!(body["bots"], 1);

        let started = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": ticket }),
            ))
            .await
            .unwrap();

        assert_eq!(started.status(), StatusCode::NO_CONTENT);
    }

    #[tokio::test]
    async fn a_waiting_match_reports_who_is_in_it_and_that_it_has_not_started() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        join_match_on(&router, &id, "Alex").await;

        let response = router
            .clone()
            .oneshot(get_request(&format!("/v1/matches/{id}")))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        let body = body_json(response).await;
        assert_eq!(body["state"], "waiting_for_players");
        assert_eq!(body["players"], 2);
        assert_eq!(body["max_players"], 4);
        assert!(
            body.get("ticket").is_none(),
            "the host's key must not leak to whoever polls"
        );
    }

    #[tokio::test]
    async fn a_started_match_says_so_even_though_it_left_the_listing() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        let ticket = created["ticket"].as_str().expect("a ticket").to_owned();
        join_match_on(&router, &id, "Alex").await;

        let started = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": ticket }),
            ))
            .await
            .unwrap();
        assert_eq!(started.status(), StatusCode::NO_CONTENT);

        let response = router
            .clone()
            .oneshot(get_request(&format!("/v1/matches/{id}")))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::OK);
        assert_eq!(body_json(response).await["state"], "in_progress");
    }

    #[tokio::test]
    async fn an_unknown_match_has_no_status() {
        let response = router()
            .oneshot(get_request(
                "/v1/matches/0b7f3f5a-1c2d-4e5f-8a9b-0c1d2e3f4a5b",
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::NOT_FOUND);
    }

    #[tokio::test]
    async fn starting_a_match_nobody_joined_is_a_conflict() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        let ticket = created["ticket"].as_str().expect("a ticket").to_owned();

        let refused = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": ticket }),
            ))
            .await
            .expect("request should be handled");

        assert_eq!(refused.status(), StatusCode::CONFLICT);
        assert_eq!(body_json(refused).await["code"], "not_enough_players");
    }

    #[tokio::test]
    async fn starting_without_a_ticket_at_all_is_refused() {
        let router = router();
        let created = create_match_on(&router, "Alice game").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let refused = router
            .clone()
            .oneshot(post_json(&format!("/v1/matches/{id}/start"), json!({})))
            .await
            .unwrap();

        assert_eq!(refused.status(), StatusCode::UNPROCESSABLE_ENTITY);
    }

    #[tokio::test]
    async fn joining_a_started_match_is_a_conflict() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        let ticket = created["ticket"].as_str().expect("a ticket").to_owned();
        join_match_on(&router, &id, "Sam").await;

        let started = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": ticket }),
            ))
            .await
            .unwrap();
        assert_eq!(started.status(), StatusCode::NO_CONTENT);

        let response = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/join"),
                json!({ "player": "Alex" }),
            ))
            .await
            .unwrap();

        assert_eq!(response.status(), StatusCode::CONFLICT);
    }

    #[tokio::test]
    async fn finishing_a_match_requires_the_instance_token() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let unauthorized = router
            .clone()
            .oneshot(
                Request::builder()
                    .method("DELETE")
                    .uri(format!("/internal/matches/{id}"))
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(unauthorized.status(), StatusCode::UNAUTHORIZED);

        let authorized = router
            .clone()
            .oneshot(
                Request::builder()
                    .method("DELETE")
                    .uri(format!("/internal/matches/{id}"))
                    .header(header::AUTHORIZATION, format!("Bearer {TOKEN}"))
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(authorized.status(), StatusCode::NO_CONTENT);
    }

    #[tokio::test]
    async fn an_unauthorized_finish_leaves_the_match_listed() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        router
            .clone()
            .oneshot(
                Request::builder()
                    .method("DELETE")
                    .uri(format!("/internal/matches/{id}"))
                    .header(header::AUTHORIZATION, "Bearer wrong")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();

        let response = router
            .clone()
            .oneshot(
                Request::builder()
                    .uri("/v1/matches")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();

        assert_eq!(
            body_json(response).await.as_array().expect("a list").len(),
            1
        );
    }
    #[tokio::test]
    async fn a_burst_of_creates_from_one_caller_is_refused() {
        // The flood #22 is about: every create takes a port from a finite pool, so the
        // limit has to bite before the pool does.
        let router = throttled_router(2, 0);

        for attempt in 0..2 {
            let response = router
                .clone()
                .oneshot(post_json(
                    "/v1/matches",
                    json!({ "name": format!("Match {attempt}"), "host": "Bryan", "max_players": 4 }),
                ))
                .await
                .expect("request should be handled");
            assert_eq!(response.status(), StatusCode::CREATED);
        }

        let response = router
            .clone()
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": "One too many", "host": "Bryan", "max_players": 4 }),
            ))
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::TOO_MANY_REQUESTS);
        assert_eq!(body_json(response).await["code"], "rate_limited");
    }

    #[tokio::test]
    async fn one_caller_cannot_hoard_the_port_pool() {
        // Distinct from the rate limit: this one is about how many matches an address
        // holds at once, not how fast it asks. Slow enough to pass the limiter and still
        // refused.
        let router = throttled_router(0, 2);

        for attempt in 0..2 {
            let response = router
                .clone()
                .oneshot(post_json(
                    "/v1/matches",
                    json!({ "name": format!("Match {attempt}"), "host": "Bryan", "max_players": 4 }),
                ))
                .await
                .expect("request should be handled");
            assert_eq!(response.status(), StatusCode::CREATED);
        }

        let response = router
            .oneshot(post_json(
                "/v1/matches",
                json!({ "name": "Third", "host": "Bryan", "max_players": 4 }),
            ))
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::TOO_MANY_REQUESTS);
        assert_eq!(body_json(response).await["code"], "too_many_matches");
    }

    #[tokio::test]
    async fn an_assignment_needs_the_instance_token() {
        let response = router()
            .oneshot(
                Request::builder()
                    .uri("/internal/instances/7000")
                    .body(Body::empty())
                    .expect("request should build"),
            )
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::UNAUTHORIZED);
    }

    #[tokio::test]
    async fn an_idle_port_answers_no_content() {
        let response = router()
            .oneshot(instance_get("/internal/instances/7000"))
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::NO_CONTENT);
    }

    #[tokio::test]
    async fn a_started_match_is_handed_to_the_instance_on_its_port() {
        let router = router();
        let created = create_match_on(&router, "Night raid").await;
        let id = created["id"].as_str().expect("an id").to_owned();
        let ticket = created["ticket"].as_str().expect("a ticket").to_owned();
        let port = created["endpoint"]["port"].as_u64().expect("a port");
        join_match_on(&router, &id, "Sam").await;

        // Nothing to serve until the host says go.
        let idle = router
            .clone()
            .oneshot(instance_get(&format!("/internal/instances/{port}")))
            .await
            .expect("request should be handled");
        assert_eq!(idle.status(), StatusCode::NO_CONTENT);

        let started = router
            .clone()
            .oneshot(post_json(
                &format!("/v1/matches/{id}/start"),
                json!({ "ticket": ticket }),
            ))
            .await
            .expect("request should be handled");
        assert_eq!(started.status(), StatusCode::NO_CONTENT);

        let assigned = router
            .oneshot(instance_get(&format!("/internal/instances/{port}")))
            .await
            .expect("request should be handled");

        assert_eq!(assigned.status(), StatusCode::OK);
        let body = body_json(assigned).await;
        assert_eq!(body["match_id"], id);
        assert_eq!(body["players"], 4, "every seat the host opened is built");
        assert_eq!(
            body["humans"], 2,
            "only the players who joined are waited for"
        );
    }

    #[tokio::test]
    async fn a_heartbeat_needs_the_instance_token() {
        let router = router();
        let created = create_match_on(&router, "Night raid").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let response = router
            .clone()
            .oneshot(
                Request::builder()
                    .method("POST")
                    .uri(format!("/internal/matches/{id}/heartbeat"))
                    .body(Body::empty())
                    .expect("request should build"),
            )
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::UNAUTHORIZED);
    }

    #[tokio::test]
    async fn a_heartbeat_with_the_token_is_accepted() {
        let router = router();
        let created = create_match_on(&router, "Night raid").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let response = router
            .oneshot(
                Request::builder()
                    .method("POST")
                    .uri(format!("/internal/matches/{id}/heartbeat"))
                    .header(header::AUTHORIZATION, format!("Bearer {TOKEN}"))
                    .body(Body::empty())
                    .expect("request should build"),
            )
            .await
            .expect("request should be handled");

        assert_eq!(response.status(), StatusCode::NO_CONTENT);
    }
    #[tokio::test]
    async fn a_spoofed_forwarded_entry_cannot_mint_a_fresh_address() {
        // Most proxies append rather than overwrite, so a caller who sends their own
        // X-Forwarded-For arrives as "<what they typed>, <what the proxy saw>". Reading
        // the front of that list means reading the attacker, and it would let one machine
        // present a different address on every request and walk through the limit
        // untouched. The entry our own proxy appended is the last one.
        let router = forwarded_router(1);

        let allowed = router
            .clone()
            .oneshot(forwarded_post("1.2.3.4, 203.0.113.9", "First"))
            .await
            .expect("request should be handled");
        assert_eq!(allowed.status(), StatusCode::CREATED);

        // Same real caller, a different lie in front of it.
        let refused = router
            .oneshot(forwarded_post("5.6.7.8, 203.0.113.9", "Second"))
            .await
            .expect("request should be handled");

        assert_eq!(
            refused.status(),
            StatusCode::TOO_MANY_REQUESTS,
            "the spoofed leading entry was believed, so the limit was bypassed"
        );
        assert_eq!(body_json(refused).await["code"], "rate_limited");
    }

    #[tokio::test]
    async fn two_real_callers_behind_the_proxy_are_still_told_apart() {
        // The other half: reading the last entry must not collapse everyone into one
        // bucket either, or a busy proxy would throttle its own users as a group.
        let router = forwarded_router(1);

        let first = router
            .clone()
            .oneshot(forwarded_post("203.0.113.9", "First"))
            .await
            .expect("request should be handled");
        assert_eq!(first.status(), StatusCode::CREATED);

        let second = router
            .oneshot(forwarded_post("203.0.113.10", "Second"))
            .await
            .expect("request should be handled");

        assert_eq!(
            second.status(),
            StatusCode::CREATED,
            "a different caller behind the same proxy was punished"
        );
    }
}
