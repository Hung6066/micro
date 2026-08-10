FROM ghcr.io/spiffe/spire-server:1.12.4 AS spire
FROM alpine:3.20
COPY --from=spire /opt/spire /opt/spire
COPY server-start.sh /run/spire/server-start.sh
ENTRYPOINT ["/bin/sh", "/run/spire/server-start.sh"]
