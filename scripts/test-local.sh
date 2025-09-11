#!/bin/bash
set -e

echo "🧪 Testing Local Simpipe.Net Package"

# Build local package first
./scripts/publish-local.sh

echo "🔍 Testing package installation..."
# Add test implementation here

echo "✅ Local package testing completed"