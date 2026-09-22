#!/usr/bin/env bash
set -euo pipefail

base="http://${CACHE_ENDPOINT}/${CACHE_BUCKET}/${GITHUB_REPOSITORY}/${CACHE_NAME}"
work="${RUNNER_TEMP}/library-cache-${CACHE_NAME}"
parent="$(dirname "${CACHE_PATH}")"
leaf="$(basename "${CACHE_PATH}")"
rm -rf "${work}"
mkdir -p "${work}"

s3() {
  printf 'user = "%s:%s"\n' "${S3_KEY}" "${S3_SECRET}" \
    | curl -sS -K - \
        --aws-sigv4 "aws:amz:${CACHE_REGION}:s3" \
        -H 'x-amz-content-sha256: UNSIGNED-PAYLOAD' \
        --connect-timeout 5 \
        --speed-limit 1048576 --speed-time 60 \
        -w '%{http_code}' \
        "$@" || true
}

miss() {
  echo "hit=false" >> "${GITHUB_OUTPUT}"
  echo "$1"
}

restore() {
  local code stored_key stored_sha
  echo "key=${CACHE_KEY}" >> "${GITHUB_OUTPUT}"
  code="$(s3 -o "${work}/entry" "${base}.entry")"
  case "${code}" in
    200) ;;
    404) miss "No ${CACHE_NAME} in the cache yet, building cold."; return ;;
    *) miss "::warning::The ${CACHE_NAME} cache could not be read (HTTP ${code}), building cold."; return ;;
  esac
  if ! read -r stored_key stored_sha < "${work}/entry"; then
    miss "::warning::The ${CACHE_NAME} cache entry is empty or malformed, building cold."
    return
  fi

  code="$(s3 -o "${work}/archive.tar.gz" "${base}.tar.gz")"
  if [ "${code}" != 200 ] \
    || ! printf '%s  %s\n' "${stored_sha}" "${work}/archive.tar.gz" | sha256sum --check --status; then
    miss "::warning::The ${CACHE_NAME} archive is missing or does not match its entry (HTTP ${code}), building cold."
    return
  fi

  mkdir -p "${work}/extract" "${parent}"
  tar -xzf "${work}/archive.tar.gz" -C "${work}/extract"
  rm -rf "${CACHE_PATH}"
  mv "${work}/extract/${leaf}" "${CACHE_PATH}"
  rm -rf "${work}"

  if [ "${stored_key}" = "${CACHE_KEY}" ]; then
    echo "hit=true" >> "${GITHUB_OUTPUT}"
    echo "Restored ${CACHE_NAME} at key ${CACHE_KEY}."
  else
    echo "hit=false" >> "${GITHUB_OUTPUT}"
    echo "Restored ${CACHE_NAME} from key ${stored_key}, the current one is ${CACHE_KEY}."
  fi
}

save() {
  local code sha
  if [ -z "${CACHE_KEY}" ]; then
    echo "::warning::No key for ${CACHE_NAME}, the restore step did not run, nothing to save."
    return
  fi
  if [ "${CACHE_HIT:-false}" = true ]; then
    echo "${CACHE_NAME} was restored at key ${CACHE_KEY}, nothing to save."
    return
  fi
  if [ ! -d "${CACHE_PATH}" ]; then
    echo "::warning::${CACHE_PATH} does not exist, nothing to save."
    return
  fi

  tar -cf - -C "${parent}" "${leaf}" | gzip -1 > "${work}/archive.tar.gz"
  sha="$(sha256sum "${work}/archive.tar.gz" | cut -d' ' -f1)"
  printf '%s %s\n' "${CACHE_KEY}" "${sha}" > "${work}/entry"

  code="$(s3 -o /dev/null -T "${work}/archive.tar.gz" "${base}.tar.gz")"
  if [ "${code}" != 200 ]; then
    echo "::warning::The ${CACHE_NAME} archive could not be saved (HTTP ${code})."
    return
  fi
  code="$(s3 -o /dev/null -T "${work}/entry" "${base}.entry")"
  if [ "${code}" != 200 ]; then
    echo "::warning::The ${CACHE_NAME} entry could not be saved (HTTP ${code})."
    return
  fi
  echo "Saved ${CACHE_NAME} at key ${CACHE_KEY} ($(du -h "${work}/archive.tar.gz" | cut -f1))."
  rm -rf "${work}"
}

case "${1:-}" in
  restore) restore ;;
  save) save ;;
  *) echo "::error::usage: cache.sh restore|save" >&2; exit 2 ;;
esac
