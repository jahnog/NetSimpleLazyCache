# NetSimpleLazyCache

`SingleFactoryCaller<T>` shares one in-flight factory call among concurrent callers of the same key. The type stores that call in a dictionary guarded by a lock. When the call finishes, the entry is removed, and a later call runs the factory again. A cancellation token from one caller does not cancel the shared factory. `T` is a reference type.

## Build

```bash
dotnet build
```

## Usage

```csharp
var cache = new SingleFactoryCaller<MyDataType>();

var result = await cache.GetOrAddAsync("my-key", async () =>
{
    return await FetchDataFromDatabase();
});
```

## Tests

```bash
dotnet test --verbosity normal
```

## Coverage

Coverlet writes the Cobertura file. ReportGenerator 5.2.4, already referenced by the test project, writes the HTML report.

```bash
dotnet test --configuration Release \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/results/coverage.cobertura.xml \
  /p:Include="[SimpleLazyCache]*" \
  /p:Exclude="[*.Tests]*"

dotnet ~/.nuget/packages/reportgenerator/5.2.4/tools/net8.0/ReportGenerator.dll \
  -reports:coverage/results/coverage.cobertura.xml \
  -targetdir:coverage/reports/html \
  -reporttypes:Html
```

Open `coverage/reports/html/index.html`. `./scripts/quick-coverage.sh` writes an OpenCover file under `coverage/quick/`.

## Mutation testing

The local tool is `dotnet-stryker` 5.0.0, declared in `dotnet-tools.json`. Restore it, then run Stryker from the test project. `--version` is the baseline label. This checkout stores that baseline under `feature/github-publish`.

```bash
dotnet tool restore
cd test/SimpleLazyCache.Unit.Tests
dotnet stryker \
  --solution ../../NetSimpleLazyCache.sln \
  --project SimpleLazyCache.csproj \
  --reporter html \
  --reporter json \
  --reporter progress \
  --with-baseline \
  --version feature/github-publish \
  --skip-version-check
```

The HTML report and the disk baseline are under `test/SimpleLazyCache.Unit.Tests/StrykerOutput/`.

## License

MIT. See [LICENSE](LICENSE).

## Project structure

```
├── src/SimpleLazyCache/                 # Library (netstandard2.0)
├── test/SimpleLazyCache.Unit.Tests/     # xUnit tests (net10.0)
├── scripts/                             # Local coverage scripts
├── LICENSE
└── dotnet-tools.json                    # Local dotnet-stryker manifest
```

`coverage/` and `StrykerOutput/` are gitignored.
