#!/bin/sh
set -eu

mkdir -p /run/spire/jwt
until [ -S /run/spire/sockets/agent.sock ]; do sleep 2; done
while true; do
  token_file=/run/spire/jwt/vault.jwt
  tmp_file=/run/spire/jwt/.vault.jwt.tmp
  rm -f "$tmp_file"
  /opt/spire/bin/spire-agent api fetch jwt \
    -socketPath /run/spire/sockets/agent.sock \
    -audience vault \
    -spiffeID "${SPIFFE_ID:?SPIFFE_ID is required}" \
    | sed -n 's/^[[:space:]]*\(eyJ[^[:space:]]*\).*$/\1/p' \
    | head -n 1 \
    > "$tmp_file"
  test -s "$tmp_file"
  # The pod fsGroup supplies the workload group; avoid chown because rootless
  # containerd/user namespaces can reject ownership changes on emptyDir.
  chmod 0440 "$tmp_file"
  mv -f "$tmp_file" "$token_file"
  sleep 300
done
