#!/usr/bin/env python3
import json
import sys

payload = json.load(sys.stdin)
metadata = payload.setdefault("metadata", {})
for key in ("namespace", "resourceVersion", "uid", "creationTimestamp", "managedFields", "ownerReferences"):
    metadata.pop(key, None)
metadata.pop("annotations", {}).pop("kubectl.kubernetes.io/last-applied-configuration", None)
metadata["namespace"] = sys.argv[1]
metadata.setdefault("labels", {})["app.kubernetes.io/managed-by"] = "ansible-data-plane-secrets"
json.dump(payload, sys.stdout)
