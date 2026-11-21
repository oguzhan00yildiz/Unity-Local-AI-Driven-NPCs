using UnityEngine;
using UnityEngine.UI;

namespace AISystem
{
    /// <summary>
    /// Shows a Health as a filled Image. Works for world-space bars above characters and for HUD bars.
    /// Colors and size are set on the prefab.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [Tooltip("Image with Image Type = Filled.")]
        [SerializeField] private Image fill;
        [Tooltip("Hide the bar while health is full (for enemies and the companion).")]
        [SerializeField] private bool hideWhenFull = true;
        [Tooltip("Object shown and hidden with the bar. Uses this GameObject's first child if empty.")]
        [SerializeField] private GameObject visual;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (visual == null) visual = transform.GetChild(0).gameObject;
            health.OnHealthChanged += Refresh;
            health.OnDied += Hide;
        }

        void Start() => Refresh();

        void OnDestroy()
        {
            health.OnHealthChanged -= Refresh;
            health.OnDied -= Hide;
        }

        private void Refresh()
        {
            fill.fillAmount = health.Fraction;
            visual.SetActive(!health.IsDead && !(hideWhenFull && health.Fraction >= 1f));
        }

        private void Hide() => visual.SetActive(false);
    }
}
