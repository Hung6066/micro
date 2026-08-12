# The sandbox is developer tooling, not the production build toolchain. Keep it
# on the current patched SDK line so the image does not ship known SDK CVEs;
# production service builds remain pinned by global.json and CI to .NET 8.0.319.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:5657c5f725f2e8923f31b2eb9d743662f2e0be50a2bee41de685fc9f12ae68ef AS sandbox

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
