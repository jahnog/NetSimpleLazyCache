# NetSimpleLazyCache

`SingleFactoryCaller<T>` gives a caller one shared in-flight factory for a key. The caller uses `GetOrAddAsync`. The type stores that call in a dictionary guarded by a lock. When the call finishes, the entry is removed, and a later call runs the factory again. A cancellation token from one caller does not cancel the shared factory. `T` is a reference type.

## Advantages

`GetOrAddAsync` throws `ArgumentException` when the key is null or empty, and `ArgumentNullException` when the factory is null. A factory that returns a null task throws `InvalidOperationException`.

The method awaits the shared task and the factory task with `ConfigureAwait(false)`. A caller can block on the returned task while a synchronization context does not run posted work, and the call still completes.

A finishing caller removes the dictionary entry only when that entry is still the call it started. A newer entry for the same key stays.

## Install

No command exists that installs the library.

From the repository root, restore the local tool declared in `dotnet-tools.json`:

```bash
dotnet tool restore
```

The library targets `netstandard2.0`. The test project targets `net10.0`.

## Uninstall

No command exists that removes this project.

## Run

No command exists that starts this project. A caller uses the library. See [Usage](#usage).

## Test

From the repository root, xUnit runs through `dotnet test`:

```bash
dotnet test --verbosity normal
```

## Debug

No command exists for debugging this project.

## Mutation test

`dotnet-tools.json` declares `dotnet-stryker` 5.0.0. From the repository root, run the install command. From `test/SimpleLazyCache.Unit.Tests`:

```bash
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

`--version` is the baseline label. This checkout stores that baseline under `feature/github-publish`. The HTML report and the disk baseline are under `test/SimpleLazyCache.Unit.Tests/StrykerOutput/`. Those files are build output.

## Reports

Coverlet writes the Cobertura file. ReportGenerator 5.2.4, already referenced by the test project, writes the HTML report. The files are build output.

From the repository root:

```bash
dotnet test --configuration Release \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/results/coverage.cobertura.xml \
  /p:Include="[SimpleLazyCache]*" \
  /p:Exclude="[*.Tests]*"
```

Coverlet resolves `CoverletOutput` from the test project, so that command writes `test/SimpleLazyCache.Unit.Tests/coverage/results/coverage.cobertura.xml`. From the repository root, copy it to the path the HTML command reads:

```bash
mkdir -p coverage/results
cp test/SimpleLazyCache.Unit.Tests/coverage/results/coverage.cobertura.xml coverage/results/coverage.cobertura.xml
```

From the repository root:

```bash
dotnet ~/.nuget/packages/reportgenerator/5.2.4/tools/net8.0/ReportGenerator.dll \
  -reports:coverage/results/coverage.cobertura.xml \
  -targetdir:coverage/reports/html \
  -reporttypes:Html
```

Open `coverage/reports/html/index.html`.

From the repository root, `./scripts/quick-coverage.sh` runs `dotnet test` with OpenCover and `CoverletOutput=./coverage/quick/`. Use that script for the OpenCover file. Use the commands above for the Cobertura file and the HTML index. Coverlet resolves `./coverage/quick/` from the test project as well.

## Build

From the repository root:

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

## License

MIT. See [LICENSE](LICENSE).

## Project structure

```
├── src/SimpleLazyCache/
├── test/SimpleLazyCache.Unit.Tests/
├── scripts/
├── LICENSE
└── dotnet-tools.json
```

`coverage/` and `StrykerOutput/` are gitignored.
