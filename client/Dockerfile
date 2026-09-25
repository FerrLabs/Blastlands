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
    && apt-get install --yes --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --system --create-home --uid 10001 blastlands

WORKDIR /app
COPY StandaloneLinux64/ /app/
COPY entrypoint.sh /app/entrypoint.sh

# Burst ships its debug symbols in a directory that says in its own name not to ship them.
RUN rm -rf /app/*_BurstDebugInformation_DoNotShip \
    && chmod +x /app/Blastlands.x86_64 /app/entrypoint.sh

USER blastlands

# No port is exposed. The ports this instance serves come from BLASTLANDS_PORT, or from
# BLASTLANDS_PORT_BASE, the pod's ordinal and BLASTLANDS_MATCHES_PER_SERVER, which is how
# the lobby's range is laid out, so a number baked in here would be a second answer that
# is wrong most of the time.
#
# The entrypoint is a thin supervisor rather than the player itself, so SIGTERM reaches a
# shell that turns it into a drain instead of reaching the server and cutting short the
# matches it is playing. A non-zero exit ends the container and leaves the restart to the
# orchestrator.
ENTRYPOINT ["/app/entrypoint.sh"]
