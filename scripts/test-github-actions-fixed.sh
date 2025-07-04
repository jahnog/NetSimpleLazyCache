#!/bin/bash

echo "🚀 Testing Fixed GitHub Actions Coverage Workflow"
echo "================================================="

# Clean up any existing coverage files
echo "🧹 Cleaning up existing coverage files..."
rm -rf coverage/
rm -rf test/*/coverage*

# Step 1: Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Step 2: Build
echo "🔨 Building..."
dotnet build --configuration Release --no-restore

# Step 3: Test with Coverage (using updated command)
echo "🧪 Running tests with coverage (updated command)..."
mkdir -p ./coverage

dotnet test --configuration Release --no-build \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage.cobertura.xml \
  /p:Include="[SimpleLazyCache]*" \
  /p:Exclude="[*.Tests]*"

# Step 4: Debug Coverage Files
echo "🔍 Looking for coverage files..."
find . -name "*.cobertura.xml" -type f || echo "No cobertura.xml files found"
echo "=== Test directory structure ==="
ls -la test/*/
echo "=== Coverage directory ==="
ls -la coverage/ || echo "Coverage directory not found"

# Step 5: Locate and Copy Coverage File (updated logic)
echo "📊 Locating and copying coverage file..."
COVERAGE_FILE=$(find ./test -name "coverage.cobertura.xml" | head -1)

if [ -n "$COVERAGE_FILE" ]; then
  echo "📊 Found coverage file: $COVERAGE_FILE"
  cp "$COVERAGE_FILE" ./coverage/coverage.cobertura.xml
  echo "✅ Copied to ./coverage/coverage.cobertura.xml"
  
  # Verify the file exists and has content
  if [ -f "./coverage/coverage.cobertura.xml" ] && [ -s "./coverage/coverage.cobertura.xml" ]; then
    echo "✅ Coverage file verified at ./coverage/coverage.cobertura.xml"
    echo "📏 File size: $(wc -c < ./coverage/coverage.cobertura.xml) bytes"
  else
    echo "❌ Coverage file is missing or empty!"
    exit 1
  fi
else
  echo "❌ No coverage file found in test directories!"
  echo "Available files:"
  find . -name "*.cobertura.xml" -type f || echo "No cobertura.xml files found anywhere"
  exit 1
fi

# Step 6: Install ReportGenerator
echo "🔧 Installing ReportGenerator..."
dotnet tool install --global dotnet-reportgenerator-globaltool

# Step 7: Generate Coverage Report (with verification)
echo "📈 Generating coverage reports..."

# Verify coverage file exists before generating report
if [ ! -f "./coverage/coverage.cobertura.xml" ]; then
  echo "❌ Coverage file not found at ./coverage/coverage.cobertura.xml"
  exit 1
fi

mkdir -p ./coverage/report

reportgenerator \
  -reports:"./coverage/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"Html;Badges;TextSummary;MarkdownSummary;MarkdownSummaryGithub" \
  -title:"NetSimpleLazyCache Coverage Report"

echo "✅ Coverage reports generated successfully"

# List generated files for debugging
echo "Generated report files:"
ls -la ./coverage/report/

# Step 8: Update Coverage Badges
echo "🏆 Updating coverage badges..."
if [ -f "./coverage/report/badge_linecoverage.svg" ]; then
  cp "./coverage/report/badge_linecoverage.svg" "./badge_linecoverage.svg"
  cp "./coverage/report/badge_branchcoverage.svg" "./badge_branchcoverage.svg" 
  cp "./coverage/report/badge_methodcoverage.svg" "./badge_methodcoverage.svg"
  echo "✅ Coverage badges updated"
else
  echo "⚠️ Coverage badges not found"
fi

# Step 9: Generate Coverage Summary
echo "📊 Generating coverage summary..."

echo ""
echo "## Coverage Summary"
echo "| Metric | Coverage |"
echo "|--------|----------|"

# Find and use the generated text summary
SUMMARY_FILE="./coverage/report/Summary.txt"
if [ -f "$SUMMARY_FILE" ]; then
  LINE_COV=$(grep "Line coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  BRANCH_COV=$(grep "Branch coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  METHOD_COV=$(grep "Method coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  echo "| Line Coverage | $LINE_COV |"
  echo "| Branch Coverage | $BRANCH_COV |"
  echo "| Method Coverage | $METHOD_COV |"
  
  echo ""
  echo "### Detailed Coverage Information"
  echo '```'
  cat "$SUMMARY_FILE"
  echo '```'
else
  echo "| Line Coverage | Unable to determine |"
  echo "| Branch Coverage | Unable to determine |"
  echo "| Method Coverage | Unable to determine |"
  echo ""
  echo "⚠️ Summary file not found at: $SUMMARY_FILE"
fi

# Step 10: Check Coverage Threshold
echo "🎯 Checking coverage thresholds..."
dotnet test --configuration Release --no-build \
  /p:CollectCoverage=true \
  /p:Threshold=95 \
  /p:ThresholdType=line \
  /p:ThresholdStat=total \
  /p:Include="[SimpleLazyCache]*" \
  /p:Exclude="[*.Tests]*" || {
    echo "❌ Coverage threshold check failed!"
    echo "Required: 95% line coverage minimum"
    exit 1
  }
echo "✅ Coverage thresholds passed!"

echo ""
echo "🎉 Fixed GitHub Actions simulation completed successfully!"
echo "📁 Reports available at: ./coverage/report/"
echo "🌐 Open HTML report: file://$(pwd)/coverage/report/index.html"
