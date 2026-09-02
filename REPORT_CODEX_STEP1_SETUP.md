# Slither Clone — Specifica operativa per Codex

## Step 1: predisposizione dell'ambiente e validazione della toolchain Windows → Android

**Stato del documento:** specifica iniziale approvata a livello architetturale  
**Data:** 2 settembre 2026  
**Destinatario:** Codex incaricato dell'implementazione  
**Piattaforma di sviluppo:** Windows, Visual Studio 2026  
**Target iniziali:** Windows desktop e smartphone Android fisico

---

## 1. Scopo dello step

Questo primo step non deve ancora implementare il gioco. Deve predisporre e verificare l'intera catena tecnica necessaria per lo sviluppo successivo:

```text
codice C# → build desktop → build Android → installazione sul telefono → avvio → debug/log
```

Lo step è concluso solo quando la stessa solution produce:

1. un eseguibile Windows minimale funzionante;
2. un'app Android minimale installabile e avviabile su uno smartphone fisico;
3. un nucleo di codice condiviso e indipendente dalla piattaforma;
4. una struttura già compatibile con la futura pipeline `input → simulazione → snapshot → rendering`.

L'APK può mostrare soltanto uno sfondo uniforme e una primitiva grafica animata. Questa prova serve a validare inizializzazione grafica, ciclo di update/rendering, lifecycle Android e deploy USB. Arena, serpente, cibo e regole appartengono allo step successivo.

## 2. Decisioni già prese e non da ridiscutere

- Linguaggio: **C#**.
- Framework multipiattaforma: **MonoGame**.
- IDE principale: **Visual Studio 2026**.
- Target di sviluppo rapido: **Windows**.
- Target di validazione reale: **Android**.
- Rendering futuro: **custom tramite `GraphicsDevice`, buffer GPU e shader**. MonoGame non deve imporre `SpriteBatch` come architettura del renderer.
- Simulazione: proprietaria, senza motore fisico general-purpose.
- Networking: assente nel primo prototipo, ma i confini software devono simulare fin dall'inizio la presenza di un server.
- Il client genera intenzioni di comando, non impone posizione o velocità.
- Il renderer non modifica e non calcola lo stato simulativo.
- Niente Unity, Godot, ECS, dependency injection o framework architetturali aggiuntivi.
- Niente codice gameplay nello step 1, salvo una prova grafica locale strettamente necessaria a verificare la toolchain.

## 3. Ruolo di MonoGame

MonoGame viene usato come layer multipiattaforma sottile per:

- creazione della finestra/surface;
- accesso al `GraphicsDevice`;
- loop dell'applicazione;
- input desktop e touch Android;
- lifecycle Android;
- packaging e deploy;
- in seguito, audio e gestione degli asset.

Non va interpretato come obbligo a costruire il gioco con sprite o astrazioni 2D ad alto livello. La pipeline prevista è:

```text
Android / Windows
        ↓
MonoGame platform layer
        ↓
GraphicsDevice
        ↓
renderer proprietario
        ↓
vertex/index/instance buffer + shader
```

Per lo smoke test è ammesso il mezzo più semplice e robusto per disegnare una primitiva. Tale codice deve però restare confinato nel progetto client e non deve diventare un precedente architetturale vincolante.

## 4. Prerequisiti da verificare o installare

Codex deve prima produrre un inventario delle installazioni esistenti e intervenire solo su ciò che manca. Non duplicare SDK, JDK o workload già correttamente gestiti da Visual Studio.

### 4.1 Strumenti obbligatori

- Git.
- Visual Studio 2026 aggiornato.
- Workload Visual Studio **.NET desktop development**.
- Workload Visual Studio **.NET Multi-platform App UI Development** per toolchain Android.
- SDK .NET compatibile con la release stabile di MonoGame selezionata.
- Workload Android .NET; da CLI la verifica equivalente è `dotnet workload list`, mentre l'installazione, se necessaria e coerente con Visual Studio, è `dotnet workload install android`.
- Android SDK.
- JDK richiesto dalla release MonoGame/.NET effettivamente installata.
- Template C# MonoGame: `MonoGame.Templates.CSharp`.
- Tool locali della Content Pipeline ripristinabili con `dotnet tool restore` se generati dal template.
- Driver USB del produttore del telefono, se Windows non rileva correttamente ADB.
- Android Platform Tools / ADB.

### 4.2 Regola sulle versioni

Non codificare nel progetto una versione “ricordata” da documentazione vecchia. Prima di creare la solution:

1. individuare l'ultima release **stabile** di MonoGame disponibile;
2. leggere i target framework generati dai template ufficiali correnti;
3. verificare che Visual Studio e i workload installati supportino quei target;
4. registrare in `docs/ENVIRONMENT.md` le versioni effettivamente usate;
5. evitare preview package, salvo incompatibilità documentata della stable e previa segnalazione.

La documentazione MonoGame corrente indica il template Android `mgandroid`, il pacchetto `MonoGame.Framework.Android` e il workload Android .NET. Le richieste puntuali di JDK/SDK Android vanno tuttavia confermate contro la release/template realmente installati, perché sono soggette a cambiamenti.

## 5. Operazioni richieste a Codex

### Fase A — Audit non distruttivo

Raccogliere e riportare almeno:

- versione di Windows;
- edizione e versione di Visual Studio;
- workload Visual Studio presenti;
- output di `dotnet --info`;
- output di `dotnet workload list`;
- versione Git;
- disponibilità dei template MonoGame (`dotnet new list monogame` o comando equivalente);
- presenza e percorso di Android SDK, build tools, platform tools e ADB;
- presenza e versione del JDK usato dalla toolchain;
- dispositivi visibili con `adb devices` quando il telefono è collegato.

Non mostrare o salvare variabili sensibili. Se più SDK/JDK sono presenti, indicare quale viene risolto dalla build anziché cancellare gli altri.

### Fase B — Installazione minima

Installare esclusivamente i componenti mancanti. Preferire:

1. Visual Studio Installer per i workload gestiti da Visual Studio;
2. `dotnet workload` per verifica/ripristino coerente;
3. template ufficiali MonoGame tramite CLI o estensione ufficiale Visual Studio;
4. Android SDK/JDK forniti o riconosciuti dal workload Microsoft, evitando installazioni parallele non necessarie.

Se un'installazione richiede interazione grafica, riavvio, autorizzazione amministrativa o accettazione di licenze, Codex deve fermarsi al punto esatto e fornire una lista breve delle azioni manuali, quindi riprendere le verifiche.

### Fase C — Creazione repository e solution

Creare un repository Git con questa struttura minima:

```text
SlitherClone/
├── SlitherClone.sln
├── Directory.Build.props
├── README.md
├── .gitignore
├── docs/
│   ├── ENVIRONMENT.md
│   └── ANDROID_DEPLOY.md
└── src/
    ├── Slither.Core/
    ├── Slither.Protocol/
    ├── Slither.Client/
    ├── Slither.Desktop/
    └── Slither.Android/
```

Responsabilità:

| Progetto | Responsabilità | Dipendenze consentite |
|---|---|---|
| `Slither.Core` | tipi matematici/simulativi e futura logica autorevole | BCL; nessuna dipendenza da MonoGame/Android |
| `Slither.Protocol` | comandi e snapshot scambiati tra client e simulazione | BCL; preferibilmente nessuna dipendenza da MonoGame |
| `Slither.Client` | presentazione, camera futura, preparazione rendering e adattamento input | Core, Protocol, MonoGame |
| `Slither.Desktop` | bootstrap Windows e debug rapido | Client, MonoGame desktop |
| `Slither.Android` | bootstrap, manifest, lifecycle e packaging Android | Client, MonoGame Android |

Se i template MonoGame correnti rendono tecnicamente più pulita una variante della struttura, Codex può proporla, ma deve preservare rigorosamente questi confini di dipendenza. Non deve fondere `Core` nel progetto grafico.

### Fase D — Contratti architetturali minimi

Creare solo gli scheletri necessari a rendere esplicito il flusso futuro. I nomi possono essere raffinati, ma il significato deve restare questo:

```csharp
public readonly record struct PlayerCommand(
    uint Sequence,
    float TargetDirectionX,
    float TargetDirectionY,
    bool Boost);

public interface ICommandSource
{
    PlayerCommand SampleCommand();
}

public interface ISimulationEndpoint
{
    void Submit(in PlayerCommand command);
    void Step(double fixedDeltaTime);
    WorldSnapshot CaptureSnapshot();
}

public interface ISnapshotRenderer
{
    void Render(WorldSnapshot snapshot, float interpolationAlpha);
}
```

In questo step `WorldSnapshot` può contenere soltanto il dato necessario allo smoke test oppure essere vuoto. Non implementare ancora rete o serializzazione. L'endpoint locale deve mantenere la stessa semantica che avrà quello remoto:

```text
input platform-specific
        ↓
PlayerCommand
        ↓
LocalSimulationEndpoint
        ↓
WorldSnapshot
        ↓
custom renderer
```

Vincoli:

- nessun riferimento a `Microsoft.Xna.Framework` in `Slither.Core` o `Slither.Protocol`;
- nessun accesso a touch, mouse o tastiera nella simulazione;
- nessuna mutazione simulativa nel renderer;
- nessun accesso diretto del renderer agli oggetti interni della simulazione: solo snapshot;
- nessuna velocità arbitraria nel comando del giocatore: la futura simulazione autorevole determinerà velocità e limite angolare;
- tutte le dipendenze devono essere orientate verso i progetti neutrali, mai verso Android/Desktop.

### Fase E — Loop temporale minimo

Impostare fin dall'inizio:

- simulazione a timestep fisso;
- rendering separato dalla frequenza di simulazione;
- accumulatore temporale e `interpolationAlpha`, anche se lo smoke test non usa ancora vera interpolazione;
- protezione da una spirale di aggiornamenti dopo pause/sospensioni dell'app, limitando il tempo accumulato o il numero massimo di step per frame;
- gestione corretta di pausa/ripresa Android.

Valore iniziale suggerito: **60 Hz** per la simulazione. Deve essere una costante/configurazione centrale, non sparsa nel codice. Non usare `float deltaTime` variabile come base della futura fisica.

## 6. Smoke test grafico condiviso

Il test deve dimostrare che update, snapshot e renderer attraversano davvero i confini definiti sopra. Non basta aprire una finestra vuota.

Comportamento minimo consigliato:

- sfondo scuro;
- un disco o triangolo colorato che si muove lentamente e rimbalza o ruota;
- posizione/angolo calcolati nel piccolo endpoint simulativo locale;
- stato trasferito tramite `WorldSnapshot`;
- disegno eseguito dal client tramite il `GraphicsDevice`;
- stesso comportamento visibile su Windows e Android;
- overlay o log con FPS/render Hz e simulation tick, eliminabile in release.

Il rendering può usare una mesh procedurale minima e un effect compatibile con entrambi i backend. Evitare asset esterni nello smoke test, così da isolare i problemi di toolchain e shader.

## 7. Configurazione Android

Configurare un package ID provvisorio stabile, per esempio:

```text
com.emilianofiorenza.slitherclone
```

Non pubblicare nulla sul Play Store. Per questa fase:

- build Debug;
- deploy USB su dispositivo fisico;
- orientamento landscape;
- fullscreen/immersive se supportato senza complicazioni;
- nessun permesso Android non necessario;
- nessun accesso Internet richiesto;
- gestione della densità indipendente dai pixel fisici;
- supporto iniziale a una versione Android compatibile con il telefono di test e coerente con i requisiti Play correnti, senza alzare inutilmente il `minSdk`.

In `docs/ANDROID_DEPLOY.md` documentare:

1. attivazione Opzioni sviluppatore e Debug USB;
2. verifica con `adb devices` e accettazione della chiave RSA sul telefono;
3. comando/build configuration per generare il pacchetto Debug;
4. procedura di installazione e aggiornamento;
5. avvio dell'app;
6. acquisizione log (`adb logcat`) filtrata per il package/processo;
7. posizione dell'APK/AAB generato;
8. problemi incontrati e soluzione.

## 8. Repository e qualità minima

Il repository deve includere:

- `.gitignore` adatto a .NET, Visual Studio, MonoGame e Android;
- nessun file generato `bin/`, `obj/`, `.vs/` o APK committato;
- versioni NuGet determinate e riproducibili;
- nullable reference types abilitati;
- warning ragionevoli attivati; evitare una politica che renda ingestibile codice generato dai template;
- formattazione coerente tramite `.editorconfig` se non già inclusa;
- README con build rapida Windows/Android;
- commit iniziale pulito dopo la verifica, se Codex è autorizzato a creare commit; altrimenti lasciare modifiche pronte e riportare lo stato Git.

Non introdurre CI/CD nello step 1. Potrà essere aggiunto quando la build locale è stabile.

## 9. Test automatici richiesti

Creare un progetto di test solo se non complica il bootstrap mobile. Minimo utile:

- test che `Slither.Core` e `Slither.Protocol` non dipendano da MonoGame, verificabile anche tramite riferimenti di progetto;
- test del fixed-step accumulator con frame brevi, frame lunghi e pausa;
- test di monotonicità del sequence number dei comandi, se già presente una sorgente comando;
- test che la cattura snapshot non esponga collezioni mutabili interne.

Non serve testare MonoGame o il driver grafico. La prova Android fisica resta un criterio di accettazione manuale.

## 10. Criteri di accettazione

Lo step 1 è completato soltanto se tutti i punti seguenti sono veri:

- [ ] La solution si apre in Visual Studio 2026 senza migrazioni o errori.
- [ ] `dotnet restore` e il ripristino dei tool locali completano correttamente.
- [ ] La build Debug desktop termina senza errori.
- [ ] Lo smoke test desktop si avvia e mostra la primitiva animata.
- [ ] La build Debug Android termina senza errori.
- [ ] ADB riconosce lo smartphone come `device`, non `unauthorized`.
- [ ] L'app viene installata e avviata sul telefono.
- [ ] La primitiva è visibile e animata sul telefono.
- [ ] Sospensione e ripresa dell'app non causano crash o salto temporale incontrollato.
- [ ] Rotazione/orientamento non rompe viewport o rendering.
- [ ] I log non mostrano eccezioni non gestite o errori grafici persistenti.
- [ ] `Slither.Core` e `Slither.Protocol` non referenziano MonoGame né Android.
- [ ] Desktop e Android usano lo stesso client e gli stessi contratti.
- [ ] Le istruzioni per ripetere build e deploy sono documentate e sono state rieseguite almeno una volta.

## 11. Deliverable attesi

Codex deve consegnare:

1. repository/solution compilabile;
2. elenco dei software installati e delle versioni in `docs/ENVIRONMENT.md`;
3. guida ripetibile in `docs/ANDROID_DEPLOY.md`;
4. smoke test funzionante su Windows;
5. build Android Debug e conferma di installazione sul dispositivo, se il telefono è disponibile;
6. breve rapporto finale con:
   - file creati/modificati;
   - comandi di build/test eseguiti;
   - esito di ciascun criterio di accettazione;
   - eventuali operazioni manuali residue;
   - rischi o incompatibilità incontrati;
   - decisioni tecniche non previste da questa specifica.

## 12. Cose esplicitamente fuori scope

- serpente e body chain;
- cibo/dot;
- collisioni;
- crescita;
- boost reale;
- camera di gioco;
- joystick o controllo touch definitivo;
- networking e server;
- prediction, interpolation di rete e reconciliation;
- economia, monete, upgrade e progressione;
- bot, multiplayer, account, backend;
- grafica definitiva, glow, skin e shader avanzati;
- audio;
- pubblicazione Play Store.

## 13. Principi che dovranno guidare lo step successivo

Questi principi non richiedono ancora implementazione completa, ma la struttura creata ora non deve ostacolarli:

1. Il client produrrà solo `TargetDirection` e `BoostPressed` con sequence number.
2. La simulazione autorevole applicherà limite di sterzata, velocità, boost, crescita e collisioni.
3. Il corpo del serpente sarà una chain filtrata/inseguitrice della testa, non segmenti rigidamente vincolati.
4. La simulazione produrrà snapshot; il renderer li consumerà senza conoscere le regole.
5. Il renderer costruirà geometria custom da chain e dot, con possibilità futura di instancing e shader.
6. L'endpoint locale sarà sostituibile da un endpoint di rete senza cambiare input e renderer.
7. La crescita per dot resterà costante; l'eventuale valore economico del dot è un concetto separato e futuro.

## 14. Istruzione iniziale sintetica per Codex

> Esegui lo Step 1 di questa specifica. Inizia con un audit non distruttivo dell'ambiente e riporta ciò che manca. Installa o richiedi solo i componenti necessari, quindi crea la solution multiprogetto mantenendo `Core` e `Protocol` indipendenti da MonoGame. Implementa uno smoke test che attraversi realmente `command → local simulation → snapshot → custom renderer`, compilalo su Windows e Android e valida il deploy su smartphone fisico. Non implementare ancora il gameplay. Documenta ogni prerequisito e rendi build e deploy ripetibili.

---

## Riferimenti tecnici verificati

- MonoGame, piattaforme supportate e template: https://docs.monogame.net/articles/getting_started/platforms.html
- MonoGame, configurazione Visual Studio 2026: https://docs.monogame.net/articles/getting_started/2_choosing_your_ide_visual_studio.html
- MonoGame, template CLI e tool restore: https://docs.monogame.net/articles/getting_started/2_choosing_your_ide_vscode.html

