# Sprechklar — Windows und Android

Entwicklungsvorschau 0.1. Native Umsetzung der Blitztext-Idee mit eigener Identität.
Das macOS-Original bleibt unverändert. Noch keine vollständige Funktionsgleichheit.

## Downloads

GitHub → Actions → **Windows and Android ports** → erfolgreicher Lauf → Artifacts.
Zum Download ist eine GitHub-Anmeldung nötig. Build-Erfolg ersetzt keinen Gerätetest.

## Windows x64

1. **Sprechklar-Windows-x64-preview** komplett entpacken.
2. **Sprechklar.exe** starten; der Unterordner **engine** muss daneben bleiben.
3. Mikrofon und Modell auswählen: **base** genauer, **tiny** schneller.
4. **Sprachmodell einmalig laden**: ca. 142 bzw. 75 MiB von Hugging Face.
5. Aufnehmen, sprechen, stoppen. Text erscheint in der App.
6. Für andere Programme: **Im Hintergrund weiterlaufen**, ins Zieltextfeld klicken,
   **Strg+Umschalt+Leertaste** startet und stoppt die Aufnahme.
7. **Strg+Umschalt+Esc** bricht ab. Maximal fünf Minuten je Aufnahme.

Bei einem Wechsel des Zielfensters oder des über UI Automation erkannten Eingabefelds wird nicht blind eingefügt. Das Ergebnis kann
in Sprechklar kopiert werden. Die Zwischenablage wird nach einer Karenzzeit
wiederhergestellt, wenn inzwischen niemand etwas anderes kopiert hat.
Windows bestätigt nicht, ob das Zielprogramm den Text angenommen hat.
Erhöhte Programme, Passwortfelder, Remote-Sitzungen und langsame Zwischenablagen
müssen gesondert auf dem Gerät getestet werden.

Für die drei Schreibmodi: Ollama separat installieren, ein kleines mehrsprachiges
Textmodell laden, Ollama im lokalen Modus (OLLAMA_NO_CLOUD=1) betreiben und dessen Modellnamen eintragen. Nur 127.0.0.1:11434 wird angesprochen.
Bei Ausfall bleibt das wortgetreue Transkript erhalten; kein Cloud-Fallback.

Verlauf ist standardmäßig aus. Optional werden maximal 100 Ergebnisse mit
Windows-DPAPI verschlüsselt gespeichert. Löschen ist in der App nach Bestätigung möglich.
Einstellungen und Modelle: %LOCALAPPDATA%/Sprechklar.
Rohaufnahmen liegen während der Verarbeitung im Benutzer-Tempordner und werden
nach regulärem Abschluss oder Abbruch gelöscht. Bei Absturz können Dateien bleiben;
temporäre Rohaufnahmen sind in 0.1 nicht separat verschlüsselt.

## Android / ARM64, einschließlich Android 16

1. **Sprechklar-Android-arm64-preview** entpacken und APK aufs Handy übertragen.
2. Installation aus dieser Quelle erlauben und APK installieren.
3. Sprechklar öffnen, Modell laden und Mikrofonzugriff beim Aufnahmeversuch erlauben.
4. Aufnehmen, stoppen und Ergebnis kopieren.
5. **Spracheingabe in Android aktivieren**: Sprechklar aktivieren.
6. Im gewünschten Textfeld über die Tastaturauswahl **Sprechklar Spracheingabe** wählen.
7. Diktieren und stoppen. **Andere Tastatur** wechselt zurück.

Passwortfelder sind gesperrt. Nach einem Feldwechsel wird nicht in das neue Feld
eingefügt; das Ergebnis lässt sich in Sprechklar ansehen.
Maximal 90 Sekunden je Aufnahme; Audio bleibt nur im RAM.
Ein Abbruch verhindert das Einfügen; die native Berechnung kann noch auslaufen.
Diese Android-Vorschau enthält noch keine Schreibmodi, Meeting-Aufzeichnung oder
gespeicherten Verlauf. Der letzte Text bleibt nur im laufenden App-Prozess.

Die APK ist mit einem temporären Entwicklungszertifikat signiert.
Bei einem späteren CI-Build kann eine Deinstallation nötig sein; diese entfernt
Einstellungen und Modelle. Dauerhafte Release-Signierung folgt.
Es erfolgt keine Play-Store-Veröffentlichung.

## Datenschutz und Kosten

Keine Telemetrie, kein Konto, keine Audio- oder Text-Uploads, kein Cloud-Fallback.
Internet ist nur beim ausdrücklich gestarteten Modelldownload erforderlich.
Lokale Inferenz verursacht keine Anbietergebühren pro Minute/Anfrage.
Strom, Speicher, Entwicklungszugang und spätere Cloud-Dienste sind getrennt.
Die gewünschte optionale Online-Verarbeitung ist in 0.1 noch nicht enthalten.
Android-Cloud-Backup ist deaktiviert.
Downloads werden gegen upstream-veröffentlichte SHA-1-Prüfsummen geprüft.
Das erkennt beschädigte/geänderte Dateien, ist aber keine moderne Signaturprüfung.

## Selbst bauen

Windows: .NET 8 SDK, CMake und MSVC. Die CLI des unten genannten whisper.cpp-Commits
bauen und inklusive benötigter Bibliotheken unter **engine** neben die App legen.

    dotnet test ports/windows/Sprechklar.Tests/Sprechklar.Tests.csproj
    dotnet publish ports/windows/Sprechklar/Sprechklar.csproj -c Release -r win-x64 --self-contained true

Android: JDK 17, Gradle 8.13, SDK 36, NDK 28.2.13676358, CMake 3.22.1.
whisper.cpp nach ports/android/vendor/whisper.cpp auschecken und auf den unten
genannten Commit setzen. Im Ordner ports/android ausführen:

    gradle :app:assembleDebug :app:lintDebug

Exakte Build-Schritte: .github/workflows/ports.yml.
Modelle sind nicht im Repository enthalten; Erstdownload erforderlich.

## Referenzen

- Blitztext 1.15.1: 26022b9190d5902e122e631eddd6b437406b10c3.
- whisper.cpp v1.9.5: d1be6fde11ac6e0407606b4e42fe72d34add8037.
- [Fortschritt und Abnahme](docs/STATUS.md).
- [Drittanbieterhinweise](THIRD_PARTY_NOTICES.md).
