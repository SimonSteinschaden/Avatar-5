using System;
using Avatar5.Contracts;
using UnityEngine;

namespace Avatar5.Networking
{
    /// <summary>Player-facing text for session errors (German, matches the team's language).</summary>
    public static class SessionErrorText
    {
        public static string For(SessionError error)
        {
            switch (error)
            {
                case SessionError.None: return string.Empty;
                case SessionError.InvalidCode: return "Der Code ist ungültig. Bitte prüfen und erneut eingeben.";
                case SessionError.SessionNotFound: return "Keine Sitzung mit diesem Code gefunden oder sie ist abgelaufen.";
                case SessionError.SessionFull: return "Die Sitzung ist voll (maximal 4 Spieler).";
                case SessionError.VersionMismatch: return "Andere Spielversion als der Host. Bitte beide dieselbe Version starten.";
                case SessionError.TimedOut: return "Zeitüberschreitung beim Verbinden.";
                case SessionError.Cancelled: return "Abgebrochen.";
                case SessionError.AlreadyInSession: return "Du bist bereits in einer Sitzung.";
                case SessionError.ServicesUnavailable: return "Online-Dienste nicht erreichbar oder Projekt nicht mit Unity Cloud verknüpft.";
                case SessionError.TransportFailed: return "Netzwerkverbindung konnte nicht aufgebaut werden.";
                case SessionError.ConnectionLost: return "Verbindung zum Host verloren.";
                case SessionError.HostEnded: return "Der Host hat die Sitzung beendet.";
                case SessionError.Kicked: return "Du wurdest aus der Sitzung entfernt.";
                default: return "Unbekannter Fehler.";
            }
        }
    }

    /// <summary>Temporary development lobby (OnGUI). Final lobby UI belongs to P07 and talks to the same ISessionService.</summary>
    public sealed class SessionDebugLobby : MonoBehaviour
    {
        private string _code = string.Empty;
        private bool _busy;
        private string _message = string.Empty;

        private void OnGUI()
        {
            var runtime = SessionRuntime.Instance;
            GUILayout.BeginArea(new Rect(20, 20, 380, 360), GUI.skin.box);
            GUILayout.Label("AVATAR5 – Sitzung (Debug)");
            if (runtime == null)
            {
                GUILayout.Label("SessionRuntime fehlt in der Szene.");
                GUILayout.EndArea();
                return;
            }

            var state = runtime.Current.State;
            var offline = state.Phase == SessionPhase.Offline || state.Phase == SessionPhase.Failed;

            GUI.enabled = offline && !_busy;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Verbindung:", GUILayout.Width(90));
            if (GUILayout.Toggle(runtime.Mode == SessionTransportMode.Direct, "Direkt (LAN)", GUI.skin.button)) runtime.Mode = SessionTransportMode.Direct;
            if (GUILayout.Toggle(runtime.Mode == SessionTransportMode.Relay, "Internet (Relay)", GUI.skin.button)) runtime.Mode = SessionTransportMode.Relay;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Klasse:", GUILayout.Width(50));
            foreach (ElementKind element in Enum.GetValues(typeof(ElementKind)))
            {
                if (GUILayout.Toggle(PlayerClassSettings.Selected == element, ElementName(element), GUI.skin.button)) PlayerClassSettings.Selected = element;
            }
            GUILayout.EndHorizontal();
            CampaignSaveSettings.ContinueRequested = GUILayout.Toggle(CampaignSaveSettings.ContinueRequested, " Spielstand fortsetzen (Host)");
            if (GUILayout.Button("Private Sitzung erstellen")) Create(runtime);
            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code, 32, GUILayout.Width(200));
            if (GUILayout.Button("Beitreten")) Join(runtime);
            GUILayout.EndHorizontal();
            GUILayout.Label(runtime.Mode == SessionTransportMode.Direct ? "Code im LAN: IP:Port des Hosts, z. B. 192.168.0.12:7777" : "Code: Einladungscode des Hosts");

            GUI.enabled = !offline && !_busy;
            if (GUILayout.Button("Sitzung verlassen")) Leave(runtime);
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label("Status: " + PhaseText(state.Phase) + (state.Role == SessionRole.None ? string.Empty : " als " + (state.Role == SessionRole.Host ? "Host" : "Client")));
            if (state.IsActive)
            {
                GUILayout.Label("Spieler: " + state.PlayerCount + " / " + state.MaxPlayers);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Einladungscode: " + state.JoinCode);
                if (!string.IsNullOrEmpty(state.JoinCode) && GUILayout.Button("Kopieren", GUILayout.Width(80))) GUIUtility.systemCopyBuffer = state.JoinCode;
                GUILayout.EndHorizontal();
            }
            var error = SessionErrorText.For(state.LastError);
            if (!string.IsNullOrEmpty(error)) GUILayout.Label("Fehler: " + error);
            if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message);
            var world = WorldHost.Instance;
            if (world != null && world.LastLoad.Error != SaveLoadError.None && world.LastLoad.Error != SaveLoadError.NotFound)
                GUILayout.Label("Spielstand nicht geladen: " + SaveErrorText(world.LastLoad.Error));
            GUILayout.Label("Protokoll " + runtime.Config.ProtocolVersion + " · Inhalt " + runtime.Config.ContentVersion);
            GUILayout.EndArea();
        }

        private async void Create(SessionRuntime runtime)
        {
            _busy = true;
            _message = "Erstelle Sitzung …";
            var result = await runtime.CreateAsync();
            _message = result.Success ? string.Empty : SessionErrorText.For(result.Error);
            _busy = false;
        }

        private async void Join(SessionRuntime runtime)
        {
            _busy = true;
            _message = "Trete bei …";
            var result = await runtime.JoinAsync(_code);
            _message = result.Success ? string.Empty : SessionErrorText.For(result.Error);
            _busy = false;
        }

        private async void Leave(SessionRuntime runtime)
        {
            _busy = true;
            await runtime.LeaveAsync();
            _message = string.Empty;
            _busy = false;
        }

        private static string ElementName(ElementKind element)
        {
            switch (element)
            {
                case ElementKind.Water: return "Wasser";
                case ElementKind.Earth: return "Erde";
                case ElementKind.Air: return "Luft";
                default: return "Feuer";
            }
        }

        private static string SaveErrorText(SaveLoadError error)
        {
            switch (error)
            {
                case SaveLoadError.UnsupportedSchema: return "von einer anderen Spielversion.";
                case SaveLoadError.ContentMismatch: return "passt nicht zu diesem Level/Inhalt.";
                case SaveLoadError.UnknownContentId: return "enthält unbekannte Objekte.";
                case SaveLoadError.Corrupt: return "beschädigt (auch das Backup).";
                default: return "Lesefehler.";
            }
        }

        private static string PhaseText(SessionPhase phase)
        {
            switch (phase)
            {
                case SessionPhase.Creating: return "erstelle";
                case SessionPhase.Joining: return "verbinde";
                case SessionPhase.Connected: return "verbunden";
                case SessionPhase.Leaving: return "verlasse";
                case SessionPhase.Failed: return "fehlgeschlagen";
                default: return "offline";
            }
        }
    }
}
