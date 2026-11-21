using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// A background villager says a short written line when the player walks past.
    /// No LLM: the line is spoken with Piper and shown in the subtitles. Skipped while another NPC talks.
    /// </summary>
    public class AmbientBark : MonoBehaviour
    {
        [SerializeField] private string speakerName = "Villager";

        [VoiceName]
        [SerializeField] private string voiceModelName = "en_US-ljspeech-medium";

        [Tooltip("One line is picked at random each time.")]
        [TextArea(1, 3)]
        [SerializeField] private string[] lines;

        [Tooltip("Distance to the player that starts a line.")]
        [SerializeField] private float range = 3.5f;

        [Tooltip("Seconds before this villager speaks again.")]
        [SerializeField] private float cooldown = 30f;

        public string VoiceModelName => voiceModelName;

        private Transform _player;
        private AISystemManager _manager;
        private float _nextBarkTime;

        void Start()
        {
            _player = GameObject.FindWithTag("Player").transform;
            _manager = AISystemManager.Instance;
        }

        void Update()
        {
            if (Time.time < _nextBarkTime) return;
            if ((_player.position - transform.position).sqrMagnitude > range * range) return;

            // Try again in a few seconds when another NPC is talking
            _nextBarkTime = _manager.SpeakLine(speakerName, voiceModelName, lines[Random.Range(0, lines.Length)], transform)
                ? Time.time + cooldown
                : Time.time + 3f;
        }
    }
}
