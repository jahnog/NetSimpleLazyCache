#!/bin/bash

# GitHub Actions Simulation Script
# This script simulates the GitHub Actions workflow steps for local testing

echo "🚀 Simulating GitHub Actions Coverage Workflow"
echo "=============================================="

cd /workspaces/NetSimpleLazyCache

# Step 1: Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Step 2: Build
echo "🔨 Building..."
dotnet build --configuration Release --no-restore

# Step 3: Test with Coverage
echo "🧪 Running tests with coverage..."
dotnet test --configuration Release --no-build \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=../../coverage/coverage.cobertura.xml \
  /p:Include="[SimpleLazyCache]*" \
  /p:Exclude="[*.Tests]*"

# Step 4: Debug Coverage Files
echo "🔍 Looking for coverage files..."
find . -name "*.cobertura.xml" -type f || echo "No cobertura.xml files found"
ls -la coverage/ || echo "Coverage directory not found"

# Step 5: Locate and Copy Coverage File (if needed)
COVERAGE_FILE=$(find . -name "*.cobertura.xml" | head -1)
if [ -n "$COVERAGE_FILE" ]; then
  echo "📊 Found coverage file: $COVERAGE_FILE"
  mkdir -p ./coverage
  if [ "$COVERAGE_FILE" != "./coverage/coverage.cobertura.xml" ]; then
    cp "$COVERAGE_FILE" ./coverage/coverage.cobertura.xml
    echo "✅ Copied to ./coverage/coverage.cobertura.xml"
  else
    echo "✅ Coverage file already in correct location"
  fi
else
  echo "❌ No coverage file found!"
  exit 1
fi

# Step 6: Install ReportGenerator (if not already installed)
echo "🔧 Installing ReportGenerator..."
dotnet tool install --global dotnet-reportgenerator-globaltool 2>/dev/null || \
dotnet tool update --global dotnet-reportgenerator-globaltool

# Step 7: Generate Coverage Report
echo "📈 Generating coverage reports..."
mkdir -p ./coverage/report

reportgenerator \
  -reports:"./coverage/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"Html;Badges;TextSummary;MarkdownSummary;MarkdownSummaryGithub" \
  -title:"NetSimpleLazyCache Coverage Report"

echo "✅ Coverage reports generated successfully"

# List generated files
echo "Generated report files:"
ls -la ./coverage/report/ || echo "No report files found"

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

# Step 9: Coverage Summary
echo "📊 Generating coverage summary..."
SUMMARY_FILE="./coverage/report/Summary.txt"
if [ -f "$SUMMARY_FILE" ]; then
  LINE_COV=$(grep "Line coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  BRANCH_COV=$(grep "Branch coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  METHOD_COV=$(grep "Method coverage:" "$SUMMARY_FILE" | grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' || echo "N/A")
  
  echo ""
  echo "## Coverage Summary"
  echo "| Metric | Coverage |"
  echo "|--------|----------|"
  echo "| Line Coverage | $LINE_COV |"
  echo "| Branch Coverage | $BRANCH_COV |"  
  echo "| Method Coverage | $METHOD_COV |"
  echo ""
  echo "### Detailed Coverage Information"
  echo "\`\`\`"
  cat "$SUMMARY_FILE"
  echo "\`\`\`"
else
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
  /p:Exclude="[*.Tests]*" && {
    echo "✅ Coverage thresholds passed!"
  } || {
    echo "❌ Coverage threshold check failed!"
    echo "Required: 95% line coverage minimum"
    exit 1
  }

echo ""
echo "🎉 GitHub Actions simulation completed successfully!"
echo "📁 Reports available at: ./coverage/report/"
echo "🌐 Open HTML report: file://$(pwd)/coverage/report/index.html"
