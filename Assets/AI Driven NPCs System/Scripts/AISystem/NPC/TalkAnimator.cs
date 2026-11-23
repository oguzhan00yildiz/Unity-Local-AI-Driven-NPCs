using UnityEngine;

namespace AISystem
{
    /// <summary>
    /// Animates an NPC while its voice plays: fades in an Animator layer with a talking gesture loop,
    /// and nods the head with the voice loudness.
    /// Works for NPCAgent replies and AmbientBark lines. Put it on the object that is passed as the speaker
    /// (the NPCAgent or AmbientBark object), with a Humanoid Animator on it or below it.
    /// </summary>
    [DisallowMultipleComponent]
    public class TalkAnimator : MonoBehaviour
    {
        [Header("Gestures")]
        [Tooltip("Animator layer that plays the talking loop (for example Idle_Talking_Loop with an upper-body mask). " +
                 "Its weight fades to 1 while this NPC talks. Without this layer only the head moves.")]
        [SerializeField] private string talkLayerName = "Talk";
        [Tooltip("Seconds to fade the talking layer in and out.")]
        [SerializeField] private float fadeTime = 0.3f;

        [Header("Head")]
        [Tooltip("Head nod in degrees at full loudness. 0 turns head motion off.")]
        [SerializeField] private float nodAngle = 7f;
        [Tooltip("How fast the head follows the voice. Higher reacts faster to each word.")]
        [SerializeField] private float headResponse = 14f;

        private Animator _animator;
        private Transform _head;
        private VoiceOutputService _voice;
        private int _layer = -1;
        private float _weight;
        private float _nod;
        private float _swayPhase;
        // Recent loudest value. Loudness is measured relative to it, so voice level and distance do not matter.
        private float _peak;

        void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _layer = _animator.GetLayerIndex(talkLayerName);
            _head = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            _swayPhase = Random.value * 10f;
        }

        void Start()
        {
            _voice = AISystemManager.Instance.voiceOutput;
        }

        private bool IsTalking
        {
            get
            {
                Transform speaker = _voice.CurrentSpeaker;
                return speaker != null && (speaker == transform || speaker.IsChildOf(transform));
            }
        }

        void Update()
        {
            if (_layer < 0) return;
            float target = IsTalking ? 1f : 0f;
            _weight = Mathf.MoveTowards(_weight, target, Time.deltaTime / Mathf.Max(fadeTime, 0.01f));
            _animator.SetLayerWeight(_layer, _weight);
        }

        // After the Animator, so the nod adds to the animated head pose
        void LateUpdate()
        {
            if (_head == null || nodAngle <= 0f) return;

            float loud = 0f;
            if (IsTalking)
            {
                float level = _voice.CurrentLoudness;
                _peak = Mathf.Max(level, _peak * Mathf.Exp(-0.5f * Time.deltaTime), 0.001f);
                loud = level / _peak;
            }
            _nod = Mathf.Lerp(_nod, loud, 1f - Mathf.Exp(-headResponse * Time.deltaTime));
            if (_nod < 0.001f) return;

            // Nod forward with each word, with a slow side-to-side sway so it does not look mechanical
            _swayPhase += Time.deltaTime * 1.7f;
            float pitch = _nod * nodAngle;
            float yaw = Mathf.Sin(_swayPhase) * _nod * nodAngle * 0.5f;
            _head.rotation = Quaternion.AngleAxis(pitch, transform.right) * Quaternion.AngleAxis(yaw, transform.up) * _head.rotation;
        }
    }
}
