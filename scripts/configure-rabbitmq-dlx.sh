#!/usr/bin/env sh
set -eu

# Apply a broker policy so existing durable queues can gain DLX routing without
# deleting queues or messages. New queues also declare the same contract in code.
rabbitmqctl set_policy \
  manufacturing-dlx \
  '^manufacturing\.(commerce-orders|analytics)\.v1$' \
  '{"dead-letter-exchange":"his-hope.dlx"}' \
  --apply-to queues \
  --priority 50
