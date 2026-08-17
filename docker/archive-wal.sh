#!/bin/sh
set -eu

source_path="$1"
wal_name="$2"
archive_dir="/var/lib/postgresql/wal-archive"
destination="$archive_dir/$wal_name"
temporary="$destination.tmp.$$"

mkdir -p "$archive_dir"
if [ -f "$destination" ]; then
  exit 0
fi

cp "$source_path" "$temporary"
chmod 0644 "$temporary"
sync
mv "$temporary" "$destination"
