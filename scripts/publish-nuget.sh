#!/bin/bash
set -e

echo "🚀 Publishing Simpipe.Net to NuGet.org"

if [ -z "$NUGET_API_KEY" ]; then
    echo "❌ Error: NUGET_API_KEY environment variable is not set"
    exit 1
fi

VERSION=${1:-1.0.0}
rm -rf ./packages && mkdir -p ./packages

echo "📥 Restoring dependencies..."
dotnet restore

echo "🔨 Building solution..."
dotnet build --configuration Release --no-restore

echo "🧪 Running tests..."
dotnet test --configuration Release --no-build

echo "📦 Creating NuGet packages..."
dotnet pack src/Simpipe.Net/Simpipe.Net.csproj \
    --configuration Release \
    --no-build \
    --output ./packages \
    -p:PackageVersion="$VERSION"

echo "📋 Packages to publish:"
ls -la ./packages/*.nupkg

echo "⚠️  About to publish to NuGet.org!"
read -p "Are you sure you want to publish version $VERSION? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Publishing cancelled"
    exit 1
fi

echo "🚀 Publishing to NuGet.org..."
dotnet nuget push ./packages/*.nupkg \
    --api-key "$NUGET_API_KEY" \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate

echo "✅ Successfully published Simpipe.Net $VERSION to NuGet.org!"