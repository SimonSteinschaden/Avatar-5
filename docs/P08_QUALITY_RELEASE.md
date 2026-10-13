# P08 — Tests, Performance, Sicherheit, Windows-Build und Release

**Priorität:** transversal ab P00, finale Abnahme P2 · **Vorgänger:** alle P00–P07 · **Nachfolger:** Veröffentlichung/Wartung

## Ziel
Jede vertikale Änderung bleibt überprüfbar. Am Ende existiert ein durchspielbarer, für den Windows-PC verpackter Release Candidate mit dokumentiertem Leistungsprofil, Netzwerktests, Lizenznachweisen und reproduzierbaren Builds.

## Input
- Git-Repository/Build-Scripts aus P00, Player-/Combat-/Network-/AI-/World-/Save-/Presentation-APIs aus P01–P07.
- Testaufgaben, Testdaten (AbilityDefinitions, Encounter/LevelDefinitions), Golden-Screenshots.
- Liste der unterstützten Windows-Versionen und definierte Referenzhardware.

## Konkrete Aufgaben / In Scope

Build- und Testautomatisierung einrichten, Netzwerk-End-to-End-Tests operationalisieren, Performancebudgets messen, Crash-/Save-Recovery prüfen, visuelle Regressionen dokumentieren, Lizenz- und Distributionfreigaben sammeln. Prüfungen beginnen ab P00 und werden vor jedem Merge und Meilenstein wiederholt.

## Testpyramide
1. **EditMode Unit:** Ability-Cost/Cooldown, Damage, Elementreaktion, Statusdauer, Quest-Transitions, XP, Reward-Dedup, Save-Migration, Input-Normalisierung.
2. **PlayMode Integration:** Player Prefab, Kamera/Collision, NetworkObject Lifecycle, Puzzle-Trigger, Checkpoint, UI-Flow, Shader-Material-Kompatibilität.
3. **Multiplayer Integration:** 1/2/4 Players, Relay real über zwei verschiedene Netze, Join/Leave, RTT/Jitter/Loss, Late-Join, Hostverlust, simultane Fähigkeiten und Bossphasen.
4. **End-to-End:** Spiel von Startmenü über Lobby/Level bis Result und Resume, Solo plus Vier-Spieler-Szenario.
5. **Manuelle Game-Feel-Tests:** Lesbarkeit, Kamera, Kampffeedback, Spaß, Konturenflimmern und Verständlichkeit im Koop.

## Konkrete Quality Gates
- **M0:** keine Compile Errors, Smoke-Tests, sauberer Windows-Build.
- **M1:** Relay-Test mit zwei echten PCs in verschiedenen Netzwerken; max. vier Teilnehmer und unabhängige Ownership.
- **M2:** 15–30 min kooperativer Vertical Slice mit Boss, Zielumfang vier Elemente mit je zwei Fähigkeiten und mindestens zwei Elementreaktionen; falls vorübergehend nur zwei Elemente implementiert sind, ist das Gate noch nicht vollständig abgenommen. Der finale Umfang der M2-Abnahme wird vor dem Build festgeschrieben.
- **M3:** Host-Save/Load + Crash Recovery; dokumentierte Koop-Fortschrittsregel; Rejoin in definierten Zuständen.
- **M4:** fünf Levels, Hub, 24 Ability-Definitionen, acht Enemy-Archetypen, vier Bosse und nachgewiesene 3–5 h via echte Testspieler.
- **M5:** 0 offene Blocker/Critical, Windows Release Build, dokumentierte Third-Party-Lizenzen, Datenschutz-/Rechtscheck.

## Messziele (Hypothesen, keine Garantie)
| Kriterium | Anfangsziel | Nachweis |
|---|---|---|
| FPS | 60 bei 1080p auf festgelegter Mittelklasse-GPU | Profiling und Capture |
| Netzwerk | 100–150 ms RTT bespielbar | Simulation + reale Internet-Tests |
| Verlust | 1 % Paketverlust ohne persistente Zustandsdrift | Paketverlust-Szenario |
| Gleichzeitige Spieler | 4 | Testsession inkl. Host |
| Save | letzter bestätigter Checkpoint rekonstruierbar | Crash-Injection/Testdaten |
| Gameplay | 3–5 h Erstspielerdauer | protokollierte Playtests |
| QA | keine unbelegten Bestanden-Meldungen | Testlogs + Build-Artefakte |

## Security/Privacy & Lizenz
Keine Geheimnisse/UGS-Zugangsdaten ins Repository; keine Benutzerpasswörter selbst verwalten. Clients dürfen weder Health/Rewards/Questzustände authoritative setzen noch fremde Actors kommandieren. Personenbezogene Daten nur soweit für den Dienst erforderlich; Datenschutzhinweise vor Veröffentlichung rechtlich prüfen. Jede verwendete Drittkomponente und jedes Asset mit kommerzieller Lizenz dokumentieren.

## Output
- `/Builds/Windows` (nicht zwingend Git-versioniert), wiederholbarer Buildprozess, Testberichte, Crash-/Performance-Metriken.
- `RELEASE_CHECKLIST.md`, Release-Notes, Third-Party-Notices und Known-Issues-Register.
- CI-/lokale Testskripte, Screenshots der visuellen Golden-Test-Szenen, E2E-Abnahmebericht.

## Schnittstelle / Feedback
| Input | QA verarbeitet | Output |
|---|---|---|
| Relevanter Feature-Commit | Edit-/PlayMode-/Buildtests | `QualityReport` → verantwortliches Paket |
| Network Build | Multi-Instance/Relay-Test | `NetworkReport` → P03/P02/P04 |
| Art-Demo | Profiling + Bildprüfung | `VisualReport` → P07 |
| Final Build | Release-Checkliste | `ReleaseCandidate` → Distribution |

## Kriterien für einen Paket-Merge
Alle Story-Akzeptanzen implementiert; `asmdef`/Unity-Meta intakt; relevante Tests durchlaufen oder nachvollziehbar als blockiert markiert; Multiplayer-Autorität nicht geschwächt; keine unerklärten neuen Dependencies; Lizenzregister gepflegt; Migrationen dokumentiert.

## Release-Out-of-Scope
Konsolen-Port, Steamworks-Integration ohne ausdrückliche Entscheidung, Crossplay, öffentliche Dedicated-Server, Pflicht-Analytics/Tracking und proprietäre Hostingplattformen.
