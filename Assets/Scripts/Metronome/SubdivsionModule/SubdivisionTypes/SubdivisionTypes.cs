/// <summary>
/// Shared enum for subdivision types used across all subdivision metronome versions
/// Version: 2025-11-10 v1
/// 
/// PURPOSE: Define available rhythm subdivision types
/// - Eighth notes: 2x parent BPM
/// - Triplets: 3x parent BPM  
/// - Sixteenth notes: 4x parent BPM
/// 
/// USAGE: Reference this enum in any subdivision-related scripts
/// </summary>
public enum SubdivisionType {
    /// <summary>
    /// Eighth notes - 2x the parent metronome's BPM
    /// Example: Parent at 120 BPM → Eighth notes at 240 BPM
    /// </summary>
    Eighth,

    /// <summary>
    /// Triplet subdivision - 3x the parent metronome's BPM
    /// Example: Parent at 120 BPM → Triplets at 360 BPM
    /// </summary>
    Triplet,

    /// <summary>
    /// Sixteenth notes - 4x the parent metronome's BPM
    /// Example: Parent at 120 BPM → Sixteenth notes at 480 BPM
    /// </summary>
    Sixteenth
}