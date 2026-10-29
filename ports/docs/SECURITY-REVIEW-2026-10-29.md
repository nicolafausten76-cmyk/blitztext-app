# Sicherheitsprüfung Sprechklar 0.1.1 — 29. Oktober 2026

Technischer Prüfstand, keine Garantie der Schadcodefreiheit.

## Nachweis

- Geprüfter und gebauter Quellstand: `525f14b63fffac312c6a2a6887c3dee1c5efa91f`.
- [Erfolgreicher vollständiger Prüflauf](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40872989693).
- Scanabschluss: 29. Oktober 2026, ca. 13:28 Uhr, Europe/Berlin.
- Scanner: ClamAV 1.4.6; Signaturstand 28168 vom 29. Oktober 2026, 06:24:16 UTC.
- Offizielles Image: `clamav/clamav@sha256:982e28f90061d0e66eb03276419f589a47c9fa601e985e472fc2f6c64b497ca0`.
- Signaturupdate erfolgreich. Die Scans liefen ohne Netzwerkzugriff mit schreibgeschützten App-Dateien.
- Windows-Paket: 481 vom Scanner gemeldete Dateien, **0 Funde**.
- Android-Paket einschließlich APK-Archiv: 7 vom Scanner gemeldete Dateien, **0 Funde**.
- Windows: 13 Unit-Tests, UI-Start, Clipboard-Datenschutzmarker und tatsächliche RAM/Pipe-Transkription der öffentlichen JFK-Testaufnahme bestanden; NuGet-Audit blockiert bekannte Paketwarnungen.
- Android: Release-Build, Unit-Tests, Lint, verpackte Berechtigungs- und Debug-Flag-Prüfung bestanden.

## Download und Integrität

- [Windows-Sicherheitsvorschau](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40872989693/artifacts/12896248943).
- SHA-256 des Windows-Artifact-ZIP: `c0b698fc2e14b3f089c32e854605f62cf0db88aaba504ee6e47acdbd0a1e3095`.
- Die enthaltene SHA256SUMS.txt benennt die Prüfsummen der Paketdateien, SOURCE-COMMIT.txt den gebauten Commit.
- [Scanprotokolle](https://github.com/nicolafausten76-cmyk/blitztext-app/actions/runs/40872989693/artifacts/12897695172).
- GitHub-Artifact-Downloads benötigen Anmeldung und sind hier bis 28. November 2026 verfügbar.

## Offene Punkte

- **Android ist noch UNSIGNED und nicht installierbar.** Die vier privaten Signierungs-Secrets fehlen.
  Kein neuer Wegwerfschlüssel und kein Debug-Fallback. Deshalb wurde kein installierbares Android-Paket veröffentlicht.
- [Einrichtung des privaten Schlüssels und Prüfumfang](SECURITY.md).
- Windows besitzt weiterhin keine Authenticode-Herausgebersignatur.
- Kein vollständiges Drittanbieteraudit, keine unabhängige reproduzierbare Binärprüfung,
  keine universelle Prompt-Injection-Abwehr. Die ausdrückliche Textfreigabe begrenzt deren Auswirkungen.
- Mikrofon, Einfügen in Zielprogramme und Laufzeit auf MateBook/S23 Ultra bleiben auf den tatsächlichen Geräten zu prüfen.
- Frühere Vorschauen werden durch diese Fassung ersetzt; alte Downloads nicht für den Gerätetest verwenden.

Der erste Windows-End-to-End-Test erkannte einen unterdrückten stdout-Ausgabepfad der Engine;
der Aufruf wurde korrigiert, ohne den Test abzuschwächen. Defender scheiterte zuvor an einem
technischen Dienst-/RPC-Fehler des Windows-CI-Rechners. Das war kein Malwarefund. Die endgültige
Paketfreigabe beruht ausdrücklich auf dem oben dokumentierten ClamAV-Scan.
