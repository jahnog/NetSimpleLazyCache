# NetSimpleLazyCache

[![Line Coverage](badge_linecoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/) [![Branch Coverage](badge_branchcoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/) [![Method Coverage](badge_methodcoverage.svg)](https://jahnog.github.io/NetSimpleLazyCache/)

A high-performance, thread-safe caching solution for .NET that prevents cache stampede scenarios using lazy evaluation and concurrent dictionary patterns.

## Features

- **Thread-Safe**: Built on `ConcurrentDictionary` for safe concurrent access
- **Cache Stampede Prevention**: Multiple concurrent requests for the same key only execute the factory once
- **Memory Leak Prevention**: Automatic cleanup of completed tasks
- **Generic Support**: Works with any reference type
- **High Performance**: Optimized for high-concurrency scenarios
- **Comprehensive Testing**: Extensive unit tests covering race conditions, memory leaks, and edge cases

## Usage

```csharp
var cache = new SingleFactoryCaller<MyDataType>();

// Multiple concurrent calls with the same key will only execute the factory once
var result = await cache.GetOrAddAsync("my-key", async () =>
{
    // Expensive operation here
    return await FetchDataFromDatabase();
});
```

## Code Coverage

This project maintains **100% code coverage** across all metrics:

- ✅ **Line Coverage**: 100% (12/12 lines)
- ✅ **Branch Coverage**: 100% (4/4 branches)  
- ✅ **Method Coverage**: 100% (2/2 methods)

### Generating Coverage Reports

Generate comprehensive coverage reports using the provided scripts:

```bash
# Full coverage report with HTML, Markdown (with Mermaid), XML, CSV formats
./scripts/generate-coverage.sh

# Quick coverage test
./scripts/quick-coverage.sh

# Using dotnet directly
dotnet test --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

**VS Code Users**: Use the Command Palette (`Ctrl+Shift+P`) and run:
- `Tasks: Run Task` → `Generate Coverage Report`
- `Tasks: Run Task` → `Quick Coverage Test`

### 🌐 Online Coverage Reports

View comprehensive coverage reports online at: **https://jahnog.github.io/NetSimpleLazyCache/**

The GitHub Pages site includes:
- 📊 **Interactive HTML Reports**: Line-by-line coverage analysis
- 📈 **Visual Dashboards**: Coverage metrics and trends  
- 📝 **Markdown Reports**: Text-based summaries
- 🏆 **Coverage Badges**: Always up-to-date status indicators

Reports are automatically updated on every push to the main branch.

### 📁 Local Coverage Reports

Coverage reports are generated in the `./coverage/` directory with multiple formats:
- **HTML Report**: `./coverage/reports/html/index.html` (Interactive browser report)
- **Markdown with Mermaid**: `./coverage/reports/markdown/Coverage_with_Mermaid.md` (Visual diagrams)
- **GitHub Markdown**: `./coverage/reports/markdown/SummaryGithub.md` (GitHub-optimized)
- **Standard Markdown**: `./coverage/reports/markdown/Summary.md` (Basic format)
- **XML Summary**: `./coverage/reports/xml/Summary.xml`
- **CSV Data**: `./coverage/reports/csv/Summary.csv`
- **Badges**: `./coverage/reports/badges/` (SVG coverage badges)

## Testing

Run the comprehensive test suite:

```bash
dotnet test --verbosity normal
```

The test suite includes:
- Basic functionality tests
- Concurrency and race condition tests
- Memory leak detection (using reflection)
- Cache stampede prevention verification
- Performance and stress testing
- Edge case handling

## Project Structure

```
├── src/
│   └── SimpleLazyCache/           # Main library
│       └── SingleFactoryCaller.cs # Core implementation
├── test/
│   └── SimpleLazyCache.Unit.Tests/ # Comprehensive test suite
├── scripts/
│   ├── generate-coverage.sh       # Full coverage report generator
│   ├── generate-coverage.ps1      # PowerShell version
│   └── quick-coverage.sh          # Quick coverage test
└── coverage/                      # Generated coverage reports (git-ignored)
```