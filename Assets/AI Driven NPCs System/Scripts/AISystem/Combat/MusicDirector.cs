using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Plays calm music, and crossfades to fight music while any enemy is fighting.
    /// The fight music starts from the beginning each time a fight starts and stays a few seconds after it ends.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        [SerializeField] private AudioClip calmMusic;
        [SerializeField] private AudioClip fightMusic;
        [Range(0f, 1f)] [SerializeField] private float calmVolume = 0.2f;
        [Range(0f, 1f)] [SerializeField] private float fightVolume = 0.25f;

        [Tooltip("Seconds for one track to fade out and the other to fade in.")]
        [SerializeField] private float fadeSeconds = 2f;

        [Tooltip("Fight music keeps playing this long after the last enemy stops fighting.")]
        [SerializeField] private float fightHoldSeconds = 4f;

        private AudioSource _calm;
        private AudioSource _fight;
        private EnemyAI[] _enemies;
        private float _fightUntil = -1f;

        void Awake()
        {
            _calm  = CreateSource(calmMusic);
            _fight = CreateSource(fightMusic);
        }

        void Start()
        {
            _enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            _calm.volume = calmVolume;
            _calm.Play();
        }

        void Update()
        {
            if (AnyEnemyFighting()) _fightUntil = Time.time + fightHoldSeconds;
            bool fighting = Time.time < _fightUntil;

            if (fighting && !_fight.isPlaying) _fight.Play();

            _calm.volume  = Mathf.MoveTowards(_calm.volume,  fighting ? 0f : calmVolume,  calmVolume  / fadeSeconds * Time.deltaTime);
            _fight.volume = Mathf.MoveTowards(_fight.volume, fighting ? fightVolume : 0f, fightVolume / fadeSeconds * Time.deltaTime);

            if (!fighting && _fight.isPlaying && _fight.volume <= 0f) _fight.Stop();
        }

        private bool AnyEnemyFighting()
        {
            foreach (var enemy in _enemies)
                if (enemy != null && enemy.IsFighting) return true;
            return false;
        }

        private AudioSource CreateSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }
    }
}
