FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:5dfdb3b19e6900a3dc449760547a0a3fdd1f47900062d274d57e7ab725484c16 AS sandbox

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
