using UnityEngine;
using UnityEngine.UI;
using ChangeComposer.Data;
using System.Collections.Generic;

namespace ChangeComposer.Controllers {
    /// <summary>
    /// Main controller for the streamlined three-track system
    /// Coordinates parsing, validation, and metronome integration
    /// </summary>
    public class StreamlinedMetronomeController : MonoBehaviour {
        [Header("Transport Controls")]
        [SerializeField] private Button startAllButton;
        [SerializeField] private Button stopAllButton;

        [Header("Metronomes")]
        [SerializeField] private PrecisionMetronome metronome1;
        [SerializeField] private PrecisionMetronome metronome2;
        [SerializeField] private PrecisionMetronome metronome3;

        [Header("Track Panels")]
        [SerializeField] private TrackPanel trackPanel1;
        [SerializeField] private TrackPanel trackPanel2;
        [SerializeField] private TrackPanel trackPanel3;

        private void Start() {
            SetupTransportControls();
            SetupTrackPanels();
            Debug.Log("StreamlinedMetronomeController initialized");
        }

        private void SetupTransportControls() {
            if (startAllButton != null)
                startAllButton.onClick.AddListener(StartAllMetronomes);

            if (stopAllButton != null)
                stopAllButton.onClick.AddListener(StopAllMetronomes);
        }

        private void SetupTrackPanels() {
            // Connect track panels to their corresponding metronomes via TrackMetronomeManager
            SetupTrackPanelMetronomes(trackPanel1, 0); // Default to Metronome 1
            SetupTrackPanelMetronomes(trackPanel2, 1); // Default to Metronome 2  
            SetupTrackPanelMetronomes(trackPanel3, 2); // Default to Metronome 3
        }

        private void SetupTrackPanelMetronomes(TrackPanel trackPanel, int defaultMetronomeIndex) {
            if (trackPanel == null) return;

            // Find the TrackMetronomeManager on the same GameObject
            var metronomeManager = trackPanel.GetComponent<TrackMetronomeManager>();
            if (metronomeManager == null) {
                Debug.LogWarning($"No TrackMetronomeManager found on {trackPanel.gameObject.name}");
                return;
            }

            // Set the metronome references
            metronomeManager.SetMetronomes(metronome1, metronome2, metronome3);

            // Set default target
            metronomeManager.SetTarget(defaultMetronomeIndex);

            Debug.Log($"Connected {trackPanel.gameObject.name} to metronomes, default target: Metronome {defaultMetronomeIndex + 1}");
        }

        public void StartAllMetronomes() {
            Debug.Log("Starting all metronomes...");

            // Start all metronomes simultaneously
            double startTime = AudioSettings.dspTime + 0.5; // 0.5 second delay

            if (metronome1 != null) {
                metronome1.SetStartTime(startTime);
                metronome1.StartMetronome();
            }

            if (metronome2 != null) {
                metronome2.SetStartTime(startTime);
                metronome2.StartMetronome();
            }

            if (metronome3 != null) {
                metronome3.SetStartTime(startTime);
                metronome3.StartMetronome();
            }

            Debug.Log($"All metronomes will start at DSP time: {startTime}");
        }

        public void StopAllMetronomes() {
            Debug.Log("Stopping all metronomes...");

            if (metronome1 != null) metronome1.PauseMetronome();
            if (metronome2 != null) metronome2.PauseMetronome();
            if (metronome3 != null) metronome3.PauseMetronome();
        }

        // TEST: Apply stop events to all metronomes
        [ContextMenu("Test Stop Events")]
        public void TestStopEvents() {
            Debug.Log("Testing stop events on all metronomes...");

            // Schedule stop events at measure 8 for testing
            var stopChange1 = MetronomeChange.CreateStopEvent(8, "Test stop 1");
            var stopChange2 = MetronomeChange.CreateStopEvent(8, "Test stop 2");
            var stopChange3 = MetronomeChange.CreateStopEvent(8, "Test stop 3");

            if (metronome1 != null) metronome1.ScheduleChange(stopChange1);
            if (metronome2 != null) metronome2.ScheduleChange(stopChange2);
            if (metronome3 != null) metronome3.ScheduleChange(stopChange3);

            Debug.Log("Stop events scheduled for measure 8 on all metronomes");
        }

        // Getters for track panels to access metronomes
        public PrecisionMetronome GetMetronome(int index) {
            switch (index) {
                case 1: return metronome1;
                case 2: return metronome2;
                case 3: return metronome3;
                default: return null;
            }
        }
    }
}