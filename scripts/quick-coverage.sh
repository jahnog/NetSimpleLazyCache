#!/bin/bash

# Quick coverage command for NetSimpleLazyCache
# Usage: ./scripts/quick-coverage.sh

echo "🚀 Quick Coverage Test for NetSimpleLazyCache"
echo "============================================"

# Run tests with basic coverage
dotnet test \
    --configuration Release \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover \
    /p:CoverletOutput=./coverage/quick/ \
    /p:Include="[SimpleLazyCache]*" \
    /p:Exclude="[*.Tests]*"

echo ""
echo "✅ Quick coverage completed!"
echo "📁 Coverage file: ./coverage/quick/coverage.opencover.xml"
