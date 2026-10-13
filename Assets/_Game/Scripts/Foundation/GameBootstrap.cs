using UnityEngine;
using UnityEngine.SceneManagement;

namespace Avatar5.Foundation
{
    /// <summary>Local-only bootstrap. Networking initialization is intentionally deferred to P03.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap _instance;
        [SerializeField] private string initialSceneName = "MainMenu";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_instance != this) return;
            if (SceneManager.GetActiveScene().name == "Bootstrap" && !string.IsNullOrEmpty(initialSceneName))
            {
                SceneManager.LoadScene(initialSceneName);
            }
        }
    }
}
