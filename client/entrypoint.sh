#!/bin/sh
set -eu

: "${BLASTLANDS_LOBBY:?is required}"
: "${BLASTLANDS_INSTANCE_TOKEN:?is required}"
: "${BLASTLANDS_TICKET_SECRET:?is required}"

if [ -z "${BLASTLANDS_PORT:-}" ]; then
  : "${BLASTLANDS_PORT_BASE:?is required when BLASTLANDS_PORT is not set}"
  : "${BLASTLANDS_POD_NAME:?is required when BLASTLANDS_PORT is not set}"

  ordinal="${BLASTLANDS_POD_NAME##*-}"
  case "${ordinal}" in
    ''|*[!0-9]*)
      echo "blastlands: ${BLASTLANDS_POD_NAME} does not end in an ordinal" >&2
      exit 1
      ;;
  esac

  case "${BLASTLANDS_PORT_BASE}" in
    ''|*[!0-9]*)
      echo "blastlands: BLASTLANDS_PORT_BASE=${BLASTLANDS_PORT_BASE} is not a number" >&2
      exit 1
      ;;
  esac

  BLASTLANDS_PORT="$((BLASTLANDS_PORT_BASE + ordinal))"
  export BLASTLANDS_PORT
fi

poll_seconds="${BLASTLANDS_POLL_SECONDS:-2}"
assignment_url="${BLASTLANDS_LOBBY}/internal/instances/${BLASTLANDS_PORT}"

echo "blastlands: waiting for a match on port ${BLASTLANDS_PORT}" >&2

body="$(mktemp)"
server_pid=""
finished_match=""
draining=""
trap 'rm -f "${body}"' EXIT
trap 'draining=1; echo "blastlands: draining, no new match will be taken" >&2' TERM INT

while true; do
  status="$(
    curl --silent --show-error --max-time 10 \
      --header "Authorization: Bearer ${BLASTLANDS_INSTANCE_TOKEN}" \
      --output "${body}" --write-out '%{http_code}' \
      "${assignment_url}" || true
  )"

  case "${status}" in
    200)
      match="$(jq -er '.match_id' <"${body}")"
      players="$(jq -er '.players' <"${body}")"
      humans="$(jq -er '.humans // .players' <"${body}")"
      # A lobby from before modes names none, and everything it ran was Arena.
      mode="$(jq -er '.mode // "arena"' <"${body}")"

      if [ -n "${draining}" ]; then
        echo "blastlands: draining, leaving ${match} for another instance" >&2
        exit 0
      fi

      if [ "${match}" = "${finished_match}" ]; then
        echo "blastlands: ${match} is still assigned after it ended, not replaying it" >&2
        sleep "${poll_seconds}" || true
        continue
      fi

      echo "blastlands: taking ${mode} match ${match}, ${players} seats for ${humans} players" >&2
      /app/Blastlands.x86_64 -batchmode -nographics -logfile - \
        --match "${match}" --players "${players}" --humans "${humans}" --mode "${mode}" &
      server_pid="$!"
      code=0
      while :; do
        wait "${server_pid}" && code=0 || code="$?"
        kill -0 "${server_pid}" 2>/dev/null || break
      done
      server_pid=""

      if [ "${code}" -ne 0 ]; then
        echo "blastlands: match ${match} ended with ${code}" >&2
        exit "${code}"
      fi

      finished_match="${match}"
      echo "blastlands: match ${match} is over" >&2
      ;;
    204) ;;
    *)
      echo "blastlands: the lobby answered ${status} for ${assignment_url}" >&2
      ;;
  esac

  if [ -n "${draining}" ]; then
    echo "blastlands: drained, shutting down" >&2
    exit 0
  fi

  sleep "${poll_seconds}" || true
done
