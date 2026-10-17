# P08 – Automatischer Testlauf

**Stand:** 17.10.2026 · Story S08-01

## Ein Befehl für alles

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Run-AllTests.ps1
```

Der Unity-Editor darf das Projekt dabei **nicht geöffnet** haben (Batchmode braucht die Projektsperre). Entweder den Editor schließen oder das Skript in einer Kopie des Repositorys laufen lassen.

| Schritt | Was passiert | Dauer (RTX-4060-PC) |
|---|---|---|
| `build` | `Avatar5BuildAll.BuildFromCommandLine`: generierte Szenen neu erzeugen, Testplayer bauen (`P03_SessionTest`, `P03_NetworkArena`, `Level01`), spielbaren Build `Artifacts/Windows/Avatar5.exe` | siehe Bericht |
| `EditMode` / `PlayMode` | Unity Test Framework, NUnit-XML | siehe Bericht |
| `P03-SessionTest` | Host + 5 Clients: Platzlimit, Versionsprüfung, Verlassen, Host-Ende | |
| `P03-NetworkPlayerTest` | 4 Spieler bewegen sich, gefälschte Befehle werden abgewiesen | |
| `P02-CombatTest` | 4 Spieler zaubern, gleiche Lebenspunkte/Ereignisse überall, Reaktionen | |
| `P04-EnemyTest` | Brute kündigt an, schlägt, wird besiegt; alle Peers einig | |
| `P05-Level01Test` | Level01 allein und zu dritt + Nachzügler durchgespielt | |
| `P06-SaveTest` | Checkpoint speichern, fortsetzen (mit Client), fremde Version abgelehnt | |

Optionen: `-SkipBuild` (vorhandene Player nutzen), `-SkipMultiprocess` (nur Unity-Tests), `-UnityPath`, `-ProjectPath`.

## Bericht

`Artifacts/TestReport/report.md` (lesbar) und `report.json` (maschinenlesbar: Commit, Branch, Unity-Version, je Schritt Status/Ergebnis/Dauer/Logdatei) plus alle Logs und XML-Dateien. Exit-Code = Anzahl fehlgeschlagener Schritte.

## Was der Lauf nicht abdeckt

- Relay über zwei echte Internetanschlüsse (M1) – braucht die Unity-Cloud-Verknüpfung und zwei PCs.
- Latenz-, Jitter- und Paketverlust-Simulation.
- Manueller Spieltest (Steuergefühl, Lesbarkeit, Spaß) und Performance auf einer festgelegten Mittelklasse-GPU.
- Golden-Screenshot-Vergleiche (P07a hat Referenzbilder, aber noch keinen automatischen Vergleich).
