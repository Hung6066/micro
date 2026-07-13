#!/bin/bash
set -euo pipefail

COCKROACH_HOST=${COCKROACH_HOST:-cockroachdb-public:26257}
COCKROACH_USER=${COCKROACH_USER:-root}
MIGRATIONS_DIR=${MIGRATIONS_DIR:-/migrations}

echo "=== CockroachDB Migration Runner ==="
echo "Host: ${COCKROACH_HOST}"
echo "User: ${COCKROACH_USER}"
echo "================================"

total=0
failed=0

for migration in $(ls "${MIGRATIONS_DIR}"/*.sql 2>/dev/null | sort); do
    filename=$(basename "${migration}")
    echo ""
    echo ">>> Applying: ${filename} ..."

    if cockroach sql \
      --host="${COCKROACH_HOST}" \
      --user="${COCKROACH_USER}" \
      --insecure \
      -f "${migration}"; then
        echo "<<< Done: ${filename}"
        total=$((total + 1))
    else
        echo "<<< FAILED: ${filename}"
        failed=$((failed + 1))
    fi
done

echo ""
echo "================================"
echo "Migrations complete: ${total} applied, ${failed} failed"
echo "================================"

if [ "${failed}" -ne 0 ]; then
    exit 1
fi
