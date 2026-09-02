# The game server image. The build context is the "Linux dedicated server" artifact from
# build.yml, not this directory: the Unity build happens in CI where the licence is, and
# this only wraps what it produced.
#
# Named for the project it comes out of. server/Dockerfile is the lobby, which is the Rust
# service; this is the Unity dedicated server, which is built from client/ because the
# client and the server are the same project by design.
FROM ubuntu:24.04

# ca-certificates because the instance calls the lobby over HTTPS in production, and
# without them every heartbeat and the final release fail on the certificate rather than
# on anything to do with the match.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --system --create-home --uid 10001 blastlands

WORKDIR /app
COPY StandaloneLinux64/ /app/

# Burst ships its debug symbols in a directory that says in its own name not to ship them.
RUN rm -rf /app/*_BurstDebugInformation_DoNotShip \
    && chmod +x /app/Blastlands

USER blastlands

# No port is exposed. The lobby decides which one this instance listens on and passes it in,
# so a number baked in here would be a second answer that is wrong most of the time.
#
# -logfile - puts Unity's log on stdout where a supervisor can read it. Without it the
# player writes to a file inside the container and a crashed instance takes its own
# explanation with it.
ENTRYPOINT ["/app/Blastlands", "-batchmode", "-nographics", "-logfile", "-"]
