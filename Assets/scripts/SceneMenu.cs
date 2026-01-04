using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class SceneMenu : MonoBehaviour, ISceneInitializable
{
    [Header("腳本")]
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;
    public AudioSettingsUI audioSettingsUI;

    [Header("其他UI")]
    public GameObject StartButton;
    public TextMeshProUGUI StartButtonText;
    public CanvasGroup buttonGroup;
    [Space]
    [Tooltip("設定面板")]
    public GameObject SettingPanel;

    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Blink")]
    public float blinkSpeed = 2.5f;      // 數值越大閃越快

    [Header("其他")]
    public GameObject BlackPanel;//黑色遮罩
    [Header("BGM")]
    public GameObject AudioSourceBackMusic;
    public float bgmFadeDuration = 1.5f;

    private Coroutine bgmFadeCo;
    private Coroutine blinkCo;
    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        sceneChangeScript = GetComponent<SceneChange>();
    }

    void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;
    }

    void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
    private void Start()
    {
        animationScript.Fade(BlackPanel, 2.5f, 1, 0, () => BlackPanel.SetActive(false));

    }

    public List<SceneInitStep> BuildInitSteps()
    {
        var steps = new List<SceneInitStep>();
        const float W = 1f / 2f;

        steps.Add(new SceneInitStep
        {
            label = "初始化 音效 狀態…",
            weight = W,
            action = Step_InitVoice
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 UI 狀態…",
            weight = W,
            action = Step_InitUI
        });

        return steps;
    }

    public IEnumerator Step_InitVoice()
    {
        // 讀取存檔的音量（沒有就用 Slider 目前值）
        float music = PlayerPrefs.GetFloat(AudioSettingsUI.MUSIC_PARAM, audioSettingsUI.musicSlider.value);
        float sfx = PlayerPrefs.GetFloat(AudioSettingsUI.SFX_PARAM, audioSettingsUI.sfxSlider.value);

        audioSettingsUI.musicSlider.value = music;
        audioSettingsUI.sfxSlider.value = sfx;

        audioSettingsUI.ApplyMusic(music);
        audioSettingsUI.ApplySFX(sfx);

        // 綁定事件
        audioSettingsUI.musicSlider.onValueChanged.RemoveListener(audioSettingsUI.ApplyMusic);
        audioSettingsUI.sfxSlider.onValueChanged.RemoveListener(audioSettingsUI.ApplySFX);

        audioSettingsUI.musicSlider.onValueChanged.AddListener(audioSettingsUI.ApplyMusic);
        audioSettingsUI.sfxSlider.onValueChanged.AddListener(audioSettingsUI.ApplySFX);


        Debug.Log("初始化聲音");

        yield return null;
    }

    private IEnumerator Step_InitUI()
    {
        // 開場先鎖住（保險）
        SetButtonVisible(false);
        SetButtonInteractable(false);

        StartButton.GetComponent<CanvasGroup>().alpha = 0;

        StartButton.GetComponent<Button>().interactable = false;

        StartButton.GetComponent<CanvasGroup>().blocksRaycasts = false;

        buttonGroup = StartButton.GetComponent<CanvasGroup>();

        SettingPanel.SetActive(false);

        Debug.Log("初始化UI");

        yield return null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("1");
            SettingPanel.SetActive(!SettingPanel.activeSelf);
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // 影片播完觸發
        StartCoroutine(ShowButtonFlow());
    }

    private IEnumerator ShowButtonFlow()
    {
        // 淡入前：不可互動
        SetButtonInteractable(false);
        SetButtonVisible(true);

        // 淡入
        animationScript.Fade(StartButton,1, 0f, 1f,null);
        yield return new WaitForSeconds(1.5f);

        // 淡入後：可互動
        SetButtonInteractable(true);

        // 開始文字閃爍
        if (StartButtonText != null)
        {
            if (blinkCo != null) StopCoroutine(blinkCo);
            blinkCo = StartCoroutine(BlinkTextAlpha(StartButtonText, blinkSpeed));
        }
    }

    private void SetButtonVisible(bool visible)
    {
        if (buttonGroup == null) return;
        buttonGroup.alpha = visible ? 1f : 0f;
    }

    private void SetButtonInteractable(bool on)
    {
        if (buttonGroup != null)
        {
            buttonGroup.interactable = on;
            buttonGroup.blocksRaycasts = on; // 超重要：淡入前不要吃到點擊
        }

        if (StartButton != null)
            StartButton.GetComponent<Button>().interactable = on;
    }

    private IEnumerator BlinkTextAlpha(TextMeshProUGUI tmp, float speed)
    {
        // 用 sin 波做柔和閃爍（不會像開關燈那樣硬）
        while (true)
        {
            float a = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f; // 0~1
            var c = tmp.color;
            c.a = Mathf.Lerp(0.2f, 1f, a); // 透明度範圍可調
            tmp.color = c;
            yield return null;
        }
    }
    public void SceneChangeToDes()
    {
        StartButton.GetComponent<Button>().interactable = false;

        // 2) BGM 淡出（跟黑幕同時開始）
        if (AudioSourceBackMusic != null)
        {
            if (bgmFadeCo != null) StopCoroutine(bgmFadeCo);
            bgmFadeCo = StartCoroutine(FadeAudio(AudioSourceBackMusic.GetComponent<AudioSource>(), bgmFadeDuration, 0f));
        }

        BlackPanel.SetActive(true);
        animationScript.Fade(
            BlackPanel, 
            1.5f,
            0f,
            1f,
            () => LoadingManager.Instance.BeginLoad("des")
        );
        //BlackPanel.SetActive(false );
    }

    public void BackToMenu()
    {
        BlackPanel.SetActive(true);
        animationScript.Fade(
            BlackPanel,
            1.5f,
            0f,
            1f,
            () => LoadingManager.Instance.BeginLoad("menu")
        );
        //BlackPanel.SetActive(false );
    }


    private IEnumerator FadeAudio(AudioSource source, float duration, float targetVolume)
    {
        float startVolume = source.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, t / duration);
            yield return null;
        }

        source.volume = targetVolume;

        // 可選：音量到 0 後直接停掉，省資源（要不要看你需求）
        // source.Stop();
    }
}
