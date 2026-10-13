# P03 — Online-Koop, Sessions und zustandsgetreue Replikation

**Priorität:** P0 (früh entwickeln) · **Vorgänger:** P00, P01-PlayerCommand, P02-Combat-Contract · **Nachfolger:** P02–P07

## Ziel
1–4 Spieler können per Einladungscode via Unity Multiplayer Services Sessions/Relay eine private Spielsitzung starten. NGO überträgt Eingaben und autoritative Zustände. Solo nutzt denselben Host-Spielpfad mit genau einem Spieler.

## Input
- `SessionConfig{maxPlayers=4,protocolVersion,contentVersion}` und Dienstkonfiguration aus P00.
- `PlayerCommand` und `PlayerPose` aus P01; `AbilityRequest` und `CombatEvent` aus P02.
- `LevelState`, `QuestState` und CheckpointSnapshot aus P05/P06.
- Unity-UGS-Projekt/Service-Berechtigungen und installierte MPS/NGO-Paketversionen.

## Kernarchitektur
**Client-Hosted / Host-Authoritative**, kein Dedicated Server. Unity Sessions verwaltet Session- und Join-Lebenszyklus; Relay vermittelt Internetverbindungen; NGO synchronisiert NetworkObjects und RPC-/State-Updates. Die aktuellen SDK-Schnittstellen müssen in der installierten Editorversion verifiziert werden. Keine Legacy-Relay-SDK-Abhängigkeit neu einführen, wenn die vereinheitlichte Multiplayer-Services-API verfügbar ist.

## Aufgaben / In Scope
1. `ISessionService`: `CreatePrivate`, `Join(code)`, `Leave`, `GetState`, `OnConnectionChanged` mit Timeout- und Cancellation-Handling.
2. Version-Handshake: Spiel-/Content-/Protocol-Version vor Spawn vergleichen; Abweichungen mit erklärbarem Fehler ablehnen.
3. Registrierte NetworkPrefabs für alle replizierten Actors und relevanten World Objects; Ownership pro Player, serverseitige Spawn/Despawn-Rechte.
4. `IPlayerCommandSink`: Netzwerkcommand empfangen, Sequence/PlayerIdentity/RateLimit prüfen und an Host-Simulation weiterleiten; der Client schickt keine Schadensergebnisse.
5. `IStateReplication`: Host veröffentlicht ActorPose/Health/Status/WorldState; delta-orientierte Replikation, Snapshot bei Join/Rejoin; visuelle Cue-Events mit deduplizierter EventId.
6. Lobby mit Debug-UI (Join-Code, Status, Slots) und später P07 UI-Adapter; Start Game nur bei bestätigtem Hoststatus.
7. Szenenwechsel für verbundene Spieler, Spawn am freigegebenen Checkpoint, aktiver Sessionzustand; Late Join nur Lobby/Hub/Checkpoint, nicht mitten im Bosskampf.
8. Disconnect: Client zurück ins Menü; temporäre Unterbrechung/Rejoin nur solange ursprüngliche Session und Host fortbestehen. **Host-Abbruch: Sitzung endet**, nächster Host kann letzten gespeicherten Checkpoint als neue Session starten. Keine nahtlose Host Migration in v1.
9. Session- und Netzmetriken: RTT, loss simulation, bytes/s, NetworkObject Count und warnings. Logging ohne persönliche IDs/Tokens.

## Out of Scope
Public Matchmaking, Ranglisten, Account-Backend, Dedicated Server, Full Host Migration, lokaler Split-Screen, Voice Chat, PvP/Anti-Cheat mit Echtgeldbezug.

## Output
- NetworkManager/SessionAdapter, Netzwerk-Player Prefab und Multiplayer-Testszenen.
- SessionState und `SessionJoinResult`; Host-Ingress und State-Replikations-Bridges.
- Integrationsprüfungen: 1/2/4 Player, Join/Leave, Paketverlust/RTT, Sessionzeitüberschreitung, Doppel-Commands.
- Betriebs-/Konfigurationsdokument für UGS-Sessions/Relay inkl. Quotas/Fehlercodes.

## Schnittstellen und Zustandsbesitz
| Input | Netzwerkverantwortung | Output / Konsument |
|---|---|---|
| `CreateSession/Join(code)` | Sessions / Relay starten | `SessionReady`, `LocalPlayerIdentity` → P01/P07 |
| `PlayerCommand` | validieren, zum Host dispatchen | `IPlayerSimulation` → P01/P02 |
| `AbilityRequest` | Actor/Ownership/sequence prüfen | `ICombatService` (P02) |
| `CombatEvent`, `WorldEvent` | nur Host → Clients replizieren | P07 Feedback, P05/P06 lesender Zustand |
| `CheckpointSnapshot` | vollständigen Join-Zustand erzeugen | Spawner & SceneState (P05/P06) |

**Kein Ring:** Networking kennt nur Interfaces/Events der Domain, die Domain ruft keine SDK-Transportmethoden direkt auf.

## Abnahme
- Zwei voneinander getrennte Windows-PCs in unterschiedlichen Internetnetzen verbinden sich per Code; vier Spieler insgesamt nach lokalem Lasttest.
- Jeder kontrolliert nur seinen Actor. Alle sehen konsistente Gegner-Lebenspunkte, Positionen, Rätselzustände und Rewards.
- Bei 100–150 ms RTT und simuliertem 1 % Loss ist das Spiel grundsätzlich bedienbar; gemessene Einschränkungen dokumentieren.
- Unbekannter/abgelaufener Joincode, Vollbelegung und Versionskonflikt erzeugen verständliche Fehler.
- Kein doppelt ausgelöster `DamageApplied` nach Retransmission; Host-Verlust produziert keine falsche Erfolgsmeldung über Migration.

## Risiken / Instrumentierung
Netz-Latenz: interpolation und ggf. späte Client Prediction nach Messung. Umfangreicher Full-State: Snapshots chunk-/versionsbewusst begrenzen. Unity-Serviceänderungen: nur einen SDK-Adapter anfassen. Failure Injection gehört zu jedem Meilenstein ab M1.

## Übergabe
**P02** erhält einen authentifizierten/validierten Host-Command-Ingress; **P04/P05** den Netzwerk-Entity-Lifecycle; **P06** Reconnect-/Checkpoint-Hooks; **P07** Sessionstatus-/Lobby-Events.
