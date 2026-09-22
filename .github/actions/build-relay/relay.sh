#!/usr/bin/env bash
set -euo pipefail

url="http://${RELAY_ENDPOINT}/${RELAY_BUCKET}/relay/${GITHUB_REPOSITORY}/${GITHUB_RUN_ID}/${RELAY_NAME}.tar.gz"
archive="${RUNNER_TEMP}/relay-${RELAY_NAME}.tar.gz"

s3() {
  printf 'user = "%s:%s"\n' "${S3_KEY}" "${S3_SECRET}" \
    | curl -sS -K - \
        --aws-sigv4 "aws:amz:${RELAY_REGION}:s3" \
        -H 'x-amz-content-sha256: UNSIGNED-PAYLOAD' \
        --connect-timeout 5 \
        --speed-limit 1048576 --speed-time 60 \
        -w '%{http_code}' \
        "$@" || true
}

put() {
  local code
  if [ ! -d "${RELAY_PATH}" ]; then
    echo "::error::${RELAY_PATH} does not exist, nothing to relay."
    exit 1
  fi
  tar -cf - -C "${RELAY_PATH}" . | gzip -1 > "${archive}"
  code="$(s3 -o /dev/null -T "${archive}" "${url}")"
  if [ "${code}" != 200 ]; then
    echo "::error::${RELAY_NAME} could not be stored in the relay (HTTP ${code})."
    exit 1
  fi
  echo "Relayed ${RELAY_NAME} ($(du -h "${archive}" | cut -f1))."
  rm -f "${archive}"
}

get() {
  local code
  code="$(s3 -o "${archive}" "${url}")"
  if [ "${code}" != 200 ]; then
    echo "::error::${RELAY_NAME} could not be read from the relay (HTTP ${code}). The job that builds it has to succeed in this same run."
    exit 1
  fi
  mkdir -p "${RELAY_PATH}"
  tar -xzf "${archive}" -C "${RELAY_PATH}"
  rm -f "${archive}"
  echo "Received ${RELAY_NAME}."
}

case "${1:-}" in
  put) put ;;
  get) get ;;
  *) echo "::error::usage: relay.sh put|get" >&2; exit 2 ;;
esac
