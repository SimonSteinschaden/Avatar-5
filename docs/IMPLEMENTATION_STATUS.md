# Avatar5 – tatsächlicher Implementierungsstand

**Zeitpunkt:** 17.10.2026 · **Letzte Story:** P07a Toon-Shading & schwarze Konturen (Branch `feature/p07a-toon-outlines`, Basis `main`)

> Hinweis: P01 (Steuerung/Kamera) liegt parallel auf `feature/p01-steuerung-kamera` und ist in diesem Branch nicht enthalten. Beim Zusammenführen beide Fassungen dieses Dokuments vereinen; ein Probe-Merge beider Branches kompilierte konfliktfrei und bestand alle Tests (siehe unten).

## Implementiert / als Source vorhanden

- Architektur- und Spezifikationsdokumente P00–P08, Schnittstellenverträge, Agentenregeln
- Git-Hygiene (.gitignore/.gitattributes), reproduzierbare lokale Git-Historie
- Unity 6.3 LTS (6000.3.26f1) URP-17.3-Projekt mit Starter-Szenen (Commit `96135bb`)
- Unity-unabhängige C#-Contracts: ElementKind, PlayerCommand, AbilityRequest, CombatEvent, WorldEvent, PresentationCue, VersionContracts
- Gameplay-Infrastruktur: pro-Actor SequenceDeduplicator
- MonoBehaviour-Bootstrap, einfaches Entwicklungsmenü, Editor-Setup für Starter-Szenen, Windows-Development-Build-Befehl

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

**Ansehen:** `Assets/_Game/Scenes/VisualStyle_Test.unity` öffnen (Konturen sind auch im Edit Mode sichtbar) → Play für Debug-Overlay und Hotkeys. Details: `docs/P07a_RENDERING_IMPLEMENTATION.md`, Stilregeln: `docs/P07a_VISUAL_STYLE_GUIDE.md`, Bilder: `docs/p07a_screenshots/`.

## Ausgeführte Prüfungen (16./17.10.2026, Unity 6000.3.26f1, Windows 11, RTX 4060, Batchmode mit GPU)

Ausgeführt in einem separaten Klon, weil der Editor das Original geöffnet hatte. Ergebnis-XML/CSV: `docs/testlaeufe/`.

| Lauf | Ergebnis |
|---|---|
| Kompilierung + Szenengenerierung (`VisualStyleSceneBuilder.BuildFromCommandLine`) | Exit 0, keine Compilerfehler/-warnungen aus P07a-Code |
| EditMode auf diesem Branch | **35/35 bestanden** (4 P00 + 31 P07a) |
| Probe-Merge mit P01 (`a5570e7`), EditMode | **61/61 bestanden** |
| Probe-Merge mit P01, PlayMode | **27 bestanden, 2 übersprungen** (Paket-eigene Input-System-Tests) |
| Windows-x64-Development-Player (nur `VisualStyle_Test`) | gebaut und gestartet; Benchmark 1080p: GPU-Median Low 0.75 ms, Medium 1.16 ms, High 1.34–1.49 ms, Ink-Pass ≈ 0.14 ms |
| 1080p-Screenshots (13 Stück, gleiche Kamera) | `docs/p07a_screenshots/` |

## Nicht ausgeführt / offen

- Kein manueller Sichttest durch eine Person im Editor, keine automatisierte Flimmermessung bei Kamerabewegung (Auto-Pan F6 zur Sichtprüfung vorhanden).
- Keine Messung auf schwächerer GPU; die „Mittelklasse-Referenz“ (Vorschlag: RTX 4060) ist noch festzulegen. Kein Unity-Profiler-Fenster-Capture (Messung programmatisch über `FrameTimingManager`).
- Transparente Objekte ohne Screen-Space-Konturen; Konturen für P01-Spieler, P04-Gegner, P05-Welt entstehen, sobald deren Szenen ein Ink-Volume bekommen und `Avatar5/Toon`-Materialien nutzen.
- P01: Steuerung/Kamera (eigener Branch); P03 Netzwerk; Schaden/Fähigkeiten/Gegner/Weltfortschritt/Persistenz; UI/Audio (P07b ff.).

## Technische Einschränkungen / Hinweise

- `SequenceDeduplicator` setzt strikt steigende Sequenznummern voraus; Wraparound und Out-of-order-Buffering sind später zu lösen.
- Der P07a-Generator überschreibt `VisualStyle_Test.unity`; Materialien, Meshes und Profile werden in-place aktualisiert (GUIDs bleiben stabil).
- URP hat `PC_Renderer.asset` beim Hinzufügen des Features migriert (Asset-Version 3). Beim Öffnen ergänzt URP ggf. `DefaultVolumeProfile.asset` um einen (deaktivierten) `InkOutlineVolume`-Eintrag.

## Nächster sinnvoller Schritt

Szene im Editor sichten und Stil freigeben (Linienstärke, Palette, Night-Preset). Danach P01-Spieler und P01-Arena auf `Avatar5/Toon` + Ink-Volume umstellen, sobald P01 gemerged ist, und eine Mittelklasse-Referenz-GPU für P08 festlegen.
