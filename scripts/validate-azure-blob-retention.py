"""Validate Azure Blob immutable retention without printing secret material."""

from __future__ import annotations

import argparse
import pathlib
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET



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


def validate_policy(xml_body: bytes, minimum_days: int) -> None:
    try:
        root = ET.fromstring(xml_body)
    except ET.ParseError as error:
        raise ValueError("Azure Blob returned invalid immutability policy XML") from error

    values = {
        element.tag.rsplit("}", 1)[-1]: (element.text or "").strip()
        for element in root.iter()
    }
    mode = values.get("PolicyMode", "").lower()
    if mode != "locked":
        raise ValueError("Azure Blob immutability policy must be locked")
    try:
        days = int(values["ImmutabilityPeriodSinceCreationInDays"])
    except (KeyError, ValueError) as error:
        raise ValueError("Azure Blob immutability retention period is missing or invalid") from error
    if days < minimum_days:
        raise ValueError(f"Azure Blob immutability retention is {days} days; minimum is {minimum_days}")


def validate(env_file: pathlib.Path, minimum_days: int = 30) -> None:
    values = parse_env(env_file)
    required = ("AZURE_STORAGE_ACCOUNT", "AZURE_STORAGE_CONTAINER", "AZURE_STORAGE_ENDPOINT", "AZURE_STORAGE_SAS_TOKEN")
    missing = [key for key in required if not values.get(key)]
    if missing:
        raise ValueError(f"Missing Azure keys: {', '.join(missing)}")

    account = values["AZURE_STORAGE_ACCOUNT"].strip()
    container = values["AZURE_STORAGE_CONTAINER"].strip()
    endpoint = values["AZURE_STORAGE_ENDPOINT"].rstrip("/")
    sas = values["AZURE_STORAGE_SAS_TOKEN"].lstrip("?").strip()
    parsed = urllib.parse.urlparse(endpoint)
    if parsed.scheme != "https" or parsed.netloc != f"{account}.blob.core.windows.net" or parsed.path not in ("", "/"):
        raise ValueError("Azure endpoint must be https://<account>.blob.core.windows.net")
    if any(token in sas for token in ("REPLACE_ME", "<", ">")):
        raise ValueError("Azure SAS is a placeholder")

    query = urllib.parse.urlencode({"restype": "container", "comp": "immutabilitypolicy"})
    url = f"{endpoint}/{urllib.parse.quote(container, safe='')}?{query}&{sas}"
    request = urllib.request.Request(
        url,
        method="GET",
        headers={"User-Agent": "his-hope-devsecops-retention-check/1", "x-ms-version": "2020-10-02"},
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            if response.status != 200:
                raise ValueError(f"Azure Blob returned HTTP {response.status}")
            validate_policy(response.read(32 * 1024), minimum_days)
    except urllib.error.HTTPError as error:
        raise ValueError(f"Azure Blob returned HTTP {error.code}") from error
    except urllib.error.URLError as error:
        raise ValueError("Azure Blob endpoint could not be reached") from error


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--env-file", required=True, type=pathlib.Path)
    parser.add_argument("--minimum-days", type=int, default=30)
    args = parser.parse_args()
    if args.minimum_days < 1:
        parser.error("--minimum-days must be positive")
    try:
        validate(args.env_file, args.minimum_days)
    except (OSError, ValueError) as error:
        print(f"Azure Blob immutable retention: FAIL - {error}")
        return 1
    print(f"Azure Blob immutable retention: PASS (locked, >= {args.minimum_days} days)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
