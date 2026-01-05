using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SuccessSceneController : MonoBehaviour, ISceneInitializable
{
    [Header("WorldScroller (無限背景)")]
    public WorldScroller worldScroller;
    public AnimationScript animationScript;
    public DialogueSystemGame03 dialogue03;
    public AudioSettingsUI audioSettingsUI;
    public FadeInByExposure fader;

    [Header("Fail UI")]
    public TextMeshProUGUI SuccessWordText;      // 文字本體（可選）
    public string SuccessWord = "END-回家";
    public float wordFadeDuration = 1.2f;

    [Header("Replay Button")]
    public GameObject replayButton;
    public float replayAppearDelay = 1.0f;
    public float replayFadeDuration = 0.4f;

    [Header("其他UI")]
    [Tooltip("對話框不遮")]public GameObject BlackPanel;
    [Tooltip("全部都遮住")]public GameObject BlackPanel2;

    [Header("設定")]
    public GameObject SettingPanel;
    [Tooltip("快速劇情")] public GameObject FastRevealButton;

    private void Awake()
    {
        if (dialogue03 == null) dialogue03 = FindAnyObjectByType<DialogueSystemGame03>();
        if (audioSettingsUI == null) audioSettingsUI = FindAnyObjectByType<AudioSettingsUI>();
        if (worldScroller == null) worldScroller = FindAnyObjectByType<WorldScroller>();
        if (animationScript == null) animationScript = FindAnyObjectByType<AnimationScript>();
    }

    public List<SceneInitStep> BuildInitSteps()
    {
        var steps = new List<SceneInitStep>();
        const float W = 1f / 8f;

        steps.Add(new SceneInitStep
        {
            label = "取得對話對應…",
            weight = W,
            action = Step_InitOwner
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 世界 狀態…",
            weight = W,
            action = Step_InitWorld
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 燈光 狀態…",
            weight = W,
            action = Step_InitFader
        });

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

        steps.Add(new SceneInitStep
        {
            label = "初始化對話狀態…",
            weight = W,
            action = Step_InitDialogue
        });

        steps.Add(new SceneInitStep
        {
            label = "載入劇情…",
            weight = W,
            action = Step_StartDialogue
        });

        steps.Add(new SceneInitStep
        {
            label = "播放場景音樂…",
            weight = 0.1f,
            action = Step_PlayBGM
        });

        return steps;
    }

    private IEnumerator Step_InitOwner()//對話觸發初始化
    {
        if (dialogue03 != null)
            dialogue03.BindOwner(this);
        yield return null;
    }

    private IEnumerator Step_InitFader()
    {
        fader.Cache();

        yield return null;
    }//光線初始化

    private IEnumerator Step_InitWorld()//初始化 世界
    {
        worldScroller.BusMove = true;
        worldScroller.autoStartMove = true;
        yield return null;
    }

    public IEnumerator Step_InitVoice()//聲音初始化
    {
        // 讀取存檔的音量（沒有就用 Slider 目前值）
        float music = PlayerPrefs.GetFloat(AudioSettingsUI.MUSIC_PARAM, audioSettingsUI.musicSlider.value);
        float sfx = PlayerPrefs.GetFloat(AudioSettingsUI.SFX_PARAM, audioSettingsUI.sfxSlider.value);

        audioSettingsUI.musicSlider.value = music;
        audioSettingsUI.sfxSlider.value = sfx;

        audioSettingsUI.ApplyMusic(music);
        audioSettingsUI.ApplySFX(sfx);

        // 綁定事件
        audioSettingsUI.musicSlider.onValueChanged.AddListener(audioSettingsUI.ApplyMusic);
        audioSettingsUI.sfxSlider.onValueChanged.AddListener(audioSettingsUI.ApplySFX);



        yield return null;
    }

    private IEnumerator Step_InitUI()//UI初始化
    {
        // 初始都先藏起來

        if (replayButton != null)
        {
            replayButton.SetActive(false);
        }
        SuccessWordText.gameObject.SetActive(false);
        BlackPanel.SetActive(true);
        BlackPanel2.SetActive(false);
        SettingPanel.SetActive(false);

        yield return null;
    }

    private IEnumerator Step_InitDialogue()
    {
        dialogue03.SetPanels(false, false);
        dialogue03.allowFastReveal = false;
        yield return null;
    }


    private IEnumerator Step_StartDialogue()
    {
        if (dialogue03 != null)
        {
            dialogue03.autoNextLine = false;

            dialogue03.TextfileCurrent = dialogue03.TextfileGame04_1;
            if (dialogue03.TextfileCurrent != null)
                dialogue03.StartDialogue(dialogue03.TextfileCurrent);
        }
        yield return null;
    }

    private IEnumerator Step_PlayBGM()
    {
        // 確保 loading 還沒淡出前不要播
        yield return null; // 保證至少等一幀

        if (audioSettingsUI != null)
        {
            audioSettingsUI.PlaySuccess(); // 或你場景對應的 BGM
        }
    }

    


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("1");
            SettingPanel.SetActive(!SettingPanel.activeSelf);
        }

        Debug.Log($"[TIME] timeScale={Time.timeScale}, dt={Time.deltaTime}, udt={Time.unscaledDeltaTime}");

        if (Time.timeScale != 1) Time.timeScale = 1f;
    }
    //======================================================
    //設定相關細節
    //======================================================
    public void FastOK()//快速通過劇情
    {
        dialogue03.allowFastReveal = !dialogue03.allowFastReveal;
        Debug.Log(dialogue03.allowFastReveal.ToString());
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

    // ====== 給 Dialogue Action 呼叫 ======

    public IEnumerator Act_WaitforSecond1()
    {
        yield return new WaitForSeconds(1f);
    }

    public IEnumerator Act_BlackPanelOff()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_ShowWordSuccess()
    {
        // 顯示文字
        if (SuccessWordText != null) SuccessWordText.text = SuccessWord;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);

        if (SuccessWordText != null)
        {
            SuccessWordText.gameObject.SetActive(true);
            animationScript.Fade(SuccessWordText.gameObject, wordFadeDuration, 0f, 1f,null);
            yield return new WaitForSeconds(1.5f);
        }

        // 等 1 秒再出現按鈕
        yield return new WaitForSeconds(replayAppearDelay);

        if (replayButton != null)
        {
            replayButton.SetActive(true);
            animationScript.Fade(replayButton, replayFadeDuration, 0f, 1f, null);
            yield return new WaitForSeconds(1.5f);
        }
    }

    public void ResetGameG()
    {
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        // 淡出
        BlackPanel2.SetActive(true);
        animationScript.Fade(BlackPanel2, 1f, 0f, 1f, null);

        // 等淡出完成（時間要跟 Fade 一致）
        yield return new WaitForSeconds(2f);

        SceneManager.LoadScene("menu");
    }
}
