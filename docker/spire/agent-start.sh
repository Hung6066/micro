#!/bin/sh
set -eu
until [ -s /run/spire/bootstrap/join-token ]; do sleep 2; done
TOKEN=$(sed -n 's/^Token:[[:space:]]*//p' /run/spire/bootstrap/join-token | tr -d '[:space:]')
test -n "$TOKEN"
exec /opt/spire/bin/spire-agent run -config /run/spire/config/agent.conf -joinToken "$TOKEN"
