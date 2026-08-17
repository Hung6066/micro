FROM ghcr.io/spiffe/spire-server:1.12.4 AS spire
FROM alpine:3.20
COPY --from=spire /opt/spire /opt/spire
COPY bootstrap.sh /run/spire/bootstrap.sh
ENTRYPOINT ["/bin/sh", "/run/spire/bootstrap.sh"]
