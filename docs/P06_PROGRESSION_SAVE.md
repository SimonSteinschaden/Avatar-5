# P06 — Charakterfortschritt, Talente und Speicherung

**Priorität:** P1 · **Vorgänger:** P02 Combat, P03 Sessions, P05 World · **Nachfolger:** P07 UI, P08 Release

## Ziel
Verlässlicher, verständlicher Fortschritt über eine 3–5-stündige Kampagne: Skills freischalten, Talentpunkte verteilen, Checkpoints verwalten und laufende Spiele nach Neustart wiederherstellen — ohne eine externe Datenbank zu benötigen.

## Input
- `ActorDefeated`, `EncounterCompleted`, `LevelCompleted`, `CheckpointReached` aus P02/P04/P05.
- `PlayerIdentity` (lokale Identität, nicht automatisch ein plattformübergreifender Account) aus P03.
- Content-IDs `AbilityId`, `TalentId`, `LevelId`, `CheckpointId`, `WorldObjectId`.
- Balancing-Definitionen zu XP-Schwellen, Talenten und vier Ability-Slots.

## Aufgaben
1. Progression: Klassenwahl, Erfahrung, Talentbaum (drei kleine Pfade je Klasse), Fähigkeitenslots und freigeschaltete Perks.
2. `IStatProvider` veröffentlicht effektive Werte an P02; Berechnung ist deterministisch/testbar, keine UI-Abhängigkeit.
3. Rewards strikt idempotent über `RewardGrantId`; kein mehrfacher XP-Gewinn durch Rejoin/Doppel-Events.
4. Save-Schema versionieren (`schemaVersion`, `contentVersion`, `saveId`, UTC-Zeit, `checkpointId`, `playerRecords`, `worldSnapshot`, `completedRewardIds`).
5. Lokales Host-Speichern via temp-Datei → Validierung → atomarer Austausch, Rotations-Backup und klarer Fehlerbehandlung; sensiblen Zugriffspfad aus `Application.persistentDataPath`.
6. **Besitz-/Vertrauensregel:** Für v1 ist Kampagnenfortschritt *host-owned*; Clients erhalten für ihre Teilnahme definierte lokale, nicht sicherheitskritische Profil-/Präferenzdaten. Dauerhafter charakterspezifischer Cross-Host-Fortschritt ohne Accounts ist **nicht zugesagt**. Vor Release explizites UX/Wording festlegen.
7. Wiederherstellung: Host lädt Checkpoint, P05 rekonstruiert Weltstatus, P04 Encounters, P02 HP/Status, P03 spawnt passende NetworkObjects. Late-Join nur in freigegebenem Zustandsfenster.
8. Schema-Migration für mindestens eine ältere Testversion; bei inkompatibler Version verständliche Meldung statt stiller Datenverlust.

## Nicht enthalten
Cloud Saves zwischen Geräten, Handel, Echtgeld-Inventar, Cross-Platform Account-Service, manipulationssichere Saves, persistente Live-World.

## Output
- `SaveGameV1` JSON, Persistenzservice und dokumentierte Speicher-/Restore-Reihenfolge.
- `ProgressionStateChanged`, `TalentUnlocked`, `CheckpointSaved`, `SaveLoadFailed`.
- UI-freie Skill-/Save-API, automatische Migrationstests und Recovery-Szenarien.

## Interfaces
| Eingehend | Aufgabe | Ausgabe |
|---|---|---|
| `RewardEvent` mit Id | einmalig XP/Skillpunkte | `ProgressionStateChanged` → P07 |
| `TalentChoice` | Berechtigung und Punkte prüfen | `IStatProvider` → P02 |
| `CheckpointReached` + Snapshots | Host Save | `CheckpointSaved` → P07 |
| `LoadSave(saveId)` | Schema prüfen und migrieren | `RestoreWorldState` → P05/P03 |

## Akzeptanz
- Derselbe RewardId wird auch nach Rejoin nur einmal angerechnet.
- Ein gültiger Save reproduziert Level, Checkpoint, Quest-/Puzzle-Zustände und Host-Profil.
- Absturz bei Speicheroperation zerstört nicht den letzten gültigen Save (Backup-Fall getestet).
- Fehlende ContentId und unbekannte SchemaVersion führen zu definierter Fehlerbehandlung.
- Endkundenoberfläche erklärt, wem Koop-Fortschritt und Speicherstand gehören.

## Übergabe
P07 rendert Talent-/Save-Menüs ausschließlich anhand der veröffentlichten lesbaren States. P08 prüft Save/Load/Upgrade/Recovery im automatisierten Regressionstest.
