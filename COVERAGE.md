# Code Coverage Setup and Usage Guide

This document explains how to generate and use code coverage reports for the NetSimpleLazyCache project.

## 🎯 Current Coverage Status

- **Line Coverage**: 100% (12/12 lines covered)
- **Branch Coverage**: 100% (4/4 branches covered)  
- **Method Coverage**: 100% (2/2 methods covered)

## 📋 Coverage Tools

The project uses a comprehensive coverage setup with multiple tools:

- **Coverlet**: Cross-platform code coverage framework for .NET
- **ReportGenerator**: Converts coverage reports to multiple formats
- **Visual Studio Test Platform**: Built-in coverage collection

## 🚀 Quick Start

### Generate Full Coverage Report
```bash
./scripts/generate-coverage.sh
```

### Quick Coverage Test
```bash
./scripts/quick-coverage.sh
```

### Using dotnet CLI directly
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## 📊 Available Report Formats

After running the coverage generation, you'll find reports in multiple formats:

### HTML Reports (Interactive)
- **Location**: `./coverage/reports/html/index.html`
- **Features**: 
  - Interactive line-by-line coverage
  - Filterable by assembly/class
  - Visual highlighting of covered/uncovered code
  - Coverage percentages per method/class
  - Enhanced charts and inline coverage views

### Markdown Reports (Documentation-ready)
- **Standard Markdown**: `./coverage/reports/markdown/Summary.md`
- **GitHub Markdown**: `./coverage/reports/markdown/SummaryGithub.md` (with collapsible sections)
- **Mermaid Enhanced**: `./coverage/reports/markdown/Coverage_with_Mermaid.md`
  - Interactive pie charts showing coverage distribution
  - Flow diagrams illustrating coverage analysis process
  - Quality gate visualization
  - Test architecture diagrams
  - Color-coded coverage status indicators

### XML Reports (Machine-readable)
- **Location**: `./coverage/reports/xml/Summary.xml`
- **Use cases**: CI/CD integration, automated analysis

### CSV Reports (Data analysis)
- **Location**: `./coverage/reports/csv/Summary.csv`
- **Use cases**: Spreadsheet analysis, trending data

### Coverage Badges (README/Documentation)
- **Location**: `./coverage/reports/badges/`
- **Available badges**:
  - `badge_linecoverage.svg` - Line coverage percentage
  - `badge_branchcoverage.svg` - Branch coverage percentage
  - `badge_methodcoverage.svg` - Method coverage percentage
  - `badge_combined.svg` - Combined coverage metrics

### Text Summary
- **Location**: `./coverage/reports/text/Summary.txt`
- **Use cases**: Console output, quick verification

## 🔧 VS Code Integration

The project includes VS Code tasks for easy coverage generation:

1. Open Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
2. Type "Tasks: Run Task"
3. Select from available coverage tasks:
   - **Generate Coverage Report** - Full report generation
   - **Quick Coverage Test** - Fast coverage check
   - **Test with Coverage (dotnet)** - Basic coverage collection
   - **Open Coverage Report** - Opens generated report

## 🔍 Coverage Configuration

### Included in Coverage
- All code in `SimpleLazyCache` namespace
- Production code only (tests excluded)

### Excluded from Coverage
- Test projects (`*.Tests`)
- Test files (`*Test*.cs`)
- Generated code

### Coverage Thresholds
The project maintains:
- **Line Coverage**: 100% (target: >95%)
- **Branch Coverage**: 100% (target: >90%) 
- **Method Coverage**: 100% (target: >95%)

## 📈 Coverage Analysis

### What's Covered
✅ **Core Functionality**
- `GetOrAddAsync` method with all parameters
- Key validation logic
- Task creation and management

✅ **Exception Paths**
- Invalid key handling (`null`/empty validation)
- Exception propagation from factory functions

✅ **Concurrency Scenarios**
- Race condition handling
- Thread-safe operations
- Cache stampede prevention

✅ **Memory Management**
- Task cleanup in `finally` blocks
- Dictionary key removal

### Coverage Verification Methods
The tests use multiple techniques to ensure comprehensive coverage:

1. **Direct Method Testing**: Every public method called with various inputs
2. **Exception Path Testing**: Deliberate triggering of error conditions
3. **Concurrency Testing**: Multiple threads accessing the same code paths
4. **Reflection-based Verification**: Testing internal state changes

## 🔄 CI/CD Integration

### GitHub Actions Example
```yaml
- name: Test with Coverage
  run: dotnet test --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

- name: Generate Coverage Report  
  run: ./scripts/generate-coverage.sh

- name: Upload Coverage to Codecov
  uses: codecov/codecov-action@v3
  with:
    file: ./coverage/results/coverage.cobertura.xml
```

### Coverage Gates
Consider adding coverage gates in CI:
```bash
# Fail build if coverage drops below threshold
dotnet test /p:CollectCoverage=true /p:Threshold=95 /p:ThresholdType=line
```

## 🛠️ Troubleshooting

### Coverage File Not Found
If coverage generation fails:
1. Ensure all packages are restored: `dotnet restore`
2. Build in Release configuration: `dotnet build -c Release`
3. Check that test project references main project

### Low Coverage Numbers
- Verify include/exclude patterns in coverage configuration
- Check that tests are actually executing the target code
- Use `--logger trx` to see detailed test output

### Report Generation Issues
- Ensure ReportGenerator tool is installed: `dotnet tool install -g dotnet-reportgenerator-globaltool`
- Check that coverage XML files are valid
- Verify file paths in scripts are correct

## 📝 Coverage Best Practices

1. **Aim for High Coverage**: Target >95% line coverage
2. **Test Exception Paths**: Don't just test happy paths
3. **Include Concurrency Tests**: Verify thread-safety
4. **Regular Coverage Reviews**: Monitor coverage trends
5. **Meaningful Tests**: Coverage is a means, not an end - ensure tests are valuable
