FROM hashicorp/vault:1.17
USER root
RUN apk add --no-cache postgresql-client
USER vault
