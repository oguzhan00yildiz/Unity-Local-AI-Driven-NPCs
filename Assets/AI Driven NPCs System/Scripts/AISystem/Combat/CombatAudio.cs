using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Sword sounds for a fighter: a swing when MeleeAttacker starts an attack, a hit when Health takes damage.
    /// Plays in 3D from the character.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CombatAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip[] swingClips;
        [SerializeField] private AudioClip[] hitClips;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

        private AudioSource _source;
        private MeleeAttacker _attacker;
        private Health _health;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _attacker = GetComponent<MeleeAttacker>();
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            _attacker.OnAttackStarted += PlaySwing;
            _health.OnDamaged += PlayHit;
        }

        void OnDisable()
        {
            _attacker.OnAttackStarted -= PlaySwing;
            _health.OnDamaged -= PlayHit;
        }

        private void PlaySwing() => PlayRandom(swingClips);

        private void PlayHit(float amount, GameObject source) => PlayRandom(hitClips);

        private void PlayRandom(AudioClip[] clips)
        {
            _source.pitch = Random.Range(0.9f, 1.1f);
            _source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
