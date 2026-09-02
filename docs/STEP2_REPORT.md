# Slither Clone — Report conclusivo Step 2

**Data:** 2 settembre 2026

**Stato:** implementato e validato su desktop e Xiaomi 15T

**Specifica di riferimento:** `STEP2_SPECIFICATION.md`

## 1. Risultato raggiunto

Lo Step 2 ha trasformato lo smoke test dello Step 1 in un primo nucleo di gioco locale e realmente utilizzabile.

La stessa simulazione condivisa ora consente di:

- controllare lo Slither con joystick virtuale e boost su Android;
- usare tastiera e mouse nella versione desktop di sviluppo;
- muoversi con sterzata limitata e corpo a catena;
- esplorare un'arena circolare molto più grande del viewport;
- mantenere la testa al centro tramite camera interpolata;
- raccogliere dot generati deterministicamente per celle;
- ottenere energia, punteggio e crescita progressiva;
- utilizzare il boost con accelerazione e decelerazione graduali;
- eseguire il gioco in fullscreen immersivo alla risoluzione nativa dello Xiaomi 15T;
- mantenere prestazioni misurate tra 118 e 120 FPS sul dispositivo dopo il caricamento iniziale.

Multiplayer, bot, collisioni competitive, morte, economia e networking non sono stati introdotti e restano fuori dallo Step 2.

## 2. Incrementi realizzati

### Step 2A — Controlli

Sono stati realizzati:

- joystick virtuale sinistro con knob, dead zone e area di attivazione estesa;
- pulsante boost destro momentaneo;
- ownership indipendente dei pointer per il multitouch;
- uso simultaneo di joystick e boost;
- adattatore desktop con WASD/frecce e barra spaziatrice;
- layout proporzionale al lato corto del viewport;
- riposizionamento ergonomico dei controlli verso l'interno e verso l'alto;
- riduzione delle dimensioni dei controlli rispetto ai primi prototipi;
- indicatore centrale della dimensione dello Slither.

Il flusso resta separato correttamente:

```text
touch / mouse / tastiera
        ↓
VirtualControls + PlatformCommandSource
        ↓
PlayerCommand
        ↓
simulazione locale autorevole
```

### Step 2B — Slither e movimento

Sono stati implementati:

- testa e 5 segmenti iniziali;
- heading corrente e heading desiderato;
- limite alla velocità angolare;
- catena di follower con vincolo di distanza;
- più iterazioni del constraint per stabilizzare il corpo;
- interpolazione tra snapshot per il rendering;
- velocità normale determinata dalla simulazione;
- boost massimo pari a tre volte la velocità normale;
- accelerazione e decelerazione progressive del boost;
- correzione graduale della direzione quando si raggiunge il limite dell'arena.

La crescita è stata modificata durante il tuning. Il nuovo segmento non viene più aggiunto dietro la coda già distanziato. Nasce nella stessa posizione dell'ultimo segmento e diventa visibile soltanto mentre il movimento propaga lo spazio lungo la catena. Questo evita la comparsa improvvisa della coda sopra altri futuri Slither o ostacoli.

### Step 2C — Camera, arena e sfondo

Sono stati realizzati:

- camera centrata sulla posizione interpolata della testa;
- scala del mondo costante;
- arena circolare di raggio molto maggiore della zona visibile;
- superficie esterna rossa non attraversabile;
- bordo dell'arena renderizzato soltanto quando entra nell'area visibile;
- sfondo ripetuto e specchiato tramite `BackGround.jpg` incorporato nell'assembly;
- sostituzione dell'immagine di sfondo mantenendo lo stesso nome della risorsa.

Non viene allocata una texture grande quanto l'arena: il background è ripetuto nello spazio del mondo.

### Step 2D — Dot, energia, punteggio e crescita

Sono stati realizzati:

- suddivisione spaziale in celle;
- generazione deterministica tramite seed e coordinate della cella;
- ID stabili per i dot;
- rimozione permanente dei dot raccolti durante la sessione;
- generazione dinamica con maggiore concentrazione nella zona dello Slither;
- collisione eseguita nella simulazione e limitata alle celle vicine;
- tre classi di dot da 1, 2 e 3 energie;
- dimensione grafica crescente con l'energia;
- palette di 10 colori chiari e saturi;
- crescita di un segmento ogni 5 energie;
- punteggio continuo, separato dall'energia residua.

Il valore mostrato parte da 50 e cresce immediatamente di 1, 2 o 3 in base al dot raccolto. La creazione di un segmento ogni 5 punti non produce salti nel punteggio.

## 3. Parametri finali

### Simulazione dello Slither

| Parametro | Valore finale |
|---|---:|
| tick simulazione | 60 Hz |
| velocità normale | 3,0 unità/s |
| velocità massima boost | 9,0 unità/s |
| accelerazione boost | 18,0 unità/s² |
| decelerazione boost | 24,0 unità/s² |
| tempo indicativo 3 → 9 | 0,33 s |
| tempo indicativo 9 → 3 | 0,25 s |
| sterzata massima normale | 260°/s |
| sterzata massima boost | 230°/s |
| raggio testa | 0,23684375 unità |
| raggio segmento | 0,23684375 unità |
| diametro desktop 1280×720 | circa 28,4 px |
| distanza nominale segmenti | 0,15 unità |
| segmenti corporei iniziali | 5 |
| iterazioni constraint | 2 |
| raggio arena | 100 unità |
| velocità di correzione al bordo | 320°/s |

Testa e segmenti hanno la stessa dimensione. La distanza tra i centri è sensibilmente inferiore al diametro, quindi i dischi risultano fortemente sovrapposti.

### Dot e distribuzione

| Parametro | Valore finale |
|---|---:|
| lato cella | 10 unità |
| dot per cella | 27 |
| raggio celle attive | 2 celle |
| raggio dot da 1 energia | 0,13 unità |
| raggio dot da 2 energie | 0,195 unità |
| raggio dot da 3 energie | 0,27 unità |
| probabilità dot da 2 | 24% |
| probabilità dot da 3 | 9% |
| intervallo generazione dinamica | 2 s |
| dot dinamici per intervallo | 4 |
| distanza generazione dinamica | 3–9 unità |
| energia per nuovo segmento | 5 |
| dot normalmente visibili | circa 674–682 su Xiaomi 15T |

### Controlli virtuali

| Parametro | Valore finale |
|---|---:|
| raggio joystick | 7,5% del lato corto |
| raggio knob | 42% del raggio joystick |
| dead zone | 15% del raggio joystick |
| centro joystick dal lato sinistro | 25,5% del lato corto |
| centro joystick dal fondo | 25% del lato corto |
| raggio boost | 6,5% del lato corto |
| centro boost dal lato destro | 24,5% del lato corto |
| centro boost dal fondo | 25% del lato corto |

## 4. Rendering finale

### Slither

La resa dello Slither usa geometria custom MonoGame:

- bordo grigio-blu esterno;
- illuminazione decentrata per dare volume ai dischi;
- ombra grigio scuro lungo la sagoma complessiva;
- testa della stessa dimensione dei segmenti;
- due sclere proporzionali alla testa;
- pupille nere spostate diagonalmente di 45° verso avanti e verso il centro;
- colore della testa modificato durante il boost.

### Dot

La resa finale usa tre livelli:

1. sottile circonferenza nera esterna;
2. base opaca scura che impedisce allo sfondo di trasparire;
3. luce radiale colorata in fusione additiva.

La fusione avviene tra i dot, non direttamente con il background. Quando dot di colori diversi si sovrappongono, i canali luminosi si sommano e la concentrazione tende al bianco, rendendo leggibili le zone ad alta densità.

### Punteggio

Il punteggio è disegnato in giallo nella parte inferiore centrale. Le cifre sono vettoriali e usano segmenti con terminali arrotondati, senza dipendere da font o asset specifici della piattaforma.

## 5. Ottimizzazione grafica dei dot

La prima versione della resa radiale generava molti anelli e triangoli per ciascun dot, oltre a creare liste e array temporanei a ogni frame. Con circa 680 dot e il framebuffer nativo 2772×1280, sullo smartphone era percepibile un leggero scatto.

Il renderer è stato riscritto usando:

- texture radiali 64×64 generate una volta all'avvio;
- atlas contenente le 10 varianti cromatiche;
- quad da due triangoli per ogni livello del dot;
- tre livelli e quindi 6 triangoli complessivi per dot;
- buffer CPU ridimensionabili e riutilizzati;
- nessuna conversione `List.ToArray()` per frame;
- una draw call raggruppata per livello.

Dopo l'ottimizzazione, il log Android ha mostrato stabilmente 118–120 FPS con circa 674–682 dot visibili.

## 6. Android e fullscreen

Sul progetto Android sono stati aggiunti:

- fullscreen immersive sticky;
- rimozione di status bar, ora, batteria e barra di navigazione;
- barre richiamabili temporaneamente tramite swipe, come previsto da Android;
- supporto alle display cutout tramite modalità `ShortEdges`;
- layout edge-to-edge;
- orientamento landscape sensibile ai due lati;
- backbuffer impostato alle dimensioni fisiche del display;
- gestione sicura di `OnActivated` prima e dopo il caricamento dei contenuti.

Il precedente backbuffer fisso 1280×720 produceva bande laterali sul display 2772×1280. L'uso delle dimensioni native ha eliminato il letterboxing.

## 7. Problemi incontrati e soluzioni

| Problema | Causa | Soluzione |
|---|---|---|
| barre Android, ora e batteria visibili | modalità fullscreen incompleta | immersive mode, edge-to-edge e hide dei system bars |
| bande laterali sullo Xiaomi | backbuffer fisso 16:9 | backbuffer nativo 2772×1280 |
| crash durante attivazione Android | callback prima della creazione del `GraphicsDevice` | guardia `_contentLoaded` |
| punteggio fermo tra una crescita e l'altra | valore calcolato solo dai segmenti | totale energia cumulativo e `SnakeSnapshot.Size` |
| crescita istantanea dalla coda | nuovo nodo creato già distanziato | nuovo nodo sovrapposto alla coda e separazione tramite movimento |
| trasparenza dei dot inefficace | alpha non premoltiplicato con `AlphaBlend` | correzione del formato colore durante i prototipi |
| dot sfocati sul background | dissolvenza applicata direttamente sul mondo | base opaca separata dalla luce additiva |
| sovrapposizioni non leggibili | blending standard | fusione additiva limitata al livello luminoso dei dot |
| leggero scatto su smartphone | centinaia di migliaia di triangoli e allocazioni per frame | atlas procedurale, quad e buffer riutilizzati |
| primo APK ottimizzato rifiutato | pacchetto incompleto/non firmato nella prima esecuzione | target esplicito `SignAndroidPackage` e verifica `.SF`/`.RSA` |

## 8. Verifiche eseguite

### Automatiche

La suite contiene 27 test, tutti superati. Copre almeno:

- fixed timestep e protezione dalle pause;
- confini architetturali di Core e Protocol;
- sequenza dei comandi;
- layout e safe area dei controlli;
- dead zone, clamp e normalizzazione del joystick;
- ownership indipendente dei pointer;
- limite di sterzata;
- follower chain stabile durante simulazioni lunghe;
- rapporto boost 3×;
- accelerazione e decelerazione del boost;
- contenimento nell'arena;
- crescita ogni 5 energie;
- punteggio continuo;
- nascita del nuovo segmento sovrapposta alla coda;
- separazione del segmento soltanto dopo il movimento;
- determinismo dei dot;
- dot interni all'arena;
- permanenza della raccolta;
- tre classi energetiche con raggi crescenti.

### Build

| Target | Esito |
|---|---|
| Desktop Debug | superato, 0 errori e 0 warning |
| Test net9.0 | 27/27 superati |
| Android Debug firmato | superato |
| Integrità APK | verificata come archivio ZIP |
| Firma APK | presenti `META-INF/ANDROIDD.SF` e `.RSA` |

### Dispositivo Android

| Voce | Esito |
|---|---|
| dispositivo | Xiaomi 15T, modello ADB `25069PTEBG` |
| risoluzione framebuffer | 2772×1280 landscape |
| installazione incrementale | superata |
| avvio launcher | superato |
| focus finestra | corretto |
| fullscreen | confermato |
| crash/eccezioni non gestite | nessuno rilevato |
| prestazioni dopo warm-up | 118–120 FPS |
| dot visibili durante la misura | circa 674–682 |

L'ultimo APK firmato misurava 45.859.580 byte, circa 43,7 MiB.

## 9. Stato dei criteri di accettazione

| Area | Stato |
|---|---|
| controlli virtuali e multitouch | implementati e provati sul dispositivo |
| input desktop equivalente | implementato |
| simulazione indipendente dalla piattaforma | confermata |
| Slither iniziale e follower chain | implementati |
| sterzata limitata | implementata |
| boost autorevole | implementato con rampa |
| camera centrata | implementata |
| arena circolare e limite | implementati |
| background ripetuto | implementato |
| dot deterministici per celle | implementati |
| raccolta e non ricomparsa | implementate |
| punteggio e crescita | implementati |
| prestazioni stabili sullo Xiaomi | confermate |
| Home → attesa → ripresa | da mantenere come prova manuale periodica |
| sessione continua di 5 minuti | consigliata prima di chiudere definitivamente il prototipo |

## 10. Variazioni rispetto alla specifica iniziale

Durante il tuning sono state approvate queste variazioni:

- velocità normale ridotta a 3 unità/s;
- boost portato a 3× con rampa progressiva;
- Slither più piccolo e segmenti più sovrapposti rispetto ai valori iniziali della specifica;
- crescita visuale ogni 5 energie;
- valore dei dot differenziato in 1, 2 e 3 energie;
- densità dei dot aumentata sensibilmente;
- grafica di Slither, occhi, ombre, background e dot sviluppata oltre la primitiva minima prevista;
- controlli ridotti e riposizionati usando come riferimento ergonomico il gioco originale;
- fullscreen Android completato e adattato al rapporto dello Xiaomi 15T.

Queste modifiche non alterano i confini architetturali previsti: input, simulazione, snapshot e rendering restano separati.

## 11. Stato Git al momento del report

Le modifiche dello Step 2 successive al commit `3b50d88` sono presenti nel working tree ma non sono ancora state consolidate in un nuovo commit e non sono state inviate al repository remoto.

Prima di iniziare lo Step 3 è consigliato:

1. escludere dal commit gli screenshot diagnostici in `artifacts/`, oppure aggiungere la cartella a `.gitignore`;
2. rieseguire un controllo finale di `git diff`;
3. creare un commit conclusivo dello Step 2;
4. eseguire il push su `main`.

## 12. Elementi da decidere nella pianificazione dello Step 3

Lo Step 2 lascia pronti alcuni confini utili per il passo successivo. La pianificazione dovrà scegliere esplicitamente priorità e ordine tra:

- presenza di più Slither nella stessa arena;
- bot locali per verificare collisioni e densità;
- collisione testa-corpo e corpo-corpo;
- morte, eliminazione e rilascio di dot;
- consumo di massa durante il boost;
- identificazione visiva, colori e skin degli Slither;
- leaderboard e informazioni HUD;
- separazione tra endpoint locale e futuro endpoint di rete;
- eventuale prediction/reconciliation soltanto dopo aver stabilizzato le regole locali;
- test di carico con molti Slither e dot sul dispositivo Android.

La raccomandazione è sviluppare prima le regole competitive in locale con bot deterministici, mantenendo il networking fuori dal primo incremento dello Step 3. In questo modo collisioni, morte e drop possono essere validate senza confondere errori di simulazione con problemi di rete.
