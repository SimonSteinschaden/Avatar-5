# AVATAR5 — Lizenz-, Asset- und Namensstrategie

**Ziel:** Kostenloser, kommerziell rechtssicher lizenzierbarer Entwicklungsstart. Keine Rechtsberatung; vor Release komplexe Lizenzfälle rechtlich prüfen.

## 1. Grundregel

Avatar5 ist **interner Codename**. Die vier Elemente und allgemeine Fantasy-Spielmechaniken dürfen als Inspiration dienen. Konkrete geschützte Avatar-/Borderlands-Charaktere, Figurenoptik, Namen, Weltkarten, Storyelemente, Musik, Logos, Texturen, Modelle und spezifisches Artwork dürfen ohne jeweilige Nutzungsrechte nicht in den Release aufgenommen werden. Der angestrebte spätere Avatar-Lizenzvertrag ist ungewiss und muss für eine Veröffentlichung **nicht** erforderlich sein. Der comicartige Stil mit schwarzen Konturen wird als eigene Umsetzung entworfen.

## 2. Erlaubnisworkflow

1. Assetquelle identifizieren, Original-Lizenz lesen und Nutzungsart prüfen (kommerzielle Videospiele, Binär-/Assetbundles, Bearbeitung, Plattform).
2. Quelle/Autor/Download-Datum/License-Version/Text-Link dokumentieren.
3. Nachweise lokal im Projektarchiv (nicht notwendigerweise im Git-Repo) speichern.
4. Import nur nach Freigabe. Änderungsdateien sauber abgrenzen.
5. Attribution in `THIRD_PARTY_NOTICES.md` und ggf. Credits, wenn verlangt.
6. Bei Unklarheiten: Asset **nicht** verwenden, sondern ersetzen oder Rechte schriftlich klären.

## 3. Klassen von Assets

| Typ | Priorität | Besonderheiten |
|---|---|---|
| Selbst erstellte Assets/Shader | sehr hoch | eindeutige Rechte, Stilkontrolle |
| CC0/Public Domain | sehr hoch | Quelle und Lizenz nachweisen, Sonderrechte prüfen |
| Klar kommerziell lizenzierte Gratispakete | hoch | Lizenztext und Redistribution-Klausel prüfen |
| CC BY | möglich | korrekte Namensnennung/Änderungskennzeichnung |
| NC / nur redaktionelle Nutzung | ausschließen | kommerzielle Veröffentlichung untersagt |
| Unklare Lizenz / extrahierte Game-Assets | ausschließen | keine belastbare Rechtebasis |
| Generierte KI-Assets | Einzelfallprüfung | Tool-Bedingungen, mögliche Ähnlichkeit und Herkunft |

## 4. Vorgeschlagenes Asset-Register

| AssetID | Kategorie | Titel | Autor | URL | Lizenz | kommerziell? | Attribution | geprüft am | Szenen/Dateien |
|---|---|---|---|---|---|---|---|---|---|
| SAMPLE-001 | 3D | Placeholder Rock | Eigenarbeit | intern | eigene Rechte | ja | nein | offen | VisualStyle_Test |
| P01-001 | 3D/Material | Greybox-Arena, Spieler-Platzhalter und 5 Greybox-Materialien | Eigenarbeit (per Editor-Skript aus Unity-Built-in-Primitives und URP/Lit erzeugt) | intern | Unity-Engine-Bestandteile + eigene Rechte | ja | nein | 16.10.2026 | P01_TestArena, Player_Placeholder.prefab, Assets/_Game/Materials |

**Achtung:** Die Sample-Zeile ist nur ein Muster und kein Beleg für einen tatsächlich erstellten Asset.

## 5. Lizenzierungsperspektive

Spätere Avatar-Lizenz nur nach Kontakt mit den dann zuständigen Rechteinhabern/Vertretern und dokumentiertem Vertrag. Bis dahin kein öffentliches Werben mit offizieller Verbindung zum Franchise. Da spätere Anpassung teuer werden kann, frühzeitig eine Präsentation des neutralen Vertical Slice und lizenzrechtliche Sondierung erwägen.

## 6. Kostenkontrolle

Kostenfreie Engine-/Tool- und Assetoptionen bevorzugen; keine Paid-Dependencies ohne ADR. Unity-/Codex-/UGS-Bedingungen und Kontingente vor Produktionsstart sowie erneut vor Release prüfen. Online Relay/Steam-Veröffentlichung können später Gebühren auslösen.
