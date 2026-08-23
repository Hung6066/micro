FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:35048e3a81e6a07c316e7bbbd80d80d2ba705fe5f23a8ed42b6638c8f4c20d30 AS sandbox

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    nodejs \
    gcc \
    clang \
    git \
    curl \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash sandbox

WORKDIR /workspace

USER sandbox

# Run with: docker run --rm --timeout 30 --read-only --network none sandbox
