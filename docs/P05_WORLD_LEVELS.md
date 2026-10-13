# P05 — Spielwelt, Level, interaktive Objekte und Kampagne

**Priorität:** P1 (erster Level P0) · **Vorgänger:** P00, P02, P03, P04 · **Nachfolger:** P06 Progression, P07 Presentation, P08 Tests

## Ziel
Ein endliches Abenteuer aus **Hub + fünf handgebauten Hauptlevels** mit eigenständiger Elementfantasy, Gegner-Encounters, Schaltern, Rätseln, Checkpoints und definierten Übergängen. Jedes Level ist auch allein lösbar; Koop eröffnet schnellere oder alternative Lösungswege.

## Input
- `LevelDefinition` mit `levelId`, Szenenreferenz, `spawns`, `encounters`, `puzzles`, `checkpoints`, Siegbedingungen.
- `CombatEvent`/`StatusChanged` aus P02; `EncounterCompleted` aus P04; `SessionState`/Host-Szenenwechsel aus P03.
- Art-/Audio-Assets mit Lizenzen aus P07; Player- und AI-Nav-Geometrie.

## Aufgaben
1. Szenengrenzen, Asset-Streaming/Loading-Bildschirm, Level- und Hub-Fluss definieren.
2. `IWorldQuery`: Kollision, Sichtlinie, navigierbare Flächen und Target-Lookup für Kampf-/KI-Systeme bereitstellen.
3. `IWorldStateService`: serverseitige Interactable-/Puzzle-/Door-/Checkpoint-States, eindeutige WorldObjectIds, synchronisierte Änderungen.
4. Elementrätsel als Daten-getriebene Bedingungen und Effekte: z. B. Wasser bewegt Mechanik; Erde stabilisiert Schalter; Luft treibt Windrad; Feuer entzündet markierte Quelle. Kein Puzzle fordert mehrere Menschen zwingend.
5. `QuestGraph` mit Abschnitten (`Find`, `Unlock`, `Defeat`, `Reach`, `Interact`) und State-Transitionen; persistierbare, versionsstabile QuestNodeIds.
6. Encounters an Aktivierungsvolumes und Story-Gates anschließen; Trennung von Rätselzustand vs. optischer Türanimation.
7. Multiplayer-Levelübergänge: Host initiiert; alle Clients bestätigen Szenenbereitschaft, dann sicher spawnen.
8. M2: Level 1 mit 15–30 Minuten, 1–2 Elementrätseln, 3 Gegnertypen und Mini-Boss. Content-Complete: fünf Levels mit angemessener Gesamtspielzeit durch Playtests nachweisen.

## Nicht im Paket
Procedurale Open World, umfangreiche NPC-Dialogbäume, Ingame-Cutscene-Produktion, vollwertiger Questeditor eines Drittanbieters.

## Output
- `Hub`, `Level01` … `Level05` mit LevelDefinition/SceneContext; zunächst Level01 vollständig.
- `PuzzleDefinition`, `QuestDefinition`, `WorldStateChanged`, `CheckpointReached`, `LevelCompleted`.
- Level-Baking/Validation-Tool für IDs, fehlende Assets, Spawnpunkte und Szenenlinks.

## Contracts
| Input | World-Transition | Output / Verbraucher |
|---|---|---|
| `InteractRequest{actorId,worldObjectId}` | Host prüft Entfernung/Queststatus | `WorldStateChanged` an P03/P07/P06 |
| `CombatEvent{targetId,status}` | Rätselzustand prüfen | `PuzzleSolved` an P03/P06/P07 |
| `EncounterCompleted` | Gate öffnen / Quest fortsetzen | `CheckpointReached`, `LevelCompleted` an P06 |
| `LoadLevelRequest(levelId)` | Host + NGO-SceneManager | `LevelReady` an P01/P03/P07 |

WorldState wird ausschließlich von World-Modul geändert. Save speichert Daten, schreibt keine selbst erfundenen Missionsfortschritte.

## Akzeptanz
- Level 1 ist solo und zu viert komplett abschließbar; kein Koop-Zwangsrätsel.
- Neustart am Checkpoint setzt persistente Rätsel-, Gate- und Questzustände korrekt zurück/wieder her.
- Clients beobachten dieselben Interactable-States, niemand kann eine verschlossene Tür nur lokal öffnen.
- Keine doppelt getriggerten Level-Complete-Events; Szenenwechsel bei Netzverzögerung stabil.
- Alle verwendeten Level-Assets haben gültige License-Einträge.

## Übergabe
**P06** erhält vollständige `Level/World/QuestSnapshot`-Daten und Checkpoint-Events; **P07** erhält Szenen-/Puzzle-/Story-Cues zur Darstellung; **P08** erhält klar definierte E2E-Testpfade.
