using UnityEngine;
using UnityEngine.UI;

namespace AISystem
{
    /// <summary>
    /// Bottom-of-screen subtitles for Push To Talk mode: the player's last line and the NPC reply.
    /// AISystemManager feeds it. Colors, fonts and layout are set on the prefab.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SubtitleView : MonoBehaviour
    {
        [Header("Player row")]
        [SerializeField] private GameObject playerRow;
        [SerializeField] private Text playerLineText;

        [Header("NPC row")]
        [SerializeField] private GameObject npcRow;
        [SerializeField] private Text npcNameText;
        [SerializeField] private Text npcLineText;

        [Header("Timing")]
        [Tooltip("Seconds the subtitles stay after the NPC stops talking.")]
        [SerializeField] private float hideDelay = 4f;
        [SerializeField] private float fadeDuration = 0.4f;

        private CanvasGroup _group;
        private float _hideAt = -1f;   // unscaled time to start fading; -1 = stay visible
        private float _targetAlpha;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            playerRow.SetActive(false);
            npcRow.SetActive(false);
        }

        void Update()
        {
            if (_hideAt >= 0f && Time.unscaledTime >= _hideAt)
            {
                _targetAlpha = 0f;
                _hideAt = -1f;
            }

            if (!Mathf.Approximately(_group.alpha, _targetAlpha))
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.unscaledDeltaTime / fadeDuration);
        }

        /// <summary>Shows what the player said and clears the previous NPC reply.</summary>
        public void ShowPlayerLine(string text)
        {
            playerLineText.text = text;
            playerRow.SetActive(true);
            npcRow.SetActive(false);
            Show();
        }

        /// <summary>
        /// Shows or updates the NPC reply. Call it again while the reply streams in.
        /// hidePlayerLine is for lines the NPC says on its own, with no player line before it.
        /// </summary>
        public void ShowNPCLine(string npcName, string text, bool hidePlayerLine = false)
        {
            if (hidePlayerLine) playerRow.SetActive(false);
            npcNameText.text = npcName;
            npcLineText.text = text;
            npcRow.SetActive(!string.IsNullOrEmpty(text));
            Show();
        }

        /// <summary>Fades the subtitles out after the hide delay.</summary>
        public void HideAfterDelay() => _hideAt = Time.unscaledTime + hideDelay;

        /// <summary>Starts fading out now.</summary>
        public void Hide()
        {
            _hideAt = -1f;
            _targetAlpha = 0f;
        }

        private void Show()
        {
            _hideAt = -1f;
            _targetAlpha = 1f;
        }
    }
}
