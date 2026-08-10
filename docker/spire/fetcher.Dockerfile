FROM ghcr.io/spiffe/spire-agent:1.15.2 AS spire
FROM alpine:3.20
COPY --from=spire /opt/spire /opt/spire
RUN apk add --no-cache sed
COPY fetch-jwt.sh /run/spire/fetch-jwt.sh
ENTRYPOINT ["/bin/sh", "/run/spire/fetch-jwt.sh"]
