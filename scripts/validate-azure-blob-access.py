"""Validate Azure Blob container access without printing the SAS value."""

from __future__ import annotations

import argparse
import datetime
import pathlib
import sys
import urllib.error
import urllib.parse
import urllib.request


REQUIRED = (
    "AZURE_STORAGE_ACCOUNT",
    "AZURE_STORAGE_CONTAINER",
    "AZURE_STORAGE_ENDPOINT",
    "AZURE_STORAGE_SAS_TOKEN",
)


def parse_env(path: pathlib.Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            raise ValueError("Azure env file contains an invalid line")
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip("\"'")
    return values


def validate(values: dict[str, str]) -> None:
    missing = [key for key in REQUIRED if not values.get(key)]
    if missing:
        raise ValueError(f"Missing Azure keys: {', '.join(missing)}")

    account = values["AZURE_STORAGE_ACCOUNT"].strip()
    container = values["AZURE_STORAGE_CONTAINER"].strip()
    endpoint = values["AZURE_STORAGE_ENDPOINT"].rstrip("/")
    sas = values["AZURE_STORAGE_SAS_TOKEN"].lstrip("?").strip()
    if any(token in sas for token in ("REPLACE_ME", "<", ">")):
        raise ValueError("Azure SAS is a placeholder")
    parsed = urllib.parse.urlparse(endpoint)
    if parsed.scheme != "https" or parsed.netloc != f"{account}.blob.core.windows.net" or parsed.path not in ("", "/"):
        raise ValueError("Azure endpoint must be https://<account>.blob.core.windows.net")
    if not container or "/" in container:
        raise ValueError("Azure container name is invalid")

    query = urllib.parse.parse_qs(sas, keep_blank_values=True)
    permissions = query.get("sp", [""])[0]
    if query.get("sr", [""])[0] != "c" or not all(permission in permissions for permission in "racwl"):
        raise ValueError("Azure SAS must be container-scoped and include read/add/create/write/list permissions")
    expiry = query.get("se", [""])[0]
    if not expiry:
        raise ValueError("Azure SAS expiry (se) is missing")
    try:
        expiry_time = datetime.datetime.fromisoformat(expiry.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError("Azure SAS expiry (se) is invalid") from error
    if expiry_time <= datetime.datetime.now(datetime.timezone.utc):
        raise ValueError("Azure SAS is expired")

    list_url = f"{endpoint}/{urllib.parse.quote(container, safe='')}?restype=container&comp=list&{sas}"
    request = urllib.request.Request(list_url, method="GET", headers={"User-Agent": "his-hope-devsecops-blob-check/1"})
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            if response.status != 200:
                raise ValueError(f"Azure Blob returned HTTP {response.status}")
            body = response.read(4096)
            if b"EnumerationResults" not in body and b"Error" in body:
                raise ValueError("Azure Blob returned an error document")
    except urllib.error.HTTPError as error:
        raise ValueError(f"Azure Blob returned HTTP {error.code}") from error
    except urllib.error.URLError as error:
        raise ValueError("Azure Blob endpoint could not be reached") from error


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--env-file", required=True, type=pathlib.Path)
    args = parser.parse_args()
    try:
        validate(parse_env(args.env_file))
    except (OSError, ValueError) as error:
        print(f"Azure Blob access: FAIL - {error}", file=sys.stderr)
        return 1
    print("Azure Blob access: PASS (SAS value redacted)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
