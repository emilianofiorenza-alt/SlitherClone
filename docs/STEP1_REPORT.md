# Report Step 1 — Bootstrap Windows e Android

**Progetto:** Slither Clone  
**Data:** 2 settembre 2026  
**Repository:** `https://github.com/emilianofiorenza-alt/SlitherClone`  
**Stato:** toolchain e smoke test validati su Windows e Xiaomi 15T; restano alcune verifiche manuali di lifecycle prima della chiusura formale dello Step 1.

## 1. Obiettivo

Lo Step 1 aveva lo scopo di verificare l'intera catena tecnica senza implementare il gameplay:

```text
codice C# -> build desktop -> build Android -> installazione USB -> avvio -> log/debug
```

È stato inoltre predisposto il flusso architetturale condiviso:

```text
input -> PlayerCommand -> simulazione locale -> WorldSnapshot -> renderer
```

## 2. Repository e Git

È stato creato il repository GitHub `SlitherClone`, inizialmente privato e vuoto, quindi collegato alla cartella locale:

```text
C:\Users\user\OneDrive\Desktop\Slither Clone
```

Commit registrati:

| Commit | Descrizione |
|---|---|
| `cab2ee6` | Specifica operativa iniziale |
| `d9ee5bc` | Bootstrap dello Step 1 e smoke test |
| `82f836a` | Validazione del deploy sullo Xiaomi 15T |

Sono stati aggiunti `.gitignore` ed `.editorconfig`. Gli artefatti `bin/`, `obj/`, `.vs/`, APK, AAB, keystore e configurazioni locali non vengono versionati.

### Problemi Git riscontrati

1. Git non era inizialmente installato o disponibile nel `PATH`; è stato installato Git for Windows 2.53.0.
2. Alcuni comandi `git init`, `commit` e `push` sono stati eseguiti inizialmente da `C:\` o `C:\Users\user`, fuori dal repository. Il problema è stato risolto entrando prima nella cartella con `Set-Location`.
3. Il primo `push` restituiva `src refspec main does not match any` perché la branch non conteneva ancora commit. Sono stati configurati nome/email Git e creato il commit iniziale.

## 3. Ambiente verificato

| Componente | Configurazione usata |
|---|---|
| Sistema operativo | Windows x64, build 10.0.26200.9168 |
| IDE | Visual Studio Community 2026 18.9.2 |
| Workload | sviluppo desktop .NET; Android/MAUI |
| .NET SDK | 10.0.400, fissato in `global.json` |
| SDK aggiuntivo | .NET 9.0.317 disponibile |
| MonoGame | 3.8.5.1 stabile |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk` |
| Build Tools | 36.0.0 |
| Android Platform Tools/ADB | 36.0.0-13206524 / ADB 1.0.41 |
| JDK | Microsoft OpenJDK 21.0.8 LTS |

Visual Studio 2022 era già presente sul PC ed è stato inizialmente aperto per errore. È stato verificato che Visual Studio Community 2026 fosse installato, completo e avviabile; le due versioni possono convivere.

I template ufficiali `MonoGame.Templates.CSharp` 3.8.5.1 sono stati installati. La versione stabile è stata confermata sul catalogo NuGet ufficiale.

## 4. Scelta dei target framework

I template MonoGame 3.8.5.1 generano:

- `net9.0` per DesktopGL;
- `net9.0-android` per Android.

Visual Studio 2026 aveva però installato il workload Android corrente per .NET 10. Il restore del progetto Android `net9.0-android` falliva con `NETSDK1147`, richiedendo un secondo workload Android legacy.

Per evitare SDK/workload Android paralleli non necessari è stata adottata questa configurazione:

- desktop e librerie neutrali: `net9.0`;
- Android e ramo mobile di `Slither.Client`: `net10.0-android`;
- build orchestrata dall'SDK .NET 10.0.400.

MonoGame 3.8.5.1 supporta .NET 8 e versioni successive, quindi la combinazione è compatibile e si è compilata senza warning.

## 5. Solution creata

La solution `SlitherClone.sln` contiene:

| Progetto | Responsabilità |
|---|---|
| `Slither.Core` | fixed-step, configurazione temporale e piccola simulazione dello smoke test |
| `Slither.Protocol` | `PlayerCommand`, `WorldSnapshot` e contratti di comunicazione |
| `Slither.Client` | adattamento input, endpoint locale, game loop e renderer condiviso |
| `Slither.Desktop` | bootstrap DesktopGL |
| `Slither.Android` | bootstrap, manifest, lifecycle e packaging Android |
| `Slither.Tests` | test temporali, protocollari e architetturali |

`Slither.Core` e `Slither.Protocol` non referenziano MonoGame, Android o `Microsoft.Xna.Framework`.

## 6. Implementazione dello smoke test

Lo smoke test utilizza:

- sfondo scuro;
- triangolo colorato generato proceduralmente;
- `VertexBuffer`, `GraphicsDevice.DrawPrimitives` e `BasicEffect`;
- nessun asset grafico esterno;
- simulazione a timestep fisso di 60 Hz;
- rendering a frequenza indipendente;
- accumulatore e `interpolationAlpha`;
- massimo di 8 step per frame e limite al tempo accumulabile;
- reset dell'accumulatore durante disattivazione/riattivazione;
- log periodico di render Hz e simulation tick.

Il comando giocatore contiene soltanto direzione desiderata e boost. Velocità, aggiornamento della posizione e rimbalzo sono determinati dall'endpoint simulativo locale. Il renderer riceve esclusivamente uno snapshot immutabile.

Controlli desktop:

- frecce direzionali: direzione;
- Spazio: boost;
- Esc: uscita.

Controlli Android:

- tocco: direzione rispetto al centro dello schermo;
- secondo tocco: boost.

La logica touch è sufficiente a verificare il flusso di input, ma non rappresenta ancora il controllo definitivo del gioco ed è stata giudicata non perfettamente funzionale durante la prova manuale.

## 7. Test e build

Risultati ottenuti:

```text
Build solution Debug: riuscita
Errori: 0
Warning: 0
Test automatici: 7 superati su 7
```

I test verificano:

- frame brevi e accumulo del fixed-step;
- protezione dopo frame molto lunghi;
- reset temporale dopo una pausa;
- monotonicità del sequence number;
- assenza di collezioni mutabili nello snapshot;
- assenza di riferimenti MonoGame/Android nelle librerie neutrali.

Lo smoke test desktop è stato avviato correttamente. Il processo è rimasto attivo e il titolo riportava render Hz e tick simulativo in avanzamento.

## 8. Configurazione e validazione Xiaomi 15T

Dispositivo rilevato:

| Voce | Valore |
|---|---|
| Modello ADB | `25069PTEBG` / `goya_eea` |
| Sistema | Xiaomi HyperOS 3.0.305.0 EEA |
| Android | 16, API 36 |
| ABI | `arm64-v8a` |
| Stato ADB | `device` |

Sul telefono sono stati abilitati:

- Opzioni sviluppatore;
- Debug USB;
- configurazione USB per trasferimento file;
- Installa tramite USB;
- Debug USB (Impostazioni di sicurezza), richiesto da HyperOS per il deploy ADB.

Non sono stati abilitati sblocco OEM o bootloader unlock.

### Blocco installazione HyperOS

I primi tentativi di installazione restituivano:

```text
INSTALL_FAILED_USER_RESTRICTED: Install canceled by user
```

ADB risultava autorizzato, il telefono non era gestito da policy aziendali e non erano presenti restrizioni utente effettive. Il blocco proveniva quindi dal livello di sicurezza HyperOS. L'attivazione di **Installa tramite USB** e **Debug USB (Impostazioni di sicurezza)** ha consentito il deploy.

## 9. Problema Fast Deployment

La prima build Debug produceva un APK di circa 8 MB. L'installazione riusciva, ma l'app terminava immediatamente con un crash nativo:

```text
No assemblies found in .../files/.__override__/arm64-v8a.
Assuming this is part of Fast Deployment. Exiting...
```

Il Debug APK generato dal normale target `Build` utilizzava il Fast Deployment .NET: gli assembly gestiti non erano incorporati nell'APK perché Visual Studio avrebbe dovuto trasferirli separatamente nella directory privata dell'app.

È stata aggiunta al progetto Android la configurazione:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
</PropertyGroup>
```

Il nuovo APK autonomo misura circa 45,7 MB, si installa direttamente con `adb install -r` e rimane in esecuzione senza crash.

Durante la ricompilazione OneDrive ha mostrato temporaneamente timestamp e dimensione del vecchio APK, portando a una reinstallazione del pacchetto precedente. Una nuova verifica dei metadati ha individuato il file aggiornato. Questo conferma che, per lo sviluppo quotidiano, è preferibile lavorare in un clone locale non sincronizzato da OneDrive, ad esempio `C:\Dev\SlitherClone`.

## 10. Risultato sul dispositivo

La prova sullo Xiaomi 15T ha confermato:

- installazione e aggiornamento tramite USB;
- avvio dell'activity;
- processo stabile;
- inizializzazione EGL/OpenGL e superficie MonoGame;
- rendering osservato intorno a 120 Hz;
- simulazione osservata intorno a 60 tick/s;
- triangolo colorato visibile e animato su sfondo scuro;
- input touch ricevuto e funzionante;
- avvio in modalità landscape;
- nessun errore grafico persistente o crash nella sessione valida.

HyperOS ha impedito ad ADB di simulare direttamente il tasto Home tramite `input keyevent`, nonostante l'app fosse installabile. Questo non influenza il gioco, ma obbliga a eseguire manualmente la prova Home/ripresa.

## 11. Stato dei criteri di accettazione

| Criterio | Stato |
|---|---|
| Restore completo | superato |
| Build desktop Debug | superata, 0 errori/0 warning |
| Smoke test desktop avviabile | superato tecnicamente |
| Build Android Debug | superata, 0 errori/0 warning |
| ADB vede il telefono come `device` | superato |
| APK installato e avviato | superato |
| Primitiva visibile e animata sul telefono | confermato manualmente |
| Input touch | confermato manualmente; controllo provvisorio |
| Landscape e viewport | confermato manualmente |
| Log privi di crash correnti persistenti | superato |
| Confini Core/Protocol | superato e coperto da test |
| Codice client condiviso Desktop/Android | superato |
| Sospensione/ripresa tramite Home | ancora da confermare manualmente |
| Apertura della solution in VS 2026 | da confermare manualmente |

## 12. Prossimi passi consigliati

1. Aprire `SlitherClone.sln` in Visual Studio 2026 e verificare che non siano richieste migrazioni.
2. Eseguire manualmente Home -> attesa -> ripresa dell'app e controllare che non vi siano salti temporali.
3. Passare allo sviluppo desktop-first dell'interfaccia minima, mantenendo layout indipendente dalla risoluzione e input astratto.
4. Definire una piccola macchina a stati per avvio, menu principale, gioco/HUD e pausa.
5. Sostituire in seguito il controllo touch provvisorio con la logica di input definitiva.

## 13. Riferimenti tecnici

- MonoGame, Visual Studio: <https://docs.monogame.net/articles/getting_started/2_choosing_your_ide_visual_studio.html>
- MonoGame, piattaforme e template: <https://docs.monogame.net/articles/getting_started/platforms.html>
- MonoGame.Templates.CSharp 3.8.5.1: <https://www.nuget.org/packages/MonoGame.Templates.CSharp/3.8.5.1>
- Android Debug Bridge: <https://developer.android.com/tools/adb>
- .NET for Android, proprietà di build: <https://github.com/dotnet/android/blob/main/Documentation/docs-mobile/building-apps/build-properties.md>
