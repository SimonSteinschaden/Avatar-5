# Avatar5 – Arbeitsanweisungen für Claude Code

## Projekt und Schutzrechte

Avatar5 ist ein **interner Projektcodename**, kein für die öffentliche Vermarktung freigegebener Produkttitel. Es entsteht eine **eigenständige** isometrische 3D-Fantasy-Action-RPG-IP für Windows, für 1–4 Personen im Online-Koop. Gewünschter Umfang: 3–5 Stunden, fünf Hauptlevel und Hub, vier Elementklassen (Feuer, Wasser, Erde, Luft), 24 Fähigkeiten, acht Gegnertypen und vier Bosse. Der Look ist comicartig mit Cel-Shading, schwarzen Objekt-Außenkonturen und ausgewählten schwarzen Kanten. Andere Spiele dürfen inspirieren, ihre geschützten Inhalte, Logos, Charaktere und Assets dürfen nicht kopiert werden. Eine mögliche spätere Avatar-Lizenz ist nicht gesichert und **keine technische Abhängigkeit**.

## Verbindliche Lesereihenfolge vor Codeänderungen

1. `README.md`
2. `AGENTS.md` (gilt ebenfalls für Claude)
3. `docs/IMPLEMENTATION_STATUS.md`
4. `docs/00_GESAMTARCHITEKTUR.md`
5. `docs/10_SCHNITTSTELLENVERTRAEGE.md` – maßgebliche Contracts
6. Das betroffene Paketdokument `docs/P00_*.md` bis `docs/P08_*.md`
7. `docs/11_ROADMAP_BACKLOG.md` und ggf. `docs/12_CODEX_AUFTRAEGE.md`

## Technisches Ziel

Unity 6.3 LTS, C#, URP, Input System, Cinemachine, Netcode for GameObjects, Unity Multiplayer Services Sessions und Relay, Unity Test Framework, Git. Versionsnummern und konkrete APIs **zuerst anhand installierter Pakete verifizieren**. Ein Client stellt Absichten; der Host entscheidet über Kampf, Gegner, Rätsel und gemeinsamen Fortschritt. Datengetriebene Fähigkeiten, klare Modulgrenzen, testbare Gameplay-Logik und Präsentation getrennt halten. Nur lizenzrechtlich erlaubte Assets für ein öffentliches GitHub-Repository verwenden.

## Aktueller Ist-Zustand

Das Projekt ist **ein Source-Starter, noch kein importiertes oder erfolgreich kompiliertes Unity-Spiel**. Die C#-Contracts, ein einfacher Bootstrap, Unity-Editor-Setups und NUnit-Testcode liegen vor. Das eigentliche Unity-URP-Projekt muss anhand `docs/SETUP_REMOTE_PC.md` auf einem Rechner mit Unity installiert und importiert werden. Multiplayer, Bewegung, Skills, Gegner, Levels, Toon-Shader und Windows-Build gelten **nicht als implementiert oder getestet**.

## Workflow und Abnahme

- Arbeite **eine klar abgegrenzte Story** nach der anderen ab. Bevorzuge frühe spielbare Vertical Slices und kleine überprüfbare Commits.
- Vor jeder Änderung Zustand von Git, Unity-Version, `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, abhängigen `.asmdef` sowie der betroffenen Dokumentation prüfen.
- Ändere Vertragsformen nur mit synchroner Anpassung der Konsumenten, Tests und `docs/10_SCHNITTSTELLENVERTRAEGE.md`.
- Für Unity-/Kampfregeln EditMode-/PlayMode-Tests schreiben. Für Netzwerklogik reale Verbindungstests separat dokumentieren.
- Nie behaupten, dass Unity kompiliert, ein Test bestanden hat oder ein Windows-Build funktioniert, wenn das nicht tatsächlich ausgeführt wurde.
- `.meta`, Asset-GUIDs, URP-Assets und Unity-Szenen konsistent halten; `Library/`, `Temp/`, Build-Artefakte und Secrets nicht committen.
- Keine Kostenpflicht, Cloud-Dienste, Drittanbieter-Assets oder neuen Backenddienste ohne explizite Zustimmung hinzufügen.
- Dokumentiere nach jeder Story: geänderte Dateien, erfüllte Abnahmebedingungen, ausgeführte Prüfungen, nicht ausgeführte Prüfungen, offene Probleme und nächsten sinnvollen Schritt. Aktualisiere `docs/IMPLEMENTATION_STATUS.md`.

## Nächster empfohlener Schritt

**P00 abschließen:** Auf dem Entwickler-PC ein natives Unity-6.3-LTS-3D-URP-Projekt erstellen und über `docs/SETUP_REMOTE_PC.md` die Starterdateien integrieren; Unity-Kompilierung, EditMode-Tests und Windows-Testbuild wirklich ausführen, die generierten notwendigen Projektdateien versionieren. Anschließend P07a (Toon-Outline-Testszene) und P01/P03 (Steuerung und Online-Multiplayer) schrittweise umsetzen. Nicht direkt mit den 24 Fähigkeiten oder fünf Levels anfangen.
