using UnityEngine;
using UnityEngine.SceneManagement;

namespace Avatar5.Foundation
{
    /// <summary>Temporary development-only OnGUI entry point; production UI belongs to P07.</summary>
    public sealed class DebugMainMenu : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 320, 200), GUI.skin.box);
            GUILayout.Label("AVATAR5 – DEVELOPMENT BUILD");
            GUILayout.Label("Architecture P00 / Greybox");
            if (GUILayout.Button("Open Gameplay Test")) SceneManager.LoadScene("Gameplay_Test");
            if (GUILayout.Button("Open Lobby Placeholder")) SceneManager.LoadScene("Lobby");
            if (GUILayout.Button("Back to Main Menu")) SceneManager.LoadScene("MainMenu");
            GUILayout.EndArea();
        }
    }
}
