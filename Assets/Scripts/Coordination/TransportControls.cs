using UnityEngine;

/// <summary>
/// TransportControls - UI transport layer for CompositionCoordinator_v2
/// Version: 2026-08-10 v1
///
/// PURPOSE:
///   Thin wrapper between the scene's Play/Stop/Reset buttons and the
///   coordinator. Exists for one reason: LoadAndStart() does not stop a
///   running system before restarting it, so pressing Play twice would
///   re-enter an already-running coordinator. PlayFromTop() guarantees a
///   clean stop first.
///
///   Stop and Reset are passthroughs. They are here only so all three
///   buttons reference a single component, which makes the wiring easier
///   to audit in the inspector.
///
/// SETUP IN INSPECTOR:
///   1. Add this component to any GameObject (the UI panel or the
///      coordinator's own GameObject both work).
///   2. Assign 'coordinator'. If this component sits on the same
///      GameObject as the coordinator, it self-assigns on Awake.
///   3. Wire buttons: Play -> PlayFromTop(), Stop -> Stop(), Reset -> ResetAll()
/// </summary>
public class TransportControls : MonoBehaviour
{

    // =========================================================
    // INSPECTOR FIELDS
    // =========================================================

    [Header("═══ COORDINATOR ═══")]
    [Tooltip("The CompositionCoordinator_v2 this transport drives. " +
             "Leave empty to auto-find on this GameObject.")]
    [SerializeField] private CompositionCoordinator_v2 coordinator;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        // Convenience: if this sits on the coordinator's GameObject,
        // wire itself up so a missed inspector drag isn't a silent failure.
        if (coordinator == null)
            coordinator = GetComponent<CompositionCoordinator_v2>();

        if (coordinator == null)
            Debug.LogError("[TransportControls] No coordinator assigned. " +
                           "Buttons will do nothing.", this);
    }

    // =========================================================
    // PUBLIC API - BUTTON TARGETS
    // =========================================================

    /// <summary>
    /// Play from the top of the composition.
    /// Stops any running metronomes first, then reloads and starts.
    /// Safe to call repeatedly, including while already playing.
    /// </summary>
    public void PlayFromTop()
    {
        if (coordinator == null) return;

        coordinator.StopAll();
        coordinator.LoadAndStart();
    }

    /// <summary>
    /// Stop all metronomes. Composition stays loaded.
    /// </summary>
    public void Stop()
    {
        if (coordinator == null) return;

        coordinator.StopAll();
    }

    /// <summary>
    /// Full reset: stop, clear metronome/scheduler state, discard the
    /// loaded composition. Next PlayFromTop() reloads from JSON.
    /// </summary>
    public void ResetAll()
    {
        if (coordinator == null) return;

        coordinator.ResetComposition();
    }
}