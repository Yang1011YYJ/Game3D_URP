using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class desC : MonoBehaviour, ISceneInitializable
{
    [Header("UI")]
    [Space]
    [Tooltip("火車整體group")] public GameObject Bus;
    [Tooltip("起始位置（畫面外）")] public Transform BusStartPoint;
    [Tooltip("停下來的位置（月台前）")] public Transform BusStopPoint;
    [Tooltip("離開位置（畫面外）")]public Transform BusLeavePoint;
    [Tooltip("火車移動速度")] public float BusSpeed = 5f;
    [Space]
    [Tooltip("說明面板")] public GameObject DesPanel;
    [Tooltip("黑色遮罩")] public GameObject BlackPanel;//
    [Tooltip("顯示地點的文字")] public TextMeshProUGUI PlaceText;
    public GameObject SettingPanel;

    [Header("角色")]
    public GameObject Player;
    public GameObject PlayerWithAnim;
    public Animator PlayerAnimator;
    [Tooltip("主角位置")] public Transform playerTransform;
    [Tooltip("主角起始位置")] public Transform playerStartPos;
    [Tooltip("主角走路速度")] public float walkSpeed = 5f;
    [Tooltip("走進來停下的位置")] public Transform middlePoint;              // 走進來停的位置
    [Tooltip("上車位置")] public Transform boardBusTarget;
    [Tooltip("手機鈴聲01")] public GameObject ring01;
    [Tooltip("手機鈴聲02")] public GameObject ring02;
    [Tooltip("外面世界模型")] public GameObject outside;
    [Tooltip("外面世界牆壁模型")] public GameObject outsideWall;
    [Tooltip("車子裡面模型")] public GameObject inside;
    [Tooltip("車內世界牆壁模型")] public GameObject insideWall;
    [Header("旋轉參數")]
    [Tooltip("玩家上車旋轉")] public float targetZ = 9.28f;   // 目標角度
    [Tooltip("旋轉速度")] public float rotateSpeed = 2f;  // 轉動速度（越大越快）

    [Tooltip("玩家進入後的位置")] public Transform InsidePos;
    [Tooltip("玩家座位位置")] public Transform InsideSitPos;


    [Header("相機")]
    [Tooltip("曝光（URP Volume）")]
    public float normalExposure = 0.5f;
    public float darkExposure = -10f;

    [Header("腳本")]
    public AnimationScript animationScript;
    public CControll cControllScript;
    public SceneChange sceneChangeScript;
    public DialogueSystemDes dialogueSystemDesScript;
    public FadeInByExposure fader;
    public WorldScroller worldScrollerScript;
    public AudioSettingsUI audioSettingsUI;
    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();
        dialogueSystemDesScript = FindAnyObjectByType<DialogueSystemDes>();
        if (Player != null) cControllScript = FindAnyObjectByType<CControll>();
        if (fader == null) fader = FindAnyObjectByType<FadeInByExposure>();
        if (worldScrollerScript == null) worldScrollerScript = FindAnyObjectByType<WorldScroller>();
        audioSettingsUI = FindAnyObjectByType<AudioSettingsUI>();
    }
    private void Start()
    {
    }

    public List<SceneInitStep> BuildInitSteps()
    {
        var steps = new List<SceneInitStep>();
        const float W = 1f / 7f;

        steps.Add(new SceneInitStep
        {
            label = "取得玩家與動畫控制…",
            weight = W,
            action = Step_CacheAnimator
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
            label = "初始化場景狀態…",
            weight = W,
            action = Step_InitWorld
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

        return steps;
    }

    private IEnumerator Step_CacheAnimator()
    {
        PlayerAnimator = cControllScript ? cControllScript.animator : null;

        if (!PlayerAnimator && PlayerWithAnim)
            PlayerAnimator = PlayerWithAnim.GetComponentInChildren<Animator>();

        if (!PlayerAnimator)
            Debug.LogError("[desC] 找不到 Player 的 Animator，請檢查 Player 階層。", this);

        yield return null; // 讓一步至少佔一幀，loading 看起來更穩
    }

    private IEnumerator Step_InitFader()
    {
        fader.Cache();

        yield return null;
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
        audioSettingsUI.musicSlider.onValueChanged.AddListener(audioSettingsUI.ApplyMusic);
        audioSettingsUI.sfxSlider.onValueChanged.AddListener(audioSettingsUI.ApplySFX);

        yield return null;
    }

    private IEnumerator Step_InitUI()
    {
        if (BlackPanel) BlackPanel.SetActive(false);
        if (DesPanel) DesPanel.SetActive(false);
        if (PlaceText) PlaceText.gameObject.SetActive(false);
        if (ring01) ring01.SetActive(false);
        if (ring02) ring02.SetActive(false);
        if (SettingPanel) SettingPanel.SetActive(false);
        if (Bus) Bus.SetActive(false);

        yield return null;
    }

    private IEnumerator Step_InitWorld()
    {
        if (worldScrollerScript) worldScrollerScript.BusMove = false;

        Player.transform.position = playerStartPos.position;
        yield return null;
    }

    private IEnumerator Step_InitDialogue()
    {
        dialogueSystemDesScript.SetPanels(false, false);
        dialogueSystemDesScript.allowFastReveal = false;
        yield return null;
    }
    

    private IEnumerator Step_StartDialogue()
    {
        if (dialogueSystemDesScript != null)
        {
            dialogueSystemDesScript.BindOwner(this);
            dialogueSystemDesScript.autoNextLine = false;
            dialogueSystemDesScript.StartDialogue(dialogueSystemDesScript.Textfile01);
        }
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

    public void FastOK()//快速通過劇情
    {
        dialogueSystemDesScript.allowFastReveal = !dialogueSystemDesScript.allowFastReveal;
        Debug.Log(dialogueSystemDesScript.allowFastReveal.ToString());
    }

    public IEnumerator PlayReverse(string stateName, float duration)//倒著播放動畫
    {
        Animator anim = cControllScript.animator;

        float t = 1f;
        while (t > 0f)
        {
            anim.Play(stateName, 0, t);
            t -= Time.deltaTime / duration;
            yield return null;
        }

        anim.Play(stateName, 0, 0f);
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

    //public void StartButton()
    //{
    //    animationScript.Fade(BlackPanel, 1.5f, "01"));
    //}
    //public void BackButton()
    //{
    //    StartCoroutine(animationScript.FadeOutAndChangeScene(BlackPanel.GetComponent<CanvasGroup>(), 1.5f, "menu"));
    //}

    // ====== 給 DialogueSystemDes 呼叫的 Action ======

    public IEnumerator Act_PhoneSprite()
    {

        //PlayerAnimator.SetBool("phone", true);
        PlayerWithAnim.GetComponent<SpriteRenderer>().sprite = cControllScript.leftphoneidle;
        yield return new WaitForSeconds(2f);
    }

    public IEnumerator Act_HangUpCall()
    {

        StartCoroutine(PlayReverse("phone", 2f));//倒著播放動畫
        yield return new WaitForSeconds(2.5f);
        if (cControllScript.animator != null)
            cControllScript.animator.SetBool("phone", false);

        yield return new WaitForSeconds(1f);
    }

    public IEnumerator Act_PickUPPhone()
    {
        if (ring01) ring01.SetActive(false);
        if (ring02) ring02.SetActive(false);
        audioSettingsUI.StopLoopSFX();

        var a1 = ring01 ? ring01.GetComponent<Animator>() : null;
        var a2 = ring02 ? ring02.GetComponent<Animator>() : null;

        if (a1) a1.SetBool("ring", false);
        if (a2) a2.SetBool("ring", false);

        if (PlayerAnimator != null)
        {
            PlayerAnimator.SetBool("phone", true);
            yield return new WaitForSeconds(cControllScript.phone.length);
        }

        yield return new WaitForSeconds(3);

        //if (CloseButton) CloseButton.SetActive(false);
    }

    public IEnumerator Act_WalkInFromRight()
    {
        if (cControllScript == null || middlePoint == null) yield break;

        cControllScript.Target3D = middlePoint.position;
        cControllScript.StartAutoMoveTo(cControllScript.Target3D);
        audioSettingsUI.PlayPlayerWalk();
        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
    }

    public IEnumerator Act_BoardBus()
    {
        if (cControllScript == null || boardBusTarget == null) yield break;

        cControllScript.Target3D = boardBusTarget.position;
        cControllScript.autoMoveFinished = false;
        cControllScript.animator.SetBool("walk", true);
        cControllScript.isAutoMoving = true;
        audioSettingsUI.PlayPlayerWalk();

        yield return new WaitUntil(() => cControllScript.autoMoveFinished);

        audioSettingsUI.StopLoopSFX();
        cControllScript.animator.SetBool("turn", true);
        yield return new WaitForSeconds(1.1f);
        Player.SetActive(false);

        // 🔒 關閉玩家控制，避免上車後亂動
        cControllScript.playerControlEnabled = false;

        // ✅ 重點：把玩家設成車的子物件
        Player.transform.SetParent(Bus.transform, true);

        cControllScript.animator.SetBool("turn", false);
    }

    public IEnumerator Act_PhoneRing()
    {
        if (ring01) ring01.SetActive(true);
        if (ring02) ring02.SetActive(true);
        audioSettingsUI.PlayPhoneRing();

        var a1 = ring01 ? ring01.GetComponent<Animator>() : null;
        var a2 = ring02 ? ring02.GetComponent<Animator>() : null;

        if (a1) a1.SetBool("ring", true);
        if (a2) a2.SetBool("ring", true);

        yield return new WaitForSeconds(3f);
    }

    public IEnumerator Act_EyeClose(float seconds = 0.8f)
    {
        if (PlayerAnimator != null)
        {
            PlayerAnimator.SetBool("eyeclose", true);
            yield return new WaitForSeconds(2);
            PlayerAnimator.SetBool("eyeclose", false);
        }
    }

    public IEnumerator Act_LightDimDown()
    {
        if (fader == null) yield break;
        yield return StartCoroutine(fader.FadeExposure(1.5f, normalExposure, darkExposure));
    }

    public IEnumerator Act_LightOn()
    {
        if (fader == null) yield break;
        yield return StartCoroutine(fader.FadeExposure(1, darkExposure, normalExposure));
    }

    public IEnumerator Act_BlackPanelOn(float duration = 0.8f)
    {
        if (fader == null) yield break;
        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1.5f, 0f, 1f, null);
        yield return new WaitForSeconds(2f);
    }

    public IEnumerator Act_BlackPanelOff(float duration = 0.8f)
    {
        if (fader == null) yield break;
        animationScript.Fade(BlackPanel, 1.5f, 1f, 0f, null);
        Debug.Log("BlackPanelOff START");

        yield return new WaitForSecondsRealtime(2f);

        Debug.Log("BlackPanelOff AFTER WAIT");
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_NextScene(string sceneName)
    {
        yield return new WaitForSeconds(2f);
        LoadingManager.Instance.BeginLoad("01");
    }


    public IEnumerator Act_showPlace()
    {
        //1.地點顯示
        Player.transform.position = playerStartPos.position;
        PlaceText.gameObject.SetActive(true);
        animationScript.Fade(PlaceText.gameObject, 2f, 0f, 1f, null);
        yield return new WaitUntil(() => PlaceText.gameObject.GetComponent<CanvasGroup>().alpha == 1);
        yield return new WaitForSeconds(1f);
        animationScript.Fade(PlaceText.gameObject, 2f, 1f, 0f, null);
        yield return new WaitUntil(() => PlaceText.gameObject.GetComponent<CanvasGroup>().alpha == 0);
        yield return new WaitForSeconds(1f);
        PlaceText.gameObject.SetActive(false);
    }

    public IEnumerator Act_BusCome()
    {
        if (Bus == null || BusStartPoint == null || BusStopPoint == null) yield break;

        // 確保沒被世界控制
        Bus.transform.SetParent(null, true);

        Bus.SetActive(true);
        Bus.transform.position = BusStartPoint.position;

        // ✅ 車開進站
        while (Vector3.Distance(Bus.transform.position, BusStopPoint.position) > 0.02f)
        {
            Bus.transform.position = Vector3.MoveTowards(
                Bus.transform.position,
                BusStopPoint.position,
                BusSpeed * Time.deltaTime
            );
            yield return null;
        }

        Bus.transform.position = BusStopPoint.position;
    }

    public IEnumerator Act_BusGo()
    {

        if (worldScrollerScript != null)
        {
            worldScrollerScript.StartMove_Speed(5f);
        }
        yield return new WaitForSeconds(3);

    }

    public IEnumerator Act_Inside()
    {
        if (outside) outside.SetActive(false);

        if (outsideWall) outsideWall.SetActive(false);
        if (inside) inside.SetActive(true);
        if (insideWall) insideWall.SetActive(true);

        // 把玩家移到室內位置（兩種做法你選一個）

        // ✅做法1：瞬移（最穩、最不會卡）
        if (InsidePos != null && cControllScript != null)
        {
            Player.transform.position = InsidePos.position;

            cControllScript.animator.SetBool("turn", false);
            Player.SetActive(true);
        }
        else if (InsidePos != null)
        {
            // 如果你玩家不是 cControllScript 那個物件，就改成你的 Player transform
            // playerTransform.position = InsidePos.position;
        }

        yield return new WaitForSeconds(3);
    }

    public IEnumerator Act_MoveToSit()
    {
        if (InsideSitPos == null || cControllScript == null) yield break;

        cControllScript.Target3D = InsideSitPos.position;
        cControllScript.StartAutoMoveTo(cControllScript.Target3D);
        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_Sleep()
    {
        if (PlayerAnimator != null)
        {
            PlayerAnimator.SetBool("sitsleep", true);
        }
        yield return new WaitForSeconds(cControllScript.sitsleep.length);

        yield return new WaitForSeconds(1);
    }

    public IEnumerator Act_PlayQuiteMusic()
    {
        audioSettingsUI.StopBGMLoop();
        audioSettingsUI.PlayDrive();
        yield return null;
    }

}
