# Projektstand und Abnahme

29. Oktober 2026. Entwurf; keine vollständige Funktionsgleichheit.

## Auftrag und Kontext

Blitztext als Windows- und Android-Anwendung umsetzen. Offline als Standard,
Online optional, lokale Nutzung ohne API-Gebühren.
Führende Ablage: dieser Repository-Ordner. Quellen: beauftragtes Repository,
öffentliche Herstellerdokumentation, ausdrücklich genannte Geräte.
Keine Daten aus der Wissens-Vault oder Zugangsdaten in den öffentlichen Quellcode übernehmen.

## Funktionsmatrix

| Funktion | Windows-Quellcode 0.1 | Android-Quellcode 0.1 |
|---|---|---|
| Lokale Spracherkennung | whisper.cpp CLI | whisper.cpp JNI |
| Mikrofon | 16 kHz PCM, bis 5 min | 16 kHz PCM, bis 90 s |
| Eingabe in andere Apps | Hotkey und Einfügen | Android-Spracheingabe (IME) |
| Hintergrundzugriff | Tray | IME |
| Wörterbuch | Regeln | Regeln |
| Vier Schreibmodi | Optional lokales Ollama | Offen |
| Verlauf | Optional, DPAPI | Nur flüchtiger letzter Text |
| Datei-/WhatsApp-Audio-Import | Offen | Offen |
| Meeting-Aufnahme/Sprechertrennung | Offen | Offen, Plattformgrenzen prüfen |
| Meeting-Zusammenfassung | Offen | Offen |
| App-/Website-Regeln | Offen | Offen |
| Anpassung an Cursor-Kontext | Offen | Offen |
| Audiowiedergabe stummschalten | Offen | Offen |
| Optionale Online-Verarbeitung | Offen | Offen |
| Signierung/Updates | Offen | Debugsignatur; Release offen |

Quellcode ist bis zum Build und Gerätetest keine bestätigte Produkteigenschaft.

## Verifizierter Build-Stand

Code-Commit: f5b58e3e1897e81caa85c6605db4d1e42d2fedb0.
[Prüflauf vom 29. Oktober 2026](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40861319751):
Windows und Android erfolgreich. Windows: sieben Textlogik-Tests, CLI-Start,
WPF-Oberflächenstart und lokale Transkription der öffentlichen JFK-Testaufnahme.
Android: Unit-Tests, ARM64-APK-Build und Lint erfolgreich.
Das ist kein Mikrofon-, Geschwindigkeits- oder Eingabetest auf den Nutzergeräten.

- [Windows-ZIP](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40861319751/artifacts/12892166049)
- [Android-APK im ZIP](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40861319751/artifacts/12892355187)

GitHub-Anmeldung für Downloads nötig; Artifacts laufen nach 30 Tagen ab.
Reproduzierbarer Quellcode bleibt im Repository.

## Abnahme des Kernablaufs

- Modell laden, Internet trennen, deutsch diktieren.
- Identische zehn Testtexte mit Namen, Zahlen, Umlauten und Fachbegriffen.
- Aufnahmezeit, Wartezeit ab Stopp, Fehler und Korrekturen dokumentieren.
- Stille, zu kurze Aufnahme, fehlendes Mikrofon und fehlendes/beschädigtes Modell.
- Fokus während der Verarbeitung wechseln: kein Einfügen ins neue Feld.
- Windows: Editor, Browser, Mailentwurf; Zwischenablage vorher/nachher.
- Android: Mailentwurf, Browser, Messenger-Entwurf, Passwortfeld und Tastaturwechsel.
- Aufnahme/Erkennung abbrechen, App wechseln/schließen und erneut aufnehmen.
- Ollama ausgefallen: Original erhalten, keine externe Verbindung.
- Verlauf aus/ein, Verschlüsselung, bestätigtes Löschen.
- Temporäre Windows-Audio-Dateien nach regulärem Abschluss entfernt.

Mit erfundenen Texten testen; keine echten Nachrichten senden.
Keine Mikrofon- oder S23-Ultra-Gerätetests in der Entwicklungsumgebung erfolgt.

## Grenzen und nächste Schritte

Native Android-Berechnung kann nach Abbruch auslaufen.
Windows-Clipboard-Wiederherstellung verwendet eine Wartezeit, keine Lesequittung.
Windows-Rohaufnahmen können bei Absturz zurückbleiben.
Leistung und Qualität auf 8-GB-Hardware müssen gemessen werden.
Beliebige Android-Telefon-/VoIP-Aufnahmen werden nicht zugesagt.
Zuerst Builds und Gerätetests, anschließend offene Originalfunktionen ergänzen.
Den App-Entwicklungs-Skill aus dem geprüften Ablauf ableiten; noch nicht installiert.

## 0.1.1 — Sicherheitsüberarbeitung (29. Oktober 2026)

Windows: ausdrückliche, einmalige Freigabe vor externem Einfügen; Ablauf nach 60 Sekunden;
Clipboard-Verlauf-/Cloud-Ausschluss; WAV und Transkript ohne App-Tempdateien über RAM/Pipes.
Android: Release statt Debug; sensible Clipboard-Markierung; Screenshot-/Vorschaubildschutz;
privater, wiederverwendbarer Signierschlüssel aus Secrets, kein Debug-Fallback.
CI: Freigabetests, Clipboard-OS-Test, echte Pipe-Transkription, NuGet-Audit,
Release-Manifestprüfung, SHA-256-Dateilisten und Defender-Scan als Paketfreigabeschranke.

Der aktuelle tatsächliche Build-/Scanstatus ist im zugehörigen Actions-Lauf zu prüfen.
Ohne Schlüssel bleibt Android nicht installierbar. Kein vollständiger Schutz vor Prompt Injection,
kein unabhängiges Binäraudit und weiterhin offene Gerätetests. Details: [SECURITY](SECURITY.md).
