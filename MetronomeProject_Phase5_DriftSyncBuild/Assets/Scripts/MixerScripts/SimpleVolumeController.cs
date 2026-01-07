using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// Simple volume and mute controller for metronomes
/// Works with pre-configured Audio Mixer Groups
/// </summary>
public class SimpleVolumeController : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    
    [Header("Volume Controls")]
    [SerializeField] private Slider metronome1VolumeSlider;
    [SerializeField] private Slider metronome2VolumeSlider;
    [SerializeField] private Slider metronome3VolumeSlider;
    [SerializeField] private Slider masterVolumeSlider;
    
    [Header("Mute Controls")]
    [SerializeField] private Toggle metronome1MuteToggle;
    [SerializeField] private Toggle metronome2MuteToggle;
    [SerializeField] private Toggle metronome3MuteToggle;
    [SerializeField] private Toggle masterMuteToggle;
    
    [Header("Volume Labels")]
    [SerializeField] private Text metronome1VolumeLabel;
    [SerializeField] private Text metronome2VolumeLabel;
    [SerializeField] private Text metronome3VolumeLabel;
    [SerializeField] private Text masterVolumeLabel;

    // Mixer parameter names (these correspond to exposed parameters in your Audio Mixer)
    private const string METRONOME1_VOLUME = "Metronome1Volume";
    private const string METRONOME2_VOLUME = "Metronome2Volume";
    private const string METRONOME3_VOLUME = "Metronome3Volume";
    private const string MASTER_VOLUME = "MasterVolume";
    
    // Store pre-mute volumes
    private float metronome1PreMuteVolume = 0f;
    private float metronome2PreMuteVolume = 0f;
    private float metronome3PreMuteVolume = 0f;
    private float masterPreMuteVolume = 0f;

    void Start()
    {
        SetupVolumeSliders();
        SetupMuteToggles();
        InitializeVolumes();
    }

    private void SetupVolumeSliders()
    {
        // Setup volume sliders with proper range (Audio Mixer uses dB, so -80 to 0)
        if (metronome1VolumeSlider != null)
            metronome1VolumeSlider.onValueChanged.AddListener(SetMetronome1Volume);
            
        if (metronome2VolumeSlider != null)
            metronome2VolumeSlider.onValueChanged.AddListener(SetMetronome2Volume);
            
        if (metronome3VolumeSlider != null)
            metronome3VolumeSlider.onValueChanged.AddListener(SetMetronome3Volume);
            
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
    }

    private void SetupMuteToggles()
    {
        if (metronome1MuteToggle != null)
            metronome1MuteToggle.onValueChanged.AddListener(MuteMetronome1);
            
        if (metronome2MuteToggle != null)
            metronome2MuteToggle.onValueChanged.AddListener(MuteMetronome2);
            
        if (metronome3MuteToggle != null)
            metronome3MuteToggle.onValueChanged.AddListener(MuteMetronome3);
            
        if (masterMuteToggle != null)
            masterMuteToggle.onValueChanged.AddListener(MuteMaster);
    }

    private void InitializeVolumes()
    {
        // Set default volumes (convert 0-1 range to dB)
        SetMetronome1Volume(0.8f);
        SetMetronome2Volume(0.8f);
        SetMetronome3Volume(0.6f);
        SetMasterVolume(0.8f);
        
        // Update sliders to match
        if (metronome1VolumeSlider != null) metronome1VolumeSlider.value = 0.8f;
        if (metronome2VolumeSlider != null) metronome2VolumeSlider.value = 0.8f;
        if (metronome3VolumeSlider != null) metronome3VolumeSlider.value = 0.6f;
        if (masterVolumeSlider != null) masterVolumeSlider.value = 0.8f;
    }

    /// <summary>
    /// Convert linear volume (0-1) to decibels for Audio Mixer
    /// </summary>
    private float LinearToDecibel(float linear)
    {
        return linear > 0 ? Mathf.Log10(linear) * 20 : -80f;
    }

    /// <summary>
    /// Volume control methods
    /// </summary>
    public void SetMetronome1Volume(float volume)
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat(METRONOME1_VOLUME, LinearToDecibel(volume));
            metronome1PreMuteVolume = volume;
            UpdateVolumeLabel(metronome1VolumeLabel, volume, metronome1MuteToggle?.isOn ?? false);
        }
    }

    public void SetMetronome2Volume(float volume)
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat(METRONOME2_VOLUME, LinearToDecibel(volume));
            metronome2PreMuteVolume = volume;
            UpdateVolumeLabel(metronome2VolumeLabel, volume, metronome2MuteToggle?.isOn ?? false);
        }
    }

    public void SetMetronome3Volume(float volume)
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat(METRONOME3_VOLUME, LinearToDecibel(volume));
            metronome3PreMuteVolume = volume;
            UpdateVolumeLabel(metronome3VolumeLabel, volume, metronome3MuteToggle?.isOn ?? false);
        }
    }

    public void SetMasterVolume(float volume)
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat(MASTER_VOLUME, LinearToDecibel(volume));
            masterPreMuteVolume = volume;
            UpdateVolumeLabel(masterVolumeLabel, volume, masterMuteToggle?.isOn ?? false);
        }
    }

    /// <summary>
    /// Mute control methods
    /// </summary>
    public void MuteMetronome1(bool mute)
    {
        if (audioMixer != null)
        {
            if (mute)
            {
                audioMixer.SetFloat(METRONOME1_VOLUME, -80f); // Mute
            }
            else
            {
                audioMixer.SetFloat(METRONOME1_VOLUME, LinearToDecibel(metronome1PreMuteVolume)); // Restore
            }
            UpdateVolumeLabel(metronome1VolumeLabel, metronome1PreMuteVolume, mute);
        }
    }

    public void MuteMetronome2(bool mute)
    {
        if (audioMixer != null)
        {
            if (mute)
            {
                audioMixer.SetFloat(METRONOME2_VOLUME, -80f);
            }
            else
            {
                audioMixer.SetFloat(METRONOME2_VOLUME, LinearToDecibel(metronome2PreMuteVolume));
            }
            UpdateVolumeLabel(metronome2VolumeLabel, metronome2PreMuteVolume, mute);
        }
    }

    public void MuteMetronome3(bool mute)
    {
        if (audioMixer != null)
        {
            if (mute)
            {
                audioMixer.SetFloat(METRONOME3_VOLUME, -80f);
            }
            else
            {
                audioMixer.SetFloat(METRONOME3_VOLUME, LinearToDecibel(metronome3PreMuteVolume));
            }
            UpdateVolumeLabel(metronome3VolumeLabel, metronome3PreMuteVolume, mute);
        }
    }

    public void MuteMaster(bool mute)
    {
        if (audioMixer != null)
        {
            if (mute)
            {
                audioMixer.SetFloat(MASTER_VOLUME, -80f);
            }
            else
            {
                audioMixer.SetFloat(MASTER_VOLUME, LinearToDecibel(masterPreMuteVolume));
            }
            UpdateVolumeLabel(masterVolumeLabel, masterPreMuteVolume, mute);
        }
    }

    /// <summary>
    /// Update volume label display
    /// </summary>
    private void UpdateVolumeLabel(Text label, float volume, bool muted)
    {
        if (label != null)
        {
            if (muted)
            {
                label.text = "MUTED";
            }
            else
            {
                label.text = $"{(volume * 100):F0}%";
            }
        }
    }

    /// <summary>
    /// Preset methods for quick setup
    /// </summary>
    public void SetTwoMetronomePreset()
    {
        // Metronome 1 and 2 active, 3 muted
        if (metronome1VolumeSlider != null) metronome1VolumeSlider.value = 0.9f;
        if (metronome2VolumeSlider != null) metronome2VolumeSlider.value = 0.9f;
        if (metronome3MuteToggle != null) metronome3MuteToggle.isOn = true;
        
        Debug.Log("Applied Two Metronome Preset");
    }

    public void SetThreeMetronomePreset()
    {
        // All three metronomes active
        if (metronome1VolumeSlider != null) metronome1VolumeSlider.value = 0.8f;
        if (metronome2VolumeSlider != null) metronome2VolumeSlider.value = 0.8f;
        if (metronome3VolumeSlider != null) metronome3VolumeSlider.value = 0.7f;
        if (metronome3MuteToggle != null) metronome3MuteToggle.isOn = false;
        
        Debug.Log("Applied Three Metronome Preset");
    }

    public void MuteAll()
    {
        if (masterMuteToggle != null)
        {
            masterMuteToggle.isOn = true;
        }
    }
}