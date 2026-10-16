# P07a – Rendering-Implementierung (Toon-Shading & schwarze Konturen)

**Stand:** 17.10.2026 · **Branch:** `feature/p07a-toon-outlines` (Basis `main` @ `96135bb`, P01 nicht enthalten) · **Status:** Style-Prototyp implementiert und in Unity getestet

## 1. Geprüfte Ist-Konfiguration

| Punkt | Tatsächlicher Wert (geprüft) |
|---|---|
| Unity | 6000.3.26f1 (`ProjectSettings/ProjectVersion.txt`, Batchmode-Log) |
| URP | `com.unity.render-pipelines.universal` 17.3.0 (Package-Cache, `package.json`) |
| Render Graph | aktiv: `m_EnableRenderCompatibilityMode: 0`, Define `URP_COMPATIBILITY_MODE` nicht gesetzt (Test `PcRenderer_HasActiveInkFeature_OnRenderGraph`) |
| Aktive Pipeline | Qualitätsstufe „PC“ → `Assets/Settings/PC_RPAsset.asset` → `PC_Renderer.asset` |
| Renderer | Forward+ (`m_RenderingMode: 2`), Depth Texture an, Opaque Texture an, MSAA aus, SSAO-Feature vorhanden |
| Grafik-API / GPU (Tests, Messung) | Direct3D 12, NVIDIA GeForce RTX 4060, Intel i5-12400F, Windows 11 |

Keine Paket- oder Versionsänderungen. Keine Drittanbieter-Assets.

## 2. Ordnerstruktur (Rendering-Bereich)

```
Assets/_Game/Rendering/
  Runtime/   Avatar5.Rendering.asmdef      – Renderer-Feature, Volume, Qualitätsprofile, Debug-Overlay,
                                              Lichtpresets, Kamera-Rig der Testszene, Benchmark
  Shaders/                                  – Avatar5/Toon, Avatar5/Toon Water, Avatar5/Toon Hull Outline,
                                              Hidden/Avatar5/InkOutline (+ HLSL-Includes)
  Materials/                                – 28 Toon-Materialien + Wasser + Hull
  Settings/                                 – ToonQuality_Low/Medium/High, VisualStyle_InkVolume
  Editor/    Avatar5.Rendering.Editor.asmdef – Generatoren, Screenshot-Tool, Benchmark-Build
Assets/_Game/Art/VisualStyleTest/Meshes/    – prozedurale Meshes (Felsen, Kristall, Kegel, Torus, Wasser)
Assets/_Game/Scenes/VisualStyle_Test.unity
Assets/_Game/Tests/Rendering/               – Avatar5.Tests.Rendering (EditMode)
```

`Avatar5.Rendering` referenziert nur URP/Core/Input System, **keine** Gameplay-, Contracts-, Player- oder Netzwerk-Assemblies (per Test abgesichert). P07a schreibt keinen Spielzustand. Eigenes Assembly statt `Avatar5.Presentation`, damit es keine Kollision mit der gleichnamigen Assembly aus dem P01-Branch gibt; spätere Zusammenführung ist eine reine Strukturentscheidung.

## 3. Toon-Shader `Avatar5/Toon` (HLSL)

Handgeschriebenes HLSL statt Shader Graph: Custom Lighting ist in Shader Graph für URP nicht offiziell vorgesehen, und HLSL gibt volle Kontrolle über Passes und Varianten.

| Pass | LightMode | Zweck |
|---|---|---|
| ToonForward | UniversalForward | Cel-Shading, Hauptlicht mit empfangenen Schatten, Zusatzlichter (Forward+ Cluster-Loop `_CLUSTER_LIGHT_LOOP`), Fog |
| ShadowCaster | ShadowCaster | URP-eigener `ShadowCasterPass.hlsl` |
| DepthOnly | DepthOnly | URP-eigener `DepthOnlyPass.hlsl` |
| DepthNormals | DepthNormals | Normalen für Konturen; **Alpha-Kanal trägt die Ink-Kategorie** (siehe 4.3) |

**Beleuchtungsmodell (`ToonLighting.hlsl`):** Half-Lambert `x = N·L·0.5+0.5 + Offset + Pinsel-Rauschen`, begrenzt durch die Schattendämpfung (Schlagschatten springen dadurch ebenfalls in die Schattenstufe) → `A5ToonBands` quantisiert in 2–4 Stufen mit einstellbarer Übergangsbreite → Farbe = `lerp(Basis × Schattenfarbe × Ambient, Basis × Lichtfarbe, Stufe)`. Danach optional harte Glanzlichter, Rim-Licht, Emission. Zusatzlichter in zwei harten Stufen.

**Material-Parameter:** Basisfarbe/-textur, Schattenfarbe (wird mit der Basis multipliziert und färbt Schatten farbig statt schwarz), Lichtstufen (2–4), Übergangsweichheit, Licht/Schatten-Offset, Ambient-Einfluss, Specular (Farbe/Größe/Weichheit), Rim (Farbe/Schwelle/Stärke), Pinsel-Wobble (Stärke/Skalierung), Farbvariation, Emission, Inner Line Weight, Exclude From Ink Lines.

**Performance-Maßnahmen:** ein CBUFFER für alle Passes (SRP-Batcher-kompatibel), GPU Instancing an, keine Texturpflicht, nur notwendige Multi-Compile-Keywords, globales Keyword `A5_TOON_SIMPLE` (Low) entfernt Rauschen/Specular/Rim und erzwingt 2 Stufen.

**Wasser `Avatar5/Toon Water`:** transparent, Tiefenstufen aus `_CameraDepthTexture` (flach → tief), animierte Wellenstreifen, Uferschaum und dünne dunkle Uferlinie, Schatten-Empfang, Vertex-Wellen. Als transparentes Objekt erzeugt Wasser selbst keine Screen-Space-Linien.

## 4. Konturen: `InkOutlineRendererFeature`

### 4.1 Technik

- `ScriptableRendererFeature` mit `ScriptableRenderPass.RecordRenderGraph` (nur Render Graph, keine Compatibility-Mode-APIs wie `Execute`/`OnCameraSetup`).
- Ein Raster-Pass (`AddRasterRenderPass`), Fullscreen-Dreieck mit Alpha-Blending auf `activeColorTexture`, Zeitpunkt `BeforeRenderingTransparents` (Wasser/VFX liegen darüber).
- Eingaben über `ConfigureInput`: Depth immer, Normals nur wenn Innenlinien aktiv. Zugriff auf `UniversalResourceData.cameraDepthTexture` / `cameraNormalsTexture` mit `builder.UseTexture`.
- Parameter kommen aus dem Volume-Override `InkOutlineVolume` (Volume-Framework). **Standard: aus.** Nur Szenen mit einem Volume, das `Enable Ink` setzt, zeichnen Linien – die P01-Arena und die Starter-Szenen bleiben unverändert (Test `OtherScenes_DoNotEnableInkLines`).

### 4.2 Kantenerkennung

- **Außen-/Silhouettenlinien:** maximaler Sprung der linearen Augentiefe (orthografisch und perspektivisch korrekt linearisiert) zwischen Pixel und 4 bzw. 8 Nachbarn im Radius *Dicke/2*. Schwelle = `Depth Threshold + Distance Scale × Tiefe`, erhöht bei streifendem Blickwinkel (verhindert falsche Linien auf Schrägen).
- **Innenlinien:** maximaler Normalenunterschied `1 − N·N'` zu Nachbarn mit **kontinuierlicher** Tiefe (Tiefensprünge zählen als Silhouette). Ausblendung mit Distanz (`Inner Fade Start/End`), damit ferne Details nicht zu Liniensalat werden.
- Getrennte Dicke und Intensität für außen und innen. Dicken in Pixeln bei 1080p, skaliert mit der Renderhöhe.
- **Flimmerreduktion:** bilineare Nachbar-Samples (Sub-Pixel-Radien → weiche, stabile Linien statt 1/2-Pixel-Springen), weiche Rampe über der Schwelle (`Edge Softness`), distanzabhängige Schwellen, 8-Tap-Sampling in High, FXAA/SMAA danach. Automatische Bild-zu-Bild-Flimmermessung ist **nicht** implementiert; für die visuelle Prüfung gibt es den Auto-Pan (F6).

### 4.3 Kategorie-Maskierung

Der DepthNormals-Pass von `Avatar5/Toon` schreibt in den sonst ungenutzten Alpha-Kanal von `_CameraNormalsTexture` (Format R8G8B8A8_SNorm):

| Alpha | Bedeutung |
|---|---|
| 0 | Standard: Silhouette + Innenlinien (auch alle Nicht-Avatar5-Shader, z. B. URP Lit) |
| 0…1 | Innenlinien abgeschwächt (`1 − Inner Line Weight`), z. B. Laub 0.25, Gras 0.7 |
| −1 | Objekt komplett ohne Tinte (`Exclude From Ink Lines`, z. B. Augen, Glüh-Effekte) |

Die Silhouette gehört dem vorderen Objekt (Kategorie des nächstgelegenen Samples). Damit ist Objektkategorie-Masking ohne zusätzlichen Pass möglich. Layer-Masking wurde bewusst nicht gebaut (würde einen weiteren Geometrie-Pass kosten).

### 4.4 Parameter (`InkOutlineVolume`)

| Parameter | Standard (Szene) | Wirkung |
|---|---|---|
| Enable Ink | an (Szene) / aus (global) | Effekt ein/aus |
| Color | fast Schwarz (0.02, 0.02, 0.035) | Linienfarbe, Alpha = Deckkraft |
| Silhouette Thickness | 3 px @1080p | Außenlinien |
| Inner Thickness | 1.6 px @1080p | Innenlinien |
| Silhouette / Inner Intensity | 1 / 0.85 | Deckkraft je Linientyp |
| Inner Lines | an | Innenlinien (braucht Normals-Prepass) |
| Depth Threshold | 0.18 m | Mindest-Tiefensprung |
| Depth Distance Scale | 0.006 /m | Schwelle wächst mit Distanz |
| Grazing Correction | 0.6 | Anti-Falschlinien auf Schrägen |
| Normal Threshold | 0.35 (≈ 49°) | Mindest-Normalenunterschied |
| Edge Softness | 0.5 | Breite der Anti-Aliasing-Rampe |
| Inner Fade Start/End | 36 / 60 m | Ausblenden ferner Innenlinien |
| High Quality Sampling | aus | 8 statt 4 Taps |
| Debug View | Off | Off / Lines Only / Line Types (rot = außen, cyan = innen) |

### 4.5 Entscheidung Inverted Hull

Ausgewertet mit `Avatar5/Toon Hull Outline` (Vertex-Extrusion in Clip-Space, konstante Pixelbreite) an Held und Gegner, Screenshots `07`–`09`. Ergebnis: Die Screen-Space-Silhouetten sind bei Gameplay-Distanz und in der Nahansicht geschlossen und gleichmäßig; Inverted Hull bringt keinen sichtbaren Gewinn, kostet aber einen zusätzlichen Draw pro Renderer und reißt an harten Kanten (Würfel) auf. **Entscheidung:** Screen-Space ist Standard; Hull bleibt als optionales Material für spätere Hero-Charaktere mit glatten Normalen (z. B. wenn Figuren vor sehr ähnlicher Tiefe stehen).

## 5. Qualitätsprofile

Assets `Assets/_Game/Rendering/Settings/ToonQuality_*.asset`, angewendet zur Laufzeit von `GraphicsQualityController` über ein privates globales Volume (Priorität 100), das globale Keyword und die Kameradaten. Beim Deaktivieren wird alles zurückgesetzt; Projekt-Assets (Pipeline-Asset, Renderer, Materialien) werden nicht verändert.

| | Low | Medium | High |
|---|---|---|---|
| Toon | vereinfacht (2 Stufen, ohne Wobble/Specular/Rim) | voll | voll |
| Silhouette | 2.6 px, 4 Taps | 3 px, 4 Taps | 3.2 px, 8 Taps |
| Innenlinien | aus (kein Normals-Prepass nötig) | 1.6 px, bis 60 m | 1.5 px, bis 80 m |
| Anti-Aliasing | keins | FXAA | SMAA High |
| Schatten | aus | an | an |

## 6. Grafik-Debug-Modus

`GraphicsDebugOverlay` in der Testszene (Play Mode):

| Taste | Funktion |
|---|---|
| F1 | Overlay ein/aus |
| F2 | Qualitätsprofil durchschalten |
| F3 | Konturen ein/aus |
| F4 | Linienansicht: normal / nur Linien / Linientypen farbig |
| F5 | Lichtpreset: Day / Golden Hour / Overcast / Night |
| F6 | automatischer Kamera-Pan (Flimmerprüfung) |
| `[` / `]` | Silhouettendicke −/+ 0.25 px |
| WASD, Mausrad | Kamera schwenken, zoomen |

Angezeigt: Auflösung, FPS, CPU-Frametime (Mittel/Max), GPU-Frametime (`FrameTimingManager`), GPU-Zeit des Ink-Passes (falls der Profiler-Recorder sie liefert), Profil, Lichtpreset, Linienparameter.

## 7. Performance (gemessen)

**Methode:** Windows-x64-Development-Player nur mit `VisualStyle_Test` (`Avatar5/Build/P07a Visual Style Benchmark`), Start mit `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -popupwindow -a5benchmark <csv>`. Pro Fall 2 s Aufwärmen + 8 s Messung mit laufendem Kamera-Pan, VSync aus. GPU-Zeit aus `FrameTimingManager`. Zwei Läufe am 17.10.2026, Rohdaten: `docs/testlaeufe/P07a_2026-10-17_Benchmark_Run1.csv`, `…_Run2.csv`.

| Fall (1920×1080, RTX 4060) | GPU Median Lauf 1 / 2 | GPU p95 Lauf 1 / 2 | CPU-Frame Median Lauf 1 / 2 |
|---|---|---|---|
| Low | 0.75 / 0.75 ms | 1.36 / 1.27 ms | 1.67 / 1.57 ms |
| Medium | 1.16 / 1.16 ms | 1.53 / 2.73 ms | 2.23 / 2.34 ms |
| High | 1.49 / 1.34 ms | 1.77 / 3.01 ms | 2.62 / 2.64 ms |
| Medium ohne Konturen | 1.03 / 1.02 ms | 2.21 / 2.41 ms | 2.20 / 2.22 ms |

- **Kosten des Ink-Passes (Medium):** ca. 0.14 ms GPU (Differenz der Mediane); der Profiler-Recorder für den Pass lieferte im Player keine GPU-Werte („n/a“), daher nur die Differenzmessung.
- **60-FPS-Ziel (16.7 ms):** Für diese Testszene auf der RTX 4060 klar erfüllt (alle Profile < 2 ms GPU). Die RTX 4060 ist ein Vorschlag als Referenz-„Mittelklasse“-GPU; **schwächere GPUs und echte Level-Komplexität wurden nicht gemessen.**
- Nicht gemacht: Aufnahme mit dem Unity-Profiler-Fenster (die Messung nutzt dieselben Zeitgeber programmatisch), Messung im Vollbild-Exklusivmodus (zwei Vollbild-Läufe beendeten sich in der RDP-Sitzung vorzeitig, ein früher Vollbild-Lauf lieferte vergleichbare GPU-Werte 0.84 / 1.16 / 1.34 / 1.23 ms).

## 8. URP-Einstellungen und Projektänderungen

- `Assets/Settings/PC_Renderer.asset`: Feature „Avatar5InkOutline“ hinzugefügt (Shader-Referenz serialisiert, damit er im Build enthalten ist). URP hat das Asset dabei auf `m_AssetVersion: 3` migriert; dabei sind die SSAO-Ressourcenfelder aus dem Asset verschwunden (URP 17.3 bezieht sie aus den globalen Einstellungen). Kein weiteres Projekt-Setting geändert.
- `Mobile_Renderer` unverändert (Mobile-Qualitätsstufe ist auf Standalone ausgeschlossen).
- Das Szenen-Volume `VisualStyle_InkVolume` schaltet für den Comic-Look das projektweite Neutral-Tonemapping, Vignette und Motion Blur aus (aus `SampleSceneProfile` des URP-Templates); diese hätten Schwarz und flache Farben vergraut.
- Hinweis: Öffnet man das Projekt, ergänzt URP im `DefaultVolumeProfile.asset` automatisch einen Standardeintrag für `InkOutlineVolume` (aus). Diese Änderung ist harmlos und kann committet werden.

## 9. Tests (ausgeführt)

Alle Läufe Unity 6000.3.26f1 Batchmode mit echter GPU (ohne `-nographics`), in einem separaten Klon, weil der Editor das Hauptprojekt geöffnet hatte.

| Lauf | Ergebnis | Datei |
|---|---|---|
| EditMode auf `feature/p07a-toon-outlines` | **35/35 bestanden** (4 P00 + 31 P07a) | `docs/testlaeufe/P07a_2026-10-17_EditMode.xml` |
| Integration: temporärer Merge mit `feature/p01-steuerung-kamera` (`a5570e7`), EditMode | **61/61 bestanden** | `…_Integration_P01_EditMode.xml` |
| Integration mit P01, PlayMode | **27 bestanden, 2 übersprungen** (Input-System-Paket-Tests, wie in P01 dokumentiert) | `…_Integration_P01_PlayMode.xml` |

P07a-Tests (Auszug): Shader ohne Kompilierfehler (4), Palette je Element inkl. Farbtonprüfung, Low/Medium/High-Kostenstaffelung, Feature im PC-Renderer + Render Graph, Volume standardmäßig aus, andere Szenen ohne Konturen, Assembly ohne Gameplay-Abhängigkeiten, **genau 2/3/4 Helligkeitsstufen auf einer Toon-Kugel**, Schattenseite farbig statt schwarz, Szene enthält alle geforderten Objekte, nur Avatar5-Shader, kein Fehlershader im Bild, Konturen an Held/Gegner/Szene (Pixelvergleich an/aus), Außen- und Innenlinien getrennt erkennbar (Debug-Farben), Rahmung und Konturen bei 1920×1080 und 2520×1080, Lesbarkeit in allen vier Lichtpresets, Profilwechsel ändert Keyword/AA/Schatten/Linien, Controller stellt alles wieder her.

**Kommandos (Projektpfad anpassen):**

```
Unity.exe -batchmode -projectPath <p> -executeMethod Avatar5.EditorTools.Rendering.VisualStyleSceneBuilder.BuildFromCommandLine
Unity.exe -batchmode -projectPath <p> -executeMethod Avatar5.EditorTools.Rendering.VisualStyleScreenshotCapture.CaptureFromCommandLine
Unity.exe -batchmode -projectPath <p> -runTests -testPlatform EditMode -testResults EditMode.xml
Unity.exe -batchmode -projectPath <p> -buildTarget Win64 -executeMethod Avatar5.EditorTools.Rendering.VisualStyleBenchmarkBuild.BuildFromCommandLine
Artifacts\P07aBenchmark\Avatar5_P07a.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -popupwindow -a5benchmark result.csv
```

## 10. Bekannte Einschränkungen

- Transparente Objekte (Wasser, spätere VFX) erhalten keine Screen-Space-Konturen; Wasser hat eine eigene Uferlinie.
- Objekte mit Shadern ohne DepthNormals-Pass erscheinen nicht in den Konturen; URP-Lit-Objekte bekommen volle Konturen (Kategorie 0).
- MSAA ist im Projekt aus; Kantenglättung kommt aus der weichen Linienrampe plus FXAA/SMAA.
- Inverted Hull benötigt glatte Normalen; an harten Kanten entstehen Lücken.
- Der Szenengenerator überschreibt `VisualStyle_Test.unity`; manuelle Änderungen gehen beim Neugenerieren verloren.
- Keine automatisierte Flimmer-Messung, kein manueller Sichttest durch eine Person, keine Messung auf schwächerer GPU.
- Der Night-Preset ist bewusst dunkel; der Lichtkreis der Feuerschale ist stilisiert hart (zwei Stufen).
- Debug-Overlay nutzt IMGUI (nur Entwicklungswerkzeug).

## 11. Asset-Lizenzen

Alle Shader, Materialien, Meshes und die Szene sind Eigenarbeit (per Code erzeugt) bzw. Unity-Built-in-Primitives. Keine Fremdtexturen, -modelle oder -schriften. Eintrag `P07a-001` in `docs/13_ASSET_LIZENZEN.md`.
