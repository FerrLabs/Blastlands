use std::sync::Arc;

use axum::extract::{Path, State};
use axum::http::{HeaderMap, StatusCode};
use axum::routing::{delete, get, post};
use axum::{Json, Router};
use serde::{Deserialize, Serialize};
use tower_http::trace::TraceLayer;

use crate::auth::require_instance_token;
use crate::error::LobbyError;
use crate::matches::{CreateMatch, GameServerEndpoint, JoinTicket, Match, MatchDirectory, MatchId};
use crate::names::DisplayName;

#[derive(Clone)]
pub struct AppState {
    pub directory: Arc<MatchDirectory>,
    pub instance_token: Arc<str>,
}

#[derive(Debug, Deserialize)]
pub struct CreateMatchRequest {
    pub name: DisplayName,
    pub host: DisplayName,
    pub max_players: u8,
}

#[derive(Debug, Deserialize)]
pub struct JoinRequest {
    pub player: DisplayName,
}

#[derive(Debug, Serialize)]
pub struct MatchSummary {
    pub id: MatchId,
    pub name: DisplayName,
    pub host: DisplayName,
    pub players: u8,
    pub max_players: u8,
}

impl From<&Match> for MatchSummary {
    fn from(entry: &Match) -> Self {
        Self {
            id: entry.id,
            name: entry.name.clone(),
            host: entry.host.clone(),
            players: entry.players.len().min(usize::from(u8::MAX)) as u8,
            max_players: entry.max_players,
        }
    }
}

#[derive(Debug, Serialize)]
pub struct MatchCreated {
    #[serde(flatten)]
    pub summary: MatchSummary,
    pub endpoint: GameServerEndpoint,
    pub ticket: JoinTicket,
}

#[derive(Debug, Serialize)]
pub struct JoinAccepted {
    pub endpoint: GameServerEndpoint,
    pub ticket: JoinTicket,
}

pub fn app(state: AppState) -> Router {
    Router::new()
        .route("/healthz", get(health))
        .route("/v1/matches", get(list_matches).post(create_match))
        .route("/v1/matches/{id}/join", post(join_match))
        .route("/v1/matches/{id}/start", post(start_match))
        .route("/internal/matches/{id}", delete(finish_match))
        .layer(TraceLayer::new_for_http())
        .with_state(state)
}

async fn health() -> StatusCode {
    StatusCode::NO_CONTENT
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
    Json(request): Json<CreateMatchRequest>,
) -> Result<(StatusCode, Json<MatchCreated>), LobbyError> {
    let (entry, ticket) = state.directory.create(CreateMatch {
        name: request.name,
        host: request.host,
        max_players: request.max_players,
    })?;

    let created = MatchCreated {
        summary: MatchSummary::from(&entry),
        endpoint: entry.endpoint,
        ticket,
    };

    Ok((StatusCode::CREATED, Json(created)))
}

async fn join_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
    Json(request): Json<JoinRequest>,
) -> Result<Json<JoinAccepted>, LobbyError> {
    let admitted = state.directory.join(id, request.player)?;

    Ok(Json(JoinAccepted {
        endpoint: admitted.endpoint,
        ticket: admitted.ticket,
    }))
}

async fn start_match(
    State(state): State<AppState>,
    Path(id): Path<MatchId>,
) -> Result<StatusCode, LobbyError> {
    state.directory.start(id)?;
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
    use crate::ports::PortPool;

    const TOKEN: &str = "instance-token";

    fn router() -> Router {
        app(AppState {
            directory: Arc::new(MatchDirectory::new(
                "game.blastlands.test".to_owned(),
                PortPool::new(7000..=7001),
            )),
            instance_token: Arc::from(TOKEN),
        })
    }

    fn post_json(uri: &str, body: Value) -> Request<Body> {
        Request::builder()
            .method("POST")
            .uri(uri)
            .header(header::CONTENT_TYPE, "application/json")
            .body(Body::from(body.to_string()))
            .expect("request should build")
    }

    async fn body_json(response: Response) -> Value {
        let bytes = to_bytes(response.into_body(), usize::MAX)
            .await
            .expect("body should be readable");
        serde_json::from_slice(&bytes).expect("body should be json")
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
    async fn joining_a_started_match_is_a_conflict() {
        let router = router();
        let created = create_match_on(&router, "Friday night").await;
        let id = created["id"].as_str().expect("an id").to_owned();

        let started = router
            .clone()
            .oneshot(post_json(&format!("/v1/matches/{id}/start"), json!({})))
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
}
