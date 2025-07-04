#!/bin/bash

# NetSimpleLazyCache Code Coverage Report Generator
# This script generates comprehensive code coverage reports

echo "🔍 Generating Code Coverage Report for NetSimpleLazyCache..."
echo "===============================    <a href="reports/html/index.html" class="report-link">📊 HTML Coverage Report (Interactive)</a>
    <a href="reports/html/summary.html" class="report-link">📋 HTML Summary</a>
    <a href="reports/markdown/Summary.md" class="report-link">📝 Markdown Summary</a>
    <a href="reports/markdown/SummaryGithub.md" class="report-link">📝 GitHub Markdown Summary</a>
    <a href="reports/markdown/Coverage_with_Mermaid.md" class="report-link">🧩 Markdown with Mermaid Diagrams</a>
    <a href="reports/xml/Summary.xml" class="report-link">📄 XML Summary</a>
    <a href="reports/csv/Summary.csv" class="report-link">📊 CSV Data</a>
    <a href="reports/text/Summary.txt" class="report-link">📋 Text Summary</a>
    <a href="reports/badges/" class="report-link">🏆 Coverage Badges</a>==========="

# Create coverage output directory
COVERAGE_DIR="./coverage"
REPORTS_DIR="$COVERAGE_DIR/reports"
RESULTS_DIR="$COVERAGE_DIR/results"

mkdir -p "$COVERAGE_DIR"
mkdir -p "$REPORTS_DIR"
mkdir -p "$RESULTS_DIR"

echo "📁 Coverage output directory: $COVERAGE_DIR"

# Clean previous coverage data
echo "🧹 Cleaning previous coverage data..."
rm -rf "$COVERAGE_DIR"/*
mkdir -p "$REPORTS_DIR"
mkdir -p "$RESULTS_DIR"

# Run tests with coverage collection
echo "🧪 Running tests with coverage collection..."
dotnet test \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory:"$RESULTS_DIR" \
    --logger:"console;verbosity=detailed" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[SimpleLazyCache]*

# Alternative method using coverlet.msbuild for more detailed coverage
echo "🎯 Running additional coverage collection with coverlet.msbuild..."
dotnet test \
    --configuration Release \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput="$RESULTS_DIR/coverage.cobertura.xml" \
    /p:Include="[SimpleLazyCache]*" \
    /p:Exclude="[*.Tests]*" \
    /p:ExcludeByFile="**/*Test*.cs"

# Find the coverage file
COVERAGE_FILE=$(find "$RESULTS_DIR" -name "*.cobertura.xml" -o -name "coverage.cobertura.xml" | head -1)

if [ -z "$COVERAGE_FILE" ]; then
    echo "❌ No coverage file found! Coverage collection may have failed."
    exit 1
fi

echo "📊 Coverage file found: $COVERAGE_FILE"

# Install ReportGenerator tool if not already installed
echo "🔧 Installing/updating ReportGenerator tool..."
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.2.4 2>/dev/null || \
dotnet tool update --global dotnet-reportgenerator-globaltool --version 5.2.4

# Generate comprehensive HTML report
echo "📈 Generating HTML coverage report..."
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/html" \
    -reporttypes:"Html;HtmlSummary;HtmlInline;HtmlChart" \
    -title:"NetSimpleLazyCache Code Coverage Report" \
    -tag:"$(date +%Y%m%d_%H%M%S)"

# Generate Markdown reports
echo "📝 Generating Markdown coverage reports..."
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/markdown" \
    -reporttypes:"MarkdownSummary;MarkdownSummaryGithub;MarkdownDeltaSummary" \
    -title:"NetSimpleLazyCache Code Coverage Report" \
    -tag:"$(date +%Y%m%d_%H%M%S)"

# Try to generate Mermaid diagrams if supported
echo "🧩 Attempting to generate Mermaid diagrams..."
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/mermaid" \
    -reporttypes:"MermaidChart" \
    -title:"NetSimpleLazyCache Code Coverage Report" \
    -tag:"$(date +%Y%m%d_%H%M%S)" 2>/dev/null || echo "ℹ️  Mermaid charts not supported in this version of ReportGenerator"

# Generate additional report formats
echo "📋 Generating additional report formats..."

# XML Summary
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/xml" \
    -reporttypes:"XmlSummary"

# CSV format (using CsvSummary instead of Csv)
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/csv" \
    -reporttypes:"CsvSummary"

# Badges
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/badges" \
    -reporttypes:"Badges"

# Text summary
reportgenerator \
    -reports:"$COVERAGE_FILE" \
    -targetdir:"$REPORTS_DIR/text" \
    -reporttypes:"TextSummary"

# Create custom Markdown report with Mermaid diagrams
echo "🎨 Creating custom Markdown report with Mermaid diagrams..."
mkdir -p "$REPORTS_DIR/markdown"
create_mermaid_markdown_report() {
    local summary_file="$REPORTS_DIR/text/Summary.txt"
    local markdown_file="$REPORTS_DIR/markdown/Coverage_with_Mermaid.md"
    
    if [ -f "$summary_file" ]; then
        # Extract coverage percentages
        local line_coverage=$(grep -o "Line coverage: [0-9]*\.*[0-9]*%" "$summary_file" | grep -o "[0-9]*\.*[0-9]*" || echo "0")
        local branch_coverage=$(grep -o "Branch coverage: [0-9]*\.*[0-9]*%" "$summary_file" | grep -o "[0-9]*\.*[0-9]*" || echo "0")
        local method_coverage=$(grep -o "Method coverage: [0-9]*\.*[0-9]*%" "$summary_file" | grep -o "[0-9]*\.*[0-9]*" || echo "0")
        
        # Create custom markdown with Mermaid
        cat > "$markdown_file" << EOF
# NetSimpleLazyCache - Code Coverage Report

Generated on: $(date)

## Coverage Summary

\`\`\`mermaid
pie title Code Coverage Metrics
    "Covered Lines" : $line_coverage
    "Uncovered Lines" : $(echo "100 - $line_coverage" | bc -l | cut -d. -f1)
\`\`\`

## Coverage Breakdown

| Metric | Coverage | Status |
|--------|----------|---------|
| Line Coverage | ${line_coverage}% | $([ "${line_coverage%.*}" -ge 95 ] && echo "✅ Excellent" || echo "⚠️ Needs Improvement") |
| Branch Coverage | ${branch_coverage}% | $([ "${branch_coverage%.*}" -ge 90 ] && echo "✅ Excellent" || echo "⚠️ Needs Improvement") |
| Method Coverage | ${method_coverage}% | $([ "${method_coverage%.*}" -ge 95 ] && echo "✅ Excellent" || echo "⚠️ Needs Improvement") |

## Coverage Flow

\`\`\`mermaid
graph LR
    A[Source Code] --> B[Test Execution]
    B --> C{Coverage Analysis}
    C --> D[Line Coverage: ${line_coverage}%]
    C --> E[Branch Coverage: ${branch_coverage}%]
    C --> F[Method Coverage: ${method_coverage}%]
    
    D --> G[Report Generation]
    E --> G
    F --> G
    
    G --> H[HTML Reports]
    G --> I[Markdown Reports]
    G --> J[Coverage Badges]
    
    style D fill:#e1f5fe
    style E fill:#e8f5e8
    style F fill:#fff3e0
\`\`\`

## Quality Gates

\`\`\`mermaid
graph TD
    A[Code Changes] --> B{Run Tests}
    B --> C{Check Coverage}
    C -->|Line ≥ 95%| D[✅ Quality Gate Passed]
    C -->|Line < 95%| E[❌ Quality Gate Failed]
    C -->|Branch ≥ 90%| D
    C -->|Branch < 90%| E
    C -->|Method ≥ 95%| D
    C -->|Method < 95%| E
    
    D --> F[Deploy/Merge]
    E --> G[Add More Tests]
    G --> B
    
    style D fill:#c8e6c9
    style E fill:#ffcdd2
    style F fill:#e1f5fe
    style G fill:#fff3e0
\`\`\`

## Detailed Coverage Information

$(cat "$summary_file" | sed 's/^/    /')

---

*This report was generated automatically by the NetSimpleLazyCache coverage system.*
EOF
        echo "✅ Custom Mermaid Markdown report created: $markdown_file"
    else
        echo "⚠️  Could not create Mermaid report: Summary.txt not found"
    fi
}

create_mermaid_markdown_report

# Create a simple index file
cat > "$COVERAGE_DIR/index.html" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>NetSimpleLazyCache - Coverage Reports</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .header { color: #333; border-bottom: 2px solid #007acc; padding-bottom: 10px; }
        .report-link { display: block; margin: 10px 0; padding: 10px; background: #f5f5f5; text-decoration: none; color: #007acc; border-radius: 4px; }
        .report-link:hover { background: #e5e5e5; }
        .timestamp { color: #666; font-size: 0.9em; }
    </style>
</head>
<body>
    <h1 class="header">NetSimpleLazyCache - Code Coverage Reports</h1>
    <p class="timestamp">Generated on: $(date)</p>
    
    <h2>Available Reports:</h2>
    <a href="reports/html/index.html" class="report-link">📊 HTML Coverage Report (Interactive)</a>
    <a href="reports/html/summary.html" class="report-link">📋 HTML Summary</a>
    <a href="reports/markdown/Summary.md" class="report-link">📝 Markdown Summary</a>
    <a href="reports/markdown/SummaryGithub.md" class="report-link">📝 GitHub Markdown Summary</a>
    <a href="reports/xml/Summary.xml" class="report-link">📄 XML Summary</a>
    <a href="reports/csv/Summary.csv" class="report-link">� CSV Data</a>
    <a href="reports/text/Summary.txt" class="report-link">� Text Summary</a>
    <a href="reports/badges/" class="report-link">🏆 Coverage Badges</a>
    
    <h2>Quick Stats:</h2>
    <div id="stats">
        <p>Coverage data processed from: <code>$(basename "$COVERAGE_FILE")</code></p>
    </div>
</body>
</html>
EOF

# Display summary information
echo ""
echo "✅ Coverage report generation completed!"
echo "=================================================="
echo "📁 Reports location: $COVERAGE_DIR"
echo "🌐 Main report: $COVERAGE_DIR/index.html"
echo "📊 Detailed HTML: $REPORTS_DIR/html/index.html"
echo ""

# Display quick coverage summary if available
if [ -f "$REPORTS_DIR/text/Summary.txt" ]; then
    echo "📈 Quick Coverage Summary:"
    echo "=========================="
    cat "$REPORTS_DIR/text/Summary.txt" | head -20
    echo ""
fi

# Show coverage percentage from badges if available
if [ -f "$REPORTS_DIR/badges/badge_linecoverage.svg" ]; then
    COVERAGE_PERCENT=$(grep -o '[0-9]*\.[0-9]*%\|[0-9]*%' "$REPORTS_DIR/badges/badge_linecoverage.svg" | head -1)
    if [ ! -z "$COVERAGE_PERCENT" ]; then
        echo "🎯 Line Coverage: $COVERAGE_PERCENT"
    fi
fi

echo ""
echo "🚀 To view the report, open: file://$PWD/$COVERAGE_DIR/index.html"
echo "   Or run: open $COVERAGE_DIR/index.html (macOS) / xdg-open $COVERAGE_DIR/index.html (Linux)"
