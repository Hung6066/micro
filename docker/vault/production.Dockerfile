FROM hashicorp/vault:1.17
USER root
RUN apk add --no-cache gettext
USER vault
