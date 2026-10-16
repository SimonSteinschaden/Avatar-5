# AVATAR5 — Technische Gesamtarchitektur v1.0

**Stand:** 13.10.2026 · **Status:** P00-Source-Starter vorhanden, Unity-Import/Tests noch ausstehend · **Plattform:** Windows PC

## Projektzusammenfassung

**Avatar5** ist ausschließlich der interne Codename eines eigenständigen, isometrischen 3D-Fantasy-Action-RPGs mit 1–4 Spielern im Online-Koop und 3–5 Stunden geplanter Kampagnenspielzeit. Das Spiel kombiniert vier Elementklassen (**Feuer, Wasser, Erde, Luft**), reaktive Elementkombinationen, dynamische Kämpfe, Rätsel und Charakterfortschritt. Inspirationsquellen sind das Elementbändigen aus *Avatar: The Last Airbender*, das isometrische Koop-Kampfsystem von *Nine Parchments* und der **comicartige, kantig konturierte 3D-Stil von *Borderlands***.

**Grafikziel:** stilisierte 3D-Modelle, abgestufte Cel-/Toon-Schattierung, markante **schwarze Außenlinien und ausgewählte schwarze Objektkanten**, kontrastreiche Farben, klare Kampfsilhouetten und gut lesbare Elementeffekte. Die visuelle Identität muss eigenständig sein; keine Übernahme geschützter Figuren, Logos, Assets oder Designs anderer Spiele.

**Lizenzstrategie:** Entwicklung einer unabhängig veröffentlichbaren Fantasy-IP. Eine offizielle Avatar-Lizenz ist ein *mögliches späteres Verhandlungsziel*, weder Voraussetzung noch zugesichert. „Avatar5“ darf ohne Markenklärung nicht als öffentlicher Produktname eingesetzt werden.

**Technischer Zielstack:** Unity 6.3 LTS (6000.3; Installation und Paketkompatibilität zu Projektstart verifizieren), C#, Universal Render Pipeline (URP), Input System, Netcode for GameObjects 2.x, Unity Multiplayer Services Sessions + Relay, Git, Unity Test Framework, Codex. Blender und rechtskonform lizenzierte kostenlose Assets bevorzugt. Keine eigene Cloud-/Datenbank-Infrastruktur für v1.0.

**Geplanter maximaler Releaseumfang:** 5 Hauptlevel + Hub, 4 Klassen × 6 Fähigkeiten = 24 Fähigkeiten, 8 reguläre Gegnertypen, 4 Bosse. Zahlen sind Planungsgrenzen; zuerst ein kleiner online spielbarer Vertical Slice.

## Lesereihenfolge

1. [Gesamtsystem, Paketlandkarte und Abhängigkeiten](docs/00_GESAMTARCHITEKTUR.md)
2. [Schnittstellenvertrag – verbindliche Quelle](docs/10_SCHNITTSTELLENVERTRAEGE.md)
3. Die neun detaillierten Paketdokumente P00–P08:

| Paket | Dokument | Ergebnis |
|---|---|---|
| P00 | [Projektfundament](docs/P00_FUNDAMENT.md) | Unity/Git/CI/Module/Entscheidungsregeln |
| P01 | [Spieler, Steuerung, Kamera](docs/P01_STEUERUNG_KAMERA.md) | reproduzierbare Spielerintents und lokale Sicht |
| P02 | [Fähigkeiten und Kampf](docs/P02_COMBAT_ABILITIES.md) | hostprüfbare Kampfregeln und Ereignisse |
| P03 | [Online-Koop und Sessions](docs/P03_MULTIPLAYER.md) | 1–4 Spieler, verbindliche Host-Replikation |
| P04 | [Gegner-KI und Bosse](docs/P04_ENEMIES_BOSSES.md) | hostgesteuerte Gegner- und Bossbegegnungen |
| P05 | [Welt, Level, Rätsel](docs/P05_WORLD_LEVELS.md) | Kampagne, Szenenübergänge, Rätselzustände |
| P06 | [Fortschritt, Speicherstände](docs/P06_PROGRESSION_SAVE.md) | dauerhafter Fortschritt und Checkpoints |
| P07 | [Grafik, Assets, UI, Audio](docs/P07_ART_UI_AUDIO.md) | konsistenter Comic-/Ink-Look, Assets, Spiel-UX |
| P08 | [Tests, Performance, Release](docs/P08_QUALITY_RELEASE.md) | nachweisbar stabile Windows-Builds |

4. [Roadmap, Prioritäten und Gates](docs/11_ROADMAP_BACKLOG.md)
5. [Sofort verwendbare Codex-Aufträge](docs/12_CODEX_AUFTRAEGE.md)
6. [Asset- und Lizenzpolitik](docs/13_ASSET_LIZENZEN.md)
7. [AGENTS.md](AGENTS.md) — direkte Regeln für Codex.
8. [CLAUDE.md](CLAUDE.md) — dieselben Regeln und der aktuelle Einstieg für Claude Code.

## Architekturprinzip

Nicht neun getrennte Programme, sondern **neun klar abgegrenzte Module/Workstreams in einem Unity-Projekt**. Sie werden über stabile Contracts und Ereignisse verbunden, nicht durch harte gegenseitige Script-Referenzen. Die reine Spiellogik ist von Input, Rendering und Transport getrennt. **Der Host entscheidet** über Schaden, KI, Rätsel, Loot und den gemeinsam gültigen Zustand.

## Erste Freigabegrenze

**M2 – Vertical Slice:** Vier Spieler aus getrennten Netzwerkverbindungen können in derselben Arena ihre Figuren steuern, mindestens zwei Elementfähigkeiten wirken, Gegner und ein Elementrätsel lösen sowie einen Mini-Boss besiegen. Erst nach Spieltests wird der restliche Kampagnenumfang verbindlich finanziert/geplant.

## Dokumentenstatus

Diese Dokumente sind **Implementierungsspezifikationen, keine lauffähige Unity-Software**. Konkrete SDK-Methodennamen und Paketversionen werden vor der Implementierung anhand der installierten Unity-Version geprüft. Beispiel-C#-Typen in den Spezifikationen sind architektonische Contracts; Codex muss ihre Unity-/Netcode-Serialisierung und tatsächliche API-Kompilierbarkeit sicherstellen.

## Öffentliche Entwicklung auf GitHub

Repository: https://github.com/SimonSteinschaden/Avatar-5

**Kein Open-Source-Lizenztext vergeben.** Der öffentliche Quellcode ist sichtbar, aber damit nicht automatisch zur beliebigen Nutzung freigegeben. Kostenlose Fremd-Assets nur einchecken, wenn ihre Lizenz die Weiterverbreitung im öffentlichen Repository erlaubt. Niemals Zugangsschlüssel oder private Zugangsdaten einchecken.

## Entwicklungsstand / Sofortstart

Der vorbereitete lokale Starterstand enthält erste C#-Contracts, Bootstrap-/Editorwerkzeuge und vorbereitete NUnit-Tests. **Unity-Kompilierung noch nicht geprüft.** Siehe [Remote-PC Setup](docs/SETUP_REMOTE_PC.md) und [Implementierungsstand](docs/IMPLEMENTATION_STATUS.md).
