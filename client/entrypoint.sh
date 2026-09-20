#!/bin/sh
set -eu

: "${BLASTLANDS_LOBBY:?is required}"
: "${BLASTLANDS_INSTANCE_TOKEN:?is required}"

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
trap 'rm -f "${body}"' EXIT
trap 'if [ -n "${server_pid}" ]; then kill -TERM "${server_pid}" 2>/dev/null || true; wait "${server_pid}" 2>/dev/null || true; fi; exit 143' TERM
trap 'if [ -n "${server_pid}" ]; then kill -INT "${server_pid}" 2>/dev/null || true; wait "${server_pid}" 2>/dev/null || true; fi; exit 130' INT

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

      if [ "${match}" = "${finished_match}" ]; then
        echo "blastlands: ${match} is still assigned after it ended, not replaying it" >&2
        sleep "${poll_seconds}"
        continue
      fi

      echo "blastlands: taking match ${match} for ${players} players" >&2
      /app/Blastlands.x86_64 -batchmode -nographics -logfile - \
        --match "${match}" --players "${players}" &
      server_pid="$!"
      code=0
      wait "${server_pid}" || code="$?"
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

  sleep "${poll_seconds}"
done
