# Ambiente di sviluppo verificato

Audit eseguito il 2 settembre 2026.

| Componente | Versione/percorso verificato |
|---|---|
| Windows | Windows build 10.0.26200.9168, x64 |
| Visual Studio | Community 2026 18.9.2 (`C:\Program Files\Microsoft Visual Studio\18\Community`) |
| Workload desktop .NET | installato |
| Workload Android/MAUI | `android` 36.1.69; `maui-windows` 10.0.20 |
| .NET SDK selezionato | 10.0.400 (fissato da `global.json`) |
| .NET SDK disponibili | 9.0.317; 10.0.400 |
| Git | 2.53.0.windows.3 |
| MonoGame | 3.8.5.1 stabile |
| Template MonoGame | `MonoGame.Templates.CSharp` 3.8.5.1 |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk` |
| Android Build Tools | 36.0.0 |
| Android platforms | API 35 e API 36 |
| ADB | 1.0.41 / platform-tools 36.0.0-13206524 |
| JDK | Microsoft OpenJDK 21.0.8 LTS (`C:\Program Files\Android\openjdk\jdk-21.0.8`) |

I template ufficiali MonoGame 3.8.5.1 generano `net9.0` per DesktopGL e `net9.0-android` per Android. Il workload Android installato e gestito da Visual Studio 2026 è quello .NET 10; per evitare una toolchain Android 9 parallela, il progetto Android e il ramo mobile di `Slither.Client` usano `net10.0-android`. Il desktop conserva `net9.0` ed è compilato dall'SDK .NET 10. MonoGame 3.8.5.1 supporta .NET 8 o successivo.

Al momento dell'audit `adb devices -l` non riportava dispositivi collegati.

Le variabili `ANDROID_HOME`, `ANDROID_SDK_ROOT` e `JAVA_HOME` non sono globalmente impostate. La build .NET/Visual Studio risolve i componenti installati; la documentazione usa percorsi espliciti per ADB così da non richiedere modifiche globali al `PATH`.
