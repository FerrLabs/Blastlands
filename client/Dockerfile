# The game server image. The build context is the "Linux dedicated server" artifact from
# build.yml, not this directory: the Unity build happens in CI where the licence is, and
# this only wraps what it produced.
#
# Named for the project it comes out of. server/Dockerfile is the lobby, which is the Rust
# service; this is the Unity dedicated server, which is built from client/ because the
# client and the server are the same project by design.
FROM ubuntu:26.04

# ca-certificates because the instance calls the lobby over HTTPS in production, and
# without them every heartbeat and the final release fail on the certificate rather than
# on anything to do with the match.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl jq \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --system --create-home --uid 10001 blastlands

WORKDIR /app
COPY StandaloneLinux64/ /app/
COPY entrypoint.sh /app/entrypoint.sh

# Burst ships its debug symbols in a directory that says in its own name not to ship them.
RUN rm -rf /app/*_BurstDebugInformation_DoNotShip \
    && chmod +x /app/Blastlands.x86_64 /app/entrypoint.sh

USER blastlands

# No port is exposed. The port this instance owns comes from BLASTLANDS_PORT, which is
# what the lobby allocated, so a number baked in here would be a second answer that is
# wrong most of the time.
#
# The entrypoint is the supervisor rather than the player: a pod on a fixed port starts
# before there is a match to run, asks the lobby which one it was given, and only then
# runs the binary, which cannot start without a match id. The player runs as a child, so
# a finished match returns to the poll loop and the pod keeps the port it owns, while a
# non-zero exit ends the container and leaves the restart to the orchestrator.
ENTRYPOINT ["/app/entrypoint.sh"]
