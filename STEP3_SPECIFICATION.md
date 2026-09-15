# Slither Clone — Specifica operativa Step 3

## Popolazione locale, collisioni competitive, morte, drop e respawn

**Data:** 3 settembre 2026  
**Destinatario:** Codex incaricato dell'implementazione  
**Prerequisito:** Step 2 completato e validato su desktop e Xiaomi 15T  
**Obiettivo:** trasformare il prototipo con un solo Slither in una prima arena locale competitiva, sufficiente a giudicare se l'interazione fra più Slither è leggibile e divertente.

---

## 1. Principio di sviluppo

Procedere rigorosamente bottom-up. Lo Step 3 non deve introdurre:

- networking;
- server remoto;
- progressione persistente;
- economia;
- menu complessi;
- nuove skin;
- particelle o rifiniture grafiche;
- dot negativi o nuove regole speculative.

La grafica dello Step 2 deve rimanere sostanzialmente congelata. Sono consentite soltanto modifiche minime indispensabili per distinguere gli Slither, mostrare morte/respawn e diagnosticare la simulazione.

La domanda a cui questo step deve rispondere è:

> Più Slither che condividono la stessa arena producono incontri, collisioni, uccisioni e raccolte del bottino interessanti con la cinematica già realizzata?

## 2. Risultato finale atteso

Al termine dello Step 3:

- il giocatore condivide l'arena con 10–20 Slither automatici;
- tutti gli Slither usano lo stesso `SnakeState`, le stesse regole e lo stesso integratore;
- ogni entità riceve un `PlayerCommand`, prodotto dal joystick oppure da un bot;
- i bot si muovono, raccolgono dot, crescono e possono usare il boost;
- la testa che tocca il corpo di un altro Slither muore;
- le collisioni sono risolte simultaneamente e non dipendono dall'ordine dell'array;
- lo Slither morto viene rimosso e parte della massa acquisita viene convertita in dot;
- giocatore e bot effettuano respawn in posizioni sicure;
- l'arena continua a funzionare per almeno 15 minuti senza crescita incontrollata di massa, memoria o numero di entità;
- è possibile aumentare la popolazione a 50 e 100 bot per misurare prestazioni e scalabilità.

## 3. Preflight e congelamento dello Step 2

Prima di modificare il codice:

1. eseguire tutti i 27 test;
2. compilare Desktop Debug e Android Debug;
3. completare la prova Android `Home → 10 s → ripresa`;
4. eseguire una sessione Step 2 continua di almeno 5 minuti;
5. annotare il commit esatto corrispondente all'APK validato;
6. creare un tag Git `step2-complete`, se autorizzato;
7. registrare i parametri cinematici correnti come baseline non modificabile durante lo Step 3.

Non ritoccare velocità, sterzata, follower chain, densità grafica o controlli durante l'introduzione dei bot. Se emerge un problema, prima stabilire se è prodotto dalla nuova logica multi-Slither.

## 4. Correzione concettuale preliminare: crescita e reward

Il codice dello Step 2 usa dot da 1, 2 e 3 energie e crea un segmento ogni 5 energie. Il progetto aveva però stabilito che ogni dot deve produrre la stessa crescita fisica; valore economico e punteggio sono concetti separati.

Prima di introdurre morte e drop, separare almeno:

```text
GrowthMass      crescita fisica dello Slither
MatchScore      punteggio della vita/partita
RewardValue     valore economico futuro, non ancora utilizzato
```

Regola Step 3:

```text
ogni dot raccolto → GrowthMass += GrowthPerDot costante
                  → MatchScore += DotScoreValue
```

Le tre dimensioni visive dei dot possono rimanere e assegnare punteggi diversi, ma non devono moltiplicare la crescita fisica. Se si decide esplicitamente di mantenere il comportamento attuale, Codex deve isolarlo dietro una policy configurabile e segnalarlo nel report; non deve lasciare crescita e punteggio implicitamente accoppiati.

## 5. Architettura multi-Slither

### 5.1 Stato del mondo

Estendere la simulazione da un singolo Slither a una collezione autorevole:

```text
WorldState
├── Tick
├── Arena
├── SnakesById
├── DotField
├── SpatialIndex
├── PendingEvents
└── Metrics
```

Ogni Slither deve avere almeno:

```csharp
SnakeId Id;
SnakeGeneration Generation;
SnakeControllerKind ControllerKind;
SnakeLifeState LifeState;
SnakeState MotionAndBody;
SnakeStatistics Statistics;
```

`SnakeId` identifica lo slot logico; `Generation` cambia a ogni respawn e impedisce che eventi o collisioni vecchie vengano applicati alla nuova vita della stessa entità.

### 5.2 Un'unica simulazione per tutti

È vietato creare una classe fisica semplificata per i bot. Il giocatore e i bot devono attraversare la stessa pipeline:

```text
HumanCommandSource ─┐
                    ├→ PlayerCommand → SnakeSimulation
BotCommandSource ───┘
```

Le sole differenze ammesse sono:

- origine del comando;
- identificatore;
- colore provvisorio;
- statistiche e stato vita.

Velocità, sterzata, boost, chain, raccolta, crescita, collisioni e bordo devono essere identici.

### 5.3 Ordine del tick

Usare un ordine esplicito e stabile:

1. acquisizione di tutti i comandi per il tick;
2. aggiornamento cinematico di tutte le teste;
3. aggiornamento di tutte le chain;
4. costruzione/aggiornamento dell'indice spaziale;
5. rilevamento raccolta dot;
6. rilevamento collisioni fra Slither;
7. produzione di eventi di morte senza mutare immediatamente le collezioni;
8. risoluzione simultanea delle morti;
9. creazione dei drop;
10. aggiornamento timer di respawn;
11. cattura dello snapshot.

Non rimuovere o respawnare entità mentre si sta iterando la collezione delle collisioni.

## 6. Incrementi obbligatori

Implementare e verificare separatamente gli incrementi 3A–3D. Ogni incremento deve compilare, conservare tutti i test precedenti e produrre un commit isolabile.

## 6A. Più Slither senza collisioni

### Popolazione iniziale

Creare una modalità configurabile `InteractionTest` con:

- 1 Slither umano;
- 10 bot come valore iniziale;
- selettore Debug per 20, 50 e 100 bot;
- spawn iniziale dei bot in una corona fra 8 e 25 unità dal giocatore;
- distanza minima fra le teste al momento dello spawn;
- nessun bot generato fuori dal limite utile dell'arena.

La concentrazione attorno al giocatore è una modalità di test, non una regola definitiva. Non far “teletrasportare” continuamente nuovi bot vicino al giocatore durante la vita.

### AI minima `WanderBot`

Il primo bot non deve giocare bene. Deve produrre input plausibili e deterministici:

- mantiene una direzione desiderata per un intervallo casuale configurabile;
- modifica gradualmente la direzione, senza jitter a ogni tick;
- applica repulsione dal bordo prima di raggiungerlo;
- usa il boost occasionalmente per intervalli brevi;
- non conosce direttamente la posizione dei dot;
- raccoglie i dot solo quando li incontra;
- usa un PRNG per-bot derivato da `WorldSeed`, `SnakeId` e `Generation`.

Default iniziali:

| Parametro | Valore suggerito |
|---|---:|
| bot iniziali | 10 |
| intervallo cambio target | 0,7–2,5 s |
| deviazione target | ±35° |
| probabilità inizio boost | 2%/s |
| durata boost | 0,3–1,0 s |
| margine correzione bordo | 12 unità |

Questi valori devono essere configurabili in un punto solo.

### Snapshot e rendering

Lo snapshot deve contenere più Slither, ma soltanto quelli potenzialmente visibili. Il renderer non deve interrogare `WorldState`.

Per distinguerli usare soltanto una palette provvisoria deterministica basata su `SnakeId`. Non creare skin o nuovi effetti.

### Accettazione 3A

- [ ] Il giocatore vede diversi Slither entro pochi secondi.
- [ ] Tutti si muovono con la stessa cinematica.
- [ ] I bot raccolgono dot e crescono con le stesse regole.
- [ ] Nessun bot modifica direttamente posizione o velocità.
- [ ] Lo snapshot contiene soltanto entità visibili/vicine.
- [ ] 20 bot funzionano senza degrado percepibile sullo Xiaomi 15T.

## 6B. Indice spaziale e collisioni

### Geometria logica

Il corpo è renderizzato mediante dischi fortemente sovrapposti. Per le collisioni non usare una semplice collezione di cerchi indipendenti come modello definitivo. Rappresentare ogni coppia di nodi consecutivi come una capsula 2D:

```text
capsule = segment(body[i], body[i+1]) + BodyCollisionRadius
head    = circle(HeadPosition, HeadCollisionRadius)
```

Test stretto testa-capsula:

1. proiettare il centro della testa sul segmento;
2. clampare il parametro nell'intervallo `[0,1]`;
3. calcolare la distanza quadratica dal punto più vicino;
4. collisione se inferiore al quadrato della somma dei raggi.

I raggi di collisione devono essere configurabili separatamente dai raggi grafici, inizialmente uguali o leggermente inferiori per evitare morti percepite come ingiuste.

### Regole provvisorie

- testa contro corpo di un altro Slither: muore la testa;
- testa contro il proprio corpo: nessuna collisione in questo step;
- corpo contro corpo: nessun effetto;
- bordo: conservare la correzione graduale dello Step 2, senza morte;
- testa contro testa: entrambi muoiono nello stesso tick;
- un'entità già marcata morta può comunque causare una morte nello stesso tick, perché la risoluzione è simultanea.

La regola testa-testa deve essere configurabile per eventuale modifica futura.

### Broad phase

Usare una griglia spaziale uniforme distinta o condivisa consapevolmente con quella dei dot. Ogni capsula corporea viene inserita nelle celle intersecate dal proprio AABB espanso del raggio di collisione.

Per ogni testa:

- interrogare soltanto le celle sovrapposte al suo AABB;
- deduplicare i candidati che attraversano più celle;
- ignorare capsule del medesimo `SnakeId`;
- eseguire il narrow phase soltanto sui candidati risultanti.

Non eseguire un confronto completo testa × tutti i segmenti del mondo a regime. È ammesso implementare temporaneamente una versione brute-force soltanto come oracle nei test su mondi piccoli, confrontandone i risultati con l'indice spaziale.

### Risoluzione simultanea

Raccogliere prima:

```csharp
CollisionEvent(
    Tick,
    VictimId,
    VictimGeneration,
    KillerId,
    ContactPoint,
    CollisionKind);
```

Solo dopo avere esaminato tutte le teste, costruire l'insieme delle vittime e applicare le transizioni di stato. Ordinare deterministicamente gli eventi per ID solo per ottenere risultati riproducibili, non per dare priorità.

### Accettazione 3B

- [ ] La testa muore toccando visivamente il corpo altrui.
- [ ] Non muore passando vicino senza contatto percepibile.
- [ ] Chiudere un cerchio non provoca autocollisione.
- [ ] Due collisioni nello stesso tick non dipendono dall'ordine delle entità.
- [ ] Lo scontro testa-testa elimina entrambi.
- [ ] Indice spaziale e oracle brute-force concordano nei test.
- [ ] Sono disponibili contatori di candidati e narrow-phase test per tick.

## 6C. Morte e conversione in dot

### Macchina a stati minima

```text
Alive
  ↓ collisione
Dying
  ↓ risoluzione tick
DeadWaitingRespawn
  ↓ timer
Respawning
  ↓ spawn valido
Alive con Generation incrementata
```

Nessuna animazione elaborata. È sufficiente rimuovere lo Slither nello snapshot successivo e mostrare i dot risultanti.

### Bilancio della massa

La morte e il respawn dei bot non devono creare energia indefinitamente. Separare:

```text
SpawnMass       massa iniziale concessa gratuitamente
AcquiredMass    massa ottenuta raccogliendo dot durante la vita
DropMass        parte di AcquiredMass restituita al mondo
DestroyedMass   sink necessario a stabilizzare l'economia
```

Regola iniziale raccomandata:

```text
DropMass = AcquiredMass × 0,80
DestroyedMass = AcquiredMass × 0,20
SpawnMass non produce dot raccoglibili
```

Per rendere visibile la morte di uno Slither appena nato si possono generare elementi grafici non raccoglibili, ma non nuova massa fisica.

Questa regola deve essere configurabile e accompagnata da metriche di conservazione. Il generatore ambientale dei dot rimane l'unica sorgente netta controllata.

### Distribuzione del drop

- distribuire i dot lungo la polilinea del corpo morto;
- aggiungere jitter piccolo e deterministico;
- conservare complessivamente `DropMass` entro la precisione definita;
- aggregare la massa in un numero ragionevole di dot per non generare migliaia di entità;
- assegnare ID stabili ai nuovi dot;
- inserire immediatamente i dot nelle celle spaziali;
- evitare che i dot compaiano tutti nello stesso punto;
- nessuna particella o esplosione grafica in questo step.

Il valore grafico 1/2/3 può essere usato per impacchettare la massa, purché la somma restituita sia esplicita e verificabile. Se la crescita resta costante per dot, non usare il valore grafico per moltiplicarla: in quel caso il numero di dot emessi, non il valore individuale, rappresenta la massa restituita.

### Attribuzione della kill

Se una testa colpisce il corpo di un altro Slither:

- vittima: Slither della testa;
- killer: proprietario del corpo;
- incrementare `Deaths` della vittima;
- incrementare `Kills` del killer;
- negli scontri testa-testa non attribuire la kill oppure attribuirla reciprocamente dietro configurazione; default: nessuna kill.

Non introdurre ancora ricompense persistenti per la kill.

### Accettazione 3C

- [ ] Lo Slither scompare una sola volta dopo la morte.
- [ ] Non può raccogliere dot o collidere mentre è morto.
- [ ] Il drop segue chiaramente la forma del corpo.
- [ ] La massa raccoglibile prodotta rispetta `DropMass`.
- [ ] Morire e respawnare senza massa acquisita non aumenta l'energia del mondo.
- [ ] Kill e death sono attribuite correttamente.
- [ ] Morti simultanee producono tutti i drop previsti una sola volta.

## 6D. Respawn, popolazione e stress test

### Respawn sicuro

Default iniziali:

| Parametro | Valore suggerito |
|---|---:|
| attesa bot | 1,0 s |
| attesa giocatore | 1,5 s |
| distanza minima da altre teste | 6 unità |
| distanza minima da corpi | 3 unità |
| margine dal bordo | 15 unità |
| tentativi casuali | 32 |

Se nessun candidato soddisfa tutti i vincoli, scegliere quello con clearance massima fra i candidati valutati; non entrare in un ciclo infinito.

Il giocatore deve rinascere con camera riallineata e accumulatore/interpolazione resettati, evitando una transizione che attraversi tutta l'arena.

### Modalità di popolazione

Implementare almeno:

```text
InteractionTest: 10–20 bot inizialmente vicini al giocatore
PopulationTest:  bot distribuiti nell'intera arena
StressTest:      50/100 bot, overlay diagnostico esteso
```

La simulazione deve continuare ad aggiornare tutti gli Slither vivi; il renderer riceve soltanto quelli visibili. Non vincolare l'esistenza dei bot al viewport.

### Obiettivi prestazionali

Su Xiaomi 15T:

- 20 bot: 60 tick/s stabili e almeno 60 FPS, con 120 FPS desiderabili;
- 50 bot: 60 tick/s stabili, qualità grafica invariata;
- 100 bot: misurazione diagnostica; non è ancora necessario garantire 120 FPS;
- nessuna crescita continua delle allocazioni per frame;
- nessun aumento non limitato di dot o massa durante una sessione di 15 minuti.

Prima di ottimizzare, misurare separatamente:

- tempo update totale;
- tempo AI;
- tempo aggiornamento chain;
- tempo indice spaziale;
- tempo collision broad phase;
- tempo collision narrow phase;
- tempo costruzione snapshot;
- tempo preparazione renderer;
- numero di Slither/nodi simulati e visibili;
- numero di candidate collision e test effettivi.

### Accettazione 3D

- [ ] Bot e giocatore effettuano respawn in aree valide.
- [ ] La camera non interpola dalla posizione di morte a quella di respawn.
- [ ] Dopo 15 minuti la popolazione è stabile.
- [ ] Massa totale, dot e memoria non divergono.
- [ ] 20 bot mantengono le prestazioni obiettivo sul telefono.
- [ ] Sono disponibili risultati misurati per 50 e 100 bot.

## 7. Generazione ambientale dei dot con più Slither

Lo Step 2 aggiunge periodicamente dot attorno allo Slither. Questa regola non può essere replicata per ogni entità, altrimenti una zona con molti giocatori genera più risorse.

Spostare la responsabilità a una policy del mondo/cella:

```text
DotPopulationPolicy
├── densità obiettivo per cella
├── quantità attuale
├── budget massimo di spawn per tick/secondo
├── seed deterministico
└── esclusione delle celle fuori arena
```

Una cella può essere mantenuta attiva perché contiene o è vicina a uno Slither, ma lo spawn dipende dalla sua densità, non dal numero dei giocatori presenti.

Non è necessario ottenere ora il bilanciamento definitivo. È necessario evitare che 20 bot moltiplichino automaticamente il tasso di generazione per 20.

## 8. Snapshot e rete futura

Estendere i contratti senza introdurre serializzazione prematura:

```text
WorldSnapshot
├── Tick
├── LocalSnakeId
├── VisibleSnakes[]
│   ├── Id + Generation
│   ├── LifeState
│   ├── head/heading
│   ├── body nodes
│   ├── boost
│   └── provisional color/style id
├── VisibleDots[]
└── Events[]
    ├── SnakeDied
    ├── SnakeRespawned
    └── DotCollected (se necessario al feedback)
```

Gli eventi devono possedere tick e ID/generazione. Il renderer può usarli per feedback minimi, ma non deve determinare morte, drop o punteggio.

Evitare di inviare nello snapshot dati AI, celle complete, spatial index o oggetti interni della simulazione.

## 9. Test automatici minimi

Mantenere tutti i test esistenti e aggiungere almeno:

### Identità e comandi

- ID univoci per Slither vivi;
- incremento `Generation` al respawn;
- comando umano e comando bot attraversano la stessa API;
- determinismo del bot a parità di seed;
- nessun accesso diretto del bot a posizione/velocità mutabile.

### Collisioni

- testa contro capsula;
- mancata collisione appena fuori tolleranza;
- estremi della capsula;
- esclusione del proprio corpo;
- testa-testa simultanea;
- due vittime nello stesso tick;
- indipendenza dall'ordine della collezione;
- equivalenza griglia/brute-force su scenari random deterministici;
- assenza di duplicati da celle multiple.

### Morte e drop

- transizioni valide della macchina a stati;
- un solo evento morte per generazione;
- nessuna attività da morto;
- conservazione di `DropMass`;
- nessun drop raccoglibile da sola `SpawnMass`;
- drop inseriti nelle celle corrette;
- morti simultanee risolte una volta;
- attribuzione kill/death.

### Respawn

- rispetto clearance quando esiste una posizione valida;
- fallback deterministico quando non esiste;
- nessun loop infinito;
- reset dell'interpolazione;
- vecchi eventi ignorati dopo cambio `Generation`.

### Economia fisica

- crescita costante per dot;
- separazione fra `GrowthMass` e `MatchScore`;
- popolazione dot indipendente dal numero di Slither nella stessa cella;
- massa totale non aumenta per cicli morte/respawn senza raccolta.

## 10. Overlay diagnostico Debug

Estendere l'overlay con:

- Slither vivi/morti/in attesa;
- bot configurati e visibili;
- nodi corporei totali e visibili;
- dot totali e visibili;
- massa negli Slither;
- massa nei dot;
- massa generata, distrutta e rilasciata;
- collision candidate/tick;
- narrow-phase test/tick;
- collisioni, morti e respawn;
- vita media dei bot;
- kill/death del giocatore;
- tempi delle principali fasi del tick.

L'overlay deve poter essere disabilitato e non deve creare allocazioni significative per frame.

## 11. Verifica manuale del gameplay

Eseguire sullo smartphone almeno:

1. incontro con un bot entro 10–20 secondi dall'avvio;
2. collisione volontaria della testa del giocatore contro un corpo;
3. collisione di un bot contro il corpo del giocatore;
4. raccolta dei dot prodotti dalla morte del bot;
5. scontro testa-testa;
6. cerchio attorno a un bot senza autocollisione;
7. osservazione di almeno 10 morti e respawn di bot;
8. sessione InteractionTest di 15 minuti;
9. test con 20 bot;
10. misure separate con 50 e 100 bot.

Annotare soprattutto:

- frequenza degli incontri;
- leggibilità di chi è morto e perché;
- sensazione di giustizia della collisione;
- interesse prodotto dai drop;
- densità percepita dell'arena;
- eventuale comportamento eccessivamente suicida o passivo dei bot;
- presenza di situazioni spontaneamente divertenti.

Non correggere graficamente una collisione poco leggibile prima di verificare raggi logici, interpolazione e ordine temporale.

## 12. Criteri di chiusura complessivi

Lo Step 3 è concluso quando:

- [ ] tutti i test dello Step 2 continuano a passare;
- [ ] tutti i nuovi test passano;
- [ ] Desktop e Android compilano senza errori e senza warning ingiustificati;
- [ ] 10–20 bot condividono realmente il mondo con il giocatore;
- [ ] i bot usano la medesima simulazione degli umani;
- [ ] collisioni e morti sono simultanee e deterministiche;
- [ ] il modello testa-capsula è coerente con ciò che si vede;
- [ ] morte, drop e respawn funzionano ripetutamente;
- [ ] morte/respawn non generano massa infinita;
- [ ] lo spawn ambientale non è moltiplicato per il numero di Slither;
- [ ] una sessione di 15 minuti è stabile;
- [ ] il gioco mantiene almeno 60 tick/s e 60 FPS con 20 bot sullo Xiaomi 15T;
- [ ] sono state raccolte misure per 50 e 100 bot;
- [ ] il risultato permette una prima valutazione reale del divertimento competitivo.

## 13. Deliverable richiesti a Codex

1. implementazione suddivisa negli incrementi 3A–3D;
2. test automatici aggiornati;
3. configurazioni `InteractionTest`, `PopulationTest` e `StressTest`;
4. APK Android Debug autonomo;
5. report con parametri effettivamente utilizzati;
6. tabella degli esiti prestazionali per 10, 20, 50 e 100 bot;
7. contabilità della massa durante la sessione lunga;
8. esito di ogni criterio di accettazione;
9. elenco dei problemi di gameplay osservati;
10. commit/tag finale dello Step 3, se autorizzato.

## 14. Istruzione sintetica da passare a Codex

> Implementa lo Step 3 in quattro incrementi isolabili: multi-Slither, collisioni, morte/drop e respawn/stress test. Mantieni una sola simulazione per giocatore e bot: cambia soltanto la sorgente dei `PlayerCommand`. Parti con 10 bot generati vicino al giocatore in modalità `InteractionTest`; usa una AI wandering deterministica e minimale. Rileva testa contro corpo tramite circle-capsule e griglia spaziale, raccogli gli eventi e risolvi tutte le morti simultaneamente per evitare dipendenza dall'ordine. Alla morte converti soltanto una frazione della massa acquisita in dot, senza trasformare la massa iniziale gratuita in risorsa, così respawn e morte non creano energia infinita. Sposta lo spawn ambientale dei dot a una policy per cella indipendente dal numero di Slither. Non aggiungere networking, progressione, economia o rifiniture grafiche. Valida prima 10–20 bot sullo Xiaomi 15T, poi misura 50 e 100 bot.

