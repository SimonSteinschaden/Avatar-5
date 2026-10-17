# Avatar5 – tatsächlicher Implementierungsstand

**Zeitpunkt:** 17.10.2026 · **Stand `main`:** P01 Spielersteuerung und isometrische Kamera + P07a Toon-Shading & schwarze Konturen (beide gemerged) · **In Arbeit (aufeinander aufbauende Branches):** P03 S03-01 Sessions (`feature/p03-sessions`) → S03-02 Netzwerk-Spieler (`feature/p03-network-player`) → P02 S02-01 Kampfkern (`feature/p02-combat-core`) → P04 S04-01 erster Gegner (`feature/p04-first-enemy`) → P05 S05-01 Weltzustand + Level01-Greybox (`feature/p05-world-core`) → P06 S06-01 Spielstand (`feature/p06-save-core`)

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

### P02 Story S02-01 – Kampfkern: `fireball` und `water_wave` hostautoritativ (Branch `feature/p02-combat-core`, baut auf S03-02 auf)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `TeamKind`, `StatusFlags`, `CombatEventKind`, `ReactionKind`, `ActorCombatState`, `ICombatService`, `IAbilityCatalog`, `ICombatEventStream`, `ICombatStateSource` | `Avatar5.Contracts` | Verträge §3d |
| `AbilityDefinition`, `AbilityCatalog`, `CombatRules`/`ReactionRule`, `StarterAbilities`, `CombatSimulation`, `ICombatWorld` | `Avatar5.Gameplay` (engine-frei) | Validierte Fähigkeitsdaten; Cooldown, Energie (+15/s), Loadout, Sequenz-Duplikate; Projektil logisch simuliert (Wand/erstes Ziel), Kegel sofort; Brennen (5/s in 0,5-s-Schritten), Nass; Reaktionen Dampf und Löschen als Regeltabelle; jeder Treffer (Cast, Ziel) genau einmal; Besiegt genau einmal |
| `AbilityDefinitionAsset`, `fireball.asset`, `water_wave.asset` | `Avatar5.Combat` (neu) | ScriptableObject-Authoring → Runtime-Definition |
| `CombatHost`, `NetworkCombatActor`, `CombatEventDeduplicator` | `Avatar5.Networking` | Host simuliert, schreibt Leben/Energie/Status in NetworkVariables, verschickt Events + Cues gebündelt; Clients deduplizieren; Trainingspuppen (5000 LP) werden vom Host gespawnt, Wiederbelebung nach 3 s |
| `CombatCueView`, `CombatFeedbackView`, `HealthBarView`, `PresentationBus`, `ActorRegistry` | `Avatar5.Presentation` / `Avatar5.Foundation` | Greybox-Feuerball, Wellenblitz, Schadens-/Reaktionstexte, Lebensbalken, Statusfarbe, lokales Kampf-HUD – nur aus Host-Ereignissen |
| `TrainingDummy.prefab`, Erweiterung `Avatar5P03NetworkArenaBuilder` | Assets/Editor | Spieler-Prefab mit Kampfwerten (100 LP, 100 Energie, Loadout Feuerball/Wasserwelle), 2 Puppen vor den Startpunkten |
| `scripts/P02-CombatTest.ps1` | – | Kampf-Mehrprozesstest |

Eingabe: neue Action `CastSecondary` (rechte Maustaste, linker Trigger) → Slot 1; `Cast` → Slot 0.

**Spielen:** wie oben in `P03_NetworkArena`; linke Maustaste Feuerball, rechte Maustaste Wasserwelle auf die Puppen vor dem Start. Erst Welle, dann Feuerball → „Dampf!“; erst Feuerball, dann Welle → „Gelöscht!“.

### P04 Story S04-01 – Erster Gegner „Brute“ mit angekündigtem Bodenschlag (Branch `feature/p04-first-enemy`, baut auf S02-01 auf)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `EnemyBrain`, `EnemyDefinition`, `EnemyTargetCandidate`, `StarterEnemies` | `Avatar5.Gameplay` (engine-frei) | Zustände Idle/Approach/Telegraph/Attack/Recover/Return/Dead; Zielwahl mit Anti-Fokus und Hysterese; Leine 18 m; Angriff 0,9 s angekündigt, Zielpunkt eingefroren |
| `RaiseTelegraph`, `TelegraphStarted`, `brute_slam` | P02-Kern | Bodenschlag = Kreis (Kegel 180°) 2,4 m, 18 Schaden, Rückstoß 1,5 m, über dieselben Kampfregeln |
| `EnemyBrainHost`, `EnemyCatalog` | `Avatar5.Networking` | Host-only Entscheidung, NavMeshAgent-Bewegung, Zustand als NetworkVariable; Clients sehen nur Transform, Zustand, Cues |
| `CombatHost` (erweitert) | `Avatar5.Networking` | Gegner-Spawns, Kandidatenliste mit Anti-Fokus, Wiederbelebung pro Actor, NavMesh-Bake beim Laden; besiegte Spieler laufen/zaubern nicht |
| `CombatCueView` (erweitert) | `Avatar5.Presentation` | roter Bodenring, der sich bis zum Schlag füllt; brauner Schlag-Blitz |
| `Enemy_Brute.prefab`, `brute_slam.asset`, `NavMeshSurface` an der Arena | Assets/Editor | Brute 12 m vor den Startpunkten (außerhalb der Aggro-Reichweite) |
| `scripts/P04-EnemyTest.ps1` | – | Gegner-Mehrprozesstest |

### P05 Story S05-01 – Weltzustand und Level01-Greybox (Branch `feature/p05-world-core`, baut auf S04-01 auf)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `WorldObjectKind`, `WorldStateCodes`, `ElementImpact`, `IWorldStateService` | `Avatar5.Contracts` | Verträge §3f |
| `LevelDefinition`, `WorldObjectDefinition`, `WorldStateService` | `Avatar5.Gameplay` (engine-frei) | Validierung (eindeutige Ids, Verweise, genau ein Ziel); Hebel mit Host-Reichweitenprüfung; verkettete Tore; Feuerschale per Element-Einschlag; Ziel → `LevelCompleted` genau einmal; Snapshot/Restore ohne Ereignisse |
| `ImpactRaised` | P02-Kern | Projektil-Ende und Kegelbereich melden, wo ein Element die Welt trifft |
| `LevelAuthoring`, `WorldObjectAuthoring` | `Avatar5.World` (neu) | Szenen-Authoring mit stabilen Ids; Darstellung (Hebel kippt, Tor senkt sich und gibt Collider/NavMesh frei, Flamme an) |
| `WorldHost` | `Avatar5.Networking` | Host-Weltzustand, Interagieren (E), Einschläge aus dem Kampf, Zielprüfung; Änderungen an alle, Snapshot für Nachzügler |
| `Level01.unity`, `Avatar5P05Level01Builder` | Assets/Editor | Start mit Hebel → Tor A; Brute-Bereich mit Deckung; Feuerschale → Tor B (braucht auch Tor A); Ziel. Menü `Avatar5/Build/Level01 Player` |
| `SessionBot`-Route, `scripts/P05-Level01Test.ps1` | – | Bots spielen das Level per Wegpunkt-Route (laufen, benutzen, kämpfen, auf Punkt feuern) |

**Spielen:** `Level01.unity` öffnen → Play → Sitzung erstellen. Zum Hebel rechts vorne, **E** → Tor A öffnet sich; Brute besiegen; Feuerball auf die Schale rechts hinten → Tor B; ins grüne Ziel. Banner „Level geschafft!“.

### P06 Story S06-01 – Spielstand SaveGameV1 (Branch `feature/p06-save-core`, baut auf S05-01 auf)

| Baustein | Assembly | Inhalt |
|---|---|---|
| `SaveGameV1`, `SaveWorldObject`, `SavePlayerRecord`, `SaveLoadError`, `SaveLoadResult` | `Avatar5.Contracts` | Verträge §3g |
| `SaveValidator`, `RewardLedger`, `SaveSnapshots` | `Avatar5.Gameplay` (engine-frei) | Schema-/Inhalts-/Id-Prüfung, einmalige Belohnungen, Welt erfassen/wiederherstellen |
| `SaveJson`, `AtomicSaveStore` | `Avatar5.Persistence` (neu) | JSON, Migration Schema 0 → 1, atomisches Schreiben mit Backup, Laden mit Backup-Fallback |
| `WorldHost` (erweitert), `CampaignSaveSettings`, Lobby-Schalter | `Avatar5.Networking` | Speichern an Checkpoints (Tor offen, Level geschafft), Fortsetzen vor Spielbeginn, verständliche Lobby-Meldung bei unbrauchbarem Stand |
| `scripts/P06-SaveTest.ps1` | – | Speichern → Neustart → Fortsetzen (mit Client) → fremde Version |

**Spielen:** in `Level01` Hebel ziehen (Tor A = Checkpoint), Spiel beenden, neu starten, in der Lobby „Spielstand fortsetzen“ anhaken, Sitzung erstellen → Tor A ist offen.

**Spielen (Brute):** in `P03_NetworkArena` Richtung Norden laufen; die Brute bemerkt Spieler ab 10 m, kündigt mit rotem Ring an und schlägt zu, wenn er voll ist. Rausgehen hilft.

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

## Ausgeführte Prüfungen P06 S06-01 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| EditMode | **140/140 bestanden** (neu: 9 Fälle Speichern/Laden: Rundreise, Backup bei kaputter Hauptdatei, Absturz vor dem Tausch, fehlender Stand, fremdes Schema ohne Backup-Fallback, Migration Schema 0, Inhalts-/Id-Abweichung, ungültiger Stand wird nicht geschrieben, Belohnung einmalig) |
| PlayMode | **34 bestanden, 2 übersprungen** (Paket-Tests) |
| `scripts/P06-SaveTest.ps1` (Level01-Player) | **10/10**: Checkpoint beim Öffnen von Tor A gespeichert (Schema 1, UTC-Zeit, Hebel + Tor A an); Neustart mit Fortsetzen stellt Hebel + Tor A vor Spielbeginn her, ein beitretender Client sieht denselben Stand; Stand mit Schema 9 wird mit klarer Meldung abgelehnt, Level startet frisch, Datei bleibt unverändert |
| Regression `P05-Level01Test.ps1` | **11/11** |

Ergebnisse: `docs/testlaeufe/P06_2026-10-17_*`.

## Ausgeführte Prüfungen P05 S05-01 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| EditMode | **131/131 bestanden** (neu: 7 Fälle Weltzustand) |
| PlayMode | **34 bestanden, 2 übersprungen** (Paket-Tests) |
| Windows-Player `Level01` | gebaut |
| `scripts/P05-Level01Test.ps1` | **11/11**: **allein** komplett durchgespielt (Hebel, Tor A, Brute, Schale, Tor B, Ziel); **zu dritt** durchgespielt, alle 3 Routen am Ziel; ein 4. Client, der erst nach dem Hebel beitrat, sah sofort Tor A offen (Snapshot) und am Ende denselben Weltzustand wie der Host |
| Regression `P04-EnemyTest.ps1` / `P03-NetworkPlayerTest.ps1` | **18/18** / **8/8** |

Ergebnisse: `docs/testlaeufe/P05_2026-10-17_*`.

## Ausgeführte Prüfungen P04 S04-01 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| EditMode | **124/124 bestanden** (neu: 8 Fälle Gegnerhirn und Bodenschlag) |
| PlayMode | **34 bestanden, 2 übersprungen** (Paket-Tests); neu: Brute kündigt an, bevor der Schlag den Host trifft (genau 18 Schaden) |
| `scripts/P04-EnemyTest.ps1` (Host + 2 Clients, Bots kämpfen gegen die Brute) | **18/18**: 4 Ankündigungen, 2 Schläge (jeder Schlag angekündigt; zweimal starb die Brute mitten in der Ankündigung), 4 Treffer auf Spieler, Brute 2× besiegt; jeder Client hat denselben Endstand wie der Host bei gleicher Ereigniszahl (245), 0 Duplikate, identische Spieler-Lebenspunkte |
| Regression `P02-CombatTest.ps1` / `P03-NetworkPlayerTest.ps1` | **25/25** / **8/8** |

Zwei Testprobleme wurden dabei behoben: Die Brute stand anfangs in Aggro-Reichweite und griff den Host an, bevor die Clients verbunden waren (Clients können frühere Ereignisse nicht kennen) – sie steht jetzt 12 m entfernt. Im Bewegungstest liefen zwei Bots je nach zufälliger Beitrittsreihenfolge ineinander; die Clients treten jetzt gestaffelt bei. Ergebnisse: `docs/testlaeufe/P04_2026-10-17_*`.

## Ausgeführte Prüfungen P02 S02-01 (17.10.2026, Unity 6000.3.26f1, Windows 11, Batchmode, separater Klon)

| Lauf | Ergebnis |
|---|---|
| EditMode | **116/116 bestanden** (neu: 12 Kampfkern-Fälle inkl. 120 Casts mit Wiederholungen → eindeutige, steigende EventIds, Leben = Summe der Schadensereignisse; identische Ereignisfolge bei gleichen Eingaben) |
| PlayMode | **33 bestanden, 2 übersprungen** (Paket-Tests); neu: Host-Feuerball über `CombatHost` trifft die gespawnte Puppe genau einmal, doppelte Sequenz castet nicht zweimal, Energie wird verbraucht |
| `scripts/P02-CombatTest.ps1` (4 Prozesse, alle Bots zaubern auf die Puppen, Client 3 versucht alle 0,05 s zu zaubern) | **25/25**: 49 angenommene Casts, Dampf 4×, Löschen 4×; jeder Client erhielt dieselben 471 Ereignisse wie der Host, 0 Duplikate; Puppen-Leben auf allen 4 Rechnern identisch und gleich der Summe der Schadensereignisse |
| Regression `scripts/P03-NetworkPlayerTest.ps1` | **8/8** |

Der erste Kampf-Build stürzte ab, weil drei MonoBehaviours in einer Datei lagen (Unity braucht Dateiname = Klassenname); aufgeteilt, danach lief alles. Ergebnisse: `docs/testlaeufe/P02_2026-10-17_*`.

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
- Netzwerk-Spieler: keine Client-Prediction (eigene Figur reagiert mit einer Netzwerk-Rundreise Verzögerung; laut Architektur erst nach Latenzmessung entscheiden). Kein manueller Spieltest mit zwei Personen, keine Latenz-/Verlustsimulation. Dodge/Interact werden übertragen, aber noch nicht ausgeführt. Late Join nur, solange die Arena geladen ist; Szenenwechsel fehlt noch.
- Input-Rebinding-UI (P07); nur JSON-Speichern/Laden ist vorhanden.
- P02: erst 2 von 24 Fähigkeiten, nur Feuer/Wasser; kein Rückstoß-Feedback auf dem Client außer der replizierten Position; Zauber verzögern sich um eine Netzwerk-Rundreise (keine Vorhersage). `CombatSimulation` merkt sich alle (Cast, Ziel)-Paare der Session (wächst mit der Spielzeit, für Level-Längen unkritisch, später kürzen). Kein manueller Spieltest, keine Balance-Prüfung.
- P04: ein Archetyp, kein EncounterDirector, keine Skalierung nach Spielerzahl, kein Boss; kein Stuck-Handling über das NavMesh hinaus; Gegner-Definition noch als Code (`EnemyCatalog`), Asset folgt mit S04-02. Spieler werden am Ort wiederbelebt (Checkpoint-Regel kommt mit P05/P06).
- P05: Level01 ist ein Greybox-Gerüst von wenigen Minuten, nicht das M2-Level (15–30 min, 1–2 Elementrätsel, 3 Gegnertypen, Mini-Boss). Kein Hub, keine Levelübergänge/Szenenwechsel im Netz, kein Checkpoint-Neustart (Snapshot/Restore existiert, wird aber noch nicht genutzt), kein QuestGraph. Bots laufen gerade Linien; ein echter Spieltest fehlt.
- P06: noch keine Progression (XP, Talente, `IStatProvider`), keine Checkpoint-Spawnpunkte (Spieler starten nach Fortsetzen am Levelstart), Spieler-Lebenspunkte werden gespeichert, aber noch nicht wiederhergestellt; nur ein Kampagnen-Slot; keine Rotation über mehr als ein Backup; Text zur Besitzregel steht nur in der Doku, noch nicht im Spiel.
- P07a: kein manueller Sichttest durch eine Person, keine Flimmermessung bei Kamerabewegung, keine Messung auf schwächerer GPU (Mittelklasse-Referenz noch festzulegen). Transparente Objekte ohne Screen-Space-Konturen. P01-Spieler und P01-Arena nutzen noch Greybox-Materialien ohne Ink-Volume.

## Technische Einschränkungen / Hinweise

- `SequenceDeduplicator` setzt strikt steigende Sequenznummern voraus; Wraparound und Out-of-order-Buffering sind später zu lösen.
- `Packages/manifest.json` enthält `testables: ["com.unity.inputsystem"]`, damit `InputTestFixture` verfügbar ist; dadurch laufen 4 Paket-eigene Tests im PlayMode mit.
- Der Arena-Generator überschreibt `P01_TestArena.unity` und `Player_Placeholder.prefab`; manuelle Änderungen dort gehen beim erneuten Generieren verloren.
- Der P07a-Generator überschreibt `VisualStyle_Test.unity`; Materialien, Meshes und Profile werden in-place aktualisiert (GUIDs bleiben stabil).
- URP hat `PC_Renderer.asset` beim Hinzufügen des Ink-Features migriert (Asset-Version 3). Beim Öffnen ergänzt URP ggf. `DefaultVolumeProfile.asset` um einen (deaktivierten) `InkOutlineVolume`-Eintrag.

## Nächster sinnvoller Schritt

Projekt mit Unity Cloud verknüpfen und Relay-Join zwischen zwei PCs testen (M1-Abnahme: `P03_NetworkArena` mit 2–4 Spielern über das Internet). Danach P06 S06-02 (XP, Talente, `IStatProvider`, Belohnungen über den Ledger), P04 S04-02 (weitere Archetypen + EncounterDirector), P02 auf 4 Elemente × 2 Fähigkeiten und Level01 auf M2-Umfang. Nebenbei: P01-Spieler/-Arena auf `Avatar5/Toon` + Ink-Volume umstellen und einen Windows-Build des Gesamtstands erstellen.
