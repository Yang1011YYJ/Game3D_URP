using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;


public class Second : MonoBehaviour
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

    [Header("遊戲用UI")]
    public GameObject ErrorPanel;
    public GameObject RedPanel;
    public GameObject PhotoFrameImage;
    public TextMeshProUGUI HintText;
    public GameObject PicturePanel;
    public GameObject Picture01;
    public GameObject Picture02;
    [Tooltip("Round UI")]
    public TextMeshProUGUI countText; // 顯示「找到 X / >5」
    [Header("Background Click")]
    public GameObject BackgroundButton; // 透明全螢幕 Button 的 GameObject


    [Header("題庫")]
    public List<SpotDef> spotPool = new();          // Inspector 拖 8 題
    private List<SpotDef> bag = new();              // 抽題用袋子（不放回）

    [Header("ErrorPanel 圖片顯示")]
    public Image errorPictureImage;                 // ErrorPanel 上用來顯示題目的 Image（Inspector拖）


    [Header("其他UI")]
    public GameObject PhonePanel;
    [Tooltip("玩家對話框還看的到")] public GameObject BlackPanel;
    [Tooltip("玩家對話框看不到")] public GameObject BlackPanel22;
    [Tooltip("遊戲名字")] public GameObject GameName;
    [Tooltip("遊戲內劇情用的時間")] public GameObject Timetext;

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

    [Header("相機相關")]
    [Tooltip("Focal length")] public float targetFocal;
    [Tooltip("移動速度")] public float speed;

    [Header("其他")]
    public Camera cam;

    private void Awake()
    {
        if (spotManager == null)
            spotManager = FindAnyObjectByType<SpotManager>();

        if (timer == null)
            timer = FindAnyObjectByType<TimeControll>();

        dialogueSystemGame01Script = FindAnyObjectByType<DialogueSystemGame01>();
        fader = FindAnyObjectByType<FadeInByExposure>();
        cControllScript = FindAnyObjectByType<CControll>();
        animationScript = FindAnyObjectByType<AnimationScript>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();

        if (dialogueSystemGame01Script != null)
            dialogueSystemGame01Script.BindOwner(this);
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
        CleanupUI();//關掉遊戲用UI
        CleanupRoundUI();

        DisableAllSpots();   // ✅ 遊戲一開始，全部 spot 關掉
        DisableBackgroundClick(); // ✅ 一開始先關

        _prevLives = (spotManager != null) ? spotManager.livesLeft : 2;

        //更新數量
        TotalFoundChangedUI(spotManager.totalFound);
    }

    private void Update()
    {
        if (photoFrameFollowEnabled)
            FollowPointer(PhotoFrameRect);
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

        //ErrorPanel.SetActive(true);
        //animationScript.Fade(ErrorPanel, 1, 0, 1, null);
        PhotoFrameImage.gameObject.SetActive(true);
        PhonePanel.gameObject.SetActive(false);

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
        spotManager.OnBackgroundClicked();
    }

    private IEnumerator StartNormalRound()//開始正式遊戲
    {
        currentDef.spotRoot.GetComponent<Button>().interactable = false;
        Debug.Log("[Second] Normal round start");
        _gameStarted = true;
        _prevLives = spotManager.livesLeft;  // ✅ 這回合開始先記住 lives

        RedPanel.gameObject.SetActive(false);
        HintText.text = "";
        OpenErrorPanel();     // ✅ 保證 ErrorPanel 會亮
        animationScript.Fade(ErrorPanel,1, 0, 1, null);
        yield return new WaitForSeconds(1.5f);
        currentDef.spotRoot.GetComponent<Button>().interactable = true;
        // ✅ 保證拍照框會出現且跟隨
        EnablePhotoFrameFollow();
        EnableBackgroundClick();
        HideTeachHint();

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

        // 4) 開啟這一題的 spot
        if (currentDef.spotRoot != null)
            currentDef.spotRoot.SetActive(true);

        // 5) 把題目圖片換到 ErrorPanel 上
        if (currentDef.questionImage != null)
            currentDef.questionImage.gameObject.SetActive(true);

        // 6) （可選）清理 UI 或提示文字
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = "在時間內找出異常！";
        }
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

    public void Btn_SelectAndStart()
    {
        StartCoroutine(SelectAndStartRoutine());
    }

    private IEnumerator SelectAndStartRoutine()
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
        if (livesLeft <= 0) return;

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
        // 0) 保險：停止互動、停止倒數
        spotManager.PauseRound();
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd();
        }

        // 1️⃣ 播成功 / 失敗演出
        if (endType == RoundEndType.FoundSpot)
        {
            yield return StartCoroutine(HandleSuccess());
            CleanupRoundUI();

            // 1️⃣ 先確保畫面回到遊戲場景（淡出黑幕）
            yield return StartCoroutine(Act_BlackPanelOff());

            // ⭐ 回合間的呼吸時間（你要的 5 秒）
            yield return new WaitForSeconds(5f);

            CheckFinalResultOrContinue();
            yield break;
        }
        // ✅ FailedRound：跑「死亡演出」再跳 fail
        yield return StartCoroutine(DeathEndFlow());
        yield break;
    }
    private IEnumerator DeathEndFlow()//遊戲失敗
    {
        // 0) 保險：停止互動、停止倒數
        spotManager.PauseRound();
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd();
        }

        

        // 2) 提示文字
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"失敗！剩餘機會：{spotManager.livesLeft}";
        }
        // 1) jumpscare（紅閃）
        yield return FlashJumpScare();
        yield return new WaitForSeconds(1.5f);

        // 2) 關掉找異常畫面，回到「正常車內」畫面
        CleanupRoundUI();
        photoFrameFollowEnabled = false;

        // 你如果想留一句提示也可以（可選）
        //if (HintText != null)
        //{
        //    HintText.gameObject.SetActive(true);
        //    HintText.text = "……你被抓到了。";
        //}

        // 3) 讓玩家「看到回到車內」一下（很重要，恐怖片節奏）
        yield return new WaitForSeconds(2f);

        // 4) 淡出 → 切 fail
        StartCoroutine(GoToLoseScene());
    }

    private void CheckFinalResultOrContinue()//判斷是通關還是失敗
    {
        if (spotManager.IsWin())
        {
            Debug.Log("[Second] GAME WIN → play ending story");
            currentPhase = GamePhase.End;

            _pendingEnding = EndingIntent.Success;

            // 播放通關劇情
            dialogueSystemGame01Script.keepTalk = true;
            dialogueSystemGame01Script.nextDialogue = dialogueSystemGame01Script.TextfileGame04;
            return;
        }

        if (spotManager.IsGameEnded())
        {
            Debug.Log("[Second] GAME OVER → play fail story");
            currentPhase = GamePhase.End;

            _pendingEnding = EndingIntent.Fail;

            // 播放失敗劇情
            dialogueSystemGame01Script.keepTalk = true;
            dialogueSystemGame01Script.nextDialogue = dialogueSystemGame01Script.TextfileGame03;
            return;
        }

        // 還沒結束 → 等劇情再呼叫下一次 GameStart
        Debug.Log("[Second] Round finished, wait for next story");
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
        countText.text = $"{totalFound} / {spotManager.totalRounds}";
    }

    private void HandleRoundBegan(int roundIndex)
    {
        // 每回合開始：開 timer
        timer.onTimeUp = () => spotManager.OnTimeout();
        timer.StartCountdown(roundSeconds);
    }

    private IEnumerator LifeLostFlow(int livesLeft)//生命-1
    {
        Debug.Log("生命-1");
        // ✅ 先暫停，避免玩家在 jumpscare 期間又扣第二次
        spotManager.PauseRound();

        

        // 2) 提示文字
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"失敗！剩餘機會：{livesLeft}";
        }
        // 1) jumpscare
        yield return StartCoroutine(FlashJumpScare());
        yield return new WaitForSeconds(1.5f);

        // 3) 如果還有命 → 重計時繼續同一回合
        if (livesLeft > 0)
        {
            RedPanel.gameObject.SetActive(false);
            HintText.gameObject.SetActive(false);

            timer.ForceEnd();
            timer.StartCountdown(roundSeconds);

            yield return new WaitForSeconds(0.2f);
            if (HintText != null) HintText.gameObject.SetActive(false);

            spotManager.ResumeRound();
            yield break;
        }

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


        yield return new WaitForSeconds(1f);
        //animationScript.Fade(ErrorPanel, 2, 1, 0, null);
        currentDef.spotRoot.SetActive(false);
        animationScript.Fade(currentDef.questionImage.gameObject, 2, 1, 0, null);
        yield return new WaitForSeconds(3f);
        //PicturePanel.gameObject.SetActive(false);
        dialogueSystemGame01Script.inputLocked = false; // 不鎖空白鍵
        

    }

    private IEnumerator FlashJumpScare()//點錯位置跳jumpscare
    {
        Debug.Log("[Second] Failure feedback:jumpscare");

        if (RedPanel != null)
        {
            RedPanel.SetActive(true);
            yield return new WaitForSeconds(1f);
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
        if (PicturePanel != null) PicturePanel.SetActive(false);
        if (GameName != null) GameName.SetActive(false);
        if (Timetext != null) Timetext.SetActive(false);
    }

    private void CleanupRoundUI()//關掉遊戲會用到的面板
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        // ✅ 建議加這行：關掉本題 spot，避免殘留到下一回合
        if (currentDef != null && currentDef.spotRoot != null)
            currentDef.spotRoot.SetActive(false);
        if (currentDef != null && currentDef.questionImage != null)
            currentDef.questionImage.gameObject.SetActive(false);
        DisableBackgroundClick();  // ✅ 回合結束 → 背景不可點
        photoFrameFollowEnabled = false; // ✅ 順便把跟隨也關掉（很重要）
        if (RedPanel != null) RedPanel.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);
        countText.gameObject.SetActive(false);
        hasSelectedRound = false;
    }

    // =====================================================
    // 📷 拍照框控制（你專案原本就有的概念）
    // =====================================================

    private void EnablePhotoFrameFollow()//拍照框跟隨滑鼠
    {
        if (PhotoFrameImage != null)
            PhotoFrameImage.SetActive(true);

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
                yield return new WaitForSeconds(0.8f); // 最後一顆拉長
            else
                yield return new WaitForSeconds(0.3f);// 每顆燈之間的節奏
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
        if (PhonePanel) PhonePanel.SetActive(false);

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

        if (PhonePanel != null) PhonePanel.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        if (PhonePanel != null) PhonePanel.SetActive(false);

        cControllScript.animator.SetBool("phone", false);
    }
    public IEnumerator Act_PickPhoneOn()
    {
        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForClipEnd(cControllScript.animator, "phone");
        if (PhonePanel != null) PhonePanel.SetActive(true);

        // 不關，交給後面劇情關（或你再做 PickPhoneOff）
        //cControllScript.animator.SetBool("phone", false);
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
    public IEnumerator Act_BusShakeWithDamping(bool strong)
    {
        if (busRb == null) yield break;

        Vector3 originPos = busRb.transform.position;
        Quaternion originRot = busRb.transform.rotation;

        float duration = strong ? 1.4f : 0.9f;

        float startPosAmp = strong ? 0.08f : 0.04f;
        float startRotAmp = strong ? 6f : 3f;

        float frequency = strong ? 18f : 12f;
        float damping = strong ? 3.2f : 4.5f;

        float t = 0f;

        while (t < duration)
        {
            t += Time.fixedDeltaTime;

            float normalized = t / duration;
            float decay = Mathf.Exp(-damping * normalized);
            float shake = Mathf.Sin(t * frequency) * decay;

            // ✅ 3D 位移：只晃 X/Y，不動 Z
            Vector3 offset = new Vector3(
                shake * startPosAmp,
                shake * startPosAmp * 0.4f,
                0f
            );

            // ✅ 3D 旋轉：用 Quaternion 疊加角度（這裡繞 Z 軸晃，像車身左右晃）
            float rotZ = shake * startRotAmp;
            Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

            busRb.MovePosition(originPos + offset);
            busRb.MoveRotation(originRot * rot);

            yield return new WaitForFixedUpdate();
        }

        // 回正
        busRb.MovePosition(originPos);
        busRb.MoveRotation(originRot);
    }
    public IEnumerator Act_BusShake(bool strong)
    {
        if (busRb == null) yield break;

        Vector3 originPos = busRb.position;
        Quaternion originRot = busRb.rotation;

        float duration = strong ? 0.9f : 0.5f;
        float posAmp = strong ? 0.06f : 0.03f;
        float rotAmp = strong ? 4f : 2f;

        float t = 0f;

        while (t < duration)
        {
            t += Time.fixedDeltaTime;

            float offsetX = Random.Range(-posAmp, posAmp);
            float offsetY = Random.Range(-posAmp * 0.5f, posAmp * 0.5f);
            float rotZ = Random.Range(-rotAmp, rotAmp);

            busRb.MovePosition(originPos + new Vector3(offsetX, offsetY, 0f));
            busRb.MoveRotation(originRot * Quaternion.Euler(0f, 0f, rotZ));

            yield return new WaitForFixedUpdate();
        }

        busRb.MovePosition(originPos);
        busRb.MoveRotation(originRot);
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
            () => sceneChangeScript.SceneC("fail")
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
            () => sceneChangeScript.SceneC("success")
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
        Timetext.GetComponent<TextMeshProUGUI>().text = time;
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_ShowPhoto(GameObject target)
    {
        //顯示圖片
        //📝 註記
        //這完全符合你現在「用編號 / 名字對應圖片」的做法
        //未來你要改成 ScriptableObject 也不衝突


        Debug.Log("showphoto");
        // 先全關
        foreach (Transform child in PicturePanel.transform)
            child.gameObject.SetActive(false);
        PicturePanel.SetActive(true);

        // 再開指定那張
        if (target != null)
            target.gameObject.SetActive(true);
        else
            Debug.LogWarning($"[Act_ShowPhoto] 找不到圖片：{target.ToString()}");
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_photoclose()
    {
        //PicturePanel 底下是 多張 Image，名字 = pictureName

        //📝 註記
        //這完全符合你現在「用編號 / 名字對應圖片」的做法
        //未來你要改成 ScriptableObject 也不衝突


        Debug.Log("showphoto");
        // 先全關
        foreach (Transform child in PicturePanel.transform)
            child.gameObject.SetActive(false);
        //Picture02.SetActive(false);
        PicturePanel.SetActive(false);
        yield return null;
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
