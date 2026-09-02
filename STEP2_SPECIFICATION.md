# Slither Clone — Specifica operativa Step 2

## Controllo mobile, cinematica dello Slither, camera, arena e dot

**Data:** 2 settembre 2026  
**Prerequisito:** Step 1 sostanzialmente completato; restano da confermare apertura della solution in Visual Studio 2026 e ciclo Android Home → ripresa.  
**Obiettivo:** ottenere sullo Xiaomi 15T il primo nucleo giocabile reale, single-player e locale, mantenendo la pipeline già predisposta per il futuro server autorevole.

---

## 1. Risultato atteso

Al termine dello Step 2 deve essere possibile:

- avviare il gioco direttamente nell'arena;
- controllare simultaneamente direzione e boost con due dita;
- vedere la testa dello Slither sempre al centro dell'area di gioco;
- osservare un corpo iniziale corto, composto da una testa e 5 segmenti;
- percepire una sterzata fluida con limite angolare, non una rotazione istantanea;
- vedere i segmenti inseguire la testa con una dinamica filtrata e naturale;
- esplorare una porzione locale di un'arena circolare più grande dello schermo;
- incontrare nuovi dot man mano che si esplorano nuove zone;
- raccogliere dot e aumentare sempre della stessa quantità la lunghezza fisica dello Slither;
- premere il boost per aumentare la velocità senza permettere al client di imporre una velocità arbitraria.

Il target principale di accettazione è Android fisico. Il desktop resta il percorso rapido di debug e deve riprodurre la stessa simulazione.

## 2. Correzioni preliminari allo Step 1

Prima di sviluppare:

1. aprire `SlitherClone.sln` in Visual Studio 2026 e verificare assenza di migrazioni/errori;
2. provare manualmente su Android: avvio → Home → attesa di almeno 10 secondi → ritorno all'app;
3. verificare che la simulazione non recuperi il tempo sospeso con un salto;
4. clonare preferibilmente il repository in `C:\Dev\SlitherClone` o altro percorso locale non sincronizzato;
5. compilare e rieseguire i 7 test esistenti dopo lo spostamento;
6. registrare l'esito nel report dello Step 1 senza riscriverne la cronologia.

Il blocco `EmbedAssembliesIntoApk=true` può restare per produrre un APK Debug autonomo via ADB. Se rallenta sensibilmente l'iterazione da Visual Studio, creare configurazioni distinte (`DebugDeploy` autonomo e `DebugFast` con fast deployment) senza cambiare il comportamento applicativo.

## 3. Scope dello Step 2

### Incluso

- joystick virtuale grafico sul lato sinistro;
- pulsante boost sul lato destro;
- multitouch reale e gestione dei pointer ID;
- adattatore tastiera desktop equivalente;
- simulazione autorevole locale della testa;
- corpo iniziale e follower chain;
- camera centrata sulla testa;
- rendering custom di arena, Slither, dot e controlli;
- arena circolare;
- generazione/attivazione spaziale dei dot;
- raccolta dei dot;
- crescita costante per dot;
- reset semplice della partita per uscita dall'arena o comando debug;
- metriche di debug essenziali.

### Escluso

- multiplayer e trasporto di rete;
- prediction/reconciliation;
- altri serpenti o bot;
- collisione testa-corpo e morte competitiva;
- consumo di massa durante il boost;
- economia, monete, livelli e upgrade;
- menu principale, account e schermate commerciali;
- skin, glow e shader grafici definitivi;
- suoni;
- restringimento temporale dell'arena.

## 4. Sequenza di implementazione obbligatoria

Lo Step 2 va sviluppato in quattro incrementi separati. Ogni incremento deve compilare e risultare utilizzabile; evitare una singola modifica monolitica.

### Step 2A — Input mobile definitivo

Implementare soltanto controlli, visualizzazione e produzione di `PlayerCommand`, mantenendo temporaneamente la primitiva dello smoke test.

#### Joystick virtuale

- posizione: parte inferiore sinistra;
- base fissa, dimensionata rispetto al lato corto del viewport;
- knob mobile entro un raggio massimo;
- zona di attivazione più ampia del cerchio visibile;
- dead zone centrale;
- uscita normalizzata `[-1, +1]` per X/Y;
- nessuna dipendenza dalla risoluzione o dai pixel fisici;
- il pointer che attiva il joystick ne conserva il controllo fino al rilascio;
- un secondo dito non deve rubare il joystick;
- al rilascio il knob torna al centro e l'ultima direzione valida dello Slither resta invariata.

Default iniziali, tutti configurabili:

| Parametro | Valore iniziale |
|---|---:|
| raggio base | 10% del lato corto |
| raggio knob | 42% del raggio base |
| dead zone | 15% del raggio base |
| margine sinistro/inferiore | 5% del lato corto |
| alpha base inattiva | 0,30 |
| alpha base attiva | 0,50 |

#### Pulsante boost

- posizione: parte inferiore destra;
- area touch circolare leggermente maggiore della grafica;
- stato booleano `Boost=true` finché il pointer assegnato resta premuto;
- feedback grafico immediato alla pressione;
- utilizzabile contemporaneamente al joystick;
- nessun toggle: è un controllo momentaneo.

Default iniziali:

| Parametro | Valore iniziale |
|---|---:|
| raggio pulsante | 8% del lato corto |
| margine destro/inferiore | 6% del lato corto |
| alpha inattivo | 0,35 |
| alpha premuto | 0,70 |

#### Safe area e coordinate

Creare un `ScreenLayout` o equivalente che trasformi safe area, viewport e densità in rettangoli/cerchi di controllo. Il layout deve essere ricalcolato su resize, cambio orientamento e ripresa Android.

Separare rigorosamente:

```text
touch in coordinate schermo
        ↓
VirtualControls
        ↓
direzione normalizzata + boost
        ↓
PlayerCommand
```

Non inviare coordinate touch assolute alla simulazione.

#### Desktop

- WASD o frecce producono la stessa direzione normalizzata;
- Spazio produce `Boost`;
- i controlli virtuali possono essere visibili in una modalità debug mobile-preview;
- nessun ramo di simulazione specifico per desktop.

### Step 2B — Testa e body chain

Sostituire la primitiva dello smoke test con lo Slither.

#### Stato autorevole minimo

```csharp
SnakeState
{
    Position HeadPosition;
    UnitVector Heading;
    double CurrentSpeed;
    IReadOnlyList<BodyNode> Body;
    double TargetLength;
}
```

Usare tipi neutrali definiti in `Slither.Core`/`Slither.Protocol`, non `Vector2` di MonoGame nei progetti neutrali.

#### Movimento della testa

Per ogni tick fisso:

1. normalizzare la direzione richiesta, ignorandola se dentro la dead zone;
2. calcolare l'errore angolare minimo fra heading corrente e target;
3. limitare la variazione con `MaxTurnRate * fixedDeltaTime`;
4. scegliere `BaseSpeed` oppure `BoostSpeed` nella simulazione;
5. aggiornare la posizione della testa;
6. aggiornare la chain;
7. eseguire raccolta dot e controllo bordo.

Parametri iniziali indicativi, da concentrare in una singola configurazione:

| Parametro | Valore iniziale |
|---|---:|
| tick simulazione | 60 Hz |
| velocità normale | 5 unità/s |
| velocità boost | 8 unità/s |
| rateo massimo di sterzata normale | 200°/s |
| rateo massimo durante boost | 170°/s |
| raggio testa | 0,50 unità |
| raggio corpo | 0,46 unità |
| distanza nominale nodi | 0,55 unità |
| nodi iniziali del corpo | 5 |

Questi numeri non sono bilanciamento definitivo. Devono permettere una prima misura oggettiva del feeling e poter essere modificati in un solo punto.

#### Dinamica della chain

La testa non deve trascinare segmenti rigidamente ruotati. Ogni nodo segue quello precedente tramite vincolo di distanza applicato lungo la congiungente:

```text
delta = previous.position - current.position
distance = length(delta)
if distance > spacing:
    current.position += normalize(delta) * (distance - spacing)
```

Applicare i follower in ordine dalla testa alla coda a ogni tick. Questo fa sì che ciascun segmento segua una scorciatoia leggermente interna nelle curve e realizza la dinamica di filtro passa-basso discussa. Deve essere verificato in particolare il comportamento durante un cerchio mantenuto: la catena deve compattarsi verso l'interno in modo progressivo, senza oscillazioni o segmenti separati.

Se un singolo passaggio produce instabilità visibile alle velocità previste, usare 2–3 iterazioni del constraint per tick oppure una correzione esponenziale controllata. Non introdurre un motore fisico.

#### Condizione iniziale

- una testa;
- 5 nodi corporei visibili, parametro configurabile a 6;
- nodi inizializzati dietro la testa lungo l'heading, già alla distanza nominale;
- nessuna fase di “srotolamento” all'avvio.

#### Rendering dello Slither

Prima versione volutamente semplice:

- testa circolare distinta;
- nodi del corpo come dischi sovrapposti o geometria instanziata;
- colore corpo uniforme;
- occhi o direzione della testa opzionali ma utili alla leggibilità;
- nessuna texture obbligatoria;
- rendering derivato esclusivamente dallo snapshot.

Il renderer può produrre una mesh procedurale/istanze. Non introdurre `SpriteBatch` come renderer principale per comodità temporanea.

### Step 2C — Camera e arena

#### Camera

La testa deve essere sempre al centro dell'area di gioco, non necessariamente al centro dell'intero framebuffer se la safe area introduce offset. La trasformazione fondamentale è:

```text
screenPosition = gameplayCenter + (worldPosition - headPosition) * pixelsPerWorldUnit
```

I controlli UI sono disegnati in screen space dopo il mondo e non subiscono la trasformazione camera.

Requisiti:

- nessun movimento simulativo per “tenere ferma” la testa: è solo la camera a compensare;
- scala iniziale costante;
- niente zoom dinamico in questo step;
- interpolare la posizione visualizzata fra snapshot consecutivi per evitare micro-scatti;
- il centro della camera usa la posizione interpolata della testa.

#### Arena circolare

- centro mondo iniziale `(0,0)`;
- raggio configurabile, inizialmente molto maggiore dell'area visibile (suggeriti 100 world units);
- bordo disegnato soltanto quando entra nel frustum/area visibile;
- sfondo interno distinto dall'esterno;
- nessuna allocazione proporzionale all'intera superficie dell'arena;
- spawn iniziale vicino al centro, con margine ampio dal bordo.

Regola provvisoria al bordo: lo Slither non può oltrepassare il limite utile `ArenaRadius - HeadRadius`. Per Step 2 applicare una correzione di posizione e ruotare gradualmente l'heading verso l'interno, oppure resettare la sessione in modalità debug. Non implementare ancora morte/drop. La scelta deve essere documentata e facilmente sostituibile.

### Step 2D — Dot, raccolta e crescita

#### Distribuzione spaziale

Non pre-generare tutti i dot dell'arena. Dividere il mondo in celle quadrate e attivare/generare le celle in un raggio attorno alla testa.

Requisiti:

- generazione deterministica per `WorldSeed + cellX + cellY`;
- densità media uniforme configurabile;
- solo celle che intersecano l'arena circolare;
- un piccolo cluster iniziale garantito nell'area di spawn per rendere il test immediato;
- quando lo Slither entra in nuove zone, le celle vengono generate prima di diventare visibili;
- ogni dot possiede un ID stabile durante la sessione;
- un dot raccolto non deve ricomparire scaricando e ricaricando la cella;
- il renderer riceve soltanto i dot visibili o vicini, non l'intero database dell'arena.

La comparsa dei dot non deve essere percepibile a schermo. Il raggio di attivazione deve superare il raggio visibile della camera di almeno una cella.

Default suggeriti:

| Parametro | Valore iniziale |
|---|---:|
| lato cella | 10 unità |
| dot medi per cella | 12 |
| raggio dot | 0,12–0,20 unità |
| raggio raccolta | `HeadRadius + DotRadius` |
| cluster iniziale | 20–30 dot entro 12 unità dallo spawn |

#### Raccolta

La raccolta appartiene alla simulazione, mai al renderer. Per ogni tick verificare soltanto le celle spaziali vicine alla testa. Non eseguire un ciclo su tutti i dot dell'arena.

Alla collisione:

1. marcare il dot come raccolto;
2. rimuoverlo dallo snapshot successivo;
3. incrementare `TargetLength` di un valore costante `GrowthPerDot`;
4. aggiornare un contatore debug dei dot raccolti.

#### Crescita costante

Ogni dot deve aggiungere esattamente la stessa lunghezza fisica. Valore iniziale suggerito:

```text
GrowthPerDot = 0,20 × BodySpacing
```

La crescita non deve dipendere da colore, dimensione grafica o posizione del dot. Accumulare la crescita frazionaria; quando è disponibile abbastanza lunghezza per un nuovo nodo, aggiungerlo in coda. Per non produrre scatti visivi, il tratto terminale può essere interpolato o scalato in funzione della frazione accumulata.

In futuro il reward economico potrà variare, ma resterà separato dalla crescita fisica.

## 5. Contratti e snapshot

Estendere `PlayerCommand` soltanto se necessario senza aggiungere velocità imposta dal client:

```csharp
public readonly record struct PlayerCommand(
    uint Sequence,
    double TargetDirectionX,
    double TargetDirectionY,
    bool HasDirection,
    bool Boost);
```

`HasDirection=false` significa “mantieni la direzione corrente”, utile quando il joystick è nella dead zone o non è premuto.

Lo snapshot minimo dovrebbe esporre dati immutabili equivalenti a:

```text
WorldSnapshot
├── Tick
├── Arena(center, radius)
├── Snake
│   ├── head position
│   ├── heading
│   ├── radius
│   ├── body positions/radii
│   └── boost state
├── VisibleDots
└── DebugCounters (solo build debug)
```

Non serializzare ancora. Evitare riferimenti a oggetti mutabili della simulazione. Per ridurre allocazioni per frame, è ammesso usare buffer snapshot riutilizzabili/double-buffered, purché il renderer non possa osservare una mutazione concorrente.

## 6. Rendering custom

Pipeline consigliata:

```text
WorldSnapshot
      ↓
visibility/camera transform
      ↓
CPU instance data
      ↓
dynamic vertex/instance buffer
      ↓
shader MonoGame
      ↓
arena → dot → corpo → testa → UI
```

Per Step 2 privilegiare chiarezza e compatibilità Android. L'instancing è desiderabile ma non obbligatorio se complica il backend OpenGL; il renderer deve però essere strutturato in modo da poterlo introdurre. Evitare una draw call per ogni dot se è possibile raggrupparli.

Ordine di rendering:

1. fondo esterno arena;
2. superficie interna arena;
3. bordo arena se visibile;
4. dot;
5. corpo dalla coda verso la testa;
6. testa;
7. controlli UI e debug overlay.

## 7. Test automatici

Conservare i test dello Step 1 e aggiungere almeno:

- joystick: dead zone, clamp al raggio massimo e normalizzazione;
- assegnazione indipendente di due pointer a joystick e boost;
- rilascio di un pointer senza perdita dell'altro controllo;
- limite massimo di sterzata per tick;
- velocità decisa dalla simulazione, non dal comando;
- inizializzazione di 5 nodi corporei alla distanza corretta;
- mantenimento della distanza massima fra nodi;
- chain stabile dopo migliaia di tick e durante traiettoria circolare;
- trasformazione camera: testa nel centro gameplay entro tolleranza numerica;
- celle generate deterministicamente dallo stesso seed;
- assenza di dot fuori dall'arena;
- un dot raccolto non ricompare;
- crescita totale dopo N dot uguale a `N × GrowthPerDot` entro tolleranza;
- nessuna dipendenza MonoGame/Android aggiunta a Core/Protocol.

## 8. Overlay diagnostico Debug

Mostrare, con possibilità di disattivazione:

- render FPS;
- simulation tick rate;
- posizione mondo della testa;
- heading e target heading;
- stato boost;
- numero nodi corpo e target length;
- cella corrente;
- celle attive;
- dot visibili e raccolti;
- draw call, se facilmente disponibile.

Il debug overlay non deve influenzare la simulazione.

## 9. Criteri di accettazione per incremento

### Step 2A

- [ ] Joystick visibile e leggibile sul lato sinistro.
- [ ] Boost visibile sul lato destro.
- [ ] Due dita comandano simultaneamente direzione e boost.
- [ ] Nessun salto del joystick quando viene aggiunto/rimosso il secondo dito.
- [ ] Layout corretto su Xiaomi 15T landscape e dopo ripresa.
- [ ] `PlayerCommand` contiene intenzione, non velocità.

### Step 2B

- [ ] Una testa e 5 parti corporee sono visibili all'avvio.
- [ ] Sterzata fluida e limitata.
- [ ] Boost aumenta la velocità solo mentre il pulsante è premuto.
- [ ] La chain non si separa e non vibra.
- [ ] In una curva mantenuta i follower tagliano progressivamente verso l'interno.
- [ ] Timestep e risultati non dipendono dal render rate.

### Step 2C

- [ ] La testa resta centrata durante movimento normale, boost e curve.
- [ ] Il mondo scorre in modo fluido sotto la testa.
- [ ] L'arena appare circolare quando si raggiunge il bordo.
- [ ] Non viene allocata/renderizzata l'intera arena.
- [ ] Il bordo non permette uscite incontrollate.

### Step 2D

- [ ] Dot presenti attorno allo spawn.
- [ ] Nuove zone contengono dot prima di entrare nel viewport.
- [ ] Nessun pop-in visibile.
- [ ] La raccolta avviene con la testa.
- [ ] Ogni dot incrementa la lunghezza della stessa quantità.
- [ ] I dot raccolti non ricompaiono tornando nella zona.
- [ ] Prestazioni stabili sullo Xiaomi 15T.

## 10. Verifica manuale del feeling

I test automatici non possono validare il comportamento percepito. Sul telefono eseguire e registrare almeno:

1. rettilineo senza toccare il joystick;
2. curve leggere e inversione rapida del joystick;
3. cerchio mantenuto per 10 secondi;
4. pressione/rilascio ripetuta del boost durante una curva;
5. controllo simultaneo con due dita senza cambio di ownership;
6. attraversamento di almeno 5 celle di dot;
7. raccolta di almeno 20 dot e osservazione della crescita;
8. avvicinamento al bordo dell'arena;
9. Home → 10 secondi → ripresa;
10. sessione continua di almeno 5 minuti senza crash o crescita anomala della memoria.

Parametri da giudicare separatamente:

- dimensione e posizione joystick;
- dead zone;
- sensibilità percepita;
- velocità normale;
- rapporto boost/base speed;
- rateo di sterzata normale e in boost;
- spaziatura e compattezza del corpo;
- scala della camera;
- densità e dimensione dei dot.

Non correggere contemporaneamente più di una categoria durante il tuning, così da mantenere leggibile la causa del cambiamento.

## 11. Deliverable Codex

Codex deve consegnare:

1. implementazione suddivisa almeno nei quattro incrementi 2A–2D;
2. test automatici aggiornati e tutti superati;
3. build Desktop e Android Debug riuscite senza warning nuovi ingiustificati;
4. APK autonomo installato sullo Xiaomi 15T;
5. breve video o sequenza di screenshot della prova manuale, se disponibile;
6. tabella dei parametri effettivamente usati;
7. report con esito di ogni criterio di accettazione;
8. elenco dei problemi osservati nel feeling, senza nasconderli dietro modifiche non documentate.

## 12. Istruzione sintetica da passare a Codex

> Implementa lo Step 2 in quattro incrementi verificabili: controlli multitouch, cinematica dello Slither, camera/arena e dot/crescita. Mantieni la pipeline `PlayerCommand → LocalSimulationEndpoint → WorldSnapshot → custom renderer`. Il client deve produrre soltanto direzione desiderata e boost; velocità e sterzata sono autorevoli nella simulazione. Parti con una testa e 5 nodi corporei, mantieni la testa al centro tramite camera e usa una follower chain a vincolo di distanza che tagli internamente le curve. Genera i dot deterministicamente per celle attorno alla zona esplorata e applica una crescita fisica costante per dot. Non introdurre multiplayer, economia, menu o grafica definitiva. Dopo ogni incremento compila, testa e verifica sullo Xiaomi 15T.

