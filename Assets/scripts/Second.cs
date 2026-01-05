using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;


public class Second : MonoBehaviour, ISceneInitializable
{
    [Header("Phase")]
    public GamePhase currentPhase = GamePhase.Playing;//一定要設值不然會是0或是第一個
    public enum EndingIntent
    {
        None,
        Success,
        Fail
    }

    private EndingIntent _pendingEnding = EndingIntent.None;


    [Header("腳本")]
    public SpotManager spotManager;
    public TimeControll timer;
    public DialogueSystemGame01 dialogueSystemGame01Script;
    public FadeInByExposure fader;
    public CControll cControllScript;
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;
    private SpotDef currentDef;
    public AudioSettingsUI audioSettingsUI;
    public WorldScroller worldScroller;

    [Header("遊戲用UI")]
    public GameObject ErrorPanel;
    public GameObject RedPanel;
    public GameObject PhotoFrameImage;
    public TextMeshProUGUI HintText;
    [Tooltip("Round UI")]
    public TextMeshProUGUI countText; // 顯示「找到 X / >5」
    public GameObject CountTextImage;
    [Header("Background Click")]
    public GameObject BackgroundButton; // 透明全螢幕 Button 的 GameObject

    [Header("設定")]
    [Tooltip("設定面板")] public GameObject SettingPanel;
    [Tooltip("快速劇情")] public GameObject FastRevealButton;

    [Header("題庫")]
    public List<SpotDef> spotPool = new();          // Inspector 拖 8 題
    private List<SpotDef> bag = new();              // 抽題用袋子（不放回）

    [Header("ErrorPanel 圖片顯示")]
    public Image errorPictureImage;                 // ErrorPanel 上用來顯示題目的 Image（Inspector拖）


    [Header("其他UI")]
    //public GameObject PhonePanel;
    [Tooltip("玩家對話框還看的到")] public GameObject BlackPanel;
    [Tooltip("玩家對話框看不到")] public GameObject BlackPanel22;
    [Tooltip("遊戲名字")] public GameObject GameName;
    [Tooltip("遊戲內劇情用的時間")] public GameObject Timetext;
    public GameObject TimetextImage;
    public GameObject ring01;
    public GameObject ring02;

    [Header("Photo Frame Follow")]
    [Tooltip("拍照框本體")] public RectTransform PhotoFrameRect;     // 拍照框本體
    public Canvas UICanvas;                  // UI Canvas
    [Tooltip("拍照框跟隨速度")] public float photoFrameFollowSpeed = 18f;

    // 內部旗標
    [Tooltip("是否開啟拍照框跟隨滑鼠")] private bool photoFrameFollowEnabled = false;
    private Coroutine photoFrameMoveRoutine;

    [Header("燈光")]
    public GameObject BusUpLightTotal;

    [Header("玩家相關")]
    public GameObject Player;
    public GameObject PlayerWithAni;
    //[Tooltip("跑到前面去看司機")] public Transform WalkToFrontPos;
    public Transform PlayerStartPos;

    [Header("車子相關")]
    [Tooltip("車子本體")] public Rigidbody busRb;
    [Header("Bus Shake (Visual Only)")] public Transform busVisualRoot; // ✅只放視覺公車，不含地板碰撞

    [Header("Game Settings")]
    public int roundSeconds = 15;
    // ===== Round gate: 讓 Act_GameStart 等到回合結束才放行 =====
    private bool _roundFinished = false;
    private bool _roundFlowDone = false;
    private RoundEndType _lastEndType;
    [Tooltip("選擇題目")]public bool selectFinish = false;
    [Tooltip("選擇題目完畢")] public bool hasSelectedRound = false;
    [Tooltip("jumpscare")]private Coroutine redFlashRoutine;
    [Header("遊戲失誤")] 
    private Coroutine _mistakeRoutine;
    private int _prevLives = -1;
    private bool _gameStarted = false;
    private bool _endedByTimeout = false;


    [Header("相機相關")]
    [Tooltip("Focal length")] public float targetFocal;
    [Tooltip("移動速度")] public float speed;

    [Header("其他")]
    public Camera cam;

    private void Awake()
    {
        if (spotManager == null) spotManager = FindAnyObjectByType<SpotManager>();
        if (timer == null) timer = FindAnyObjectByType<TimeControll>();

        if (dialogueSystemGame01Script == null) dialogueSystemGame01Script = FindAnyObjectByType<DialogueSystemGame01>();
        if (fader == null) fader = FindAnyObjectByType<FadeInByExposure>();
        if (cControllScript == null) cControllScript = FindAnyObjectByType<CControll>();
        if (animationScript == null) animationScript = FindAnyObjectByType<AnimationScript>();
        if (sceneChangeScript == null) sceneChangeScript = FindAnyObjectByType<SceneChange>();
        if (audioSettingsUI == null) audioSettingsUI = FindAnyObjectByType<AudioSettingsUI>();
        if (worldScroller == null) worldScroller = FindAnyObjectByType<WorldScroller>();

        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        if (spotManager != null)
        {
            spotManager.OnLivesChanged += HandleLivesChanged;
            spotManager.OnRoundEnded += HandleRoundEnded;
            spotManager.OnRoundBegan += HandleRoundBegan;
            spotManager.OnTotalFoundChanged += TotalFoundChangedUI;
        }

    }

    private void OnDisable()
    {
        if (spotManager != null)
        {
            spotManager.OnLivesChanged -= HandleLivesChanged;
            spotManager.OnRoundEnded -= HandleRoundEnded;
            spotManager.OnRoundBegan -= HandleRoundBegan;
            spotManager.OnTotalFoundChanged -= TotalFoundChangedUI;

        }

    }

    private void Start()
    {
        
    }

    public List<SceneInitStep> BuildInitSteps()
    {
        var steps = new List<SceneInitStep>();
        const float W = 1f / 10f;

        steps.Add(new SceneInitStep
        {
            label = "取得對話對應…",
            weight = W,
            action = Step_InitOwner
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 燈光 狀態…",
            weight = W,
            action = Step_InitFader
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 世界 狀態…",
            weight = W,
            action = Step_InitWorld
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
            label = "初始化玩家位置…",
            weight = W,
            action = Step_InitPlayerPos
        });

        steps.Add(new SceneInitStep
        {
            label = "初始化 回合 狀態…",
            weight = W,
            action = Step_InitRoundUI
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
        if (dialogueSystemGame01Script != null)
            dialogueSystemGame01Script.BindOwner(this);
        yield return null;
    }

    private IEnumerator Step_InitFader()
    {
        fader.Cache();

        yield return null;
    }//光線初始化

    private IEnumerator Step_InitWorld()
    {
        worldScroller.BusMove = true;

        yield return null;
    }//世界初始化

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
        CleanupUI();//關掉所有UI
        //教學UI關掉
        CleanupRoundUI();
        SettingPanel.SetActive(false);

        yield return null;
    }

    private IEnumerator Step_InitPlayerPos()//玩家位置初始化
    {
        Player.transform.position = PlayerStartPos.position;
        yield return null;
    }
    private IEnumerator Step_InitRoundUI()//回合數量初始化
    {
        DisableAllSpots();   // ✅ 遊戲一開始，全部 spot 關掉
        DisableBackgroundClick(); // ✅ 一開始先關

        _prevLives = (spotManager != null) ? spotManager.livesLeft : 2;

        //更新數量
        TotalFoundChangedUI(spotManager.totalFound);

        //時間
        if (timer.timerText != null) timer.timerText.gameObject.SetActive(false);
        timer.TimeTextImage.SetActive(false);
        yield return null;
    }

    private IEnumerator Step_InitDialogue()
    {
        dialogueSystemGame01Script.SetPanels(false, false);
        dialogueSystemGame01Script.allowFastReveal = false;
        yield return null;
    }


    private IEnumerator Step_StartDialogue()
    {
        if (dialogueSystemGame01Script != null)
        {
            dialogueSystemGame01Script.autoNextLine = false;

            dialogueSystemGame01Script.TextfileCurrent = dialogueSystemGame01Script.TextfileGame01;
            dialogueSystemGame01Script.StartDialogue(dialogueSystemGame01Script.TextfileCurrent);
        }
        yield return null;
    }

    private IEnumerator Step_PlayBGM()
    {
        audioSettingsUI.StopLoopBGM();
        // 確保 loading 還沒淡出前不要播
        yield return null; // 保證至少等一幀

        if (audioSettingsUI != null)
        {
            audioSettingsUI.PlayDrive(); // 或你場景對應的 BGM
        }
    }


    private void Update()
    {
        if (photoFrameFollowEnabled)
            FollowPointer(PhotoFrameRect);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("1");
            SettingPanel.SetActive(!SettingPanel.activeSelf);
        }

        if (Time.timeScale != 1) Time.timeScale = 1f;
    }
    //======================================================
    //設定相關細節
    //======================================================
    public void FastOK()//快速通過劇情
    {
        dialogueSystemGame01Script.allowFastReveal = !dialogueSystemGame01Script.allowFastReveal;
        Debug.Log(dialogueSystemGame01Script.allowFastReveal.ToString());
    }

    // =====================================================
    // 🎬 劇情呼叫入口
    // =====================================================

    /// <summary>
    /// 由對話 / 劇情呼叫（gamestart）
    /// </summary>
    public IEnumerator Act_GameStart()//開始遊戲(狀態辨識)
    {
        if (!hasSelectedRound)//題目沒須選完不開始
        {
            Debug.LogWarning("[Second] 還沒 SelectRound，不能開始 GameStart");
            yield break;
        }

        // 每次開始一回合前，重置閘門
        _roundFinished = false;
        _roundFlowDone = false;

        ErrorPanel.SetActive(true);
        animationScript.Fade(ErrorPanel, 1, 0, 1, null);
        PhotoFrameImage.gameObject.SetActive(true);
        //PhonePanel.gameObject.SetActive(false);
        //yield return new WaitForSeconds(1.5f);

        // ✅（重點）鎖對話系統輸入，避免空白鍵亂跳
        dialogueSystemGame01Script.inputLocked = true; // 開始

        // ===== 正式遊戲回合開始 =====
        yield return StartCoroutine(StartNormalRound());

        // ✅ 讓「劇情 action」卡住，直到 SpotManager 回合結束
        yield return new WaitUntil(() => _roundFinished);

        // ✅ 再等到回合結束演出也跑完（不然你可能一結束就跳劇情）
        yield return new WaitUntil(() => _roundFlowDone);
    }

    /// <summary>
    /// 由對話 / 劇情呼叫（teachstart）
    /// </summary>

    // =====================================================
    // 🎓 教學流程
    // =====================================================


    // =====================================================
    // 🎮 正式遊戲流程
    // =====================================================
    // 入口1：點到 spot
    public void OnSpotClicked()
    {
        if (currentPhase == GamePhase.Playing)
        {
            currentDef.spotRoot.GetComponent<Button>().interactable = false;
            spotManager.OnPlaySpotClick();
            return;
        }

    }
    //入口2:點到背景
    public void OnBackgroundClicked()
    {
        if (currentPhase != GamePhase.Playing) return;
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd();
        }
        spotManager.OnBackgroundClicked();
    }

    private IEnumerator StartNormalRound()//開始正式遊戲
    {
        currentDef.spotRoot.GetComponent<Button>().interactable = false;
        Debug.Log("[Second] Normal round start");
        _gameStarted = true;
        _prevLives = spotManager.livesLeft;  // ✅ 這回合開始先記住 lives

        RedPanel.gameObject.SetActive(false);
        HintText.gameObject.SetActive(true);
        HintText.gameObject.GetComponent<CanvasGroup>().alpha = 1;
        HintText.text = "請在15秒內點擊異常!";

        // 4) 開啟這一題的 spot
        if (currentDef.spotRoot != null) currentDef.spotRoot.SetActive(true);

            

        // 5) 把題目圖片換到 ErrorPanel 上
        if (currentDef.questionImage != null)
        {
            animationScript.Fade(currentDef.questionImage.gameObject, 1, 0, 1, null);
            currentDef.questionImage.gameObject.SetActive(true);
        }

        OpenErrorPanel();     // ✅ 保證 ErrorPanel 會亮
        animationScript.Fade(ErrorPanel,1, 0, 1, null);
        yield return new WaitForSeconds(1.5f);
        currentDef.spotRoot.GetComponent<Button>().interactable = true;
        // ✅ 保證拍照框會出現且跟隨
        EnablePhotoFrameFollow();
        EnableBackgroundClick();
        //HideTeachHint();

        // 開回合
        spotManager.BeginRound();

        // Timer 超時 = 當成失誤
        //timer.onTimeUp = () =>
        //{
        //    Debug.Log("[First] Timeout");
        //    spotManager.OnTimeout();
        //};

        //timer.StartCountdown(roundSeconds);
        yield return null;
    }
    private void SetupTargets()
    {
        selectFinish = false;
        // 1) 初始化抽題袋（bag）—不放回
        if (bag == null) bag = new List<SpotDef>();
        if (bag.Count == 0)
        {
            bag.AddRange(spotPool);

            // 你如果希望每次重新進遊戲題目順序不同，可以洗牌
            for (int i = 0; i < bag.Count; i++)
            {
                int r = Random.Range(i, bag.Count);
                (bag[i], bag[r]) = (bag[r], bag[i]);
            }
        }

        // 2) 抽一題（從 bag 取出並移除 = 不放回）
        currentDef = bag[0];
        bag.RemoveAt(0);

        Debug.Log($"[Second] Pick Def id={currentDef.id}");

        // 3) 先把所有題目的 spot 都關掉
        for (int i = 0; i < spotPool.Count; i++)
        {
            if (spotPool[i].spotRoot != null)
                spotPool[i].spotRoot.SetActive(false);
        }

        //// 4) 開啟這一題的 spot
        //if (currentDef.spotRoot != null)
        //    currentDef.spotRoot.SetActive(true);

        //// 5) 把題目圖片換到 ErrorPanel 上
        //if (currentDef.questionImage != null)
        //    currentDef.questionImage.gameObject.SetActive(true);

        //// 6) （可選）清理 UI 或提示文字
        //if (HintText != null)
        //{
        //    HintText.gameObject.SetActive(true);
        //    HintText.gameObject.GetComponent<CanvasGroup>().alpha = 1;
        //    HintText.text = "在時間內找出異常！";
        //}
        selectFinish = true;
        hasSelectedRound = true;

    }

    private void OpenErrorPanel()
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(true);
    }
    private void EnableBackgroundClick()
    {
        if (BackgroundButton != null) BackgroundButton.SetActive(true);
    }

    private void DisableBackgroundClick()
    {
        if (BackgroundButton != null) BackgroundButton.SetActive(false);
    }

    // =====================================================
    // 🎮 回合指定對應spot
    // =====================================================

    public IEnumerator Act_SelectRound() 
    {
        SetupTargets();      // ← 唯一差異點（教學 vs 隨機）
        yield return null;
    }

    //public void Btn_SelectAndStart()
    //{
    //    StartCoroutine(SelectAndStartRoutine());
    //}

    public IEnumerator SelectAndStartRoutine()
    {
        yield return StartCoroutine(Act_SelectRound());
        yield return StartCoroutine(Act_GameStart());
    }

    private void DisableAllSpots()//關掉所有spot和對應圖片
    {
        for (int i = 0; i < spotPool.Count; i++)
        {
            if (spotPool[i] != null && spotPool[i].spotRoot != null)
                spotPool[i].spotRoot.SetActive(false);
            if (spotPool[i] != null && spotPool[i].questionImage != null)
                spotPool[i].questionImage.gameObject.SetActive(false);
        }
    }


    // =====================================================
    // 📡 SpotManager 事件回調
    // =====================================================

    private void HandleRoundEnded(int roundIndex, int totalFound, RoundEndType endType)//計算當前回合數
    {
        Debug.Log($"[First] Round {roundIndex} end: {endType}");

        // 停 timer
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd(); // ✅ 真正停止倒數協程 + 停止 UI 更新（你 TimeControll 要做到）
        }
        


        // 記錄回合結果（讓 Act_GameStart 知道回合已經結束）
        _roundFinished = true;
        _lastEndType = endType;

        // 跑回合結束演出，演出跑完才真正「放行」
        StartCoroutine(RoundEndFlow_AndMarkDone(endType));
    }
    private void HandleLivesChanged(int livesLeft)//生命改變
    {
        // 還沒開始正式遊戲時（ResetGame / BeginRound 同步）不要演出
        if (!_gameStarted)
        {
            _prevLives = livesLeft;
            return;
        }

        // ✅ lives 沒變少（例如 BeginRound 只是同步）→ 不要演出
        if (_prevLives != -1 && livesLeft >= _prevLives)
        {
            _prevLives = livesLeft;
            return;
        }

        
        _prevLives = livesLeft;

        // lives==0 的死亡演出交給 RoundEnded(FailedRound)
        //if (livesLeft <= 0) return;

        // ✅ 每次扣命都要：jumpscare + 重計時（如果還活著）
        StartCoroutine(LifeLostFlow(livesLeft));
    }
    private IEnumerator RoundEndFlow_AndMarkDone(RoundEndType endType)
    {
        _roundFlowDone = false;

        yield return StartCoroutine(RoundEndFlow(endType));

        _roundFlowDone = true;
    }

    private IEnumerator RoundEndFlow(RoundEndType endType)//回合結束的流程
    {
        _roundFlowDone = false;
        // 0) 保險：停止互動、停止倒數
        spotManager.PauseRound();
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd();
        }

        // 1. 成功時的特別演出 (閃光、文字)
        if (endType == RoundEndType.FoundSpot)
        {
            yield return StartCoroutine(HandleSuccess());
        }
        else
        {
            // 失敗時，LifeLostFlow 已經播過 Jumpscare 了，這裡只需做最後的清理
            yield return new WaitForSeconds(1f);
        }
        Debug.Log("111111");
        // ⭐ 用完一定要重置，不然下一回合會殘留
        _endedByTimeout = false;

        // 2️⃣ 回合 UI 清乾淨（畫面回到正常狀態）
        CleanupRoundUI();

        //// ⭐ 回合間的呼吸時間（你要的 5 秒）
        //Debug.Log("回合結束，等待 5 秒呼吸時間...");
        //yield return new WaitForSeconds(5f);

        // 4️⃣ 現在才判斷最終結局
        CheckFinalResultOrContinue();
        _roundFlowDone = true;

        //// ✅ FailedRound：跑「死亡演出」再跳 fail
        //yield return StartCoroutine(DeathEndFlow());
        //yield break;
    }

    private void CheckFinalResultOrContinue()//判斷是通關還是失敗
    {
        // 取得當前狀態以便偵錯
        int found = spotManager.totalFound;
        int target = spotManager.needFoundToWin;
        int currentRound = spotManager.roundIndex;
        int maxRounds = spotManager.totalRounds;
        int lives = spotManager.livesLeft;

        Debug.Log($"[結算檢查] 進度：{found}/{target} | 生命：{lives} | 回合：{currentRound}/{maxRounds}");

        if (spotManager.IsWin())
        {
            Debug.Log("[Second] GAME WIN → play ending story");
            currentPhase = GamePhase.End;

            _pendingEnding = EndingIntent.Success;

            // 播放通關劇情
            dialogueSystemGame01Script.keepTalk = true;
            dialogueSystemGame01Script.TextfileCurrent = dialogueSystemGame01Script.TextfileGame04;
            dialogueSystemGame01Script.nextDialogue = dialogueSystemGame01Script.TextfileCurrent;
            dialogueSystemGame01Script.isBusy = false;
            return;
        }

        else if (lives <= 0 || currentRound >= maxRounds)
        {
            Debug.Log("[Second] GAME OVER → play fail story");
            currentPhase = GamePhase.End;

            _pendingEnding = EndingIntent.Fail;

            // 播放失敗劇情
            dialogueSystemGame01Script.keepTalk = true;
            dialogueSystemGame01Script.TextfileCurrent = dialogueSystemGame01Script.TextfileGame03;
            dialogueSystemGame01Script.nextDialogue = dialogueSystemGame01Script.TextfileCurrent;
            dialogueSystemGame01Script.isBusy = false;
            return;
        }
        else
        {
            // ⭐ 關鍵補齊點
            Debug.Log("[Second] Continue to next round");

            //if (_roundFinished == false || _roundFlowDone == false)
            //{
            //    Debug.Log(_roundFinished);
            //    Debug.Log(_roundFlowDone);
            //    return;
            //}
            //if (dialogueSystemGame01Script != null && dialogueSystemGame01Script.isBusy) return;
            StartCoroutine(SelectAndStartRoutine());

        }
    }

    private IEnumerator GoToWinScene()
    {
        // 可選：淡出、關燈、音效
        yield return new WaitForSeconds(0.5f);

        BlackPanel22.SetActive(true);
        animationScript.Fade(
            BlackPanel22,
            1f,
            0f,
            1f,
            () => sceneChangeScript.SceneC("success")
        );
    }

    private IEnumerator GoToLoseScene()
    {
        yield return new WaitForSeconds(0.5f);

        cControllScript.animator.SetBool("die", true);
        yield return new WaitForSeconds(3);

        BlackPanel22.SetActive(true);
        animationScript.Fade(
            BlackPanel22,
            1f,
            0f,
            1f,
            () => sceneChangeScript.SceneC("fail")
        );
    }
    private void TotalFoundChangedUI(int totalFound)//更新UI
    {
        if (countText == null || spotManager == null) return;

        countText.gameObject.SetActive(true); // 要不要顯示你可控
        CountTextImage.SetActive(true);
        countText.text = $"{totalFound} / {spotManager.totalRounds}";
    }

    private void HandleRoundBegan(int roundIndex)
    {
        // 每回合開始：開 timer
        timer.onTimeUp = () =>
        {
            _endedByTimeout = true;
            spotManager.OnTimeout();
        };
        timer.StartCountdown(roundSeconds);
    }

    private IEnumerator LifeLostFlow(int livesLeft)//生命-1
    {
        Debug.Log("生命-1");
        // ✅ 先暫停，避免玩家在 jumpscare 期間又扣第二次
        spotManager.PauseRound();

        // 1. 不論是第幾次失敗，一律先播 Jumpscare
        yield return StartCoroutine(HandleFailure());

        // 2) 提示文字
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.alpha = 1;

            HintText.text = _endedByTimeout ? $"……太慢了！剩餘機會：{livesLeft}"
                                            : $"這裡沒有異常！剩餘機會：{livesLeft}";


        }
        
        yield return new WaitForSeconds(2f);// 讓玩家看清楚文字

        // 3. 根據生命值判斷後續行為
        if (livesLeft > 0)
        {
            // --- 還有生命：回合繼續 ---
            RedPanel.gameObject.SetActive(false);
            HintText.gameObject.SetActive(false);

            timer.ForceEnd();
            timer.StartCountdown(roundSeconds);// 重新計時

            yield return new WaitForSeconds(0.2f);
            HintText.gameObject.SetActive(true);
            HintText.alpha = 1;
            HintText.text = "再試一次!";


            spotManager.ResumeRound();// 回到可點擊狀態
            yield break;
        }
        else
        {
            // --- 沒有生命了：正式結束回合 ---
            Debug.Log("生命耗盡，準備結束回合");
            spotManager.ForceRoundEnd();
        }


        // ⭐ 用完一定要重置，不然下一回合會殘留
        _endedByTimeout = false;
        // livesLeft==0 的話：
        // SpotManager 會觸發 HandleRoundEnded(FailedRound) 去走結尾/跳場景
    }

    // =====================================================
    // 🎉 成功 / 失敗演出
    // =====================================================

    private IEnumerator HandleSuccess()//點到正確位置
    {
        Debug.Log("[First] Success feedback");

        //生命加滿
        if(spotManager.livesLeft < 2)
        {
            spotManager.livesLeft = 2;
        }


        // TODO：成功閃光 / 正確提示
        // 先暫停回合（避免玩家連點）
        spotManager.PauseRound();

        // 顯示「異常捕捉...」
        HintText.gameObject.SetActive(true);
        HintText.text = "異常捕捉中...";

        // 閃一下
        yield return StartCoroutine(fader.FlashByExposure(10f, 0.01f, 0.08f));

        // 等一下做出「正在處理」的感覺
        yield return new WaitForSeconds(1.6f);

        // 顯示「成功!」
        if (HintText != null)
            HintText.text = "異常捕捉成功!";

        //顯示照片
        //StartCoroutine(Act_ShowPhoto(Picture01));
        //yield return new WaitForSeconds(1);

        //更新數量
        TotalFoundChangedUI(spotManager.totalFound);

        //關卡畫面淡出
        yield return new WaitForSeconds(1f);
        animationScript.Fade(ErrorPanel, 2, 1, 0, null);
        animationScript.Fade(HintText.gameObject, 2, 1, 0, null);
        animationScript.Fade(PhotoFrameImage, 2, 1, 0, null);
        currentDef.spotRoot.SetActive(false);
        animationScript.Fade(currentDef.questionImage.gameObject, 2, 1, 0, null);
        yield return new WaitForSeconds(3f);
        //PicturePanel.gameObject.SetActive(false);
        dialogueSystemGame01Script.inputLocked = false; // 不鎖空白鍵
        

    }

    private IEnumerator HandleFailure()//失敗跳jumpscare
    {
        Debug.Log("[Second] Failure feedback:jumpscare");

        if (RedPanel != null)
        {
            RedPanel.SetActive(true);
            yield return new WaitForSeconds(0.05f);
            RedPanel.SetActive(false);
            yield return new WaitForSeconds(0.05f);
            RedPanel.SetActive(true);
            yield return new WaitForSeconds(0.05f);
            RedPanel.SetActive(false);
            yield return new WaitForSeconds(0.05f);
            RedPanel.SetActive(true);
        }

    }

    // =====================================================
    // 🧹 UI 清理
    // =====================================================

    private void CleanupUI()//關掉遊戲會用到的UI
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(false);

        if (RedPanel != null) RedPanel.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);
        if (BlackPanel != null) BlackPanel.SetActive(false);
        if (BlackPanel22 != null) BlackPanel22.SetActive(false);
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (RedPanel != null) RedPanel.SetActive(false);
        if (GameName != null) GameName.SetActive(false);
        if (Timetext != null) Timetext.SetActive(false);
        TimetextImage.SetActive(false);
    }

    private void CleanupRoundUI()//關掉遊戲會用到的面板
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        // ✅ 建議加這行：關掉本題 spot，避免殘留到下一回合
        if (currentDef != null && currentDef.spotRoot != null)
            currentDef.spotRoot.SetActive(false);
        if (currentDef != null && currentDef.questionImage != null)currentDef.questionImage.gameObject.SetActive(false);
        DisableBackgroundClick();  // ✅ 回合結束 → 背景不可點
        photoFrameFollowEnabled = false; // ✅ 順便把跟隨也關掉（很重要）
        if (RedPanel != null) RedPanel.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);
        countText.gameObject.SetActive(false);
    
        CountTextImage.SetActive(false);
        hasSelectedRound = false;
    }

    // =====================================================
    // 📷 拍照框控制（你專案原本就有的概念）
    // =====================================================

    private void EnablePhotoFrameFollow()//拍照框跟隨滑鼠
    {
        if (PhotoFrameImage != null)
        {
            PhotoFrameImage.SetActive(true);
            PhotoFrameImage.GetComponent<CanvasGroup>().alpha = 1;
        }
            

        photoFrameFollowEnabled = true;

        // ⚠️ 如果教學自動移動還在跑，先停掉
        if (photoFrameMoveRoutine != null)
        {
            StopCoroutine(photoFrameMoveRoutine);
            photoFrameMoveRoutine = null;
        }
        // TODO：如果你是用 Update 跟隨滑鼠，這裡只要開旗標
    }

    private void FollowPointer(RectTransform frame)//拍照框跟隨滑鼠
    {
        if (frame == null || UICanvas == null) return;

        Vector2 screenPos = Input.mousePosition;
        if (Input.touchCount > 0)
            screenPos = Input.GetTouch(0).position;

        RectTransform canvasRect = UICanvas.transform as RectTransform;
        Vector2 localPoint;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            UICanvas.worldCamera,
            out localPoint
        ))
        {
            frame.anchoredPosition = Vector2.Lerp(
                frame.anchoredPosition,
                localPoint,
                photoFrameFollowSpeed * Time.deltaTime
            );
        }
    }

    public IEnumerator WaitForClipEnd(Animator animator, string stateName, int layer = 0)//等動畫撥完
    {
        if (animator == null) yield break;

        // 等到 clip 出現
        while (true)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(layer);
            if (clips.Length > 0 && clips[0].clip != null && clips[0].clip.name == stateName)
                break;
            yield return null;
        }

        // 等播完（normalizedTime >= 1）
        while (animator.GetCurrentAnimatorStateInfo(layer).normalizedTime < 1f)
            yield return null;
    }

    private void ShowTeachHint()//教學顯示提示文字
    {
        if (HintText != null)
        {
            HintText.text = "盡快點擊異常!";
            HintText.gameObject.SetActive(true);
        }
    }

    private void HideTeachHint()//教學文字關掉
    {
        if (HintText != null)
            HintText.gameObject.SetActive(false);
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


    // =====================================================
    // 🎬 Second 專用劇情演出 Action
    // =====================================================

    // =====================================================
    // 燈光效果
    // =====================================================
    public IEnumerator Act_RedLight()// 紅光警告（失誤 / jumpscare 前導）
    {
        
        if (RedPanel == null) yield break;

        RedPanel.SetActive(true);
        yield return new WaitForSeconds(0.15f);
        RedPanel.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        RedPanel.SetActive(true);
        yield return new WaitForSeconds(0.2f);
        RedPanel.SetActive(false);
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_BusLightBright()//3.車頂燈光閃爍
    {
        
        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(0.5f);
    }
    public IEnumerator Act_LightFlickerOnce()
    {
        //車內燈瞬間閃爍

        //📝 註記
        //這是「廉價但有效」的恐怖感
        //非常符合你現在風格，不用上 shader

        if (BusUpLightTotal == null) yield break;

        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(0.05f);
        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.08f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(1.5f);
    }//3.車頂燈光閃爍
    public IEnumerator Act_LightOn()//global light開燈
    {
        Debug.Log($"[SceneFlow] Start id={GetInstanceID()}");
        //cameraMoveControllScript.cam.transform.position = StartPoint.position;
        // 1) 曝光淡入
        //if (!dialogueSystemGame00Script.skipRequested)
        //{
        yield return StartCoroutine(fader.FadeExposure(1.5f, -10f, 0.5f));
        yield return new WaitForSeconds(2f);
        //}
        //else
        //{
        //    // 直接把曝光設到「看完劇情後」一致
        //    fader.SetExposureImmediate(0.5f);
        //}
    }
    //public IEnumerator Act_LightBlack()//global light關燈
    //{
    //    // 1) 曝光淡入
    //    //if (!dialogueSystemGame00Script.skipRequested)
    //    //{
    //    fader.SetExposureImmediate(-10f);
    //    yield return new WaitForSeconds(2f);
    //    //}
    //    //else
    //    //{
    //    //    // 直接把曝光設到「看完劇情後」一致
    //    //    fader.SetExposureImmediate(0.5f);
    //    //}
    //}

    public IEnumerator Act_BuslightCloseOneByOne()// 公車內燈一顆一顆關掉
    {
        // 公車內燈一顆一顆關掉（不追求精準，只要氣氛）
        if (BusUpLightTotal == null) yield break;

        int childCount = BusUpLightTotal.transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform light = BusUpLightTotal.transform.GetChild(i);
            light.gameObject.SetActive(false);

            if (i == childCount - 1)
                yield return new WaitForSeconds(1.5f); // 最後一顆拉長
            else
                yield return new WaitForSeconds(1f);// 每顆燈之間的節奏
        }

        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_LightDimDown()//global light關燈
    {
        if (fader != null)
        {
            yield return StartCoroutine(fader.FadeExposure(1.5f, 0.5f, -10f));
            yield return new WaitForSeconds(2f);
        }

    }
    // =====================================================
    // 手機效果
    // =====================================================
    public IEnumerator Act_HangUpPhone()
    {
        //if (PhonePanel) PhonePanel.SetActive(false);

        StartCoroutine(PlayReverse("phone", 2f));//倒著播放動畫
        yield return new WaitForSeconds(2.5f);
        if (cControllScript.animator != null)
            cControllScript.animator.SetBool("phone", false);

        //cControllScript.animator.Play("phone", 0, 1f);   // 從最後一幀開始
        //cControllScript.animator.speed = -1f;          // 反向播放
        //yield return new WaitUntil(() => cControllScript.animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 0f);
        //cControllScript.animator.speed = 1f; // 記得還原


        yield return new WaitForSeconds(1f);
    }
    public IEnumerator Act_PickPhone()
    {
        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForClipEnd(cControllScript.animator, "phone");// ← 這裡填 clip 名

        //if (PhonePanel != null) PhonePanel.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        //if (PhonePanel != null) PhonePanel.SetActive(false);

        cControllScript.animator.SetBool("phone", false);
    }
    public IEnumerator Act_PickPhoneOn()
    {
        if (ring01) ring01.SetActive(false);
        if (ring02) ring02.SetActive(false);
        audioSettingsUI.StopLoopSFX();

        var a1 = ring01 ? ring01.GetComponent<Animator>() : null;
        var a2 = ring02 ? ring02.GetComponent<Animator>() : null;

        if (a1) a1.SetBool("ring", false);
        if (a2) a2.SetBool("ring", false);

        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForClipEnd(cControllScript.animator, "phone");

        yield return new WaitForSeconds(3);
        //if (PhonePanel != null) PhonePanel.SetActive(true);

        // 不關，交給後面劇情關（或你再做 PickPhoneOff）
        //cControllScript.animator.SetBool("phone", false);
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

    // =====================================================
    // 畫面控制效果
    // =====================================================
    public IEnumerator Act_BlackPanelOn()//玩家對話框會露出來的那個黑幕淡入
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_CameraBack()// 鏡頭拉遠
    {
        // 確保是 Physical Camera
        cam.usePhysicalProperties = true;

        float current = cam.focalLength;

        while (Mathf.Abs(current - targetFocal) > 0.01f)
        {
            current = Mathf.MoveTowards(
                current,
                targetFocal,
                speed * Time.deltaTime
            );

            cam.focalLength = current;
            yield return null;
        }

        cam.focalLength = targetFocal; // 保險收尾
        yield return new WaitForSeconds(1.5f); // 等一幀，確保位置同步
    }

    public IEnumerator Act_BlackPanelShutOff()//玩家對話框會露出來的那個黑幕瞬間淡入
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 0.1f, 0f, 1f, null);
        yield return new WaitForSeconds(0.5f);
    }
    public IEnumerator Act_BlackPanelOn2()//所有UI都看不到的黑幕淡入
    {
        if (BlackPanel22 == null || animationScript == null) yield break;

        BlackPanel22.SetActive(true);
        animationScript.Fade(BlackPanel22, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_BlackPanelOff()//玩家對話框會露出來的那個黑幕淡出
    {
        if (BlackPanel == null || animationScript == null) yield break;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);
    }

    // =====================================================
    // 公車物理效果
    // =====================================================
    public IEnumerator Act_BusShake(bool strong)
    {
        if (busVisualRoot == null) yield break;

        Vector3 originLocalPos = busVisualRoot.localPosition;
        Quaternion originLocalRot = busVisualRoot.localRotation;

        float duration = strong ? 0.9f : 0.5f;
        float posAmp = strong ? 0.03f : 0.015f;
        float rotAmp = strong ? 1.5f : 0.8f;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float offsetX = Random.Range(-rotAmp, rotAmp);
            float offsetY = Random.Range(-posAmp * 0.5f, posAmp * 0.5f);
            float rotZ = Random.Range(-posAmp, posAmp);

            busVisualRoot.localPosition = originLocalPos + new Vector3(offsetX, offsetY, 0f);
            busVisualRoot.localRotation = originLocalRot * Quaternion.Euler(0f, 0f, rotZ);

            yield return null;
        }

        busVisualRoot.localPosition = originLocalPos;
        busVisualRoot.localRotation = originLocalRot;

        yield return new WaitForSeconds(1.5f);
    }

    // =====================================================
    // 場景控制
    // =====================================================
    public IEnumerator Act_FailScene()// 失敗轉場（淡黑 → 切場）
    {
        
        if (BlackPanel == null || animationScript == null || sceneChangeScript == null)
            yield break;

        BlackPanel.SetActive(true);

        animationScript.Fade(
            BlackPanel,
            1f,
            0f,
            1f,
            () => LoadingManager.Instance.BeginLoad("fail")
        );

        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_SuccessScene()// 通關轉場（淡黑 → 切場）
    {

        if (BlackPanel == null || animationScript == null || sceneChangeScript == null)
            yield break;

        BlackPanel.SetActive(true);

        animationScript.Fade(
            BlackPanel,
            1f,
            0f,
            1f,
            () => LoadingManager.Instance.BeginLoad("success")
        );

        yield return new WaitForSeconds(1.5f);
    }

    // =====================================================
    // 人物相關效果(包含物理和動畫)
    // =====================================================
    public IEnumerator Act_LeftRight()//玩家往左右看
    {
        //玩家裝有animator那個物件切換sprite到leftidle，然後用flip.x=true去做往右看的樣子，左右左右左右
        //📝 註記
        //這一段是你之前「用 sprite +flipX 偽動畫」的做法
        //如果你之後改成純 Animator，這支可以直接刪

        if (cControllScript == null || cControllScript.spriteRenderer == null)
            yield break;

        var sr = cControllScript.spriteRenderer;
        var anim = cControllScript.animator;

        // 暫停 Animator，避免被蓋掉
        if (anim != null) anim.enabled = false;

        sr.sprite = cControllScript.leftidle;
        sr.flipX = false;
        yield return new WaitForSeconds(1f);

        sr.flipX = true;
        yield return new WaitForSeconds(1f);

        sr.flipX = false;
        yield return new WaitForSeconds(1);

        sr.sprite = cControllScript.idle;

        // 還原 Animator
        if (anim != null) anim.enabled = true;
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_idle()
    {
        // 還原 Animator
        if (cControllScript.animator != null) cControllScript.animator.enabled = true;
        //所有 bool → 預設值
        //所有 trigger → 清空
        //回到 Default State
        //連 IK / Root Motion 都一起重置
        cControllScript.animator.Rebind();
        cControllScript.animator.Update(0f);
        yield return new WaitForSeconds(1);
    }
    public IEnumerator Act_PlayerToDie()//玩家死亡動畫
    {
        if (cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("die", true);
        yield return new WaitForSeconds(2.5f);//動畫兩秒
    }

    // =====================================================
    // 其他細節效果
    // =====================================================
    public IEnumerator Act_SetTimeText(string time)
    {
        //指定ShowTimeText為19:30
        Timetext.gameObject.SetActive(true);
        TimetextImage.SetActive(true);
        Timetext.GetComponent<TextMeshProUGUI>().text = time;
        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_WaitForSecondsCoroutine(float sec)
    {
        float startTime = Time.time;
        Debug.Log($"[WaitTest] 開始等待，時間：{startTime}");

        yield return new WaitForSeconds(sec);

        float endTime = Time.time;
        Debug.Log($"[WaitTest] 等待結束，時間：{endTime}，實際經過：{endTime - startTime} 秒");
    }


    //public IEnumerator Act_BigPictureZoom()
    //{
    //    //放大圖片某處，放大位置我會放一個transform，朝著那裏放大就可以了
    //    //📝 註記
    //    //這是「世界座標放大」，非常適合你現在的公車＋場景構圖
    //    //你之後要改成 UI 放大，只要換這支

    //    if (mainCamera == null || BigPictureZoomTarget == null)
    //        yield break;

    //    Vector3 camStartPos = mainCamera.transform.position;
    //    Vector3 dir = (BigPictureZoomTarget.position - camStartPos).normalized;
    //    Vector3 zoomPos = camStartPos + dir * zoomDistance;

    //    float t = 0f;

    //    // Zoom in
    //    while (t < zoomDuration)
    //    {
    //        t += Time.deltaTime;
    //        mainCamera.transform.position =
    //            Vector3.Lerp(camStartPos, zoomPos, t / zoomDuration);
    //        yield return null;
    //    }

    //    yield return new WaitForSeconds(zoomHoldTime);

    //    t = 0f;
    //    // Zoom out
    //    while (t < zoomDuration)
    //    {
    //        t += Time.deltaTime;
    //        mainCamera.transform.position =
    //            Vector3.Lerp(zoomPos, camStartPos, t / zoomDuration);
    //        yield return null;
    //    }

    //    mainCamera.transform.position = camStartPos;
    //    yield return new WaitForSeconds(1.5f);
    //}

    //public IEnumerator Act_ShowGameTitle()
    //{
    //    GameName.SetActive(true);
    //    yield return new WaitForSeconds(1.5f);
    //}

    //public IEnumerator Act_WalkToFront()
    //{
    //    if (cControllScript == null || WalkToFrontPos == null) yield break;

    //    cControllScript.StartAutoMoveTo(WalkToFrontPos.position);

    //    yield return new WaitUntil(() => cControllScript.autoMoveFinished);
    //    yield return new WaitForSeconds(1f);
    //}
}
