# Build e deploy Android Debug

## Preparazione del telefono

1. Aprire **Impostazioni > Informazioni sul telefono** e toccare sette volte **Numero build**.
2. Nelle **Opzioni sviluppatore**, abilitare **Debug USB**.
3. Collegare il telefono con un cavo dati USB e scegliere una modalità che consenta il trasferimento dati.
4. Alla prima connessione, accettare sul telefono la chiave RSA del computer.

## Verifica ADB

```powershell
$slitherAdb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
& $slitherAdb version
& $slitherAdb devices -l
```

Lo stato deve essere `device`. Se appare `unauthorized`, sbloccare il telefono, revocare le autorizzazioni Debug USB e riconnetterlo. Se l'elenco è vuoto, provare un altro cavo/porta e installare il driver USB del produttore.

## Build Debug

Dalla radice del repository:

```powershell
dotnet restore .\SlitherClone.sln
dotnet build .\src\Slither.Android\Slither.Android.csproj -c Debug
```

Per trovare il pacchetto prodotto:

```powershell
Get-ChildItem .\src\Slither.Android\bin\Debug -Recurse -Include *.apk,*.aab
```

## Installazione e aggiornamento

```powershell
$slitherAdb = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
$slitherApk = (Get-ChildItem .\src\Slither.Android\bin\Debug -Recurse -Filter *-Signed.apk | Select-Object -First 1).FullName
& $slitherAdb install -r $slitherApk
```

## Avvio

```powershell
& $slitherAdb shell monkey -p com.emilianofiorenza.slitherclone -c android.intent.category.LAUNCHER 1
```

## Log filtrato

```powershell
$slitherPid = (& $slitherAdb shell pidof com.emilianofiorenza.slitherclone).Trim()
& $slitherAdb logcat --pid=$slitherPid
```

Arrestare `logcat` con `Ctrl+C`. L'app non richiede permessi Internet o altri permessi Android aggiuntivi.

## Controlli manuali di accettazione

- triangolo colorato visibile e animato su sfondo scuro;
- orientamento landscape e viewport corretto;
- sospensione e ripresa senza crash o salto temporale;
- nessuna eccezione non gestita o errore grafico persistente nei log.
