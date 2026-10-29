# Sprechklar 0.1.1 — Sicherheitsstatus und Signierung

Diese Seite beschreibt technische Schutzmaßnahmen und Prüfgrenzen, keine Sicherheitsgarantie.

## Windows

- Jede neue Aufnahme widerruft die bisherige Einfügefreigabe.
- Das Transkript erscheint zuerst in Sprechklar. Es wird nie automatisch in eine andere App eingefügt.
- Nach eigener Prüfung kann der Nutzer den genauen Text für das ursprüngliche Feld freigeben.
  Anschließend dort innerhalb von 60 Sekunden **Strg+Alt+Eingabe** drücken.
- Freigaben sind einmalig. Änderungen am Ergebnis, Abbruch und neue Aufnahmen widerrufen sie.
  Fenster, UI-Automation-Feldidentität und Passwortstatus werden unmittelbar vor Einfügen geprüft.
- Diese Bestätigung ist eine Grenze gegen die Auswirkungen von Prompt Injection. Sie beweist
  nicht, dass ein Sprachmodell eingesprochene Anweisungen ignoriert. Auch wortgetreue Transkripte
  können Erkennungsfehler enthalten. Keine ungeprüften Befehle in Terminals einfügen.
- Kopieren und Einfügen setzen die Windows-Marker zum Ausschluss aus Zwischenablageverlauf,
  Cloud-Zwischenablage und Monitorverarbeitung. Das verhindert nicht den Zugriff anderer
  Programme, die auf demselben Desktop die aktuelle Zwischenablage lesen dürfen.
- Audioaufnahme als WAV im RAM, Übergabe an whisper-cli über stdin, Transkript über stdout.
  Die App erzeugt dabei keine temporären WAV- oder Transkriptdateien. App-eigene Audiopuffer
  werden nach regulärem Abschluss verworfen/überschrieben. Keine Garantie gegen Paging,
  Prozessabbilder oder privilegierte Speicheranalyse.
- Dateien aus früheren Versionen werden nicht automatisch gelöscht.
- Optionaler Verlauf bleibt Windows-DPAPI-verschlüsselt; Einstellungen/Wörterbuch bleiben
  unverschlüsselt im Benutzerprofil. Der Verlauf ist standardmäßig deaktiviert.
- Ollama ist ein separat betriebener lokaler Dienst. Ihn mit OLLAMA_NO_CLOUD=1 betreiben,
  nur lokale Modelle verwenden und nicht ins Netzwerk freigeben.
- Die Windows-App besitzt noch kein öffentlich vertrauenswürdiges Authenticode-Zertifikat.
  Virenscan und SHA-256-Prüfsummen ersetzen keine Herausgebersignatur.

## Android

- Release-Build mit debuggable=false und jniDebuggable=false; keine Debug-Signierung.
- Verpackte Berechtigungen werden auf RECORD_AUDIO und INTERNET begrenzt.
- Die App markiert kopierte Transkripte als sensibel; das ist kein vollständiger
  Schutz vor einem berechtigten Empfänger der aktuellen Zwischenablage.
- Die Hauptansicht verwendet FLAG_SECURE gegen Screenshots/Recent-App-Vorschaubilder.
- Kein zusätzliches Textmodell, kein Tool-Agent, kein Auslesen umgebender Texte.
- Passwortfelder sind gesperrt. Feldwechsel/ausgeblendete Tastatur verhindern das Einfügen.
- Cloud-Backup bleibt aus; Audio liegt im RAM. Screenshotschutz und RAM-Nutzung
  garantieren keinen Schutz gegen ein kompromittiertes oder privilegiert ausgelesenes Gerät.

## Dauerhafte private Android-Signierung

Ein Release ohne privaten Schlüssel wird als **UNSIGNED, nicht installierbar** gebaut und geprüft.
Der Workflow veröffentlicht in diesem Fall kein installierbares Android-Paket. Er generiert
keinen Ersatzschlüssel und fällt nicht auf ein Debug-Zertifikat zurück.

Benötigte GitHub-Repository-Secrets:
- ANDROID_KEYSTORE_BASE64 — Base64 der privaten PKCS12-Keystore-Datei
- ANDROID_STORE_PASSWORD — Keystore-Passwort
- ANDROID_KEY_ALIAS — Alias, z. B. sprechklar
- ANDROID_KEY_PASSWORD — Schlüsselpasswort (bei PKCS12 normalerweise identisch)

Falls noch kein Schlüssel existiert:
1. JDK mit keytool verwenden, einen privaten Ordner außerhalb des Repositorys anlegen.
2. ports/scripts/New-AndroidSigningKey.ps1 mit -KeyStorePath aufrufen.
   Das Skript überschreibt keine bestehende Datei; keytool fragt das Passwort interaktiv ab.
3. Keystore und Passwort getrennt und privat sichern. Nicht als Artifact, Chat-Anhang,
   Git-Datei oder Wissensnotiz ablegen.
4. Die vier Werte unter Repository → Settings → Secrets and variables → Actions hinterlegen.
   Alternativ mit dem bereits authentifizierten GitHub CLI: gh secret set verwenden.
   Keystore-Base64 über stdin direkt an gh secret set übergeben; nicht ausgeben oder kopieren.
5. Den Workflow erneut ausführen. Er prüft die APK-Signatur und dokumentiert den öffentlichen
   Zertifikat-Fingerprint. Spätere Builds verwenden denselben Schlüssel.

Der CI-Runner erhält den Schlüssel nur aus Secrets und legt ihn im privaten temporären Verzeichnis
ab. Dieser Pfad wird nie als Artifact hochgeladen. Der Workflow entfernt die Datei anschließend.
Wer die erste Debug-Vorschau bereits installiert hat, muss für den Wechsel auf den privaten
Release-Schlüssel normalerweise einmal deinstallieren; dabei gehen lokale Modelle/Einstellungen verloren.

## Build- und Paketprüfung

- Exakter whisper.cpp-Commit und festgelegte GitHub-Action-Commits.
- Windows-Unit-Tests prüfen Freigabe, Einmaligkeit, Verfall, Textänderung und fehlendes Zielfeld.
- UI-Starttest, echter Windows-Clipboard-Roundtrip der Datenschutzmarker.
- Öffentliche JFK-Testaufnahme durch den tatsächlichen WAV-im-RAM/stdin/stdout-Pfad der App.
- NuGet-Audit für direkte und transitive Abhängigkeiten; Auditwarnungen scheitern den Build.
- Android-Release-Unit-Tests, Lint, Prüfung der tatsächlichen Manifest-Berechtigungen und Debug-Flags.
- Bei vorhandenem Schlüssel: apksigner verify mit öffentlichem Zertifikat-Fingerprint.
- SHA256SUMS.txt benennt die Dateien des jeweiligen Pakets und SOURCE-COMMIT.txt dessen Quellstand.
- Separater Linux-Runner scannt exakt diese Dateien einschließlich APK-Archiv mit ClamAV.
  Ein offizielles ClamAV-1.4-Containerimage liefert die Engine und vorinstallierte Datenbanken;
  sein konkreter Digest wird protokolliert und im gesamten Prüflauf wiederverwendet.
  Signaturen werden aktualisiert. Falls das Update fehlschlägt, sind höchstens sieben Tage
  alte offizielle Datenbanken zulässig; die Engine erzwingt diese Grenze.
  Nur der Signatur-Updater hat Netzwerkzugang und sieht keine App-Dateien.
  Der eigentliche Scanner läuft mit --network none und schreibgeschützten Paketen.
  Scannerfehler, Malwarefunde, überschrittene Scanlimits oder fehlende Prüfung blockieren
  die geprüften Downloadpakete. Die Defender-Alternative scheiterte zuvor am Dienst des
  Windows-Build-Rechners; dies war kein Malwarefund.
- Rohkandidaten heißen candidate-* und sind **keine freigegebenen Downloads**.
- Sprechklar-security-reports enthält Scanprotokolle, auch bei Fehlern. Nur erfolgreich gescannte
  Pakete tragen den Namen Sprechklar-*-security-preview.

Ein einzelner negativer Virenscan beweist keine Schadcodefreiheit. Kein vollständiges Audit aller
Drittanbieterquellen, keine unabhängige reproduzierbare Binärprüfung, keine universelle
Prompt-Injection-Abwehr und noch keine Mikrofon-/Ziel-App-Tests auf den Nutzergeräten.
