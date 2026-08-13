FROM docker.io/library/rust:1.88-bookworm AS builder

WORKDIR /src

COPY Cargo.toml Cargo.lock ./
COPY crates ./crates

RUN cargo build --release --locked --bin blastlands-lobby

FROM gcr.io/distroless/cc-debian12:nonroot

COPY --from=builder /src/target/release/blastlands-lobby /usr/local/bin/blastlands-lobby

EXPOSE 8080
USER nonroot
ENTRYPOINT ["/usr/local/bin/blastlands-lobby"]
