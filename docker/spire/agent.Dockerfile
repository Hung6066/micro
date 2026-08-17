FROM ghcr.io/spiffe/spire-agent:1.12.4 AS spire
FROM alpine:3.20
COPY --from=spire /opt/spire /opt/spire
COPY agent-start.sh /run/spire/agent-start.sh
ENTRYPOINT ["/bin/sh", "/run/spire/agent-start.sh"]
