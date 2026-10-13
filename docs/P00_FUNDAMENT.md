# P00 — Projektfundament, Architektur und Werkzeugkette

**Priorität:** P0 · **Abhängigkeiten:** Projektdefinition · **Folgepakete:** P01, P02, P03, P07, P08

## Ziel
Ein reproduzierbares Unity-6.3-LTS-/URP-/C#-Projekt mit klaren Modulgrenzen, CI-Basis, Git- und Codex-Konventionen, das ohne Compilerfehler geöffnet und als leerer Windows-Build gestartet werden kann.

## Input
- Projektvorgaben aus `README.md` (Windows, isometrisch, Online-Koop, kostenloser Einstieg).
- Systeminvarianten aus `00_GESAMTARCHITEKTUR.md`.
- Stabiler Datenschnittstellenentwurf aus `10_SCHNITTSTELLENVERTRAEGE.md`.
- Lokale Unity-/Git-Installation; wenn fehlend: reproduzierbare Setup-Anleitung statt fingierter Ausführung.

## Aufgaben / In Scope
1. Unity-Hub-Projekt auf Zielversion anlegen; URP-Renderer konfigurieren; konkrete kompatible Versionen für Input System, Cinemachine, AI Navigation, NGO, MPS, Test Framework pinnen. Keine ungetesteten Preview-Pakete ohne dokumentierte Entscheidung.
2. Gitignore für `Library`, `Temp`, Logs und lokale User Settings, während `Assets/*.meta`, Szenen, Packages und ProjectSettings versioniert werden.
3. Zielordner, Namespaces und `.asmdef` wie in der Gesamtarchitektur festlegen; Contract-Assembly einrichten.
4. `Bootstrap`, `MainMenu`, `Lobby`, `Gameplay_Test` als minimale Szenen anlegen. Bootstrapping darf fehlende UGS-Verbindung im Offline-Testmodus sauber behandeln.
5. Konfigurationsobjekte für World-/Tick-/Netzwerk-/Graphics-Profiles definieren; Umgebungs-/Service-IDs nicht im Repository geheim speichern.
6. EditMode-Smoke-Test, PlayMode-Smoke-Test und Batchmode-Build-Script erzeugen; Ausführungsanweisungen dokumentieren.
7. Architekturentscheidungen (ADR), User-Settings, Definition of Done und Commit-/Branch-Konventionen definieren.

## Out of Scope
Kampf, reales Lobby-Join, Gegner, vollständige GUI, kostenpflichtiger CI-Host, eigene Backendserver.

## Output / Deliverables
- Öffnendes Unity-Projekt samt `Packages/manifest.json`, `ProjectSettings`, Szenen und `.meta`-Dateien.
- `Avatar5.Contracts` sowie leere/kleine Assemblies der späteren Module; keine zyklischen Abhängigkeiten.
- `BuildWindows`-Entry-Point oder dokumentiertes Unity-Batchmode-Kommando; Testlog; `docs/DECISIONS.md`.
- Minimale `BootstrapConfig` mit BuildVersion, ContentSchemaVersion, NetworkProtocolVersion.

## Vertrag zu nächsten Paketen
- **P01** erhält `GameBootstrap.Ready`, `InputSettings`, definierte Szenen und Contract-Assembly.
- **P03** erhält `NetworkProtocolVersion`, Environment-Konfiguration und Session-Bootstrap-Hooks.
- **P07** erhält aktiven URP-Renderer, Material-/Shader-Ordner und GraphicsProfile.
- **P08** erhält deterministisch ausführbare Tests/Build-Kommandos.
- **Niemals:** andere Pakete sollen `ProjectSettings` oder Unity-Paketversionen ohne ADR austauschen.

## Akzeptanz / Nachweise
- Projekt öffnet ohne Compilerfehler; `Gameplay_Test` startet.
- Git-Checkout auf einem zweiten Rechner ist rekonstruierbar (vorausgesetzt gleiche Unity-Version).
- Edit-/PlayMode-Smoke-Tests grün; `Windows x64`-Development-Build startet.
- Fehlende SDK-Zugangsdaten verhindern nicht den lokalen Boot-/Combat-Test.
- Dependency-Zyklus-Prüfung über `.asmdef` und Unity Console ohne Fehler.

## Risiken und Abwehr
Paketinkompatibilität: Versionen tatsächlich im Editor prüfen. CI ohne Unity-Lizenz: lokale Batch-Tests zuerst. Unity-Asset-Metadaten: alle Referenzen versionieren. Geheimnisse nie in Git.

## Übergabe / Definition of Ready P01
Ein Kompilat, eine begehbare Greybox-Szene, funktionierender Input-Packagezugriff und dokumentierte Kamera-/Player-Contract-IDs.
