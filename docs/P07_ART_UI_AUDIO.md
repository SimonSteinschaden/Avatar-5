# P07 — Comicgrafik, schwarze Konturen, UI, Audio und Asset-Pipeline

**Priorität:** P0 für Style-Prototyp; P1 für finale Assets · **Vorgänger:** P00/URP; States/Events aus P01–P06 · **Nachfolger:** P08 Performance/Release

## Ziel und Referenz
Das Spiel erhält einen **eigenständigen stilisierten Fantasy-Look mit deutlich schwarzen Außenkonturen und ausgewählten internen Objektkanten**, inspiriert von der visuellen Lesbarkeit und Comicwirkung von *Borderlands*. Die Umsetzung basiert auf Unity URP. Keine marken-/urheberrechtlich geschützten Grafiken, Logos, Charaktere, Texturen oder Eins-zu-eins-Rekonstruktionen übernehmen.

## Input
- URP Renderer/Pipeline-Konfiguration aus P00.
- `ActorPose`, `AbilityCastAccepted`, `DamageApplied`, `StatusChanged`, `EncounterCompleted`, `WorldStateChanged`, `SessionState`, `ProgressionStateChanged`.
- Free-/CC0-/eindeutig kommerziell erlaubte 3D-Modelle, Sounds, Texturen samt **Herkunfts-/Lizenzdaten**.
- Kamera-Winkel/typischer Maßstab aus P01 und Performanceziel 1080p/60 FPS aus P08.

## Konkrete Aufgaben / In Scope

1. Frühestmöglichen Style-Spike mit Grundkörpern in separater Unity-Szene erstellen; stilisierte Silhouette und schwarze Innenkanten visuell freigeben lassen.
2. Wiederverwendbare URP-Toon-Materialien, Outline-Renderer-Feature und Grafikqualitätsprofile entwickeln und technisch dokumentieren.
3. Charakter-/Umgebungs-Assetpipeline, Animation-Controller und Import-/Lizenzregeln festlegen.
4. Ereignisgesteuerte Darstellung von Combat-, World-, Session- und Progression-Zuständen samt SFX/VFX-/UI-Feedback integrieren.
5. Text- und Gamepad-Lesbarkeit, Skalierung sowie 1080p-Performance gegen Golden-Tests abnehmen.

## Visuelle Designbibel (verbindlich für v1)
1. **Silhouetten:** 1–3 Pixel Schwarzlinie bei 1080p als Startwert, skaliert nach Auflösung/Distanz; Hauptfiguren, Gegner und wichtige Props deutlich, unwichtige Vegetation zurückhaltend.
2. **Interne Kanten:** ausgewählte Linien an Tiefe-/Normalen-Sprüngen. Vermeide überzeichnetes Flimmern in Texturen, Haaren und transparenten Effekten.
3. **Toon-/Cel-Lighting:** abgestufte diffuse Hell-/Dunkel-Bänder (z. B. 2–4 Stufen), sparsame harte Highlights, stylisierte Schatten, keine fotorealistischen PBR-Details als Standard.
4. **Palette:** kräftige, unterscheidbare Elementfarben: Feuer (warm orange/rot), Wasser (cyan/blau), Erde (ocker/stein), Luft (helltürkis/weiß). Schwarze Konturen überall, aber nicht die komplette Szene abdunkeln.
5. **Kontrast und UX:** rot/gelb für Gefahr/Telegraph, blau/neutral für Support, klare Ability-Radien. Nicht nur Farbe zur Information verwenden.
6. **Umgebungen:** handgezeichnet wirkende einfache Materialien; lesbare Wege, hohe Silhouettenklarheit; keine ungefilterten Mix-and-Match-Fototexturen.
7. **3D-Assets:** moderat polygonarm bis mittelkomplex; Skelett-Animationen, LODs und Normals sauber; Einheitenskalierung 1 Unity Unit ~ 1 m.

## Technischer Rendering-Auftrag
- **Erste Wahl:** URP `ScriptableRendererFeature` / Render-Graph-kompatibler Screen-Space-Outline-Pass, der Tiefe und Normalen zur Kantenerkennung nutzt; konfigurierbare Intensität, Dicke, Tiefenschwelle, Normalenschwelle und Kategorien-Maskierung. API an die installierte URP-Version anpassen.
- **Optionale Ergänzung:** gezieltes Inverted-Hull-/Mesh-Expansion-Outline für wichtige Spielfiguren, wenn Silhouette in Screen-Space nicht ausreichend stabil ist.
- **Toon Surface Shader:** Shader Graph oder HLSL, konfigurierbare Lichtstufen und Material-Farbrampen. Custom Lighting muss mit URP-Version getestet werden; keine automatische Shader-Graph-Funktion voraussetzen.
- **Effektpipeline:** `VisualCue` mit `cueId`, `position`, `direction`, `serverTick`, `seed`; lokale VFX Graph/ParticleSystem/AudioSource-Steuerung. Nur Cue-IDs über Netzwerk, keine Partikelzustände.
- **Qualitätsprofile:** Low/Medium/High mit reduzierbaren Kanten-/Transparenz-/Partikeleffekten; deaktivierbares Outline/Foto-Sensitivitätsprofil als Option.
- **Proof of Style:** Screenshots (Spieler, Gegner, Terrain, UI) unter identischer Kamera bei 1080p; aus 5–10 m Kameraentfernung klar lesbar und ohne auffälliges temporales Flimmern.

## UI & UX-Arbeiten
Start/Optionen/Lobby mit Invite-Code, Charakterwahl, HP/Energie, vier Ability-Slots und Cooldown, Koop-Spielernamen, Boss-Health, Pings, Interaktion, Missionsziele, Talentbaum, Speichern/Checkpoint-Hinweise. M0–M2 Debug-UI genügt; finale Darstellung später. Gamepad-Navigation und Rebinding berücksichtigen.

## Audio-Arbeiten
Element-SFX nach vier Klangfamilien, Cast/Hit/Status/Warning, Bossphasen, UI-Klicks, atmosphärische Musik. Audio lokal aus Events triggern und begrenzen (Voice-Pooling / Audio-Mixer), keine permanenten Soundtracks über Netzwerk senden. Audio/Voice Chat separat; kein Voice Chat für v1.

## Asset-/Lizenz-Pipeline
- Originaldatei, Quelle/URL, Asset-Name, Autor, Lizenztyp, Lizenztext-Link, Lizenzdatum, kommerzielle Nutzbarkeit, Attribution, Änderungen, eingebundene Szene erfassen.
- Fremd-Assets nie ohne klare Lizenz importieren. Asset-Store-Lizenzen und Mixamo-Bedingungen projektspezifisch prüfen.
- Lizenzen für Schriftarten, Sound und Musik ebenfalls registrieren; kommerzielle Nutzung und Vertrieb abdecken. Siehe `13_ASSET_LIZENZEN.md`.
- Einheitliche Mesh-/Material-Konvention: benannte Import-Presets, FBX/glTF bevorzugt, Shaderkompatibilität, Prefabs pro Asset, Dokumentation fehlender Anis/Texturen.

## Output / Schnittstellen
| Input | Presentation-Aufgabe | Output |
|---|---|---|
| `ActorPose` | Lauf-/Aim-Animation, Kamerafollow | lokales Bild; *kein* Game-State-Write |
| `CombatEvent`/`VisualCue` | VFX/Toon-Hit/HUD-SFX | Unity-Particles, Audio und UI |
| `SessionState` | Join-/Lobby-/Reconnect-UX | `Create/Join/Leave` Commands via P03-Port |
| `ProgressionState` | Skilltree-UI | `TalentChoice` an P06-Port |
| URP Depth/Normals | Ink-Edge Renderer Feature | Konturlayer auf Framebuffer |

P07 darf **keine Schadensergebnisse, Spielerpositionen oder Questfreigaben direkt setzen**. Buttons senden Commands an den zuständigen Port.

## Akzeptanz
- In einer Demo mit Player, Enemy, Felsen, Holz und Wasser sind schwarze Außenlinien sowie relevante innere Kanten konsistent sichtbar.
- Konturen bleiben bei Kamera-Bewegung sauber, transparente Fähigkeiten verdecken kein kritisches Telegraphing; 1080p/60-FPS-Ziel auf definierter Testhardware messbar.
- Alle wichtigen Elemente sind durch Silhouette, Farbe *und* Form erkennbar; auch bei reduzierten VFX-Einstellungen.
- Zwei kostenlose Drittanbieter-Assets und mindestens ein selbst erstelltes Material sind lizenzgeprüft, korrekt importiert und sichtbar integriert.
- Menü- und Koop-UI sind mit Maus/Tastatur und Controller bedienbar.
- Der Stil wirkt eigenständig; keine Referenz-Assets aus Borderlands/Avatar übernommen.

## Übergabe
**P08** erhält Golden-Screenshot-Szenen, Grafikqualitätsprofile, UI-E2E-Pfade, Performance-Messszenen und Asset-/Lizenzregister. Alle anderen Pakete liefern Events/States und dürfen keine eigenen konkurrierenden VFX-Synchronisationen erzeugen.
