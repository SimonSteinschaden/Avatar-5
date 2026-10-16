# Avatar5 – tatsächlicher Implementierungsstand

**Zeitpunkt:** 16.10.2026 · **Letzte Story:** P01 Spielersteuerung und isometrische Kamera (Branch `feature/p01-steuerung-kamera`)

## Implementiert / als Source vorhanden

- Architektur- und Spezifikationsdokumente P00–P08, Schnittstellenverträge, Agentenregeln
- Git-Hygiene (.gitignore/.gitattributes), reproduzierbare lokale Git-Historie
- Unity 6.3 LTS (6000.3.26f1) URP-Projekt mit Starter-Szenen (Commit `96135bb`)
- Unity-unabhängige C#-Contracts: ElementKind, PlayerCommand, AbilityRequest, CombatEvent, WorldEvent, PresentationCue, VersionContracts, **PlayerPose, IPlayerCommandSink, IPlayerPoseSource (P01)**
- Gameplay-Infrastruktur: pro-Actor SequenceDeduplicator
- MonoBehaviour-Bootstrap, einfaches Entwicklungsmenü, Editor-Setup für Starter-Szenen, Windows-Development-Build-Befehl

### P01 – Steuerung, Zielen, Kamera (lokal, Greybox)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `PlanarMath`, `InputIntentMath`, `MovementSettings`, `PlayerMotorCore`, `PlayerCommandGate`, `PlayerCommandSequencer`, `ClientTickClock` | `Avatar5.Gameplay` (engine-frei) | Kamerarelative Bewegungsabsicht, Radial-Deadzone, Diagonal-Clamp, Beschleunigung/Abbremsung, Drehgeschwindigkeit, Blickrichtung nach Ziel (sonst Laufrichtung), Substeps mit Obergrenze gegen Teleport bei Framerateeinbruch, Actor-/Duplikatprüfung |
| `CharacterMotor`, `PlayerMovementDefinition`, `FallRespawn` | `Avatar5.Player` (neu) | CharacterController-Kollision, Schwerkraft, Teleport/Respawn-Ereignis, `IPlayerCommandSink` + `IPlayerPoseSource` |
| `PlayerInputAdapter`, `IsometricCameraRig`, `CameraProfile` | `Avatar5.Input` | Input Actions → `PlayerCommand` (WASD/Pfeile/linker Stick, Maus-Bodenprojektion/rechter Stick, Cast/Interact/Dodge/Pause/Zoom), Rebinding-JSON-Vertrag; Cinemachine 3.1.7 mit gesperrtem Isowinkel, gedämpftem Follow, Zoomgrenzen, Snap bei Respawn |
| `AimIndicatorView`, `MovementDebugHud` | `Avatar5.Presentation` (neu) | Zielmarker und Debug-HUD, lesen nur `PlayerPose` |
| `Avatar5PlayerInput.inputactions`, `PlayerMovementDefinition.asset`, `IsometricCameraProfile.asset` | Assets | Action Map `Gameplay`, Schemes Keyboard&Mouse / Gamepad |
| `P01_TestArena.unity`, `Player_Placeholder.prefab`, Greybox-Materialien | Assets | 40×40-m-Arena aus Unity-Primitives: Randwände, Kisten, Säulenslalom, diagonale Wand, 2,8-m-Korridor, flache Stufe, Rampe; in den Build Settings |
| `Avatar5P01ArenaBuilder` | Editor | Menü `Avatar5/Setup/Build P01 Test Arena` bzw. `-executeMethod …BuildFromCommandLine` erzeugt Arena, Prefab und Daten-Assets neu |

**Spielen:** `Assets/_Game/Scenes/P01_TestArena.unity` öffnen → Play. WASD/linker Stick bewegen, Maus/rechter Stick zielen, Mausrad zoomt, Esc/Start pausiert die Eingabe.

## Ausgeführte Prüfungen (16.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode)

Ausgeführt in einer Kopie des Projekts, weil der Editor das Original geöffnet hatte. Ergebnis-XML: `docs/testlaeufe/`.

| Lauf | Kommando | Ergebnis |
|---|---|---|
| Kompilierung + Arena-Generierung | `Unity.exe -batchmode -projectPath <projekt> -executeMethod Avatar5.EditorTools.Avatar5P01ArenaBuilder.BuildFromCommandLine -quit` | Exit 0, keine Compilerfehler |
| EditMode | `Unity.exe -batchmode -projectPath <projekt> -runTests -testPlatform EditMode -testResults EditMode-results.xml` | **30/30 bestanden** (4 bestehende P00-Tests + 26 P01-Fälle) |
| PlayMode | `Unity.exe -batchmode -projectPath <projekt> -runTests -testPlatform PlayMode -testResults PlayMode-results.xml` | **25/25 Avatar5-Tests bestanden**; zusätzlich 2 bestandene + 2 vom Paket selbst ignorierte Input-System-Fixture-Tests (`testables`) |

Abgedeckt: acht Richtungen mit gleicher Geschwindigkeit/Strecke (Logik und echter CharacterController), Deadzone/Analogwert, Kamerarelativität, unabhängiges Zielen mit Maus und rechtem Stick über echte Bindings (virtuelle Geräte), Stick-Loslassen hält Ziel, Maus außerhalb des Fensters, Wandstopp ohne Geschwindigkeitsaufbau, Gleiten an Wänden, kein Tunneln bei 1,5-s-Framehängern, sehr hohe Bildraten, Teleport/Respawn mit Kamera-Snap, Kamera folgt Spieler und bleibt bei 16:9 und 21:9 zentriert, Zoomgrenzen, fremde ActorId/Duplikate abgewiesen, deterministische Command-Sequenzen, Szenen-Smoke-Test der Arena inklusive Kollision mit Kiste.

Erster Lauf fand einen echten Fehler (Figur bewegte sich bei sehr hohen Bildraten nicht, weil `CharacterController.minMoveDistance` und eine Epsilon-Schranke kleine Schritte verwarfen); behoben und durch Tests abgesichert.

## Nicht ausgeführt / offen

- **Kein manueller Spieltest** mit echter Tastatur/Maus/Controller, kein Feel-Tuning der Werte (Tempo 6,5 m/s, Beschleunigung 55, Drehung 1080°/s, Kamera 40°/45°, Ortho-Größe 8).
- **Kein Windows-Build** für diesen Stand erstellt.
- Lokale Occlusion-Behandlung der Kamera (Hindernisse vor dem Spieler ausblenden) fehlt; geplant mit P07-Outline/Fade.
- Dodge/Cast/Interact werden nur als Absicht im `PlayerCommand` geliefert; Ausführung gehört zu P02.
- Netzwerk: kein NGO-Code. `IPlayerCommandSink` ist der Andockpunkt für P03; Host-Validierung der maximalen Strecke und Connection-Ownership folgen dort.
- Input-Rebinding-UI (P07); nur JSON-Speichern/Laden ist vorhanden.
- Toon-Shader/Outline (P07a), Schaden/Fähigkeiten/Gegner/Weltfortschritt/Persistenz.

## Technische Einschränkungen / Hinweise

- `SequenceDeduplicator` setzt strikt steigende Sequenznummern voraus; Wraparound und Out-of-order-Buffering sind später zu lösen.
- `Packages/manifest.json` enthält `testables: ["com.unity.inputsystem"]`, damit `InputTestFixture` verfügbar ist; dadurch laufen 4 Paket-eigene Tests im PlayMode mit.
- Der Arena-Generator überschreibt `P01_TestArena.unity` und `Player_Placeholder.prefab`; manuelle Änderungen dort gehen beim erneuten Generieren verloren.

## Nächster sinnvoller Schritt

Kurzer manueller Spieltest der Arena mit Maus/Tastatur und Controller zum Feintuning, danach P03 (Online-Sessions mit NGO) auf Basis von `IPlayerCommandSink`/`PlayerPose` oder P07a (Toon-Outline-Testszene).
