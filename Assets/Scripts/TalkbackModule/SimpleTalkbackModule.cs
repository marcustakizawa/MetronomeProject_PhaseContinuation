using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple module that can trigger voice/talkback audio samples at specific measure/beat positions
/// </summary>

public class SimpleTalkbackModule : MonoBehaviour
{
    [Header("Metronome Reference")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource talkbackAudioSource;
    [SerializeField] private List<AudioClip> talkbackSamples = new List<AudioClip>();
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1.0f;
    [SerializeField] private bool muteAudio = false;

    [Header("Testing")]
    [SerializeField] private AudioClip testSample;
    [Range(0f, 1f)]
    [SerializeField] private float testSampleVolume = 1.0f;
    [SerializeField] private int testMeasure = 1;
    [SerializeField] private int testBeat = 1;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    // For storing scheduled triggers in editor
    [System.Serializable]
    public class TalkbackTrigger {
        public AudioClip sample;
        public int measureNumber;
        public int beatNumber;
        [Range(0f, 1f)]
        public float volume = 1.0f;
        public string description;
        public bool processed = false;
    }

    [Header("Scheduled Triggers")]
    [SerializeField] private List<TalkbackTrigger> scheduledTriggers = new List<TalkbackTrigger>();

    private void Start() {
        // Find metronome if not set
        if (metronome == null)
            metronome = FindObjectOfType<PrecisionMetronome_v4_MultipleDisplays>();

        // Create audio source if needed
        if (talkbackAudioSource == null) {
            talkbackAudioSource = gameObject.AddComponent<AudioSource>();
            talkbackAudioSource.playOnAwake = false;
        }

        // Subscribe to metronome events
        if (metronome != null) {
            // Subscribe to both beat and measure change events
            metronome.OnBeatTriggered += OnMetronomeBeat;
            metronome.OnReset += OnMetronomeReset;
            Debug.Log($"[Talkback] Connected to metronome: {metronome.name}");
        } else {
            Debug.LogWarning("[Talkback] No metronome found!");
        }

        // Reset all triggers
        ResetAllTriggers();
    }

    private void Update() {
        // Update audio source mute state
        if (talkbackAudioSource != null)
            talkbackAudioSource.mute = muteAudio;
    }

    private void OnDestroy() {
        // Unsubscribe from events
        if (metronome != null) {
            metronome.OnBeatTriggered -= OnMetronomeBeat;
            metronome.OnReset -= OnMetronomeReset;
        }
    }

    /// <summary>
    /// Called when the metronome triggers a beat
    /// </summary>
    private void OnMetronomeBeat(int beat) {
        if (debugMode) {
            Debug.Log($"[Talkback] Beat at M{metronome.CurrentMeasure}:B{beat}");
        }

        // Check for any scheduled triggers
        CheckScheduledTriggers(metronome.CurrentMeasure, beat);
    }

    /// <summary>
    /// Called when metronome is reset
    /// </summary>
    private void OnMetronomeReset() {
        ResetAllTriggers();

        if (debugMode) {
            Debug.Log("[Talkback] Reset all triggers due to metronome reset");
        }
    }

    /// <summary>
    /// Check if any scheduled triggers should play at this measure/beat
    /// </summary>
    private void CheckScheduledTriggers(int measure, int beat) {
        foreach (var trigger in scheduledTriggers) {
            if (!trigger.processed &&
                trigger.measureNumber == measure &&
                trigger.beatNumber == beat) {
                PlaySample(trigger.sample, trigger.volume);
                trigger.processed = true;

                if (debugMode) {
                    Debug.Log($"[Talkback] Triggered: {trigger.description} at M{measure}:B{beat} (Vol: {trigger.volume:F2})");
                }
            }
        }
    }

    /// <summary>
    /// Play a talkback sample immediately with specified volume
    /// </summary>
    public void PlaySample(AudioClip sample, float volume = 1.0f) {
        if (sample == null || talkbackAudioSource == null) return;

        // Skip if muted
        if (muteAudio) return;

        talkbackAudioSource.clip = sample;
        talkbackAudioSource.volume = volume * masterVolume; // Apply both volumes
        talkbackAudioSource.Play();

        if (debugMode) {
            Debug.Log($"[Talkback] Playing sample: {sample.name} (Vol: {talkbackAudioSource.volume:F2})");
        }
    }

    /// <summary>
    /// Play a talkback sample by index from the talkbackSamples list
    /// </summary>
    public void PlaySampleByIndex(int index, float volume = 1.0f) {
        if (index >= 0 && index < talkbackSamples.Count) {
            PlaySample(talkbackSamples[index], volume);
        } else {
            Debug.LogWarning($"[Talkback] Invalid sample index: {index}");
        }
    }

    /// <summary>
    /// Schedule a talkback sample to play at a specific measure/beat
    /// </summary>
    public void ScheduleTalkback(AudioClip sample, int measure, int beat, float volume = 1.0f, string description = "") {
        if (sample == null) {
            Debug.LogWarning("[Talkback] Cannot schedule null sample");
            return;
        }

        TalkbackTrigger trigger = new TalkbackTrigger {
            sample = sample,
            measureNumber = measure,
            beatNumber = beat,
            volume = volume,
            description = description,
            processed = false
        };

        scheduledTriggers.Add(trigger);

        if (debugMode) {
            Debug.Log($"[Talkback] Scheduled: {sample.name} at M{measure}:B{beat} (Vol: {volume:F2}) - {description}");
        }
    }

    /// <summary>
    /// Reset all triggers so they can fire again
    /// </summary>
    public void ResetAllTriggers() {
        foreach (var trigger in scheduledTriggers) {
            trigger.processed = false;
        }

        if (debugMode) {
            Debug.Log($"[Talkback] Reset all triggers");
        }
    }

    /// <summary>
    /// For testing - Play the test sample directly
    /// </summary>
    [ContextMenu("Test Play Sample")]
    public void TestPlaySample() {
        if (testSample != null) {
            PlaySample(testSample, testSampleVolume);
        } else {
            Debug.LogWarning("[Talkback] No test sample assigned");
        }
    }

    /// <summary>
    /// For testing - Schedule the test sample at the specified measure/beat
    /// </summary>
    [ContextMenu("Schedule Test Sample")]
    public void ScheduleTestSample() {
        if (testSample != null) {
            ScheduleTalkback(testSample, testMeasure, testBeat, testSampleVolume, "Test trigger");
            Debug.Log($"[Talkback] Scheduled test sample at M{testMeasure}:B{testBeat} (Vol: {testSampleVolume:F2})");
        } else {
            Debug.LogWarning("[Talkback] No test sample assigned");
        }
    }
}