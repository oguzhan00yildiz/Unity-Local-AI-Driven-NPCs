using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AISystem
{
    /// <summary>
    /// Full-screen message with a restart button, used for "Game Over" and "Quest complete".
    /// Show() is hooked to UnityEvents (Health.onDied, QuestManager stage events). Style is set on the prefab.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class EndScreen : MonoBehaviour
    {
        [SerializeField] private Button restartButton;
        [SerializeField] private float fadeDuration = 0.8f;
        [Tooltip("Seconds to wait before the screen appears, e.g. to let a death animation play.")]
        [SerializeField] private float showDelay = 1.2f;

        private CanvasGroup _group;
        private float _showAt = -1f;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            SetVisible(0f);
            restartButton.onClick.AddListener(Restart);
        }

        void Update()
        {
            if (_showAt < 0f || Time.unscaledTime < _showAt) return;
            SetVisible(Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / fadeDuration));
        }

        public void Show()
        {
            if (_showAt >= 0f) return;
            _showAt = Time.unscaledTime + showDelay;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var input = FindFirstObjectByType<PlayerInputHandler>();
            if (input != null) input.CursorLocked = false;
        }

        public void Restart()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // Demo scenes are often not in Build Settings; the Editor can still load them by path
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        private void SetVisible(float alpha)
        {
            _group.alpha = alpha;
            _group.interactable = _group.blocksRaycasts = alpha > 0.5f;
        }
    }
}
