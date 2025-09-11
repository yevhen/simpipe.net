#!/bin/bash
set -e

echo "📦 Publishing Simpipe.Net to Local Feed"

mkdir -p local-feed
VERSION="1.0.0-dev$(date +%Y%m%d%H%M%S)"

echo "🏗️ Building packages with version: $VERSION"

dotnet pack src/Simpipe.Net/Simpipe.Net.csproj \
    --configuration Release \
    --output local-feed \
    -p:PackageVersion="$VERSION" \
    --verbosity quiet

echo "✅ Published packages to local feed"
echo "💡 Now run: dotnet restore && dotnet test"