using UnityEngine;
using UnityEngine.UI;

namespace AISystem
{
    /// <summary>Shows the current quest objective. Text style is set on the prefab.</summary>
    public class QuestHUD : MonoBehaviour
    {
        [SerializeField] private QuestManager quest;
        [SerializeField] private Text objectiveText;
        [Tooltip("Hidden when the objective is empty.")]
        [SerializeField] private GameObject panel;

        void Start()
        {
            quest.OnStageChanged += Show;
            Show(quest.CurrentStage);
        }

        void OnDestroy() => quest.OnStageChanged -= Show;

        private void Show(StoryLore.QuestStage stage)
        {
            objectiveText.text = stage.objective;
            panel.SetActive(!string.IsNullOrEmpty(stage.objective));
        }
    }
}
