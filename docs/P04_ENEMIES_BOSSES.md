# P04 — Gegner-KI, Encounter-System und Bosse

**Priorität:** P1 (Vertical Slice P0) · **Vorgänger:** P02 Combat, P03 Host, P05 Navigationsumgebung (stufenweise) · **Nachfolger:** P05 Campaign, P06 Rewards, P07 Animation/VFX

## Ziel
Klar lesbare, kooperationsfähige Gegner, die in einer hostautoritativen Welt kämpfen. Zum Release: 8 wiederverwendbare Archetypen und 4 Bosse; im Vertical Slice zunächst 3 Archetypen und 1 Mini-Boss.

## Input
- `CombatEvent`, `ActorState`, `ICombatService` aus P02.
- Host-Tick und Netzwerk-Actor-Lifecycle aus P03.
- `NavMesh`, SpawnVolume, EncounterDefinition, LevelBounds und PatrolRoutes aus P05.
- `EnemyDefinition/AttackDefinition` (Health, Resistance, Range, TelegraphDuration, TargetRules, LootTableId).

## Aufgaben
1. `EnemyBrain` als testbare State Machine (`Idle`, `Patrol`, `Alert`, `Approach`, `Telegraph`, `Attack`, `Recover`, `Stagger`, `Dead`).
2. Entscheidungslogik nur auf Host. Clients zeigen replizierte Intent-/Pose-/Attack-Cues und Animation.
3. Navigation, Stuck-Handling und Rückkehr ins Encounter-Gebiet; keine unendliche Verfolgung über Levelgrenzen.
4. Zielauswahl in Koop: Nähe, Threat und Cooldown; Anti-Focus-Logik, damit stets alle Spieler sinnvoll reagieren können.
5. Attack-Telegraphing (Bodenmarker, Aufladezeit) als Gameplay-Datenwert; tatsächlicher Schaden über P02, nicht im Animator.
6. EncounterDirector spawnt/entspawnt definierte Gruppen, skaliert vereinbarte Parameter nach Spielerzahl (nicht einfach HP mal vier), meldet Completion.
7. Boss-Phase-Machine mit 2–3 Phasen, Unterbrechung/Telegraph und Checkpointregel.
8. Editor-Debuganzeigen für AI-State, gewähltes Ziel, NavPath, Cooldowns, EncounterId.

## Nicht im Paket
Nutzersteuerung, Missions-/Speicherlogik, Netzwerktransport, Endgrafik, procedurale Open World.

## Output / Artefakte
- Prefab-Archetypen mit eindeutigem `EnemyTypeId`, EncounterDefinitions, BossPhaseDefinitions.
- `IEncounterService` zur Aktivierung/Pause/Completion; `EnemyStateChanged`, `EncounterCompleted`, `BossPhaseChanged`.
- Greybox-Arena für Solo-, Duo-, Viererspieler-Abnahme und AI EditMode-/PlayMode-Tests.

## Contracts
| Konsumiert | Verarbeitet | Liefert |
|---|---|---|
| `CombatEvent` | Schaden/Status/Tod interpretiert | AI-State / Gegneranimation an P07 |
| `HostTick` + `WorldQuery` | KI tickt auf Host | `AbilityRequest`/AttackIntent an P02 |
| `EncounterStart{levelId,encounterId,playerCount}` | Spawn/Wave/Bosslogik | `EncounterCompleted` an P05/P06 |
| `BossPhaseDefinition` | Phasenwechsel | `BossPhaseChanged` an P03/P07 |

Die KI konsumiert Combat-Events, schreibt aber niemals direkt HP. Gegnerfähigkeiten laufen durch die gleichen P02-Regeln wie Spieleraktionen.

## Akzeptanz
- Gegner navigieren um Hindernisse, agieren lesbar, haben keine Hänger in der Standardarena.
- Alle Angriffe sind vor dem Treffer erkennbar. Kein Doppel-Damage durch Animation+Kampfcode.
- Solo und vier Spieler bestehen dieselbe Encounter-State-Machine; Skalierung ist getestet.
- Boss hat definierte Phasen, bricht beim Host-Disconnect sauber ab, erlaubt keinen doppelten Reward.
- Event-Replay eines abgeschlossenen Encounters bleibt idempotent.

## Übergabe
**P05** nutzt `EncounterCompleted` für Gates/Storyfortschritt; **P06** verwendet eindeutige Encounter-/Boss-Reward-Ids; **P07** baut Animationen, SFX und Angriffstelegraphen auf den gelieferten Cues auf.
