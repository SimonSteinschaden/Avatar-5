# AVATAR5 — Kopierfertige Codex-Aufträge pro Architekturpaket

**Nutzung:** Immer nur einen Auftrag aktiv bearbeiten. `AGENTS.md`, `00_GESAMTARCHITEKTUR.md`, den zentralen Schnittstellenvertrag und das jeweilige Paketdokument als Projektdateien bereitstellen. Keines der folgenden Briefings erklärt ein ganzes Paket mit nur einem Prompt für „fertig“: Codex soll zuerst die Stories zerlegen und danach Story für Story implementieren.

## Gemeinsamer Pflichtabschluss (für jeden Prompt)

> Starte mit Repository- und Versionprüfung. Lies alle relevanten Spezifikationen. Arbeite ohne neue unbekannte Kosten/SDKs. Unterteile das Paket in kleine testbare Teilstories und implementiere **nur die erste noch offene, abnahmefähige Story**. Prüfe Compilation, relevante Unity Tests und bei Networking reale Integration soweit verfügbar. Liefere: geänderte Dateien, Tests/Logs, bekannte Einschränkungen, betroffene Contracts und nächsten konkreten Prompt. Behaupte nicht, dass Tests bestanden wurden, wenn Unity nicht ausführbar ist. Ändere Schnittstellen nur gemeinsam mit Dokumentation und Tests.

## Auftrag P00 — Foundation

```text
Du bist Senior Unity-/Build-Architect für Avatar5. Lies AGENTS.md, docs/00_GESAMTARCHITEKTUR.md, docs/10_SCHNITTSTELLENVERTRAEGE.md, docs/P00_FUNDAMENT.md. Prüfe das vorhandene Repository und die lokale Unity-Installation. Erstelle zuerst einen Implementierungsplan für P00 und setze danach die kleinste vertikal abgeschlossene Story um: ein kompilierbares Unity-6.3-LTS-/URP-Projekt mit Git-Hygiene, Bootstrap- und Gameplay-Testszene, mindestens einem EditMode-Test und einem Windows-Smoke-Build-Skript. Pinne Paketversionen nur nach Prüfung. Aktualisiere Test- und Setup-Dokumentation. Gib einen exakten Testbericht und den nächsten P00-Schritt aus.
```

## Auftrag P07a — Shader-/Outline-Stiltest (direkt nach P00)

```text
Lies P07_ART_UI_AUDIO.md und P08_QUALITY_RELEASE.md. Erstelle in einer eigenen VisualStyle_Test-Szene mit Capsule (Spieler), Gegner-Cube, Felsen und Boden einen URP-konformen comicartigen Look: abgestufte Toon-Lichtbänder, deutliche schwarze Silhouetten und ausgewählte interne Objektkanten. Prüfe Renderer Features/Render Graph anhand der installierten Unity-Version. Keine Fremdassets und keine Borderlands-Inhalte importieren. Parameter für Linienbreite, Tiefen-/Normalenschwellen, Low/High-Profil veröffentlichen. Lege Screenshots und Performance-Messverfahren bei. Ändere keine Netzwerk- oder Combatregeln.
```

## Auftrag P01 — Player/Camera

```text
Lies P01_STEUERUNG_KAMERA.md und die gemeinsamen Contracts. Implementiere die erste kleine Story: Input Actions Move/Aim, kamerabezogener CharacterMotor, unabhängiges Zielen und isometrische CameraProfile-Konfiguration in einer Greybox-Testszene. Produziere PlayerCommand-Absichten statt autoritativer Netzwerkpositionen. Schreibe reproduzierbare Bewegungs-/Kollisionstests. Keine Netztransport-/Combatimplementation.
```

## Auftrag P03 — Sessions/Relay

```text
Lies P03_MULTIPLAYER.md. Prüfe echte Versionen des installierten Unity Multiplayer Services SDK, Netcode for GameObjects und Unity Transport. Implementiere zunächst private Create/Join/Leave-Session mit vier Plätzen, Join-Code, Versionsprüfung, einfachen Verbindungsanzeigen und klaren Fehlermeldungen. Danach erst NetworkPlayer/Ownership in separater Story. Host-authoritative Topologie, kein eigener Backenddienst. Teste zwei getrennte Prozesse und dokumentiere alle noch ausstehenden Tests auf getrennten Internetanschlüssen.
```

## Auftrag P02 — Combat/Abilities

```text
Lies P02_COMBAT_ABILITIES.md und 10_SCHNITTSTELLENVERTRAEGE.md. Implementiere als erste Story eine testbare Runtime AbilityDefinition, IAbilityCatalog, Cooldown-/Ressourcenvalidierung und AbilityRequest-Resultat ohne UI. Nächste separat umzusetzende Story: Fireball und Water Wave mit hostvalidiertem Damage/Status, IDs und Testfällen für doppelte Requests. Keine clientseitigen Schadensberechnungen. Sichtbare VFX ausschließlich per PresentationCue.
```

## Auftrag P04 — AI/Encounters

```text
Lies P04_ENEMIES_BOSSES.md. Implementiere zuerst eine EnemyBrain-State-Machine mit Idle/Approach/Telegraph/Attack/Recover/Dead; der Host wählt Ziele und nutzt P02 CombatService für Treffer. Erstelle einen testbaren Gegner mit NavMesh und Telegraphed Attack. Anschließend EncounterDirector als getrennte Story. Animation und Sound nur über Events an P07. Teste Solo und Mehrspielersitzung.
```

## Auftrag P05 — World/Levels

```text
Lies P05_WORLD_LEVELS.md. Implementiere zunächst LevelDefinition, WorldStateService, eindeutige WorldObjectIds und ein hostvalidiertes Interactable in Level01. Ergänze danach ein Elementrätsel und einen checkpointfähigen Levelübergang als getrennte Stories. Plane solo-lösbare Mechaniken und synchronisierte WorldStateChanged-Events. Nutze keine direkten UI-/Network-Domain-Abhängigkeiten.
```

## Auftrag P06 — Progression/Save

```text
Lies P06_PROGRESSION_SAVE.md. Implementiere zuerst ein versioniertes SaveGameV1-Schema, Validierung, Atomicsave+Backup und Unit-Tests. Danach Restore über Welt-/Combat-Ports. Starte nicht mit Cloud-Saves oder fremden Authentifizierungsdiensten. Dokumentiere ausdrücklich: v1 Kampagnen-Save ist host-owned, plattformübergreifende Client-Profile nicht zugesichert. Fortschritt und Rewards müssen event-idempotent sein.
```

## Auftrag P07b — Presentation Integration

```text
Lies P07_ART_UI_AUDIO.md. Verbinde den freigegebenen Toon-/Outline-Renderer mit Gameplay-Testlevel und veröffentliche eine konsistente UI-/VFX-Eventbrücke. Visualisierung liest ActorPose, CombatEvent, WorldState, SessionState und ProgressionState. Buttons senden nur definierte Commands an die jeweiligen Domain-Ports. Erstelle Audio- und Partikel-Pooling, Grafikeinstellungen, Lizenzregister und Screenshot-/Performance-Abnahmetests. Keine geschützten Referenzspiel-Assets.
```

## Auftrag P08 — Test/Release

```text
Lies P08_QUALITY_RELEASE.md. Erstelle zuerst einen automatisierten EditMode-/PlayMode-Smoketestlauf mit maschinenlesbarem Bericht und reproduzierbarem Windows-Development-Build. Erweitere danach um 1/2/4-Spieler-Testmatrix, echte Relay-E2E-Checkliste, Netzstörungssimulation und Golden-Screenshot-/Performance-Szenarien. Melde reale Tests getrennt von geplanten Tests. Bei Fehlschlägen gib genaue Reproduktion und betroffenes Paket zurück statt fachfremd großflächig umzubauen.
```

## Arbeitsdisziplin bei parallelen Agents

Unabhängige Branches pro Modul möglich; **nicht gleichzeitig** `Packages/manifest.json`, URP Renderer Asset, ProjectSettings, MainMenu- oder Bootstrap-Szene editieren. Contract-Änderungen nur über zentrale Prüfung und gemeinsamen Integrationsbranch. Integration spätestens nach jeder abgeschlossenen vertikalen Story, nicht erst zum Milestone-Ende.
