# NetSimpleLazyCache Code Coverage Report Generator (PowerShell)
# This script generates comprehensive code coverage reports

Write-Host "🔍 Generating Code Coverage Report for NetSimpleLazyCache..." -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green

# Create coverage output directory
$CoverageDir = "./coverage"
$ReportsDir = "$CoverageDir/reports"
$ResultsDir = "$CoverageDir/results"

New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null
New-Item -ItemType Directory -Force -Path $ReportsDir | Out-Null
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

Write-Host "📁 Coverage output directory: $CoverageDir" -ForegroundColor Yellow

# Clean previous coverage data
Write-Host "🧹 Cleaning previous coverage data..." -ForegroundColor Yellow
Remove-Item -Path "$CoverageDir/*" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $ReportsDir | Out-Null
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

# Run tests with coverage collection
Write-Host "🧪 Running tests with coverage collection..." -ForegroundColor Yellow
dotnet test `
    --configuration Release `
    --collect:"XPlat Code Coverage" `
    --results-directory:"$ResultsDir" `
    --logger:"console;verbosity=detailed" `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include='[SimpleLazyCache]*'

# Alternative method using coverlet.msbuild
Write-Host "🎯 Running additional coverage collection with coverlet.msbuild..." -ForegroundColor Yellow
dotnet test `
    --configuration Release `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="$ResultsDir/coverage.cobertura.xml" `
    /p:Include="[SimpleLazyCache]*" `
    /p:Exclude="[*.Tests]*" `
    /p:ExcludeByFile="**/*Test*.cs"

# Find the coverage file
$CoverageFile = Get-ChildItem -Path $ResultsDir -Filter "*.cobertura.xml" -Recurse | Select-Object -First 1
if (-not $CoverageFile) {
    $CoverageFile = Get-ChildItem -Path $ResultsDir -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
}

if (-not $CoverageFile) {
    Write-Host "❌ No coverage file found! Coverage collection may have failed." -ForegroundColor Red
    exit 1
}

Write-Host "📊 Coverage file found: $($CoverageFile.FullName)" -ForegroundColor Green

# Install ReportGenerator tool if not already installed
Write-Host "🔧 Installing/updating ReportGenerator tool..." -ForegroundColor Yellow
try {
    dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.2.4 2>$null
} catch {
    dotnet tool update --global dotnet-reportgenerator-globaltool --version 5.2.4
}

# Generate comprehensive HTML report
Write-Host "📈 Generating HTML coverage report..." -ForegroundColor Yellow
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/html" `
    -reporttypes:"Html;HtmlSummary;HtmlInline;HtmlChart" `
    -title:"NetSimpleLazyCache Code Coverage Report" `
    -tag:"$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# Generate Markdown reports
Write-Host "📝 Generating Markdown coverage reports..." -ForegroundColor Yellow
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/markdown" `
    -reporttypes:"MarkdownSummary;MarkdownSummaryGithub;MarkdownDeltaSummary" `
    -title:"NetSimpleLazyCache Code Coverage Report" `
    -tag:"$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# Try to generate Mermaid diagrams if supported
Write-Host "🧩 Attempting to generate Mermaid diagrams..." -ForegroundColor Yellow
try {
    reportgenerator `
        -reports:"$($CoverageFile.FullName)" `
        -targetdir:"$ReportsDir/mermaid" `
        -reporttypes:"MermaidChart" `
        -title:"NetSimpleLazyCache Code Coverage Report" `
        -tag:"$(Get-Date -Format 'yyyyMMdd_HHmmss')" 2>$null
} catch {
    Write-Host "ℹ️  Mermaid charts not supported in this version of ReportGenerator" -ForegroundColor Cyan
}

# Generate additional report formats
Write-Host "📋 Generating additional report formats..." -ForegroundColor Yellow

# XML Summary
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/xml" `
    -reporttypes:"XmlSummary"

# CSV format
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/csv" `
    -reporttypes:"CsvSummary"

# Badges
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/badges" `
    -reporttypes:"Badges"

# Text summary
reportgenerator `
    -reports:"$($CoverageFile.FullName)" `
    -targetdir:"$ReportsDir/text" `
    -reporttypes:"TextSummary"

# Create custom Markdown report with Mermaid diagrams
Write-Host "🎨 Creating custom Markdown report with Mermaid diagrams..." -ForegroundColor Yellow
$SummaryFile = "$ReportsDir/text/Summary.txt"
$MarkdownFile = "$ReportsDir/markdown/Coverage_with_Mermaid.md"

if (Test-Path $SummaryFile) {
    # Extract coverage percentages
    $Content = Get-Content $SummaryFile -Raw
    $LineCoverage = if ($Content -match "Line coverage: ([\d.]+)%") { $Matches[1] } else { "0" }
    $BranchCoverage = if ($Content -match "Branch coverage: ([\d.]+)%") { $Matches[1] } else { "0" }
    $MethodCoverage = if ($Content -match "Method coverage: ([\d.]+)%") { $Matches[1] } else { "0" }
    
    $UncoveredLines = [math]::Round(100 - [double]$LineCoverage, 1)
    
    # Create custom markdown with Mermaid
    $MarkdownContent = @"
# NetSimpleLazyCache - Code Coverage Report

Generated on: $(Get-Date)

## Coverage Summary

``````mermaid
pie title Code Coverage Metrics
    "Covered Lines" : $LineCoverage
    "Uncovered Lines" : $UncoveredLines
``````

## Coverage Breakdown

| Metric | Coverage | Status |
|--------|----------|---------|
| Line Coverage | ${LineCoverage}% | $( if ([double]$LineCoverage -ge 95) { "✅ Excellent" } else { "⚠️ Needs Improvement" } ) |
| Branch Coverage | ${BranchCoverage}% | $( if ([double]$BranchCoverage -ge 90) { "✅ Excellent" } else { "⚠️ Needs Improvement" } ) |
| Method Coverage | ${MethodCoverage}% | $( if ([double]$MethodCoverage -ge 95) { "✅ Excellent" } else { "⚠️ Needs Improvement" } ) |

## Coverage Flow

``````mermaid
graph LR
    A[Source Code] --> B[Test Execution]
    B --> C{Coverage Analysis}
    C --> D[Line Coverage: ${LineCoverage}%]
    C --> E[Branch Coverage: ${BranchCoverage}%]
    C --> F[Method Coverage: ${MethodCoverage}%]
    
    D --> G[Report Generation]
    E --> G
    F --> G
    
    G --> H[HTML Reports]
    G --> I[Markdown Reports]
    G --> J[Coverage Badges]
    
    style D fill:#e1f5fe
    style E fill:#e8f5e8
    style F fill:#fff3e0
``````

## Quality Gates

``````mermaid
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
``````

## Detailed Coverage Information

``````
$(Get-Content $SummaryFile -Raw)
``````

---

*This report was generated automatically by the NetSimpleLazyCache coverage system.*
"@

    Set-Content -Path $MarkdownFile -Value $MarkdownContent -Encoding UTF8
    Write-Host "✅ Custom Mermaid Markdown report created: $MarkdownFile" -ForegroundColor Green
} else {
    Write-Host "⚠️  Could not create Mermaid report: Summary.txt not found" -ForegroundColor Yellow
}

# Create a simple index file
$IndexContent = @"
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
    <p class="timestamp">Generated on: $(Get-Date)</p>
    
    <h2>Available Reports:</h2>
    <a href="reports/html/index.html" class="report-link">📊 HTML Coverage Report (Interactive)</a>
    <a href="reports/html/summary.html" class="report-link">📋 HTML Summary</a>
    <a href="reports/markdown/Summary.md" class="report-link">📝 Markdown Summary</a>
    <a href="reports/markdown/SummaryGithub.md" class="report-link">📝 GitHub Markdown Summary</a>
    <a href="reports/markdown/Coverage_with_Mermaid.md" class="report-link">🧩 Markdown with Mermaid Diagrams</a>
    <a href="reports/xml/Summary.xml" class="report-link">📄 XML Summary</a>
    <a href="reports/csv/Summary.csv" class="report-link">� CSV Data</a>
    <a href="reports/text/Summary.txt" class="report-link">📝 Text Summary</a>
    <a href="reports/badges/" class="report-link">🏆 Coverage Badges</a>
    
    <h2>Quick Stats:</h2>
    <div id="stats">
        <p>Coverage data processed from: <code>$($CoverageFile.Name)</code></p>
    </div>
</body>
</html>
"@

Set-Content -Path "$CoverageDir/index.html" -Value $IndexContent

# Display summary information
Write-Host ""
Write-Host "✅ Coverage report generation completed!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "📁 Reports location: $CoverageDir" -ForegroundColor Yellow
Write-Host "🌐 Main report: $CoverageDir/index.html" -ForegroundColor Yellow
Write-Host "📊 Detailed HTML: $ReportsDir/html/index.html" -ForegroundColor Yellow
Write-Host ""

# Display quick coverage summary if available
$SummaryFile = "$ReportsDir/text/Summary.txt"
if (Test-Path $SummaryFile) {
    Write-Host "📈 Quick Coverage Summary:" -ForegroundColor Cyan
    Write-Host "==========================" -ForegroundColor Cyan
    Get-Content $SummaryFile | Select-Object -First 20
    Write-Host ""
}

Write-Host ""
$FullCoveragePath = Resolve-Path "$CoverageDir/index.html"
Write-Host "🚀 To view the report, open: file://$FullCoveragePath" -ForegroundColor Green
Write-Host "   Or run: start $CoverageDir/index.html (Windows)" -ForegroundColor Green
