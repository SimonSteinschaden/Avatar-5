# Avatar5 – tatsächlicher Implementierungsstand

**Zeitpunkt:** 17.10.2026 · **Stand `main`:** P01 Spielersteuerung und isometrische Kamera + P07a Toon-Shading & schwarze Konturen (beide gemerged) · **In Arbeit:** P03 S03-01 Sessions (`feature/p03-sessions`) und S03-02 Netzwerk-Spieler (`feature/p03-network-player`)

## Implementiert / als Source vorhanden

- Architektur- und Spezifikationsdokumente P00–P08, Schnittstellenverträge, Agentenregeln
- Git-Hygiene (.gitignore/.gitattributes), reproduzierbare lokale Git-Historie
- Unity 6.3 LTS (6000.3.26f1) URP-Projekt mit Starter-Szenen (Commit `96135bb`)
- Unity-unabhängige C#-Contracts: ElementKind, PlayerCommand, AbilityRequest, CombatEvent, WorldEvent, PresentationCue, VersionContracts, **PlayerPose, IPlayerCommandSink, IPlayerPoseSource (P01)**, **SessionConfig, SessionState, SessionError, ISessionService (P03)**
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

### P03 Story S03-01 – Private Session erstellen, beitreten, verlassen (Branch `feature/p03-sessions`)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `SessionConfig`, `SessionState`, `SessionError`, `SessionJoinResult`, `ISessionService` | `Avatar5.Contracts` | Session-Port, siehe `10_SCHNITTSTELLENVERTRAEGE.md` §3b |
| `ConnectionPayload`, `SessionAdmission`, `AdmissionReasonCodec`, `JoinCodes` | `Avatar5.Gameplay` (engine-frei) | Versions-Handshake (Protokoll + Content), max. 4 Plätze, Ablehnungsgrund an Client, Code-Normalisierung, `IP:Port`-Parser |
| `NetcodeSessionServiceBase`, `DirectSessionService`, `RelaySessionService`, `SessionRuntime`, `SessionDebugLobby` | `Avatar5.Networking` (neu) | NGO-Approval vor jedem Spawn, Spielerzahl per Named Message an Clients, Host-Ende mit Grund „HostEnded“, Direkt-UDP (LAN/Test), Unity Sessions + Relay, OnGUI-Lobby mit Code/Status/Slots/deutschen Fehlertexten, Kommandozeilen-Automation |
| `P03_SessionTest.unity`, `Avatar5P03SessionSceneBuilder` | Assets/Editor | Testszene; Menü `Avatar5/Build/P03 Session Test Player` |
| `scripts/P03-SessionTest.ps1` | – | Mehrprozess-Test Host + 5 Clients |

Pakete neu: `com.unity.netcode.gameobjects` 2.13.3, `com.unity.transport` 2.7.4, `com.unity.services.multiplayer` 2.3.3 (Versionen = Empfehlung des installierten Editors). Betrieb/Fehlercodes: `docs/P03_SESSIONS_BETRIEB.md`.

### P03 Story S03-02 – Netzwerk-Spielerfiguren mit Ownership (Branch `feature/p03-network-player`, baut auf S03-01 auf)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `ActorIds`, `HostCommandIngress`, `CommandRateLimiter`, `CommandSendThrottle` | `Avatar5.Gameplay` (engine-frei) | ActorId aus Verbindung, Sender-/Ownership-Prüfung, Token-Bucket 60/s (Burst 20), Client-Drossel |
| `NetworkPlayerAvatar`, `NetPlayerCommand`, `NetPoseExtras` | `Avatar5.Networking` | Owner schickt nur Absichten per RPC; Host simuliert `CharacterMotor`; `NetworkTransform` (Server-Autorität, Interpolation); Kamera/HUD/Zielmarker folgen der eigenen Figur |
| `NetworkPlayerSpawner` | `Avatar5.Networking` | Host spawnt pro Verbindung eine Figur an einem freien von 4 Startpunkten; Despawn bei Verlassen durch NGO |
| `SessionBot` | `Avatar5.Networking` | nur für Tests: `-a5-bot-move`, `-a5-bot-delay`, `-a5-bot-hijack`, `-a5-report-players` |
| `NetworkPlayer.prefab`, `P03_NetworkArena.unity`, `Avatar5P03NetworkArenaBuilder` | Assets/Editor | aus P01-Platzhalter und P01-Arena generiert (P01-Dateien bleiben unverändert); Netzwerk-Tickrate 20 |
| `scripts/P03-NetworkPlayerTest.ps1` | – | Mehrprozess-Test mit 4 Spielern |

Kleine P01-Ergänzungen (nur Lese-Properties): `CharacterMotor.Definition`, `PlayerInputAdapter.Actions`, `AimIndicatorView.Marker`. `RequestStatus.RejectedRateLimited` neu (Contracts §3c).

**Spielen (LAN/gleicher PC):** `P03_NetworkArena.unity` öffnen → Play → „Private Sitzung erstellen“. Zweite Instanz (Build `Avatar5/Build/P03 Network Arena Player`) → Code `127.0.0.1:7777` → Beitreten. Jede Instanz steuert nur ihre eigene Figur.

### P07a – Toon-Shading und schwarze Konturen (Style-Prototyp)

| Baustein | Ort | Inhalt |
|---|---|---|
| `Avatar5/Toon` (HLSL) | `Assets/_Game/Rendering/Shaders` | 2–4 Lichtstufen, Schattenfarbe, Übergangsweichheit, Hauptlicht + Forward+-Zusatzlichter, Schatten, Specular/Rim, Pinsel-Wobble, DepthNormals mit Ink-Kategorie |
| `Avatar5/Toon Water`, `Avatar5/Toon Hull Outline` | dito | Comic-Wasser mit Tiefenstufen/Schaum/Uferlinie; optionale Inverted-Hull-Kontur (evaluiert, nicht Standard) |
| `InkOutlineRendererFeature` + `InkOutlineVolume` | `Avatar5.Rendering` | Render-Graph-Pass: Silhouetten über Tiefe, Innenlinien über Normalen, getrennte Dicke/Intensität, Schwellen, Farbe, Ein/Aus, Kategorie-Masking, Debug-Ansichten; im `PC_Renderer` registriert, ohne Volume inaktiv |
| `ToonQualityProfile` ×3, `GraphicsQualityController` | dito + `Rendering/Settings` | Low / Medium / High zur Laufzeit, ohne Projekt-Assets zu verändern |
| `GraphicsDebugOverlay`, `LightingPresetSwitcher`, `VisualStyleCameraRig`, `VisualStyleBenchmark` | dito | Debug-Modus (F1–F6, `[`/`]`), 4 Lichtpresets, eigenständige Isometrie-Kamera (P01-Werte 40°/45°, Ortho 8), Frametime-Messung per Kommandozeile |
| 28 Toon-Materialien | `Assets/_Game/Rendering/Materials` | Feuer, Wasser, Erde, Luft, Neutral (Holz/Stein/Metall/Stoff), Umgebung, Figuren, Gegner |
| `VisualStyle_Test.unity` | `Assets/_Game/Scenes` | Held, Gegner, Fluss mit Wasser, Holzbrücke und Pavillon, Ruinen/Steinblöcke, Felsen, Bäume, Element-Objekte, Kantentest-Sockel, gerichtetes Licht mit Schatten |
| Generatoren/Werkzeuge | `Assets/_Game/Rendering/Editor` | `Avatar5/Setup/Build P07a Visual Style Test`, `…/Capture P07a Screenshots`, `Avatar5/Build/P07a Visual Style Benchmark` |

**Ansehen:** `Assets/_Game/Scenes/VisualStyle_Test.unity` öffnen (Konturen sind auch im Edit Mode sichtbar) → Play für Debug-Overlay und Hotkeys. Details: `docs/P07a_RENDERING_IMPLEMENTATION.md`, Stilregeln: `docs/P07a_VISUAL_STYLE_GUIDE.md`, Bilder: `docs/p07a_screenshots/`. Der Stil ist von Maya vorläufig akzeptiert (17.10.2026).

## Ausgeführte Prüfungen P03 S03-02 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| EditMode | **104/104 bestanden** (neu: 9 Fälle Ingress/Rate-Limit/Drossel) |
| PlayMode | **32 bestanden, 2 übersprungen** (Paket-Tests); neu: Solo-Host-Figur wird gespawnt, gehört dem Host, bewegt sich durch Host-Befehle |
| Windows-Player `P03_NetworkArena` | gebaut |
| `scripts/P03-NetworkPlayerTest.ps1` (4 Prozesse, Direkt-UDP, 60 FPS) | **8/8**: Host sieht 4 Figuren; Client 1 bewegt sich +X (8,5 m), Client 2 −Z (8,5 m), Client 3 −X (20,5 m); Host-Figur bleibt stehen; >4000 gefälschte Befehle von Client 3 für fremde Figuren abgewiesen und ohne Wirkung; 0 Rate-Limit-Treffer bei normalen Clients; Client 2 sieht Client 1 an derselben Stelle wie der Host |
| Regression `scripts/P03-SessionTest.ps1` | **11/11** weiterhin bestanden |

Ergebnisse: `docs/testlaeufe/P03_2026-10-17_S03-02_*`.

## Ausgeführte Prüfungen P03 S03-01 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| Frischer Import mit neuen Paketen, Kompilierung | Exit 0, keine Compilerfehler |
| EditMode (`-runTests -testPlatform EditMode`) | **95/95 bestanden** (64 Avatar5.Tests.EditMode inkl. 30 neue Session-Regel-Fälle, 31 Rendering) |
| PlayMode (`-runTests -testPlatform PlayMode`) | **31 bestanden, 2 übersprungen** (Paket-eigene Input-System-Tests); neu: Solo-Host verbunden 1/4 und verlassen, zweites Erstellen abgewiesen, ungültiger Code, niemand unter der Adresse → `SessionNotFound` |
| Windows-x64-Development-Player (nur `P03_SessionTest`) | gebaut |
| Mehrprozess-Test `scripts/P03-SessionTest.ps1` (6 Prozesse, Direkt-UDP auf einem PC) | **11/11**: Host 4/4, Client sieht 4/4, 5. Spieler `SessionFull`, Protokoll 99 `VersionMismatch`, Verlassen → 3/4, Host beendet → Clients `HostEnded` |

Ergebnisse: `docs/testlaeufe/P03_2026-10-17_*`. Der erste Mehrprozess-Lauf (7/11) fand zwei echte Fehler, beide behoben: gleichzeitige Beitritte wurden doppelt gezählt (3. Spieler fälschlich „voll“), und Clients sahen beim Host-Ende nur „Verbindung verloren“.

## Ausgeführte Prüfungen P07a (16./17.10.2026, Unity 6000.3.26f1, Windows 11, RTX 4060, Batchmode mit GPU)

| Lauf | Ergebnis |
|---|---|
| Kompilierung + Szenengenerierung (`VisualStyleSceneBuilder.BuildFromCommandLine`) | Exit 0, keine Compilerfehler/-warnungen aus P07a-Code |
| EditMode auf dem P07a-Branch | **35/35 bestanden** (4 P00 + 31 P07a) |
| Probe-Merge mit P01 (`a5570e7`, identischer Code wie dieser Merge), EditMode | **61/61 bestanden** |
| Probe-Merge mit P01, PlayMode | **27 bestanden, 2 übersprungen** (Paket-eigene Input-System-Tests) |
| Windows-x64-Development-Player (nur `VisualStyle_Test`) | gebaut und gestartet; Benchmark 1080p: GPU-Median Low 0.75 ms, Medium 1.16 ms, High 1.34–1.49 ms, Ink-Pass ≈ 0.14 ms |
| 1080p-Screenshots (13 Stück, gleiche Kamera) | `docs/p07a_screenshots/` |

Beim Merge nach `main` gab es Konflikte nur in `docs/IMPLEMENTATION_STATUS.md` und `docs/13_ASSET_LIZENZEN.md`; der Code entspricht dem getesteten Probe-Merge. Auf dem Merge-Commit selbst wurden keine weiteren Unity-Läufe ausgeführt.

## Ausgeführte Prüfungen P01 (16.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode)

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
- P03: **Relay/Internet nicht getestet**, weil das Projekt noch nicht mit einem Unity-Cloud-Projekt verknüpft ist (`cloudProjectId` leer); der Relay-Adapter ist nur kompiliert. Kein Test über zwei PCs/Internetanschlüsse (M1-Abnahme), keine Latenz-/Verlustsimulation, keine Netzmetriken, kein Rejoin, kein Szenenwechsel. Keine manuelle Bedienung der Debug-Lobby durch eine Person.
- Netzwerk-Spieler: keine Client-Prediction (eigene Figur reagiert mit einer Netzwerk-Rundreise Verzögerung; laut Architektur erst nach Latenzmessung entscheiden). Kein manueller Spieltest mit zwei Personen, keine Latenz-/Verlustsimulation. Dodge/Cast/Interact werden übertragen, aber noch nicht ausgeführt (P02). Late Join nur, solange die Arena geladen ist; Szenenwechsel fehlt noch.
- Input-Rebinding-UI (P07); nur JSON-Speichern/Laden ist vorhanden.
- Schaden/Fähigkeiten/Gegner/Weltfortschritt/Persistenz.
- P07a: kein manueller Sichttest durch eine Person, keine Flimmermessung bei Kamerabewegung, keine Messung auf schwächerer GPU (Mittelklasse-Referenz noch festzulegen). Transparente Objekte ohne Screen-Space-Konturen. P01-Spieler und P01-Arena nutzen noch Greybox-Materialien ohne Ink-Volume.

## Technische Einschränkungen / Hinweise

- `SequenceDeduplicator` setzt strikt steigende Sequenznummern voraus; Wraparound und Out-of-order-Buffering sind später zu lösen.
- `Packages/manifest.json` enthält `testables: ["com.unity.inputsystem"]`, damit `InputTestFixture` verfügbar ist; dadurch laufen 4 Paket-eigene Tests im PlayMode mit.
- Der Arena-Generator überschreibt `P01_TestArena.unity` und `Player_Placeholder.prefab`; manuelle Änderungen dort gehen beim erneuten Generieren verloren.
- Der P07a-Generator überschreibt `VisualStyle_Test.unity`; Materialien, Meshes und Profile werden in-place aktualisiert (GUIDs bleiben stabil).
- URP hat `PC_Renderer.asset` beim Hinzufügen des Ink-Features migriert (Asset-Version 3). Beim Öffnen ergänzt URP ggf. `DefaultVolumeProfile.asset` um einen (deaktivierten) `InkOutlineVolume`-Eintrag.

## Nächster sinnvoller Schritt

Projekt mit Unity Cloud verknüpfen und Relay-Join zwischen zwei PCs testen (M1-Abnahme: `P03_NetworkArena` mit 2–4 Spielern über das Internet). Danach P02 (`fireball`, `water_wave` hostautoritativ), P04 (erste Gegner), P05 Level 1. Nebenbei: P01-Spieler/-Arena auf `Avatar5/Toon` + Ink-Volume umstellen und einen Windows-Build des Gesamtstands erstellen.
