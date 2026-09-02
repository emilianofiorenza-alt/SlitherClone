# Slither Clone

Prototipo multipiattaforma in C# e MonoGame. Lo Step 1 valida la toolchain Windows/Android e i confini `input -> simulazione -> snapshot -> rendering`; non contiene ancora gameplay.

## Requisiti

- Visual Studio Community 2026 con sviluppo desktop .NET e workload Android/MAUI
- .NET SDK 10.0.400
- MonoGame 3.8.5.1
- Android SDK e OpenJDK installati dal Visual Studio Installer

## Build rapida

```powershell
dotnet restore .\SlitherClone.sln
dotnet build .\src\Slither.Desktop\Slither.Desktop.csproj -c Debug
dotnet run --project .\src\Slither.Desktop\Slither.Desktop.csproj -c Debug
dotnet build .\src\Slither.Android\Slither.Android.csproj -c Debug
```

Per ambiente e deploy Android vedere [`docs/ENVIRONMENT.md`](docs/ENVIRONMENT.md) e [`docs/ANDROID_DEPLOY.md`](docs/ANDROID_DEPLOY.md).
