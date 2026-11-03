FROM openfga/openfga:v1.22.1@sha256:b852071314f1fda1194d341a6ebac0a4f619b3cd87a6626d2ccc7048af4ec671 AS upstream
FROM alpine:3.22
RUN apk add --no-cache ca-certificates postgresql-client
COPY --from=upstream /openfga /usr/local/bin/openfga
COPY --from=upstream /usr/local/bin/grpc_health_probe /usr/local/bin/grpc_health_probe
COPY docker/openfga/entrypoint.sh /usr/local/bin/openfga-entrypoint
RUN sed -i 's/\r$//' /usr/local/bin/openfga-entrypoint && chmod 0555 /usr/local/bin/openfga-entrypoint
USER 10001:10001
EXPOSE 8080 8081
ENTRYPOINT ["/usr/local/bin/openfga-entrypoint"]
