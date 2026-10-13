# P02 — Elementfähigkeiten, Combat Core und Statusreaktionen

**Priorität:** P0 · **Vorgänger:** P00, P01-Intents, P03-Authoritätsadapter · **Nachfolger:** P04 AI, P05 World, P06 Progression, P07 VFX

## Ziel
Ein testbares, datengetriebenes Kampfsystem mit vier Elementfamilien und kombinierbaren Effekten. Der Host validiert Zauber, Treffer, Ressourcen, Cooldowns und Statuszustände; Clients rendern daraus lesbares Feedback.

## Input
- `PlayerCommand`/`AbilityRequest` aus P01 und P03; `ActorState`/Definitionen aus P00.
- Weltabfrage `IWorldQuery` (Collisions, Line-of-Sight, Interactable); P05 ergänzt echte Leveldaten.
- `AbilityDefinition` (ID, Element, Slot, Cost, Range, Cooldown, CastTime, Targeting, EffectGraph, VfxCueId).
- Progression-Modifikatoren via `IStatProvider` aus P06 (bis dahin Default-Stats).

## Aufgaben / In Scope
1. Reine Domain-Services für Abilities, Cooldowns, Ressourcen und `CombatResolver`; ScriptableObject → Runtime-Definition validieren.
2. Erster Vertical Slice: `fireball` (Projektile / Burning) und `water_wave` (AoE / Wet / Knockback); danach zwei Abilities je Element, schließlich 24 für Release.
3. Status-Reaktionen als deklarative Regeln: `Wet + Freeze => Frozen`, `Burning + Water => Extinguish/Steam` etc. **Konkrete Balance/Fachregeln als Konfiguration**, kein chaotisches Sonderfall-`if`-Netz.
4. Trefferlogik: Host spawnt/logisch simuliert Projektile, fragt Collision/Teams/Immunitäten, wendet Schaden genau einmal an; Fernwirkung und AoE erhalten eindeutige Cast-/Hit-IDs.
5. Schutz vor Spam und fehlerhaften Clients: Cooldown, Energie, Actor-Status, Range, Richtung, Ownership, deduplizierte RequestIds, serverseitige Clock.
6. Getrennte Präsentations-Events mit `cueId`, `entityId`, `serverTick` und reproduzierbarem Zufalls-Seed; keine redundante Partikelsynchronisierung.

## Außerhalb des Pakets
Gamepad, Kamera, Netzwerktransport, Buff-UI, Shader/Particles, Gegner-KI, Talentbaumoberfläche.

## Output
- Contract-basiertes `ICombatService`, `IAbilityCatalog`, `ICombatEventStream`.
- `AbilityDefinition`-Assets, Elementreaktions-Regeltabelle, Runtime-Validierung und Debug-Combat-Szenario.
- `AbilityRejected`, `AbilityCastAccepted`, `DamageApplied`, `StatusChanged`, `ActorDefeated`-Events.
- Tests für Cooldowns, Reichweite, einmalige Damage-Anwendung, Buff-Stacking und Reaktionsprioritäten.

## Schnittstellen / Autorität
| Input | Verarbeitung | Output → Folgepaket |
|---|---|---|
| `AbilityRequest` | `ValidateActor/Ability/Cost/Range` beim Host | `AbilityCastAccepted/Rejected` → P03/P07 |
| Hit-/Collision-Info | Damage/Resistance/Status Resolver | `DamageApplied`, `StatusChanged` → P03/P04/P06/P07 |
| `ActorDefeated` | eindeutiger Death Event | P04 Encounter, P05 Mission, P06 Reward |
| `IStatProvider` | Stats & Abilities verändern | effektive Cooldowns/Damage → P02 |

Domain schreibt `ActorHealth` allein über Combat-Port. API-Beispiele und Idempotenz siehe zentrale Contracts. P03 transportiert die Events, berechnet sie nicht neu.

## Kampfdesign-Leitplanken
- Maximal vier aktive Ability-Slots pro Charakter; Skills müssen visuell unterscheidbar bleiben.
- Elementkombinationen sollen Kooperation belohnen, Solospiel aber nicht blockieren. Gegnerresistenzen durch telegraphierte Effekte anzeigen.
- Schadenszahlen, Resistances, AoE-Radien und Attack-Time sind Balancing-Konfigurationswerte.
- Animation/VFX dürfen nicht Voraussetzung für die korrekte Kampfberechnung sein.

## Akzeptanz
- Vier Spieler wirken gleichzeitig Zauber ohne doppelten Schaden oder auseinanderlaufende Health-Werte.
- Unberechtigte Casts und Duplikate werden konsistent abgelehnt.
- Zwei Elementreaktionen sind Ende M2 im Multiplayer reproduzierbar getestet.
- Gegner und Rätsel können dieselben Combat-/Status-Events konsumieren.
- 100+ simulierte Combat-Events in einem EditMode-Test ergeben erwartete eindeutige Resultate.

## Übergabe
**P04** bekommt Combat-Ports für Gegnerfähigkeiten/Hitpoints; **P05** Statusreaktionen auf Weltobjekten; **P06** Kill-/Mission-Events; **P07** rein visuelle Cues; **P03** die finalen Hostzustände zur Replikation.
