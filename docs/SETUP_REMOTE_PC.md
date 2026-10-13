# Avatar5 – Remote-PC Einrichtung (Windows)

## Was hier wirklich enthalten ist

Dies ist ein **lokales Git-Repository mit ersten C#-Implementierungen**, Tests, Editor-Setup-Skripten und den Architekturpaketen P00–P08. **Kein bereits durch Unity kompilierter vollständiger Game-Build.** Die Unity-Pakete, ProjectSettings und URP-Pipeline sollen ausdrücklich durch ein reales Unity-Hub-3D-URP-Projekt erzeugt werden. Unverifizierte Unity-SDK-Versionen werden nicht fest gepinnt.

## Voraussetzungen auf dem Remote-PC

1. Windows 10/11 x64 mit funktionierender Remote-Desktop- bzw. Streaming-Sitzung. Für 3D-Entwicklung empfiehlt sich eine tatsächlich zugängliche GPU und ausreichend RAM/SSD. Genauere Dimensionierung richtet sich nach Grafikkarte und Projektumfang.
2. Unity Hub installieren und mit kostenloser Unity-ID anmelden.
3. Unity **6.3 LTS** auswählen, inklusive **Windows Build Support (IL2CPP)** und idealerweise Visual Studio Integration.
4. Visual Studio 2022 (mit Game Development with Unity) oder VS Code (C# Dev Kit/Unity Extensions) konfigurieren.
5. Git for Windows installieren, `git --version` prüfen.
6. Codex CLI oder IDE-Erweiterung installieren, mit ChatGPT anmelden; Repository-Verzeichnis als Workspace auswählen.
7. Blender ist optional und wird erst für gezielte Modellanpassungen gebraucht.

## Unity-URP-Template in Avatar5 importieren

1. Dieses Starter-Repository entpacken oder per `git clone` herunterladen, z. B. nach `C:\Dev\Avatar5`.
2. Unity Hub → New project → **3D (URP)** → Unity 6.3 LTS; temporären Projektordner `C:\Dev\Avatar5-URP-Template` wählen.
3. PowerShell im Starter-Repository öffnen:

```powershell
.\scripts\Import-UnityTemplate.ps1 -TemplateProject 'C:\Dev\Avatar5-URP-Template'
```

4. Unity Hub → Add project from disk → `C:\Dev\Avatar5` (Starter-Repository).
5. Unity importiert Dateien und erzeugt automatisch noch fehlende `.meta`-Dateien. Nach erfolgreichem Import diese Dateien in Git übernehmen.
6. Unity Menü **Avatar5 → Setup → Generate Starter Scenes** ausführen. Vier Szenen werden erstellt (Bootstrap, MainMenu, Lobby, Gameplay_Test).
7. `Bootstrap` öffnen → Play: Debug-Menü erscheint. Über Debug-Menü nach `Gameplay_Test` wechseln.
8. Unity Test Runner → EditMode → `ContractsSmokeTests` ausführen.
9. Unity Menü **Avatar5 → Build → Windows x64 Development** für einen ersten lokalen Build.

**Nicht als bestanden behaupten:** Unity-Compile, Unity-Test und Windows-Build sind bis zu diesen manuellen Schritten **ausstehend**.

## Öffentliches GitHub-Repository verbinden

Das öffentliche Repository ist bereits erstellt: https://github.com/SimonSteinschaden/Avatar-5

Das Starter-Archiv enthält die lokale Git-Historie und eine vorkonfigurierte `origin`-Remote. Nach Entpacken in `C:\Dev\Avatar5` und nach Installation von Git for Windows:

```powershell
cd C:\Dev\Avatar5
git remote -v
git status
git push -u origin main
```

Beim Push wird ggf. eine Anmeldung bei GitHub erforderlich. **Nie `git push --force` verwenden**, falls sich der Remote-Stand zwischenzeitlich verändert hat. In diesem Fall zuerst den Remote-Stand prüfen und konfliktfrei zusammenführen.

Der Unity-URP-Template-Import erfolgt unabhängig davon. Nach erfolgreicher Editor-Kompilierung `.meta`-, `Packages`- und `ProjectSettings`-Dateien versionieren; `Library`, `Temp` und Builds nicht versionieren.

Wichtig für dieses öffentliche Repository: Keine fremden Assets ohne Weiterverbreitungsrecht, keine Zugangsdaten und keine geschützten Avatar-/Borderlands-Inhalte einchecken.

## Optional / später

- Unity Gaming Services Cloud-Projekt und Relay-Sessions: erst P03, nicht für P00 erforderlich.
- Grafik-Assets: erst ein stilisierter URP-Shader-/Ink-Outline-Test (P07a), bis dahin Greybox.
- Kein eigener Linux-Server, keine Datenbank, keine gekauften Asset-Packs erforderlich.
