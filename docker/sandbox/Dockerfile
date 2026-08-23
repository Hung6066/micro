FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:306301580fcaa5b445180e759db59309979002d1000669cb4cf58a567d0014bc AS sandbox

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
