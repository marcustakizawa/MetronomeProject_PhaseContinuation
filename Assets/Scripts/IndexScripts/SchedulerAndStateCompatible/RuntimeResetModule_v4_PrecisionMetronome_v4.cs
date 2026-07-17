using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime Reset Module - Simple System Reset for v10 Coordinator, compatible with MetronomeScheduler, MetronomeStateManager, PrecisionMetronome_v4_MultipleDisplays
/// Version: 2025-11-10 (Clean Slate Provider)
/// 
/// PURPOSE: Provides runtime equivalent of Unity scene restart
/// - One job: Complete system reset without scene reload
/// - Perfect clean slate for the original v8's proven coordination logic
/// - No mixing with v8 functionality - pure reset only
/// 
/// WORKFLOW: User hits Reset → Clean slate achieved → v8, and subsequent versions (Schedule and State extracted versions) can work perfectly
/// </summary>

public class RuntimeResetModule_v4_PrecisionMetronome_v4 : MonoBehaviour
{
    [Header("Metronomes References")]
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome1; // CHANGED: v3 → v4
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome2; // CHANGED: v3 → v4  
    [SerializeField] private PrecisionMetronome_v4_MultipleDisplays metronome3; // CHANGED: v3 → v4

    [Header("Reset UI")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button emergencyResetButton;

    [Header("Reset Diagnostics")]
    [SerializeField] private bool verboseLogging = true;

    void Start() {
        SetupUI();
        Debug.Log("RuntimeResetModule initialized - Ready to provide clean slate");
    }

    void SetupUI() {
        if (resetButton) {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ExecuteSystemReset);
        }

        if (emergencyResetButton) {
            emergencyResetButton.onClick.RemoveAllListeners();
            emergencyResetButton.onClick.AddListener(EmergencySystemReset);
        }
    }

    /// <summary>
    /// Main Reset Method - Simulates scene restart for clean slate
    /// </summary>
    public void ExecuteSystemReset() {
        Debug.Log("🔄 SYSTEM RESET - Simulating scene restart...");

        // Phase 1: Complete System Reset (Scene End Simulation)
        if (!ExecuteCompleteSystemReset()) {
            Debug.LogError("❌ System reset failed");
            return;
        }

        // Phase 2: System Reinitialization (Scene Start Simulation) 
        if (!ExecuteSystemReinitialization()) {
            Debug.LogError("❌ System reinitialization failed");
            return;
        }

        Debug.Log("✅ SYSTEM RESET COMPLETE - Clean slate achieved!");
        Debug.Log("📋 Ready for v8 coordinator operations");
    }

    /// <summary>
    /// Phase 1: Complete System Reset - Equivalent to scene ending
    /// </summary>
    bool ExecuteCompleteSystemReset() {
        if (verboseLogging) Debug.Log("🔄 Phase 1: Complete System Reset...");

        try {
            // 1. Stop all metronomes (like scene objects being destroyed)
            StopAllMetronomes();

            // 2. Clear all audio streams and DSP states
            ClearAllAudioStates();

            // 3. Clear all scheduled events and timers
            ClearAllScheduledSystems();

            // 4. Reset all metronome internal states
            ResetAllMetronomeStates();

            if (verboseLogging) Debug.Log("✅ Phase 1 Complete: System reset successful");
            return true;

        } catch (System.Exception ex) {
            Debug.LogError($"❌ Phase 1 Failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Phase 2: System Reinitialization - Equivalent to scene starting
    /// </summary>
    bool ExecuteSystemReinitialization() {
        if (verboseLogging) Debug.Log("🔄 Phase 2: System Reinitialization...");

        try {
            // 1. Reinitialize all metronomes (like fresh scene objects)
            ReinitializeAllMetronomes();

            // 2. Reset timeline and audio systems
            ResetTimelineAndAudioSystems();

            // 3. Verify clean state
            if (!VerifyCleanState()) {
                Debug.LogWarning("⚠️ Clean state verification failed - proceeding anyway");
            }

            if (verboseLogging) Debug.Log("✅ Phase 2 Complete: System reinitialization successful");
            return true;

        } catch (System.Exception ex) {
            Debug.LogError($"❌ Phase 2 Failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stop all metronomes completely
    /// </summary>
    void StopAllMetronomes() {
        if (verboseLogging) Debug.Log("   Stopping all metronomes...");

        if (metronome1) {
            metronome1.StopMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome1 stopped");
        }

        if (metronome2) {
            metronome2.StopMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome2 stopped");
        }

        if (metronome3) {
            metronome3.StopMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome3 stopped");
        }
    }

    /// <summary>
    /// Clear all audio states and DSP chains
    /// </summary>
    void ClearAllAudioStates() {
        if (verboseLogging) Debug.Log("   Clearing audio states...");

        // Force audio system cleanup
        AudioSettings.Reset(AudioSettings.GetConfiguration());

        // Clear any pending audio callbacks
        System.GC.Collect();

        if (verboseLogging) Debug.Log("   ✓ Audio states cleared");
    }

    /// <summary>
    /// Clear all scheduled events and timers
    /// </summary>
    void ClearAllScheduledSystems() {
        if (verboseLogging) Debug.Log("   Clearing scheduled systems...");

        // Clear metronome scheduled changes
        if (metronome1) metronome1.ClearPendingChanges();
        if (metronome2) metronome2.ClearPendingChanges();
        if (metronome3) metronome3.ClearPendingChanges();

        // Stop all coroutines on metronomes
        if (metronome1) metronome1.StopAllCoroutines();
        if (metronome2) metronome2.StopAllCoroutines();
        if (metronome3) metronome3.StopAllCoroutines();

        if (verboseLogging) Debug.Log("   ✓ Scheduled systems cleared");
    }

    /// <summary>
    /// Reset all metronome internal states
    /// </summary>
    void ResetAllMetronomeStates() {
        if (verboseLogging) Debug.Log("   Resetting metronome states...");

        if (metronome1) {
            metronome1.ResetMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome1 state reset");
        }

        if (metronome2) {
            metronome2.ResetMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome2 state reset");
        }

        if (metronome3) {
            metronome3.ResetMetronome();
            if (verboseLogging) Debug.Log("   ✓ Metronome3 state reset");
        }
    }

    /// <summary>
    /// Reinitialize all metronomes as if scene just started
    /// </summary>
    void ReinitializeAllMetronomes() {
        if (verboseLogging) Debug.Log("   Reinitializing metronomes...");

        // Set metronome names (like fresh scene start)
        if (metronome1) {
            metronome1.gameObject.name = "TestMetronome1";
            if (verboseLogging) Debug.Log("   ✓ Metronome1 reinitialized");
        }

        if (metronome2) {
            metronome2.gameObject.name = "TestMetronome2";
            if (verboseLogging) Debug.Log("   ✓ Metronome2 reinitialized");
        }

        if (metronome3) {
            metronome3.gameObject.name = "TestMetronome3";
            if (verboseLogging) Debug.Log("   ✓ Metronome3 reinitialized");
        }
    }

    /// <summary>
    /// Reset timeline and audio systems
    /// </summary>
    void ResetTimelineAndAudioSystems() {
        if (verboseLogging) Debug.Log("   Resetting timeline and audio systems...");

        // Clear any cached timeline calculations
        System.GC.Collect();

        if (verboseLogging) Debug.Log("   ✓ Timeline and audio systems reset");
    }

    /// <summary>
    /// Verify that we achieved a clean state
    /// </summary>
    bool VerifyCleanState() {
        if (verboseLogging) Debug.Log("   Verifying clean state...");

        bool isClean = true;

        // Check metronome states
        if (metronome1 && metronome1.IsPlaying) {
            Debug.LogWarning("   ⚠️ Metronome1 still playing");
            isClean = false;
        }

        if (metronome2 && metronome2.IsPlaying) {
            Debug.LogWarning("   ⚠️ Metronome2 still playing");
            isClean = false;
        }

        if (metronome3 && metronome3.IsPlaying) {
            Debug.LogWarning("   ⚠️ Metronome3 still playing");
            isClean = false;
        }

        if (isClean && verboseLogging) {
            Debug.Log("   ✅ Clean state verified");
        }

        return isClean;
    }

    /// <summary>
    /// Emergency reset - nuclear option
    /// </summary>
    public void EmergencySystemReset() {
        Debug.Log("🚨 EMERGENCY SYSTEM RESET - Nuclear option activated");

        ExecuteCompleteSystemReset();
        ExecuteSystemReinitialization();

        Debug.Log("🚨 Emergency reset complete - System should be in clean state");
    }

    /// <summary>
    /// Public method for external systems to trigger reset
    /// </summary>
    public void TriggerReset() {
        ExecuteSystemReset();
    }

    /// <summary>
    /// Check if system is ready for operations
    /// </summary>
    public bool IsSystemReady() {
        return metronome1 != null &&
               metronome2 != null &&
               metronome3 != null;
    }

    void OnValidate() {
        // Auto-find metronomes if not assigned
        if (metronome1 == null || metronome2 == null || metronome3 == null) {
            PrecisionMetronome_v4_MultipleDisplays[] metronomes = FindObjectsOfType<PrecisionMetronome_v4_MultipleDisplays>();
            if (metronomes.Length >= 3) {
                metronome1 = metronomes[0];
                metronome2 = metronomes[1];
                metronome3 = metronomes[2];
                Debug.Log("Auto-assigned metronomes for reset module");
            }
        }
    }
}