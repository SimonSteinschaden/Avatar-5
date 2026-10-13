# P01 — Spielersteuerung, Bewegung, Zielen, isometrische Kamera

**Priorität:** P0 · **Vorgänger:** P00 · **Nachfolger:** P03 Networking, P02 Kampf, P07 Presentation

## Ziel
Ein in einer 3D-Greybox direkt steuerbarer Platzhaltercharakter mit isometrischer Kamera, unabhängigem Zielen und einer **reproduzierbaren Eingabe-Schnittstelle**, die später vom Host verarbeitet werden kann.

## Input
- Input System, URP-Szene und Bootstrap aus P00.
- Werte aus `PlayerMovementDefinition` (Tempo, Beschleunigung, Drehung, Kollisionsgröße).
- Gameplay-Commands und IDs aus `10_SCHNITTSTELLENVERTRAEGE.md`.
- Lokale Geräte: Tastatur/Maus, Controller.

## Aufgaben / In Scope
1. Input Actions: `Move(Vector2)`, `Aim(Vector2/PointerWorldRay)`, `Cast(slot)`, `Interact`, `Dodge`, `Pause`; Input als pro Frame normalisierte Werte erfassen; World-Aim auf Boden-/Zielebene projizieren.
2. `PlayerInputAdapter` wandelt Raw Input in `PlayerCommand` mit `sequenceId` und `clientTick` um. Ein Adapter pro lokaler Person (v1 nur ein lokaler Spieler pro Client).
3. `CharacterMotor` verarbeitet abstrakte Bewegungsintents, Geschwindigkeiten, Kollision und Rotation; keine Kenntnis von Unity Relay oder HUD.
4. Kamera mit konfigurierbarer Neigung, Distanz, Orbit standardmäßig gesperrt, gedämpftem Follow, Zoomgrenzen, lokaler Occlusion-Behandlung und sicherem Kamera-Fallback bei Respawn.
5. Platzhaltermodell, Hindernisse, Wall-Collision, Debug-HUD für Bewegungs-/Zielrichtung, kurze Teststrecke.
6. Input-Rebinding als Contract vorbereiten (vollständiges UI kommt in P07).

## Out of Scope
Anzugs-/Animationsqualität, Schaden, online replizierte Bewegung, vollständige Controller-Konfigurationsscreens.

## Output / Artefakte
- Unity Player Prefab mit Motor, Input Adapter, Aim Point und visueller Platzhalterdarstellung.
- `PlayerInputActions` und `CameraProfile` als Assets.
- Ein `PlayerCommand`-Stream und lesbare `PlayerPose`-Snapshots über Interfaces.
- Testarena sowie EditMode-Tests für Normalisierung/Sequenzen, PlayMode-Tests für Kollision/Kamera.

## Schnittstellen
| Eingehend | Ausgehend | Verbraucher |
|---|---|---|
| `InputAction`-Werte | `PlayerCommand{entityId,seq,tick,move,aim,action}` | P03 Command-Ingress; lokal P02 nach Host-Brücke |
| `ActorState`, `PlayerPose` | Kamera-Target und Rotation/Animation-Lesezustand | P07 |
| `CameraProfile` | Bildschirm-/World-Aim-Hit | P02 Targeting |

`PlayerCommand` ist **eine Absicht**, keine autoritative Weltkoordinate. Der Client darf keine endgültige Position erzwingen. Der Host validiert maximale Strecke/Bewegungsregeln, später auch Stun/Slow.

## Edge Cases
Controller abgesteckt, Maus außerhalb Spielbereich, Pause, Respawn, Szene gewechselt, Tod/Stun, mehrfacher Cast-Button, Zoom nahe Geometrie, Spiel im Hintergrund.

## Akzeptanzkriterien
- Bewegung in acht Richtungen, diagonal nicht schneller als axial; unabhängiges Zielen mit Maus oder rechter Stickachse.
- Hindernisse stoppen Bewegung, keine Teleportation bei Framerateeinbruch.
- Kameraführung bleibt bei 16:9 und 21:9 stabil; fokussiert Actor nach Respawn.
- Motor lässt sich im EditMode mit vorgefertigten `PlayerCommand`-Sequenzen testen.
- Im Netzwerk mit P03: Zwei Instanzen besitzen unabhängigen lokalen Input, beobachten jeweils Remote-Motor-Pose, niemals Remote-Controllerrechte.

## Übergabe
**P03** übernimmt Command-Stream/Player Prefab zur Synchronisierung; **P02** konsumiert Cast- und Aim-Intents; **P07** nutzt Pose/Action-Events für Animation und Kameraeffekte. Kein P01-Code soll direkt ein Combat-Ergebnis berechnen.
