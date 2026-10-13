# Avatar5 – tatsächlicher Implementierungsstand

**Zeitpunkt:** 13.10.2026

## Implementiert / als Source vorhanden

- Architektur- und Spezifikationsdokumente P00–P08, Schnittstellenverträge, Agentenregeln
- Git-Hygiene (.gitignore/.gitattributes), reproduzierbare lokale Git-Historie
- Erste Unity-unabhängige C#-Contracts: ElementKind, PlayerCommand, AbilityRequest, CombatEvent, WorldEvent, PresentationCue, VersionContracts
- Gameplay-Infrastruktur: pro-Actor SequenceDeduplicator (erste Deduplizierungsregel; Netzwerk-Ownership-Check ausstehend)
- MonoBehaviour-Bootstrap und einfaches Entwicklungsmenü
- Unity Editor-Menü zum einmaligen Generieren der vier Greybox-Starter-Szenen
- Unity-Editor-Windows-Development-Build-Befehl
- Vier NUnit-EditMode-Tests als Code; **noch nicht ausgeführt**
- PowerShell-Importskript für die Unity-Hub-generierten URP-Projektdateien

## Noch ausstehend

- Unity 6.3 LTS und korrekte URP-Template-Dateien auf einem realen Entwickler-PC
- Unity-Editor-Kompilierung / tatsächliche Testresultate / Windows-Build
- .meta-Dateien und Scenes (werden durch Unity beim Import bzw. Setup generiert)
- PlayerController, Kamera-Interaktion und Input Actions (P01)
- Tatsächlicher Toon-Shader/Outline (P07a)
- Network-/Sessions-/Relay-Implementierung (P03)
- Schaden/Fähigkeiten/Gegner/Weltfortschritt/Persistenz

## Technische Einschränkungen / Hinweise

- Die Beispiele implementieren **keine** Netcode-Serialisierung; insbesondere Actor-Ownership muss separat auf Hostseite geprüft werden.
- `SequenceDeduplicator` setzt strikt steigende Sequenznummern voraus; Wraparound und Out-of-order-Buffering sind später zu lösen.
- Dieser Repository-Stand ist ein **Source-Starter**, kein fertig importiertes Unity-Paket oder spielbarer Build.
