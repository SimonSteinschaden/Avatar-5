# Codex-Regeln für AVATAR5

1. Lies vor jeder Implementierung `README.md`, `docs/00_GESAMTARCHITEKTUR.md`, `docs/10_SCHNITTSTELLENVERTRAEGE.md` und das betroffene Pxx-Dokument.
2. Arbeite nur an der zugewiesenen Story/den zugewiesenen Assembly-Modulen. Schnittstellenänderungen zuerst in `docs/10_SCHNITTSTELLENVERTRAEGE.md` dokumentieren; Konsumenten und Tests mitziehen.
3. Prüfe installierte Unity-Editorversion und Paket-APIs real. Keine Paket- oder API-Versionen erfinden. Unity 6.3 LTS/URP sind Zielvorgaben, keine ungeprüfte Ist-Konfiguration.
4. Immer compile-/testfähig hinterlassen. EditMode- und PlayMode-Tests für geänderte Regeln; Netzwerk-Integrationstests für Replikation und Zustandsübergänge. Nicht ausgeführte Tests explizit kennzeichnen.
5. Host autoritativ für alle Gameplay-Ergebnisse; Clients senden Absichten, keine Schadens- oder Loot-Wahrheiten. Regeln und Presentation trennen.
6. Kleine vertikale Features statt riesiger ungeprüfter Changesets. Eine Story = ein nachvollziehbarer Git-Branch/Commit-Set mit Testbelegen.
7. Behandle `.meta`-Dateien und `.asmdef` als Teil der Quellen. Keine GUID-Kollisionen, keine gebrochenen Prefab-/Material-/Scene-Referenzen.
8. Asset-Lizenz, Herkunft und Attribution in `docs/13_ASSET_LIZENZEN.md` erfassen. Keine geschützten Avatar-/Borderlands-Inhalte übernehmen.
9. Keine neuen Cloud-, Payment-, Datenbank- oder kostenpflichtigen Abhängigkeiten ohne dokumentierte ADR-Freigabe.
10. Am Ende jeder Aufgabe: geänderte Dateien, implementierte Akzeptanzkriterien, Testkommandos + Resultate, offene Risiken und nächster Integrationsschritt.
