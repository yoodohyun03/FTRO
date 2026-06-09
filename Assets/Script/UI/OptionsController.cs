using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class OptionsController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private TextMeshProUGUI resolutionText;
    [SerializeField] private Button prevResBtn;
    [SerializeField] private Button nextResBtn;

    private List<Resolution> uniqueResolutions = new List<Resolution>();
    private int currentResIndex = 0;
    private const string BGM_VOLUME_KEY = "BGMVolume";

    private void Awake()
    {
        // Auto-find if null
        if (bgmSlider == null) bgmSlider = transform.Find("OptionsMenuPanel/BGM_Group/BGM_Slider")?.GetComponent<Slider>();
        if (resolutionText == null) resolutionText = transform.Find("OptionsMenuPanel/Resolution_Group/Resolution_Value")?.GetComponent<TextMeshProUGUI>();
        if (prevResBtn == null) prevResBtn = transform.Find("OptionsMenuPanel/Resolution_Group/Prev_Btn")?.GetComponent<Button>();
        if (nextResBtn == null) nextResBtn = transform.Find("OptionsMenuPanel/Resolution_Group/Next_Btn")?.GetComponent<Button>();
    }

    private void Start()
    {
        InitializeVolume();
        InitializeResolutions();
        
        if (prevResBtn != null) prevResBtn.onClick.AddListener(PrevResolution);
        if (nextResBtn != null) nextResBtn.onClick.AddListener(NextResolution);
    }

    private void InitializeVolume()
    {
        float savedVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 0.5f);
        if (bgmSlider != null)
        {
            bgmSlider.value = savedVolume;
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.onValueChanged.AddListener(SetVolume);
        }
        ApplyVolume(savedVolume);
    }

    private void InitializeResolutions()
    {
        Resolution[] allResolutions = Screen.resolutions;
        uniqueResolutions.Clear();

        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < allResolutions.Length; i++)
        {
            string key = allResolutions[i].width + "x" + allResolutions[i].height;
            if (!seen.Contains(key))
            {
                seen.Add(key);
                uniqueResolutions.Add(allResolutions[i]);
                
                if (allResolutions[i].width == Screen.currentResolution.width &&
                    allResolutions[i].height == Screen.currentResolution.height)
                {
                    currentResIndex = uniqueResolutions.Count - 1;
                }
            }
        }
        
        UpdateResolutionUI();
    }

    public void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
        ApplyVolume(volume);
    }

    private void ApplyVolume(float volume)
    {
        AudioListener.volume = volume;
        AudioSource[] allSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var source in allSources)
        {
            if (source.loop && (source.name.ToLower().Contains("bgm") || source.name.ToLower().Contains("music") || source.name == "Happy"))
            {
                source.volume = volume;
            }
        }
    }

    public void NextResolution()
    {
        currentResIndex = (currentResIndex + 1) % uniqueResolutions.Count;
        ApplyResolution();
    }

    public void PrevResolution()
    {
        currentResIndex--;
        if (currentResIndex < 0) currentResIndex = uniqueResolutions.Count - 1;
        ApplyResolution();
    }

    private void ApplyResolution()
    {
        Resolution res = uniqueResolutions[currentResIndex];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        UpdateResolutionUI();
        Debug.Log($"Resolution applied: {res.width}x{res.height}");
    }

    private void UpdateResolutionUI()
    {
        if (resolutionText != null && uniqueResolutions.Count > 0)
        {
            Resolution res = uniqueResolutions[currentResIndex];
            resolutionText.text = $"{res.width} x {res.height}";
        }
    }

    public void SetupReferences(Slider slider, TextMeshProUGUI resTxt, Button prev, Button next)
    {
        bgmSlider = slider;
        resolutionText = resTxt;
        prevResBtn = prev;
        nextResBtn = next;
    }
}
