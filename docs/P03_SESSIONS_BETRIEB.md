# P03 – Sessions: Betrieb und Konfiguration

**Stand:** 17.10.2026 · Story S03-01 (privat erstellen, beitreten, verlassen)

## Pakete (aus der Editor-Empfehlung von Unity 6000.3.26f1 übernommen)

| Paket | Version | Zweck |
|---|---|---|
| `com.unity.netcode.gameobjects` | 2.13.3 | NetworkManager, Connection Approval, Named Messages |
| `com.unity.transport` | 2.7.4 | UDP-/Relay-Transport (`UnityTransport`) |
| `com.unity.services.multiplayer` | 2.3.3 | Sessions (privat, Join-Code) + Relay; zieht `services.core`, `services.authentication` u. a. nach |

Keine Legacy-Relay- oder Lobby-SDKs; Sessions/Relay laufen über die vereinheitlichte Multiplayer-Services-API.

## Zwei Verbindungsarten, ein Vertrag

| Modus | Klasse | Join-Code | Braucht Unity Cloud |
|---|---|---|---|
| Direkt (LAN, gleicher PC) | `DirectSessionService` | `IP:Port` des Hosts, Standard 7777 | nein |
| Internet | `RelaySessionService` | Einladungscode der Session | ja |

Beide erben von `NetcodeSessionServiceBase`: gleiche Versionsprüfung, Platzlimit (4), Fehlercodes und `SessionState`. `SessionRuntime` (DontDestroyOnLoad, am NetworkManager) hält den aktiven Dienst; `SessionDebugLobby` ist die vorläufige OnGUI-Lobby (P07 ersetzt sie).

## Unity-Cloud-Verknüpfung (nur für Relay)

1. Unity-Editor › **Edit › Project Settings › Services** › Organisation wählen › Projekt anlegen oder verknüpfen. Danach steht `cloudProjectId` in `ProjectSettings/ProjectSettings.asset`.
2. Im Unity Dashboard für das Projekt **Multiplayer › Sessions/Relay** aktivieren, falls das Dashboard es verlangt.
3. Anmeldung läuft anonym (`SignInAnonymouslyAsync`), keine Accounts.

Ohne Verknüpfung liefert der Relay-Modus `ServicesUnavailable` mit dem Hinweis im Log; der Direktmodus funktioniert unabhängig davon.

**Kosten/Kontingente:** Relay und Sessions haben ein kostenloses Kontingent; Überschreitung kann Gebühren auslösen (siehe `13_ASSET_LIZENZEN.md` §6). Vor öffentlichen Tests die aktuellen Unity-Bedingungen prüfen.

## Fehlercodes

| `SessionError` | Wann | Text in der Lobby |
|---|---|---|
| `InvalidCode` | Code-Format falsch | Der Code ist ungültig … |
| `SessionNotFound` | Code unbekannt/abgelaufen, oder im Direktmodus niemand unter der Adresse | Keine Sitzung mit diesem Code gefunden … |
| `SessionFull` | 4 Plätze belegt (Host zählt mit) | Die Sitzung ist voll … |
| `VersionMismatch` | andere Protokoll- oder Content-Version | Andere Spielversion als der Host … |
| `TimedOut` | Host antwortet nicht rechtzeitig (15 s Approval, 30 s Relay-Join) | Zeitüberschreitung … |
| `ServicesUnavailable` | Unity Services nicht erreichbar / Projekt nicht verknüpft / Rate-Limit | Online-Dienste nicht erreichbar … |
| `TransportFailed` | Port belegt, Transport fehlt | Netzwerkverbindung konnte nicht aufgebaut werden. |
| `HostEnded` | Host beendet oder verlässt (keine Host-Migration in v1) | Der Host hat die Sitzung beendet. |
| `ConnectionLost`, `Kicked`, `AlreadyInSession`, `Cancelled`, `Unknown` | siehe Name | – |

Logs enthalten keine Tokens oder Spieler-Ids. Jeder Zustandswechsel steht als eine Zeile `A5SESSION mode=… phase=… role=… players=x/y code=… error=…` im Player-Log.

## Testen

- **Im Editor:** `Assets/_Game/Scenes/P03_SessionTest.unity` öffnen › Play › „Private Sitzung erstellen“. Ein zweiter Prozess (Build) tritt mit `127.0.0.1:7777` bei.
- **Testplayer bauen:** Menü `Avatar5/Build/P03 Session Test Player` oder `Unity.exe -batchmode -projectPath <projekt> -executeMethod Avatar5.EditorTools.Avatar5P03SessionSceneBuilder.BuildPlayerFromCommandLine -quit` → `Artifacts/P03/Avatar5_P03.exe`.
- **Mehrprozess-Test:** `powershell -File scripts/P03-SessionTest.ps1` startet Host + 5 Clients auf diesem PC und prüft: 4/4 belegt, 5. Spieler abgewiesen, falsche Version abgewiesen, Verlassen → 3/4, Host-Ende → Clients erhalten `HostEnded`.
- **Kommandozeile des Players:** `-a5-session host|join`, `-a5-mode direct|relay`, `-a5-code CODE`, `-a5-port N`, `-a5-protocol N` (nur für Mismatch-Tests), `-a5-leave-after S`, `-a5-quit-after S`.

## Noch offen

- Relay-Test über zwei PCs in verschiedenen Internetanschlüssen (M1-Abnahme) – braucht die Cloud-Verknüpfung.
- Paketverlust/Latenz-Simulation, Netzmetriken (RTT, bytes/s), Rejoin, Szenenwechsel und Spieler-Actors folgen in den nächsten P03-Stories.
