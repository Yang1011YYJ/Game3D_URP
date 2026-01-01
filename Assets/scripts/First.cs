using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public enum GamePhase
{
    Teaching,
    Playing,
    End
}

public class First : MonoBehaviour
{
    [Header("Phase")]
    public GamePhase currentPhase = GamePhase.Teaching;

    [Header("腳本")]
    public SpotManager spotManager;
    public TimeControll timer;
    public DialogueSystemGame00 dialogueSystemGame00Script;
    public FadeInByExposure fader;
    public CControll cControllScript;
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;

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


    [Header("其他UI")]
    public GameObject PhonePanel;
    [Tooltip("玩家對話框還看的到")]public GameObject BlackPanel;
    [Tooltip("玩家對話框看不到")] public GameObject BlackPanel22;
    [Tooltip("遊戲名字")] public VideoPlayer GameNamevideoPlayer;
    [Tooltip("遊戲名字Rawimage")] public RawImage GameNameRawImage;
    [Tooltip("遊戲內劇情用的時間")] public GameObject Timetext;

    [Header("設定")]
    [Tooltip("設定面板")] public GameObject SettingPanel;
    [Tooltip("快速劇情")] public GameObject FastRevealButton;

    [Header("Big Picture Zoom放大圖片功能")]
    public Transform BigPictureZoomTarget;
    public Camera mainCamera;
    public float zoomDistance = 2.5f;
    public float zoomDuration = 0.8f;
    public float zoomHoldTime = 2f;


    [Header("Photo Frame Follow")]
    [Tooltip("拍照框本體")] public RectTransform PhotoFrameRect;     // 拍照框本體
    public Canvas UICanvas;                  // UI Canvas
    [Tooltip("拍照框跟隨速度")] public float photoFrameFollowSpeed = 18f;
    [Tooltip("拍照框自動移動速度")] public float photoFrameAutoMoveSpeed = 900f;
    // 教學用目標（UI 或世界座標都可以）
    [Tooltip("教學用目標")]public Transform PhotoFrameTeachTarget;
    [Tooltip("第一次教學用目標")] public Transform PhotoFrameTeachTarget01;
    [Tooltip("第二次教學用目標")] public Transform PhotoFrameTeachTarget02;

    // 內部旗標
    [Tooltip("是否開啟拍照框跟隨滑鼠")] private bool photoFrameFollowEnabled = false;
    private Coroutine photoFrameMoveRoutine;

    [Header("燈光")]
    public GameObject BusUpLightTotal;

    [Header("玩家相關")]
    public GameObject Player;
    public GameObject PlayerWithAni;
    [Tooltip("跑到前面去看司機")]public Transform WalkToFrontPos;
    [Tooltip("為了不要被遮住移動一下")] public Transform WalkToPos2;
    public Transform PlayerStartPos;

    [Header("車子相關")]
    [Tooltip("車子本體")] public Rigidbody busRb;
    [Header("Bus Shake (Visual Only)")]public Transform busVisualRoot; // ✅只放視覺公車，不含地板碰撞


    [Header("Game Settings")]
    public int roundSeconds = 15;
    // ===== Round gate: 讓 Act_GameStart 等到回合結束才放行 =====
    private bool _roundFinished = false;
    private bool _roundFlowDone = false;
    private RoundEndType _lastEndType;

    [Header("教學相關")]
    public bool _teachingRunning = false;
    public bool _teachingDone = false;
    public bool inTeach01 = false;


    private void Awake()
    {
        if (spotManager == null)
            spotManager = FindAnyObjectByType<SpotManager>();

        if (timer == null)
            timer = FindAnyObjectByType<TimeControll>();

        dialogueSystemGame00Script = FindAnyObjectByType<DialogueSystemGame00>();
        fader = FindAnyObjectByType<FadeInByExposure>();
        cControllScript = FindAnyObjectByType<CControll>();
        animationScript = FindAnyObjectByType<AnimationScript>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();

        if (dialogueSystemGame00Script != null)
            dialogueSystemGame00Script.BindOwner(this);

        CleanupUI();//關掉所有UI
        //教學UI關掉
        CleanupRoundUI();
        PhotoFrameTeachTarget01.gameObject.SetActive(false);
        PhotoFrameTeachTarget02.gameObject.SetActive(false);
        SettingPanel.SetActive(false );
        inTeach01 = false;
        GameNamevideoPlayer.loopPointReached += OnVideoEnd;
        Player.transform.position = PlayerStartPos.position;

        //更新數量
        TotalFoundChangedUI(spotManager.totalFound);
    }

    private void OnEnable()
    {
        if (spotManager != null)
            spotManager.OnRoundEnded += HandleRoundEnded;
    }

    private void OnDisable()
    {
        if (spotManager != null)
            spotManager.OnRoundEnded -= HandleRoundEnded;
    }

    private void Start()
    {
        

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
    }
    //======================================================
    //設定相關細節
    //======================================================
    public void FastOK()//快速通過劇情
    {
        dialogueSystemGame00Script.allowFastReveal = !dialogueSystemGame00Script.allowFastReveal;
        Debug.Log(dialogueSystemGame00Script.allowFastReveal.ToString());
    }


    // =====================================================
    // 🎬 劇情呼叫入口
    // =====================================================

    /// <summary>
    /// 由對話 / 劇情呼叫（gamestart）
    /// </summary>
    public IEnumerator Act_GameStart()//開始遊戲(狀態辨識)
    {
        // 每次開始一回合前，重置閘門
        _roundFinished = false;
        _roundFlowDone = false;

        ErrorPanel.SetActive(true);
        animationScript.Fade(ErrorPanel, 1, 0, 1, null);
        yield return new WaitForSeconds(1.5f);
        PhotoFrameImage.gameObject.SetActive(true);
        PhotoFrameTeachTarget.gameObject.SetActive(true);
        PhonePanel.gameObject.SetActive(false);

        // ✅（重點）鎖對話系統輸入，避免空白鍵亂跳
        dialogueSystemGame00Script.inputLocked = true; // 開始

        if (currentPhase == GamePhase.Teaching)
        {
            _teachingRunning = true;
            _teachingDone = false;
            dialogueSystemGame00Script.inputLocked = true;  // 教學開始


            yield return StartCoroutine(StartTeachingRound());

            // ✅（重點）等玩家真的完成教學
            yield return new WaitUntil(() => _teachingDone);

            _teachingRunning = false;

            // ✅ 解鎖對話系統
            dialogueSystemGame00Script.inputLocked = false; // 教學結束

            yield break;
        }
    }

    /// <summary>
    /// 由對話 / 劇情呼叫（teachstart）
    /// </summary>
    public void TeachStart()//教學狀態切換
    {
        currentPhase = GamePhase.Teaching;
    }
    // 入口1：點到 spot
    public void OnSpotClicked()
    {
        if (currentPhase == GamePhase.Teaching)
        {
            StartCoroutine( HandleSuccess());
            return;
        }

        if (currentPhase == GamePhase.Playing)
        {
            spotManager.OnPlaySpotClick();
            return;
        }

    }
    // =====================================================
    // 🎓 教學流程
    // =====================================================

    private IEnumerator StartTeachingRound()//開始教學回合
    {
        Debug.Log("[First] Teaching round start");
        yield return null;

        DisablePhotoFrameFollow();
        yield return new WaitUntil(() => ErrorPanel.GetComponent<CanvasGroup>().alpha == 1);
        ShowTeachHint();
        yield return new WaitForSeconds(1);
        MovePhotoFrameToTeachTarget();


        // ❌ 教學不開 timer、不記分
    }
    private Coroutine teachRoutine;

    public void Act_RequestTeach1()
    {
        currentPhase = GamePhase.Teaching;   // ✅ 教學正式啟動
        _teachingRunning = true;
        _teachingDone = false;
        inTeach01 = true;
        if (teachRoutine != null) return;
        teachRoutine = StartCoroutine(Teach1Routine()); // ← 你自己的教學流程
    }

    public void Act_RequestTeach2()
    {
        currentPhase = GamePhase.Teaching;   // ✅ 教學正式啟動
        _teachingRunning = true;
        _teachingDone = false;
        inTeach01 = false;
        if (teachRoutine != null) return;
        teachRoutine = StartCoroutine(Teach2Routine());
    }

    private IEnumerator Teach1Routine()
    {
        // TODO: 教學1內容
        PhotoFrameTeachTarget = PhotoFrameTeachTarget01;
        PicturePanel.SetActive(true);
        Picture01.SetActive(true);
        yield return null;
        teachRoutine = null;
    }

    private IEnumerator Teach2Routine()
    {
        // TODO: 教學2內容
        PhotoFrameTeachTarget = PhotoFrameTeachTarget02;

        PhotoFrameTeachTarget01.gameObject.SetActive(false);
        PhotoFrameTeachTarget02.gameObject.SetActive(true);
        HintText.gameObject.GetComponent<CanvasGroup>().alpha = 1;
        PhotoFrameImage.GetComponent<CanvasGroup>().alpha = 1;
        yield return null;
        teachRoutine = null;
    }
    private void EndTeaching()
    {
        PhotoFrameTeachTarget01.gameObject.SetActive(false);
        PhotoFrameTeachTarget02.gameObject.SetActive(false);
        Picture01.gameObject.SetActive(false);
        Picture02.gameObject.SetActive(false);

        //EnablePhotoFrameFollow();
        //HideTeachHint();

        currentPhase = GamePhase.Playing;
    }


    // =====================================================
    // 🎮 正式遊戲流程
    // =====================================================


    // =====================================================
    // 📡 SpotManager 事件回調
    // =====================================================

    private void HandleRoundEnded(int roundIndex, int totalFound, RoundEndType endType)//計算當前回合數
    {
        Debug.Log($"[First] Round {roundIndex} end: {endType}");

        // 停 timer
        if (timer != null)
            timer.onTimeUp = null;

        // 記錄回合結果（讓 Act_GameStart 知道回合已經結束）
        _roundFinished = true;
        _lastEndType = endType;

        // 跑回合結束演出，演出跑完才真正「放行」
        StartCoroutine(RoundEndFlow_AndMarkDone(endType));
    }

    private IEnumerator RoundEndFlow_AndMarkDone(RoundEndType endType)
    {
        _roundFlowDone = false;

        yield return StartCoroutine(RoundEndFlow(endType));

        _roundFlowDone = true;
    }

    private IEnumerator RoundEndFlow(RoundEndType endType)//回合結束的流程
    {
        // 1️⃣ 播成功 / 失敗演出
        if (endType == RoundEndType.FoundSpot)
        {
            yield return StartCoroutine(HandleSuccess());
        }
        else
        {
            yield return StartCoroutine(HandleFailure());
        }

        // 2️⃣ 回合 UI 清乾淨（畫面回到正常狀態）
        CleanupRoundUI();

        // 3️⃣ 等一幀，確保畫面穩定
        yield return null;

        // 4️⃣ 現在才判斷最終結局
        CheckFinalResultOrContinue();
    }

    private void CheckFinalResultOrContinue()//判斷是通關還是失敗
    {
        if (spotManager.IsWin())
        {
            Debug.Log("[First] GAME WIN");
            currentPhase = GamePhase.End;

            StartCoroutine(GoToWinScene());
            return;
        }

        if (spotManager.IsGameEnded())
        {
            Debug.Log("[First] GAME OVER");
            currentPhase = GamePhase.End;

            StartCoroutine(GoToLoseScene());
            return;
        }

        // 還沒結束 → 等劇情再呼叫下一次 GameStart
        Debug.Log("[First] Round finished, wait for story");
    }

    private IEnumerator GoToWinScene()
    {
        // 可選：淡出、關燈、音效
        yield return new WaitForSeconds(0.5f);

        animationScript.Fade(
            BlackPanel,
            1f,
            0f,
            1f,
            () => sceneChangeScript.SceneC("success")
        );
    }

    private IEnumerator GoToLoseScene()
    {
        yield return new WaitForSeconds(0.5f);

        animationScript.Fade(
            BlackPanel,
            1f,
            0f,
            1f,
            () => sceneChangeScript.SceneC("fail")
        );
    }
    private void TotalFoundChangedUI(int totalFound)//更新UI
    {
        if (countText == null || spotManager == null) return;

        
        countText.text = $"{totalFound} / {spotManager.totalRounds}";
    }


    // =====================================================
    // 🎉 成功 / 失敗演出
    // =====================================================

    private IEnumerator HandleSuccess()//點到正確位置
    {
        Debug.Log("[First] Success feedback");

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
        if (inTeach01)
        {
            StartCoroutine(Act_ShowPhoto(Picture01));
            yield return new WaitForSeconds(1);
        }

        //更新數量
        TotalFoundChangedUI(spotManager.totalFound);

        
        yield return new WaitForSeconds(1f);
        Picture02.gameObject.SetActive(false);
        Picture01.gameObject.SetActive(false);
        animationScript.Fade(ErrorPanel, 2, 1, 0, null);
        animationScript.Fade(HintText.gameObject, 2, 1, 0, null);
        animationScript.Fade(PhotoFrameImage, 2, 1, 0, null);
        PicturePanel.gameObject.SetActive(false);

        dialogueSystemGame00Script.inputLocked = false; // 教學結束
        yield return new WaitForSeconds(3f);
        CleanupRoundUI();
        
        // ✅如果是在教學，就在這裡結束教學並放行劇情
        if (currentPhase == GamePhase.Teaching)
        {
            EndTeaching();          // 你已經有寫：切換到 Playing、開跟隨、關提示
            _teachingDone = true;   // ✅放行 Act_GameStart
        }
    }

    private IEnumerator HandleFailure()//點錯位置
    {
        Debug.Log("[First] Failure feedback");

        if (RedPanel != null)
        {
            RedPanel.SetActive(true);
            yield return new WaitForSeconds(1f);
            RedPanel.SetActive(false);
        }

        //CleanupRoundUI();
    }

    // =====================================================
    // 🧹 UI 清理
    // =====================================================

    private void CleanupUI()//關掉所有UI
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
        if (GameNameRawImage != null) GameNameRawImage.gameObject.SetActive(false);
        if (Timetext != null) Timetext.SetActive(false);
        Picture02.SetActive(false);
        Picture01.SetActive(false);
    }

    private void CleanupRoundUI()//關掉遊戲會用到的UI
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (RedPanel != null) RedPanel.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);
        countText.gameObject.SetActive(false);
        PhotoFrameTeachTarget01.gameObject .SetActive(false);
        PhotoFrameTeachTarget02.gameObject .SetActive(false);
    }

    // =====================================================
    // 📷 拍照框控制（你專案原本就有的概念）
    // =====================================================

    private void DisablePhotoFrameFollow()//拍照框不能跟隨滑鼠
    {
        photoFrameFollowEnabled = false;

        // ❗ 不要 SetActive(false)
        // 教學流程還會用到同一個框
        // TODO：關閉滑鼠跟隨（不要 Destroy）
    }

    private void MovePhotoFrameToTeachTarget()//拍照框自動移動
    {
        if (PhotoFrameRect == null || PhotoFrameTeachTarget == null || UICanvas == null)
            return;

        // 教學時一定關掉滑鼠跟隨
        photoFrameFollowEnabled = false;

        // 停掉可能殘留的移動
        if (photoFrameMoveRoutine != null)
            StopCoroutine(photoFrameMoveRoutine);

        PhotoFrameTeachTarget.gameObject.SetActive(true);
        photoFrameMoveRoutine = StartCoroutine(
            MoveFrameToTarget(PhotoFrameRect, PhotoFrameTeachTarget, photoFrameAutoMoveSpeed)
        );

        // TODO：把拍照框移到教學指定 UI 位置
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

    private IEnumerator MoveFrameToTarget(RectTransform frame, Transform target, float speed)//拍照框自動移動細節
    {
        while (Vector3.Distance(frame.position, target.position) > 0.5f)
        {
            frame.position = Vector3.MoveTowards(frame.position, target.position, speed * Time.deltaTime);
            yield return null;
        }
        frame.position = target.position;
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



    //===========================================
    //Action動作
    //===========================================





    //===========================================
    //光線控制
    //===========================================
    public IEnumerator Act_BusLightBright()
    {
        //3.車頂燈光閃爍
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

        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_LightOn()
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
    public IEnumerator Act_LightBlack()
    {
        fader.SetExposureImmediate(-10f);
        Debug.Log($"[LightBlack] after set: {fader.GetExposure()} frame={Time.frameCount}");

        yield return null; // 關鍵：等一幀，看看有沒有被改回去

        Debug.Log($"[LightBlack] next frame: {fader.GetExposure()} frame={Time.frameCount}");
        yield return new WaitForSeconds(2f);
    }
    public IEnumerator Act_LightDimDown()
    {
        if (fader != null)
        {
            yield return StartCoroutine(fader.FadeExposure(1.5f, 0.5f, -10f));
            yield return new WaitForSeconds(2f);
        }

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
    }

    //============================================
    //玩家相關物理和動畫
    //============================================
    public IEnumerator Act_WalkToFront()
    {
        if (cControllScript == null || WalkToFrontPos == null) yield break;

        cControllScript.StartAutoMoveTo(WalkToFrontPos.position);

        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        yield return new WaitForSeconds(1f);
    }
    public IEnumerator Act_WalkToPos2()
    {
        if (cControllScript == null || WalkToPos2 == null) yield break;

        cControllScript.StartAutoMoveTo(WalkToPos2.position);

        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        yield return new WaitForSeconds(1f);
    }
    public IEnumerator Act_LeftRight()
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

    //=============================================
    //手機操作
    //=============================================
    public IEnumerator Act_HangUpPhone()
    {
        if (PhonePanel) PhonePanel.SetActive(false);

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

        yield return new WaitForSeconds(1.5f);

        cControllScript.animator.SetBool("phone", false);
    }
    public IEnumerator Act_PickPhoneOn()
    {
        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForClipEnd(cControllScript.animator, "phone");
        //if (PhonePanel != null) PhonePanel.SetActive(true);

        // 不關，交給後面劇情關（或你再做 PickPhoneOff）
        //cControllScript.animator.SetBool("phone", false);
    }

    //=============================================
    //畫面控制
    //=============================================
    public IEnumerator Act_BlackPanelOn()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_BlackPanelOff()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_BlackPanelShutOff()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 0.1f, 0f, 1f, null);
        yield return new WaitForSeconds(0.5f);
    }
    
    public IEnumerator Act_BlackPanelOn2()
    {
        if (BlackPanel22 == null || animationScript == null) yield break;

        BlackPanel22.SetActive(true);
        animationScript.Fade(BlackPanel22, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_SetTimeText(string time)
    {
        //指定ShowTimeText為19:30
        Timetext.gameObject.SetActive(true);
        Timetext.GetComponentInChildren<TextMeshProUGUI>().text = "19:00";
        yield return new WaitForSeconds(1);
        Timetext.GetComponentInChildren<TextMeshProUGUI>().text = time;
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_ShowPhoto(GameObject target)
    {
        //PicturePanel 底下是 多張 Image，名字 = pictureName

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

    public IEnumerator Act_picture2Open()
    {

        // 再開指定那張
        if (Picture02 != null)
            Picture02.gameObject.SetActive(true);
        else
            Debug.LogWarning($"[Act_ShowPhoto] 找不到圖片：{Picture02.ToString()}");

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
        Picture02.SetActive(false);
        PicturePanel.SetActive(false);
        yield return null;
    }

    public IEnumerator Act_BigPictureZoom()
    {
        //放大圖片某處，放大位置我會放一個transform，朝著那裏放大就可以了

        //📝 註記
        //這是「世界座標放大」，非常適合你現在的公車＋場景構圖
        //你之後要改成 UI 放大，只要換這支

        if (mainCamera == null || BigPictureZoomTarget == null)
            yield break;

        Vector3 camStartPos = mainCamera.transform.position;
        Vector3 dir = (BigPictureZoomTarget.position - camStartPos).normalized;
        Vector3 zoomPos = camStartPos + dir * zoomDistance;

        float t = 0f;

        // Zoom in
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            mainCamera.transform.position =
                Vector3.Lerp(camStartPos, zoomPos, t / zoomDuration);
            yield return null;
        }

        yield return new WaitForSeconds(zoomHoldTime);

        t = 0f;
        // Zoom out
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            mainCamera.transform.position =
                Vector3.Lerp(zoomPos, camStartPos, t / zoomDuration);
            yield return null;
        }

        mainCamera.transform.position = camStartPos;
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_ShowGameTitle()
    {
        GameNameRawImage.gameObject.SetActive(true);
        GameNamevideoPlayer.Play();
        animationScript.Fade(GameNameRawImage.gameObject, 1, 0, 1, null);

        yield return new WaitForSeconds(6.5f);
        animationScript.Fade(GameNameRawImage.gameObject, 1, 1, 0, null);
        yield return new WaitForSeconds(1.5f);
        GameNameRawImage.gameObject.SetActive(false);
    }

    //=============================================
    //公車物理控制
    //=============================================
    public IEnumerator Act_BusShakeWithDamping(bool strong)
    {
        if (busVisualRoot == null) yield break;

        Vector3 originLocalPos = busVisualRoot.localPosition;
        Quaternion originLocalRot = busVisualRoot.localRotation;

        float duration = strong ? 1.4f : 0.9f;

        float startPosAmp = strong ? 0.04f : 0.02f; // ✅視覺晃動建議小一點
        float startRotAmp = strong ? 2.0f : 1.0f;   // ✅2~1度很夠騙了

        float frequency = strong ? 18f : 12f;
        float damping = strong ? 3.2f : 4.5f;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float normalized = t / duration;
            float decay = Mathf.Exp(-damping * normalized);
            float shake = Mathf.Sin(t * frequency) * decay;

            Vector3 offset = new Vector3(
                shake * startPosAmp,
                shake * startPosAmp * 0.4f,
                0f
            );

            float rotZ = shake * startRotAmp;
            Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

            busVisualRoot.localPosition = originLocalPos + offset;
            busVisualRoot.localRotation = originLocalRot * rot;

            yield return null;
        }

        busVisualRoot.localPosition = originLocalPos;
        busVisualRoot.localRotation = originLocalRot;
    }
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
            float rotZ =  Random.Range(-posAmp, posAmp);

            busVisualRoot.localPosition = originLocalPos + new Vector3(offsetX, offsetY, 0f);
            busVisualRoot.localRotation = originLocalRot * Quaternion.Euler(0f, 0f, rotZ);

            yield return null;
        }

        busVisualRoot.localPosition = originLocalPos;
        busVisualRoot.localRotation = originLocalRot;
    }
    
    
    
   

    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("影片播放結束");
        // 可以接續其他邏輯，例如對話、切場景
    }
}
