#!/bin/sh
set -eu

: "${BLASTLANDS_LOBBY:?is required}"
: "${BLASTLANDS_INSTANCE_TOKEN:?is required}"
: "${BLASTLANDS_TICKET_SECRET:?is required}"

# The server asks the lobby for its matches itself, one slot per port, and keeps
# running between them. What is left here is the part a process cannot do for itself:
# hearing SIGTERM without dying of it. PID 1 gets the signal, the server does not, and
# the file tells it to stop taking matches and to exit once the ones it has are over.
drain_file="${BLASTLANDS_DRAIN_FILE:-/tmp/blastlands-draining}"
export BLASTLANDS_DRAIN_FILE="${drain_file}"
rm -f "${drain_file}"

trap 'touch "${drain_file}"; echo "blastlands: draining, no new match will be taken" >&2' TERM INT

/app/Blastlands.x86_64 -batchmode -nographics -logfile - &
server_pid="$!"

# wait returns early when a trapped signal arrives, so it is called again until the
# server has really gone. Its exit code is the container's: non-zero is a crash, and a
# crash is what the orchestrator's restart is for.
code=0
while :; do
  wait "${server_pid}" && code=0 || code="$?"
  kill -0 "${server_pid}" 2>/dev/null || break
done

exit "${code}"
