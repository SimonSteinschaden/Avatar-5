# AVATAR5 — Roadmap, Product Backlog, Lieferreihenfolge

**Version 1.0 · Planungsbasis, keine verbindliche Zeitschätzung.** Prioritäten: `P0` zwingender Kern/Architekturrisiko, `P1` Releaseumfang, `P2` Polish/Release. Ein „Promptzyklus“ umfasst einen beauftragten Codex-Task einschließlich plausibler Test- und Korrekturrunde; die tatsächlichen Iterationen schwanken stark.

## 1. Architekturpakete vs. Lieferstories

Die Architektur hat **neun große Pakete P00–P08**. Jedes Paket besteht aus mehreren Stories. Die Entwicklung folgt vertikalen Integrationsschritten; nicht zuerst alle P02-Features vollständig bauen und Monate später Netzwerk hinzufügen.

| Story-Gruppe | Paket | Prio | Ergebnis | Voraussetzung | Beispielhafte Promptzyklen |
|---|---|---|---|---|---:|
| S00-01 | P00 | P0 | Unity+URP+Git+Build+Contracts | keine | 6–12 |
| S07-01 | P07 | P0 | erster Toon-/Outline-Look auf Primitives | S00-01 | 4–9 |
| S01-01 | P01 | P0 | isometrischer Greybox-Controller | S00-01 | 8–15 |
| S03-01 | P03 | P0 | Sessions, Relay, Join-Code | S00-01 | 12–22 |
| S03-02 | P03 | P0 | Networked Player + ActorOwnership | S01-01,S03-01 | 12–24 |
| S02-01 | P02 | P0 | zwei testbare Zauber im Hostkontext | S01-01,S03-02 | 10–20 |
| S02-02 | P02 | P0 | Damage, Status & Elementreaktionen | S02-01 | 14–28 |
| S04-01 | P04 | P0 | 3 Gegner, Mini-Boss und Netzwerk | S02-02 | 12–24 |
| S05-01 | P05 | P0 | 1 kompletter Level und 2 Rätsel | S04-01 | 15–30 |
| S06-01 | P06 | P1 | Host-Checkpoint, Save, Restore | S05-01 | 12–22 |
| S07-02 | P07 | P1 | UI, Audio, Animator, VFX integriert | S05-01 | 18–35 |
| S05-02 | P05 | P1 | Hub + weitere 4 Levels | S05-01,S06-01 | 35–70 |
| S02-03 | P02 | P1 | Ausbau auf 24 Abilities | S02-02 | 12–26 |
| S04-02 | P04 | P1 | 8 Gegnertypen, 4 Bosse | S04-01 | 12–26 |
| S06-02 | P06 | P1 | Talentbäume, XP, Skill-UI | S06-01 | 10–20 |
| S07-03 | P07 | P1 | einheitliche Release-Assets | S07-02 | 18–40 |
| S08-01 | P08 | P0→P2 | kontinuierliche Testautomatisierung | S00-01 | 12–24 fortlaufend |
| S08-02 | P08 | P2 | finale Performance/Regression/Release | alle | 20–45 |

**Hinweis zur Kostenschätzung:** Die Storyzahlen sind additive Planungsgrößen mit Überschneidungsrisiko und nicht direkt 1:1 mit der früheren, gröberen 19-Arbeitspaket-Kalkulation gleichzusetzen. Für das Gesamtspiel dient **ca. 250–550 Promptzyklen** als sehr grobe Größenordnung; Gameplay-Polish kann diese übersteigen. Entscheidend sind die Gates, nicht ein festes Promptkontingent.

## 2. Meilensteine mit verbindlichen Gates

| Gate | Inhalt | Releasebedingung |
|---|---|---|
| **M0: Foundation** | P00 + P07 Style-Spike | Checkout → Build und Outline/Toon-Demo |
| **M1: Network Sandbox** | P01 + P03 | 2 echte Internet-PCs, max. 4 Teilnehmer in Tests, unabhängige Controls |
| **M2: Vertical Slice** | P02 + P04 + P05 Level01 | 15–30 min Koop-Level, Gegner, Elementreaktionen, 1 Mini-Boss |
| **M3: Persistence/UX** | P06 + P07 | verständliche Lobby, Save/Resume, Skillfortschritt, Rejoin-Regel |
| **M4: Content Complete** | weitere P02/P04/P05/P07 | 5 Levels, Hub, 24 Abilities, 8 Gegner, 4 Bosse, Beta-Durchspiel |
| **M5: Release Candidate** | P08 + alle | Windows-Build, Testprotokolle, Lizenzen, 0 kritische Blocker |

## 3. Vertical-Slice-Definition

Die Qualität von M2 hat Vorrang vor einer starren Menge an Zaubern. Standardziel: 4 Elemente mit je 2 nutzbaren Abilities, mindestens 2 tatsächlichen Elementinteraktionen, ein Level von 15–30 Minuten, 3 Enemy-Archetypen, 1 Mini-Boss und mindestens 2 Rätsel. Wenn der Netzwerk-/Combat-Kern die Ressourcen übersteigt, **zuerst 2 Elemente vollständig** mit stabiler Online-Funktion testen, danach auf 4 erweitern. Solofähigkeit und 4-Spieler-Koop beibehalten.

## 4. Abnahmeverantwortung

- **Codex:** implementiert, testet, dokumentiert, sammelt Logs.
- **Projektleitung/Benutzer:** entscheidet über Spielgefühl, Art Direction, Scope und Gate-Abnahme.
- **Automatisierter Test:** prüft deterministische Regeln und technische Regression.
- **Manueller Multiplayer-Test:** prüft reale Internetverbindungen, Kamera, Gameplay und visuelle Lesbarkeit.

## 5. Abhängigkeiten im Tagesgeschäft

P00 etabliert Contracts. P01 liefert Commands. P03 schafft die echte gemeinsame Session. P02 simuliert Kampfregeln. P04 steuert Feinde. P05 stellt Welt und Quests. P06 speichert und lädt. P07 visualisiert alle gelesenen Daten. P08 kontrolliert jede Lieferung. Graphisch können Greybox und Outline-Prototyp parallel zu P01/P03 laufen, **aber nicht unkoordiniert dieselben Projektsettings editieren**.

## 6. Definition of Done (jede Story)

- Story-Input und Akzeptanz aus Paketdokument geklärt.
- Code und Assets erstellt, echte Build-/Testresultate vermerkt.
- Verträge und Versionen aktualisiert, keine neuen zyklischen Abhängigkeiten.
- Host-authoritative Design weiterhin erfüllt.
- Visuelle/UX-Änderungen per Screenshot/Videos nachgewiesen, soweit möglich.
- Drittanbieterressourcen mit Lizenz im Register.
- Nicht gelöste Probleme als explizite Bugs/Backlog-Items erfasst.
