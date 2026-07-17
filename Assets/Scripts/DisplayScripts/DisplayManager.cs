using UnityEngine;
using UnityEngine.UI;

public class DisplayManager : MonoBehaviour
{
    [SerializeField] private bool enableMultipleDisplays = true;
    [SerializeField] private Text displayInfoText; // Optional - for debugging
    
    void Start()
    {
        // Log display count
        Debug.Log($"Display count: {Display.displays.Length}");
        UpdateDisplayInfoText();
        
        // Activate additional displays if enabled
        if (enableMultipleDisplays)
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                Display.displays[i].Activate();
                Debug.Log($"Activated display {i}: {Display.displays[i].systemWidth}x{Display.displays[i].systemHeight}");
            }
            
            UpdateDisplayInfoText();
        }
    }
    
    private void UpdateDisplayInfoText()
    {
        if (displayInfoText != null)
        {
            string info = $"Displays: {Display.displays.Length}\n";
            for (int i = 0; i < Display.displays.Length; i++)
            {
                info += $"Display {i}: {Display.displays[i].systemWidth}x{Display.displays[i].systemHeight} " +
                       $"({(Display.displays[i].active ? "Active" : "Inactive")})\n";
            }
            displayInfoText.text = info;
        }
    }
    
    public void LogDisplayInfo()
    {
        for (int i = 0; i < Display.displays.Length; i++)
        {
            Debug.Log($"Display {i}: " +
                     $"Resolution: {Display.displays[i].systemWidth}x{Display.displays[i].systemHeight}, " +
                     $"Active: {Display.displays[i].active}");
        }
    }
    
    public void ExitFullscreen()
    {
        Debug.Log("Exit fullscreen requested");
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        Screen.fullScreen = false;
    }
    
    public void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        Debug.Log($"Fullscreen toggled: {Screen.fullScreen}");
    }
}