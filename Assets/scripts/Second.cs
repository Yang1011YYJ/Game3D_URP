using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using static DialogueSystemGame00;
using static UnityEngine.GraphicsBuffer;



//public enum SceneCheckpoint
//{
//    Start = 0,
//    AfterFadeIn = 10,
//    AfterCameraMove = 20,
//    AfterRoofBlink = 30,
//    AfterDialogue = 40,
//    AfterTeach1 = 50,
//    AfterTeach2 = 60
//}

public class Second : MonoBehaviour
{

    //[System.Serializable]
    //private class TeachingSnapshot
    //{
    //    public bool playerActive;
    //    public bool playerControl;

    //    public bool phonePanelActive;
    //    public bool blackPanelActive;

    //    public bool errorPanelActive;
    //    public bool errorPlaceActive;
    //    public bool circlePlaceActive;

    //    public bool hintActive;
    //    public bool photoFrameActive;
    //    public bool correctPanelActive;
    //    public bool wrongPanelActive;
    //    public bool smileActive;

    //    public bool photoPanelActive;
    //    public bool titlePanelActive;
    //    public bool timeTextActive;

    //    public float exposure;         // 需要 fader 提供 getter
    //    public Color errorLightColor;

    //    public Vector3 camPos;
    //}
    //private TeachingSnapshot _teachSnap;
    [Tooltip("測試用")]
    public GameObject StartGameButton;

    [Header("腳本")]
    public AnimationScript animationScript;
    public CControll cControllScript;
    [Tooltip("場景中負責計算找錯誤數量的管理員")] public SpotManager spotManager;
    public TimeControll timer;
    public CameraMoveControll cameraMoveControllScript;
    public FadeInByExposure fader;
    public DialogueSystemGame01 dialogueSystemGame01Script;
    public WorldScroller worldScrollerScript;

    [Header("異常相關")]
    [Tooltip("異常畫面的背景")] public GameObject ErrorPanel;
    [Tooltip("設定異常的位置組")] public GameObject ErrorPlace;
    [Tooltip("設定異常的圈圈")] public GameObject CirclePlace;
    //[Tooltip("教學-設定異常的圈圈")] public GameObject CirclePlaceTeach;
    [Tooltip("異常光線")] public Light ErrorLight;
    public bool StartError;
    [Tooltip("開始找錯")] public bool ErrorStart;
    [Tooltip("異常觸發")] public bool eT1;
    [Tooltip("異常數量")] public int errorTotal = 10;
    [Tooltip("笑臉")] public Transform smileTf;
    [Header("再挑戰設定")]
    public int roundSeconds = 15;   // 每次重開倒數的秒數
    [Header("容錯次數（血量）")]
    public int maxLives = 2;
    public int lives = 2;
    private bool penaltyRunning = false;
    [Header("整場累積目標")]
    public int winTotalCaptured = 10;
    public int totalCaptured = 0;      // ✅整場累積
    private bool gameEnding = false;   // ✅避免重複觸發結局
    //[Header("失敗")]
    //private bool pendingFail = false;
    //private string pendingFailReason = "";


    [Header("玩家")]
    public GameObject Player;
    [Tooltip("玩家起始出現的位置")] public Transform PlayerStartPos;
    public Transform targetPoint;


    [Header("相機")]
    //public Transform TargetPoint;
    //public Transform StartPoint;
    //public float MoveSpeed =5f;
    public float lightUPDuration = 1.2f;
    [Header("Big Picture Zoom (Perspective Camera)")]
    [Tooltip("照片上對焦的「空物件」")] public Transform bigPictureTarget;      // 照片上對焦的「空物件」
    [Tooltip("推進/拉回時間")] public float zoomDuration = 0.8f;        // 推進/拉回時間
    [Tooltip("放大後停留秒數")] public float zoomHoldTime = 3f;           // 放大後停留秒數
    [Tooltip("相機往前推的距離")] public float zoomDistance = 2.5f;         // 相機往前推的距離


    [Header("拍照框點擊狀態")]
    public bool photoFrameClicked = false;

    [Header("燈光")]
    public GameObject BusUpLightTotal;

    [Header("教學")]
    [Tooltip("查看教學")] public bool CheckTeach = false;
    //[Tooltip("系統提是文字")] public TextMeshProUGUI HintText;
    [Header("教學拍照框 (UI)")]
    [Tooltip("拍照框 Image（UI）")] public RectTransform PhotoFrameRect;
    [Tooltip("拍照框的 Image（用來開關）")] public Image PhotoFrameImage;
    [Tooltip("拍照框跟隨指標速度")] public float photoFrameFollowSpeed = 18f;

    [Header("UI Raycaster")]
    public Canvas UICanvas;                       // 你的 UI Canvas
    public GraphicRaycaster uiRaycaster;          // Canvas 上的 GraphicRaycaster
    public EventSystem eventSystem;               // 場景 EventSystem

    [Header("拍照結果面板")]
    public GameObject WrongPhotoPanel;            // 可選：如果你也想有錯誤面板


    [Header("手機 UI")]
    [Tooltip("顯示在畫面上的手機介面 Panel")] public GameObject PhonePanel;
    [Tooltip("手機裡的『相機』按鈕")] public UnityEngine.UI.Button CameraButton;
    [Tooltip("紀錄玩家有沒有按相機")] public bool hasPressedCamera = false;

    [Header("拍照流程")]
    [Tooltip("正確照片顯示的 Panel")] public GameObject CorrectPhotoPanel;          // 正確照片顯示的 Panel
    [Tooltip("快門按鈕（拍照按鈕）")] public Button ShutterButton;                  // 快門按鈕（拍照按鈕）
    [Tooltip("玩家有沒有按快門")] public bool hasPressedShutter = false;        // 玩家有沒有按快門
    [Header("照片顯示")]
    [Tooltip("顯示照片的 Panel（可選，用來整組開關）")]
    public GameObject PhotoPanel;

    [Tooltip("照片槽位：元素0=01、元素1=02...（把 picture01、picture02 依序拖進來）")]
    public Image[] pictures;


    [Header("其他")]
    public GameObject BlackPanel;//黑色遮罩
    public GameObject BlackPanel22;//黑色遮罩
    [Tooltip("控制紅光閃爍的協程")] Coroutine warningCoroutine;
    [Header("遊戲失敗")]
    [Tooltip("紅色面板")] public GameObject RedPanel;
    [Tooltip("時間顯示")] public TextMeshProUGUI timetext;
    [Tooltip("遊戲名稱")] public GameObject TitlePanel;

    [Header("車子")]
    [Tooltip("車子本體父物件")] public Transform busRoot; // Inspector 指到你的
    public Rigidbody busRb;

    [Header("遊戲本體")]
    public TextMeshProUGUI HintText;     // 可選：提示文字
    private bool roundRunning = false;
    private bool roundEnding = false;
    // 讓拍照框跟隨滑鼠只在回合中跑
    private bool followFrame = false;

    private void ResetLivesOnSuccess()//重置次數
    {
        lives = maxLives;
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"剩餘容錯：{lives}/{maxLives}";
        }
    }

    public void ConsumeLife(string reason)//這裡會宣告遊戲失敗
    {
        if (gameEnding) return;
        if (penaltyRunning) return;
        if (!roundRunning || roundEnding) return; // ← 建議補這行

        lives--;

        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"{reason}\n剩餘容錯：{Mathf.Max(lives, 0)}/{maxLives}";
        }

        StartCoroutine(JumpscareAndContinueOrFail(reason));
        
    }

    private IEnumerator JumpscareAndContinueOrFail(string reason)//嚇一跳然後判斷繼續或失敗
    {
        penaltyRunning = true;

        if (spotManager != null) spotManager.SetSpotsInteractable(false);

        if (RedPanel != null) RedPanel.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (RedPanel != null) RedPanel.SetActive(false);

        if (lives <= 0)
        {
            // ✅ 失敗：直接走通用結局（不要再開別的協程）
            yield return StartCoroutine(EndGameRoutine("Fail", false, reason));
            yield break;
        }

        // ✅ 續命：重開倒數、放行互動
        if (timer != null)
        {
            timer.ForceEnd();
            timer.StartCountdown(roundSeconds);
        }
        if (spotManager != null) spotManager.SetSpotsInteractable(true);

        penaltyRunning = false;
    }



    private enum FlowStage
    {
        Cutscene,        // 劇情段（可跳）
    }
    private FlowStage stage = FlowStage.Cutscene;

    private Coroutine sceneFlowRoutine;
    private void OnDisable()
    {
        Debug.LogWarning($"[First] OnDisable !!!  id={GetInstanceID()}  frame={Time.frameCount}");
    }

    private void OnDestroy()
    {
        Debug.LogWarning($"[First] OnDestroy !!!  id={GetInstanceID()}  frame={Time.frameCount}");
    }

    private void OnEnable()
    {
        Debug.Log($"[First] OnEnable id={GetInstanceID()}");
    }

    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        timer = FindAnyObjectByType<TimeControll>();
        cameraMoveControllScript = FindAnyObjectByType<CameraMoveControll>();
        fader = FindAnyObjectByType<FadeInByExposure>();
        worldScrollerScript = FindAnyObjectByType<WorldScroller>();
        dialogueSystemGame01Script = FindAnyObjectByType<DialogueSystemGame01>();
        if (cControllScript == null)
        {
            cControllScript = FindAnyObjectByType<CControll>();
        }
        if (spotManager == null)
        {
            spotManager = FindAnyObjectByType<SpotManager>();
        }
        if (UICanvas == null) UICanvas = FindAnyObjectByType<Canvas>();
        if (uiRaycaster == null && UICanvas != null) uiRaycaster = UICanvas.GetComponent<GraphicRaycaster>();
        if (eventSystem == null) eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (dialogueSystemGame01Script != null)
            dialogueSystemGame01Script.BindOwner(this);
    }
    private void Start()
    {
        ErrorPanel.SetActive(false);
        ErrorPlace.SetActive(false);
        //CirclePlaceTeach.SetActive(false);
        CirclePlace.SetActive(false);
        timetext.gameObject.SetActive(false);
        TitlePanel.SetActive(false);
        PhotoPanel.SetActive(false);
        RedPanel.SetActive(false);
        BlackPanel.SetActive(false);
        BlackPanel22.SetActive(false);
        smileTf.gameObject.SetActive(false);
        BusUpLightTotal.SetActive(true);
        ErrorLight.color = new Color(1, 0, 0, 0);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);

        if (PhonePanel != null)
            PhonePanel.SetActive(false);

        hasPressedCamera = false;

        eT1 = false;
        ErrorStart = false;
        if (CorrectPhotoPanel != null)
            CorrectPhotoPanel.SetActive(false);

        hasPressedShutter = false;

        worldScrollerScript.StartMove_Speed(5f);

        // 整個場景流程交給協程控制，Start 只負責開頭
        //sceneFlowRoutine = StartCoroutine(SceneFlow());


    }

    public void OnShutterButtonClicked()
    {
        Debug.Log("[First] 玩家按下快門（拍照）");
        hasPressedShutter = true;
    }

    //public IEnumerator PhotoStoryFlow()
    //{
    //    // A) 打開 error 面板（你原本就有）
    //    openErrorPanel();

    //    // B) 等面板淡入完成（你原本也是用 alpha 等）
    //    var cg = ErrorPanel.GetComponent<CanvasGroup>();
    //    if (cg != null)
    //        yield return new WaitUntil(() => cg.alpha >= 1f);

    //    // C) 系統提示文字顯示（看你文字在哪裡，這裡用對話系統示範）
    //    // 你可以換成 ErrorPanel 裡的 TMP 文字也行
    //    // 例：dialogueSystemGame00Script.StartDialogue(dialogueSystemGame00Script.TextfileXXX);
    //    // yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);

    //    // D) 等玩家按下快門
    //    hasPressedShutter = false;
    //    if (ShutterButton != null)
    //        ShutterButton.interactable = true;

    //    yield return new WaitUntil(() => hasPressedShutter);

    //    if (ShutterButton != null)
    //        ShutterButton.interactable = false;

    //    // E) 閃光：fader 持續 1 秒（0.1 秒爆亮 + 0.9 秒回復）
    //    // 你可以依你專案曝光值微調
    //    float baseExposure = 0.5f;   // 你前面 FadeExposure 結束是 0.5
    //    float flashExposure = 2.5f;  // 閃光更亮

    //    // 先瞬間變亮（很短）
    //    yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
    //    // 再回復（把剩下時間用掉）
    //    yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));

    //    // F) 正確照片 Panel 出現
    //    if (CorrectPhotoPanel != null)
    //    {
    //        CorrectPhotoPanel.SetActive(true);
    //        // 你如果想淡入，也可以用 animationScript.Fade(CorrectPhotoPanel, ……)
    //    }

    //    // G) 繼續對話
    //    if (TextfileAfterPhoto != null)
    //    {
    //        dialogueSystemGame00Script.StartDialogue(TextfileAfterPhoto);
    //        yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);
    //    }
    //}

    //private void ApplyCheckpointState(SceneCheckpoint cp)
    //{
    //    // --- 共通：把一些可能殘留的協程效果停掉/歸零 ---
    //    if (warningCoroutine != null) { StopCoroutine(warningCoroutine); warningCoroutine = null; }

    //    // UI/面板先收乾淨（避免跳過時殘留）
    //    if (HintText != null) HintText.gameObject.SetActive(false);
    //    if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
    //    if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
    //    if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);
    //    if (ErrorPanel != null) ErrorPanel.SetActive(false);
    //    if (ErrorPlace != null) ErrorPlace.SetActive(false);
    //    if (CirclePlace != null) CirclePlace.SetActive(false);
    //    if (PhonePanel != null) PhonePanel.SetActive(false);
    //    if (smileTf != null) smileTf.gameObject.SetActive(false);

    //    // 紅光：一般劇情到 Teach1 結束後應該回到原本（你預設 alpha=0）
    //    if (ErrorLight != null)
    //    {
    //        var c = ErrorLight.color;
    //        c.a = 0f;
    //        ErrorLight.color = c;
    //    }

    //    // 曝光：你的 SceneFlow 開頭最後是 0.5
    //    // ⚠️ 最穩：直接把曝光設到終點（不要再 FadeExposure）
    //    if (fader != null) fader.SetExposureImmediate(0.5f);
    //    // ↑ 你需要在 FadeInByExposure 裡加一個 SetExposureImmediate(float v)（下面我給你）

    //    // 相機：劇情正常走完鏡頭會到 TargetPoint
    //    //if (cameraMoveControllScript != null && cameraMoveControllScript.cam != null && TargetPoint != null)
    //    //{
    //    //    cameraMoveControllScript.cam.transform.position = TargetPoint.position;
    //    //}

    //    // --- 依 checkpoint 決定玩家/控制狀態 ---
    //    // 在你原本流程：對話後會把玩家藏起來、鎖控制，然後進 Teach1/Teach2

    //    dialogueSystemGame01Script.currentCP = cp;
    //}


    //IEnumerator AbnormalCaptureFlow(Transform targetTf, bool darkFirst, bool showSmile, float waitSmileSeconds)
    //{
    //    // ✅ 進教學第一刻：拍快照（只拍一次，不要每一段都拍）
    //    if (_teachSnap == null) CaptureBeforeTeaching();

    //    // 先鎖玩家、藏玩家
    //    if (Player != null) Player.SetActive(false);
    //    if (cControllScript != null) cControllScript.playerControlEnabled = false;
    //    if (PhonePanel != null) PhonePanel.SetActive(false);

    //    float baseExposure = 0.5f;     // 你目前流程就是用 0.5 當正常亮度
    //    float darkExposure = -2.0f;    // 第二段要暗下來用

    //    //// 1) 需要暗下來才做（第二段）
    //    //if (darkFirst)
    //    //    yield return StartCoroutine(fader.FadeExposure(1.0f, baseExposure, darkExposure));

    //    // 2) 打開 error panel
    //    Debug.Log("[Teach] AbnormalCaptureFlow ENTER");
    //    openErrorPanel();

    //    // 等淡入完成
    //    var cg = ErrorPanel != null ? ErrorPanel.GetComponent<CanvasGroup>() : null;
    //    if (cg != null)
    //        yield return new WaitUntil(() => cg.alpha >= 1f);

    //    // 4) 顯示提示文字（你可以改文案）
    //    if (HintText != null)
    //    {
    //        HintText.gameObject.SetActive(true);
    //        HintText.text = showSmile
    //            ? "盡快點擊異常！"
    //            : "對準異常，按下快門";
    //    }

    //    // 3) 第二段：等 3 秒後 smile 出現
    //    if (showSmile && smileTf != null)
    //    {
    //        smileTf.gameObject.SetActive(false);
    //        yield return new WaitForSeconds(waitSmileSeconds);
    //        smileTf.gameObject.SetActive(true);

    //        // 第二段目標就是 smile
    //        targetTf = smileTf;
    //    }

    //    // 5) 指引框出現 + 移到指定目標
    //    if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(true);

    //    //if (PhotoFrameRect != null && targetTf != null)
    //    //    yield return StartCoroutine(MoveFrameToTargetAny(PhotoFrameRect, targetTf, photoFrameAutoMoveSpeed));

    //    // 6) 等玩家點擊拍照框（改成 Button OnClick）
    //    photoFrameClicked = false;

    //    // 拿到 Button
    //    var btn = PhotoFrameImage != null ? PhotoFrameImage.GetComponent<Button>() : null;

    //    // 在「移動期間」先鎖住，避免還沒到位就被點
    //    if (btn != null) btn.interactable = false;

    //    // 只移動一次：移到指定目標
    //    if (PhotoFrameRect != null && targetTf != null)
    //        yield return StartCoroutine(MoveFrameToTargetAny(PhotoFrameRect, targetTf, photoFrameAutoMoveSpeed));

    //    // 移完再允許點
    //    if (btn != null) btn.interactable = true;

    //    photoFrameClicked = false;
    //    yield return new WaitUntil(() => photoFrameClicked);

    //    if (btn != null) btn.interactable = false;



    //    // 7) 點框 = 快門 → 閃光
    //    if (HintText != null) HintText.text = "影像鎖定…";

    //    float flashExposure = 2.5f;
    //    yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
    //    yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));

    //    // 8) 顯示正確面板並等待 3 秒（期間不恢復、不淡出）
    //    if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(true);
    //    if (HintText != null) HintText.text = "異常影像已成功保存";

    //    yield return new WaitForSeconds(3f);

    //    // 9) 3 秒後才開始淡出（面板淡出）
    //    if (CorrectPhotoPanel != null)
    //    {
    //        // 如果你 CorrectPhotoPanel 也想淡出，前提：它有 CanvasGroup
    //        CanvasGroup correctCg = CorrectPhotoPanel.GetComponent<CanvasGroup>();
    //        if (correctCg != null)
    //        {
    //            // 先確保 alpha 是 1
    //            correctCg.alpha = 1f;
    //            animationScript.Fade(CorrectPhotoPanel, 0.6f, 1f, 0f, null);
    //            yield return new WaitForSeconds(0.6f);
    //        }
    //        CorrectPhotoPanel.SetActive(false);
    //    }

    //    // Hint 也收掉
    //    if (HintText != null) HintText.gameObject.SetActive(false);

    //    // 拍照框與 smile 收掉
    //    if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
    //    if (showSmile && smileTf != null) smileTf.gameObject.SetActive(false);

    //    // ErrorPanel 淡出（你要「恢復原本狀況」= 這裡才開始）
    //    if (ErrorPanel != null)
    //    {
    //        animationScript.Fade(ErrorPanel, 0.6f, 1f, 0f, null);
    //        yield return new WaitForSeconds(0.6f);
    //        ErrorPanel.SetActive(false);
    //    }
    //    if (ErrorPlace != null) ErrorPlace.SetActive(false);
    //    if (CirclePlace != null) CirclePlace.SetActive(false);

    //    // ✅ 教學結束最後：恢復到進教學前
    //    RestoreAfterTeaching();
    //    PhonePanel.SetActive(false);

    //    photoFrameClicked = false;

    //}


    //IEnumerator MoveFrameToTargetUI(RectTransform frame, Transform uiTarget, float speed)
    //{
    //    if (frame == null || uiTarget == null) yield break;

    //    RectTransform targetRect = uiTarget as RectTransform;
    //    if (targetRect == null)
    //    {
    //        Debug.LogWarning("[TeachPhotoFlow] PhotoFrameTeachTarget 不是 RectTransform，請放 UI 物件或一個 RectTransform。");
    //        yield break;
    //    }

    //    // 目標 anchoredPosition
    //    Vector2 targetAnchored = targetRect.anchoredPosition;

    //    while (Vector2.Distance(frame.anchoredPosition, targetAnchored) > 5f)
    //    {
    //        frame.anchoredPosition = Vector2.MoveTowards(frame.anchoredPosition, targetAnchored, speed * Time.deltaTime);
    //        yield return null;
    //    }

    //    frame.anchoredPosition = targetAnchored;
    //}

    IEnumerator MoveFrameToTargetAny(RectTransform frame, Transform target, float speed)
    {
        if (frame == null || target == null || UICanvas == null) yield break;

        // 1) 目標如果是 UI（RectTransform）
        RectTransform targetRect = target as RectTransform;
        if (targetRect != null)
        {
            Vector2 dest = targetRect.anchoredPosition;
            while (Vector2.Distance(frame.anchoredPosition, dest) > 5f)
            {
                frame.anchoredPosition = Vector2.MoveTowards(frame.anchoredPosition, dest, speed * Time.deltaTime);
                yield return null;
            }
            frame.anchoredPosition = dest;
            yield break;
        }

        // 2) 目標如果是世界座標：世界 → 螢幕 → Canvas local
        RectTransform canvasRect = UICanvas.transform as RectTransform;
        Camera cam = UICanvas.worldCamera != null ? UICanvas.worldCamera : Camera.main;

        while (true)
        {

            Vector3 screen = cam != null ? cam.WorldToScreenPoint(target.position) : Camera.main.WorldToScreenPoint(target.position);

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, UICanvas.worldCamera, out localPoint);

            if (Vector2.Distance(frame.anchoredPosition, localPoint) <= 5f)
            {
                frame.anchoredPosition = localPoint;
                break;
            }

            frame.anchoredPosition = Vector2.MoveTowards(frame.anchoredPosition, localPoint, speed * Time.deltaTime);
            Debug.Log($"[MoveFrameToTargetAny] dist={Vector2.Distance(frame.anchoredPosition, localPoint)} localPoint={localPoint}");

            yield return null;
        }
    }


    void FollowPointer(RectTransform frame)//拍照框跟隨滑鼠
    {
        if (frame == null || UICanvas == null) return;

        Vector2 screenPos = Input.mousePosition;
        if (Input.touchCount > 0) screenPos = Input.GetTouch(0).position;

        RectTransform canvasRect = UICanvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, UICanvas.worldCamera, out Vector2 localPoint))
        {
            // ✅ 直接貼到滑鼠（不插值、不延遲）
            frame.anchoredPosition = localPoint;
        }
    }

    bool PointerDownThisFrame()
    {
        if (Input.touchCount > 0) return Input.GetTouch(0).phase == TouchPhase.Began;
        return Input.GetMouseButtonDown(0);
    }

    bool IsPointerOverUI(GameObject target)
    {
        if (target == null || uiRaycaster == null || eventSystem == null) return false;

        Vector2 screenPos = Input.mousePosition;
        if (Input.touchCount > 0) screenPos = Input.GetTouch(0).position;

        var ped = new PointerEventData(eventSystem) { position = screenPos };
        var results = new List<RaycastResult>();
        uiRaycaster.Raycast(ped, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject == target) return true;
            // 如果 target 底下還有子物件（例如框有裝飾），也算點到
            if (results[i].gameObject.transform.IsChildOf(target.transform)) return true;
        }
        return false;
    }

    void CleanupPhotoTeachUI()
    {
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);

        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);

        if (HintText != null) HintText.gameObject.SetActive(false);

        // Error UI 都收起
        if (CirclePlace != null) CirclePlace.SetActive(false);
        if (ErrorPlace != null) ErrorPlace.SetActive(false);

        if (ErrorPanel != null)
        {
            // 你要淡出也行，我這邊直接關
            ErrorPanel.SetActive(false);
        }
    }


    //IEnumerator SceneFlow()
    //{


    //    //// 4) 對話
    //    //if (!dialogueSystemGame01Script.skipRequested)
    //    //{
    //    //    dialogueSystemGame01Script.StartDialogue(dialogueSystemGame01Script.TextfileGame01);
    //    //    yield return new WaitUntil(() => dialogueSystemGame01Script.FirstDiaFinished);
    //    //}
    //    //else
    //    //{
    //    //    // 跳過後：把對話系統收掉，並避免 WaitUntil 卡住
    //    //    if (dialogueSystemGame00Script != null)
    //    //    {
    //    //        dialogueSystemGame00Script.StopTyping();
    //    //        dialogueSystemGame00Script.isTyping = false;
    //    //        dialogueSystemGame00Script.SetPanels(false, false);
    //    //        dialogueSystemGame00Script.FirstDiaFinished = true;
    //    //    }
    //    //}
    //    //ApplyCheckpointState(SceneCheckpoint.AfterDialogue);

    //    // ---- 到這裡：劇情段結束，切到教學段（先不給跳） ----
    //    //stage = FlowStage.Teaching;

    //    //Player.SetActive(false);
    //    //BlackPanel.SetActive(false);
    //    //cControllScript.playerControlEnabled = false;
    //    //PhonePanel.SetActive(false);
    //    //// 5) Teach1
    //    //// 教學前「標準狀態」統一一下，確保跳過跟乖乖看完一樣
    //    //ApplyBeforeTeachingState();

    //    //// Teach 1
    //    //teachRoutine = StartCoroutine(AbnormalTeach_1());
    //    //yield return teachRoutine;
    //    //teachRoutine = null;
    //    //ApplyCheckpointState(SceneCheckpoint.AfterTeach1);

    //    //Debug.Log("中間劇情");

    //    //// Teach 2
    //    //teachRoutine = StartCoroutine(AbnormalTeach_2());
    //    //yield return teachRoutine;
    //    //teachRoutine = null;
    //    //ApplyCheckpointState(SceneCheckpoint.AfterTeach2);


    //    ////2.1紅光亮起
    //    //redLight();
    //    //yield return new WaitUntil(() => ErrorLight.color.a==1f);

    //    //////2.1對話
    //    ////DSG00.StartDialogue(DSG00.TextfileLookPhone);
    //    ////yield return new WaitForSeconds(1f);

    //    ////2.2看手機
    //    //cControllScript.animator.SetBool("phone", true);
    //    //yield return StartCoroutine(WaitForAnimation(cControllScript.animator, "phone"));
    //    ////yield return new WaitForSeconds(0.5f);
    //    //hasPressedCamera = false;
    //    //// ⏳ 在這裡乖乖等玩家按
    //    //yield return new WaitUntil(() => hasPressedCamera);

    //    //// 玩家已經按了相機，可以收手機 UI、結束手機動畫
    //    //PhonePanel.SetActive(false);
    //    //cControllScript.animator.SetBool("phone", false);

    //    ////3.errorpanel亮起
    //    //// 🔥 紅光閃完 → 顯示異常提示 Panel
    //    //Player.SetActive(false);
    //    //openErrorPanel();

    //    ////4.等error面板出現再開始倒數計時
    //    //yield return new WaitUntil(() => ErrorPanel.GetComponent<CanvasGroup>().alpha == 1);

    //    ////5.開始倒數計時
    //    //timer.StartCountdown(15);

    //    ////6.開始找錯
    //    //ErrorStart = true;
    //    //errorResultHandled = false;
    //}


    //第一段：你給一個異常目標 Transform（你說你會自己拉）

    public void StartRoundManual()//遊戲開始
    {
        
        StartCoroutine(Act_PlayFindSpotsRound());
    }
    // Update is called once per frame
    void Update()
    {
        // 跳過劇情（你也可以改成 UI Button 來呼叫 SkipToTeaching()）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 劇情段：ESC = 跳到下一個 MUST label，沒有就直接結束對話
            if (stage == FlowStage.Cutscene && dialogueSystemGame01Script != null)
            {
                //dialogueSystemGame01Script.SkipByEsc_ToNextMustOrEnd();
                return;
            }

            // 如果你之後真的要「教學段也能跳」，再在這裡加規則
        }

        timer.timerText.gameObject.SetActive(ErrorPanel.activeSelf);
        if (spotManager == null) return;

        //1. 成功條件：找到全部，且時間還沒負數
        if (ErrorStart && spotManager.foundCount >= spotManager.totalCount && timer.currentTime >= 0f)
        {
            ErrorStart = false;   // 關閉這一輪檢查
            timer.ForceEnd();
            StartCoroutine(OnErrorComplete()); // 通關
        }
        //else if (ErrorStart && timer.currentTime <= 0f && spotManager.foundCount < spotManager.totalCount)//2. 失敗條件：時間 < 0 且還沒找完
        //{
        //    //遊戲失敗
        //    errorResultHandled = true;
        //    ErrorStart = false;
        //    timer.ForceEnd();
        //    //StartCoroutine(ErrorMistake());   // 失敗
        //}

    }

    void LateUpdate()
    {
        if (!followFrame) return;
        FollowPointer(PhotoFrameRect);
    }

    public IEnumerator AbnormalLight(float duration, float start, float end)//讓窗外異常光線啟動（瞬間變紅、變亮）
    {
        float timer = 0f;
        Color c = ErrorLight.color;
        c.a = start;
        ErrorLight.color = c;

        // 淡入
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            c.a = Mathf.Lerp(start, end, t);
            ErrorLight.color = c;

            yield return null;
        }

        c.a = end;
        ErrorLight.color = c;
    }

    void redLight()
    {
        // 開始紅光閃爍（等你之後實作 abnormal() 和 RecoverNormalLight()）
        warningCoroutine = StartCoroutine(WindowWarningLoop());
    }

    //教學完成：停止紅光、恢復正常光線、之後可接下一段劇情
    IEnumerator OnErrorComplete()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log("[First] 教學完成：找到足夠異常，恢復正常光線");

        // 停止紅光閃爍
        if (warningCoroutine != null)
        {
            //StopCoroutine(warningCoroutine);
            warningCoroutine = null;


            if (ErrorPlace != null)
                ErrorPlace.SetActive(false); // 關閉異常提示界面
            spotManager.ClearAllCircles();
            ErrorStart = false;
            CirclePlace.SetActive(false);
            animationScript.Fade(ErrorPanel, 2f, 1f, 0f, null);
            yield return new WaitForSeconds(2f);
            ErrorPanel.SetActive(false);
            Player.SetActive(true);

            // 恢復正常光線
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(AbnormalLight(2f, 1f, 0f));
            yield return new WaitForSeconds(0.5f);

            // 可選：恢復玩家控制／進入下一段劇情
            if (cControllScript != null)
            {
                cControllScript.playerControlEnabled = true;
            }
        }
    }

    //紅光閃爍的循環（之後你可以在裡面呼叫 2D Light 或改 shader 顏色）
    IEnumerator WindowWarningLoop()
    {
        Debug.Log("[First] 紅光閃爍啟動");
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(AbnormalLight(2f, 0f, 1f));
        //while (!teachFinished)
        //{
        //    // TODO：讓窗外變紅、閃爍一次
        //    AbnormalLightOn();

        //    yield return new WaitForSeconds(0.3f);

        //    // TODO：紅光變暗 / 關閉
        //    AbnormalLightOff();
        Debug.Log("開始變紅");
        yield return new WaitForSeconds(2.5f);
        //}
    }

    public IEnumerator WaitForAnimation(Animator animator, string stateName)
    {
        // 等到進入該動畫 state
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        // 動畫還在播
        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
    }

    public void openErrorPanel()
    {
        Debug.Log("[First] 顯示異常提示畫面 ErrorPlace");
        ErrorPlace.SetActive(true);
        ErrorPanel.SetActive(true);
        animationScript.Fade(ErrorPanel, 2f, 0f, 1f, null);
        CirclePlace.SetActive(true);
        spotManager.RefreshActiveSpots();
    }

    public void OnCameraButtonClicked()
    {
        Debug.Log("[First] 玩家按下手機裡的相機按鈕");
        hasPressedCamera = true;
    }

    public void OnPhotoFrameClicked()
    {
        Debug.Log("[First] 玩家點擊拍照指引框");
        photoFrameClicked = true;
    }

    // 燈光閃爍一次（你可自己調節節奏）
    public IEnumerator LightFlickerOnce()
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

    public IEnumerator Act_LeftRight()
    {
        if (cControllScript == null) yield break;

        // 只抓一個 Renderer，避免改錯人
        SpriteRenderer sr = cControllScript.spriteRenderer;
        Animator anim = cControllScript.animator;

        if (sr == null) yield break;

        // ✅ 1. 關 Animator（關鍵）
        if (anim != null) anim.enabled = false;

        // ✅ 2. 切到 leftidle
        sr.sprite = cControllScript.leftidle;

        sr.flipX = false;
        yield return new WaitForSeconds(1f);
        Debug.Log(1);

        sr.flipX = true;
        yield return new WaitForSeconds(1f);
        Debug.Log(1);

        sr.flipX = false;
        yield return new WaitForSeconds(1f);
        Debug.Log(1);

        sr.flipX = true;
        yield return new WaitForSeconds(1f);
        Debug.Log(1);

        // ✅ 3. 切回 idle
        sr.flipX = false;
        sr.sprite = cControllScript.idle;

        yield return new WaitForSeconds(0.2f);

        // ✅ 4. 開回 Animator
        if (anim != null) anim.enabled = true;
        yield return new WaitForSeconds(1.5f);
    }

    // ======= 框內判定：DifferenceSpot 會用到 =======

    public bool IsSpotInsideFrame(DifferenceSpot spot)
    {
        if (PhotoFrameRect == null || spot == null) return true;

        RectTransform spotRect = spot.GetComponent<RectTransform>();
        if (spotRect == null) return true;

        // 用 Spot 的中心點做判定（簡單且穩）
        Camera cam = (UICanvas != null && UICanvas.worldCamera != null) ? UICanvas.worldCamera : Camera.main;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, spotRect.position);

        // 判定 spot 中心點是否落在拍照框 Rect 裡
        return RectTransformUtility.RectangleContainsScreenPoint(PhotoFrameRect, screenPos, cam);
    }


    // 顯示照片（你可以自己接 UI Image / Panel）
    // 顯示照片：n=1 就顯示 pictures[0]（也就是 01）
    public void ShowPhotoByNumber(int n)
    {
        if (pictures == null || pictures.Length == 0)
        {
            Debug.LogWarning("[First] pictures 尚未設定（Inspector 沒拖）");
            return;
        }

        int idx = n - 1;
        if (idx < 0 || idx >= pictures.Length)
        {
            Debug.LogWarning($"[First] 找不到照片編號 {n:D2}（pictures 長度={pictures.Length}）");
            return;
        }

        // 先全部關掉（確保只顯示一張）
        for (int i = 0; i < pictures.Length; i++)
        {
            if (pictures[i] != null) pictures[i].gameObject.SetActive(false);
        }

        // 開 panel（如果你有）
        if (PhotoPanel != null) PhotoPanel.SetActive(true);

        // 開指定那張
        if (pictures[idx] != null)
        {
            pictures[idx].gameObject.SetActive(true);
            Debug.Log($"[First] ShowPhoto -> {n:D2}");
        }
        else
        {
            Debug.LogWarning($"[First] pictures[{idx}] 是空的，請確認 Inspector");
        }
    }

    // 讓劇本 action 呼叫：支援 "01"、"1"、"ShowPhoto_01"、"ShowPhoto01"、"S02_Photo_01a" 都盡量抓出編號
    public void ShowPhoto(string photoKey)
    {
        if (string.IsNullOrEmpty(photoKey))
        {
            Debug.LogWarning("[First] ShowPhoto photoKey 為空");
            return;
        }

        // 1) 從字串抓出最後一段連續數字（例如 01、02、12）
        // 例："ShowPhoto_S02_Photo_01a" -> 01
        int number = ExtractLastNumber(photoKey);

        if (number <= 0)
        {
            Debug.LogWarning($"[First] ShowPhoto 無法解析編號：{photoKey}");
            return;
        }

        ShowPhotoByNumber(number);
    }

    // 抓字串最後一段連續數字
    private int ExtractLastNumber(string s)
    {
        int end = -1;
        for (int i = s.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(s[i]))
            {
                end = i;
                break;
            }
        }
        if (end == -1) return -1;

        int start = end;
        while (start - 1 >= 0 && char.IsDigit(s[start - 1]))
            start--;

        string numStr = s.Substring(start, end - start + 1);

        if (int.TryParse(numStr, out int result))
            return result;

        return -1;
    }

    // 可選：關掉照片（用在 ReturnToBus 或照片停留結束時）
    public void HidePhotoPanel()
    {
        if (pictures != null)
        {
            for (int i = 0; i < pictures.Length; i++)
            {
                if (pictures[i] != null) pictures[i].gameObject.SetActive(false);
            }
        }
        if (PhotoPanel != null) PhotoPanel.SetActive(false);
    }


    // bigpicture：放大照片 target 區塊（你可做成 UI Zoom、或開一個放大 Panel）
    public IEnumerator BigPictureZoom()
    {
        Debug.Log("[First] BigPictureZoom");
        Camera cam = cameraMoveControllScript.cam;
        if (cam == null || bigPictureTarget == null)
            yield break;

        // 1️⃣ 記住原本狀態
        Vector3 camStartPos = cam.transform.position;

        // 計算「往 target 方向前進」
        Vector3 dir = (bigPictureTarget.position - camStartPos).normalized;
        Vector3 camZoomPos = camStartPos + dir * zoomDistance;

        float t = 0f;

        // 2️⃣ 推進（Zoom In）
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float lerp = t / zoomDuration;
            cam.transform.position = Vector3.Lerp(camStartPos, camZoomPos, lerp);
            yield return null;
        }
        cam.transform.position = camZoomPos;

        // 3️⃣ 停留
        yield return new WaitForSeconds(zoomHoldTime);

        // 4️⃣ 拉回（Zoom Out）
        t = 0f;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float lerp = t / zoomDuration;
            cam.transform.position = Vector3.Lerp(camZoomPos, camStartPos, lerp);
            yield return null;
        }
        cam.transform.position = camStartPos;

        // 5️⃣ 關掉照片 UI
        if (PhotoPanel != null)
            PhotoPanel.SetActive(false);

        Debug.Log("[First] BigPictureZoom END");
        yield return new WaitForSeconds(0.8f);
    }

    // 時間跳轉：你如果有時間 UI 文字就改它
    public void SetTimeText(string timeText)
    {
        Debug.Log($"[First] TimeJump -> {timeText}");
        timetext.text = timeText;
    }

    // 顯示遊戲標題（你可以做一個 TitlePanel）
    public IEnumerator ShowGameTitle()
    {
        Debug.Log("[First] ShowGameTitle");
        TitlePanel.SetActive(true);
        animationScript.Fade(TitlePanel, 1.5f, 0f, 1f, null);
        yield return new WaitForSeconds(2f);
        TitlePanel.SetActive(false);
    }

    public IEnumerator Act_BusLightBright()
    {
        //// 2) 鏡頭移動
        //if (!dialogueSystemGame00Script.skipRequested)
        //{
        //    cameraMoveControllScript.cam.transform.position = StartPoint.position;
        //    yield return StartCoroutine(cameraMoveControllScript.MoveCameraTo(TargetPoint.position, MoveSpeed));
        //    yield return new WaitForSeconds(2f);
        //}
        //else
        //{
        //    // 直接到位
        //    cameraMoveControllScript.cam.transform.position = TargetPoint.position;
        //}
        //ApplyCheckpointState(SceneCheckpoint.AfterCameraMove);
        // 3) 車頂燈閃
        //if (!dialogueSystemGame00Script.skipRequested)
        //{
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
        //}
        //else
        //{
        //    // 跳過後，燈光要跟「劇情看完」一致：最後是亮著
        //    BusUpLightTotal.SetActive(true);
        //}
        //ApplyCheckpointState(SceneCheckpoint.AfterRoofBlink);
    }

    public IEnumerator Act_LightOn()
    {
        Debug.Log($"[SceneFlow] Start id={GetInstanceID()}");
        //stage = FlowStage.Cutscene;
        ////0.鎖連續跳過劇情
        //stage = FlowStage.Cutscene;
        //dialogueSystemGame00Script.allowFastReveal = false;
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
        //ApplyCheckpointState(SceneCheckpoint.AfterFadeIn);
    }

    public IEnumerator Act_LightBlack()
    {
        // 1) 曝光淡入
        //if (!dialogueSystemGame00Script.skipRequested)
        //{
        fader.SetExposureImmediate(-10f);
        yield return new WaitForSeconds(2f);
        //}
        //else
        //{
        //    // 直接把曝光設到「看完劇情後」一致
        //    fader.SetExposureImmediate(0.5f);
        //}
    }
    public void OnClickBackground()
    {
        if (!roundRunning || roundEnding || penaltyRunning || gameEnding) return;
        ConsumeLife("點錯了！");
    }
    public IEnumerator Act_PlayFindSpotsRound()//遊戲本體
    {
        if (roundRunning) yield break;

        roundRunning = true;
        roundEnding = false;

        StartGameButton.SetActive(false);
        // 先把 UI 收乾淨
        if (RedPanel != null) RedPanel.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);

        // 先鎖住 spots（等面板淡入完成再開）
        if (spotManager != null)
        {
            spotManager.second = this;
            spotManager.RefreshActiveSpots();
            spotManager.SetSpotsInteractable(false);
        }

        // 1) 顯示 ErrorPanel + Place + CirclePlace（你原本 openErrorPanel 會做）
        openErrorPanel();

        spotManager.text.gameObject.SetActive(true);

        // 2) 拍照框出現 + 開始跟隨
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(true);
        followFrame = true;

        // 3) 等 ErrorPanel 淡入完成才開始倒數
        var cg = ErrorPanel != null ? ErrorPanel.GetComponent<CanvasGroup>() : null;
        if (cg != null)
            yield return new WaitUntil(() => cg.alpha >= 1f);

        // 4) 倒數開始
        if (timer != null)
        {
            if (timer.timerText != null) timer.timerText.gameObject.SetActive(true);

            // 讓 TimeControll 到時候能回呼（下面我會給 TimeControll 改法）
            timer.onTimeUp = () =>
            {
                // ✅ 超時算一次失敗（扣血），但不當作整回合結束
                ConsumeLife("時間到！");
            };

            timer.StartCountdown(15);
        }

        // 5) 開放玩家點 spot
        if (spotManager != null) spotManager.SetSpotsInteractable(true);

        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = "在時間內找出所有異常！";
        }

        // 6) 等回合結束（成功或失敗會把 roundRunning 關掉）
        yield return new WaitUntil(() => roundRunning == false);
        yield return new WaitForSeconds(1.5f);
    }

    // ✅ 每個 spot 點中且判定成功時，DifferenceSpot 會呼叫這個
    public void OnSpotCaptured(DifferenceSpot spot)
    {
        if (!roundRunning || roundEnding || gameEnding) return;
        // ✅ 只要點對一次，就把容錯回滿（中斷連續失敗）
        ResetLivesOnSuccess();
        // ✅只負責整場累積
        totalCaptured++;
        Debug.Log($"[Game] totalCaptured = {totalCaptured}/{winTotalCaptured}");

        // 顯示進度（可選）
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"累積保存：{totalCaptured}/{winTotalCaptured}";
        }

        // 原本的回合內流程
        StartCoroutine(SpotCaptureFeedbackAndCheckEnd());
    }

    private IEnumerator EndGameRoutine(string sceneName, bool playJumpscareFirst, string reason = "")//遊戲結束
    {
        if (gameEnding) yield break;
        gameEnding = true;

        // 1) 停回合/停輸入
        roundEnding = true;
        roundRunning = false;

        // 2) 停計時 & 鎖互動
        if (timer != null)
        {
            timer.onTimeUp = null;
            timer.ForceEnd();
        }
        if (spotManager != null) spotManager.SetSpotsInteractable(false);
        followFrame = false;
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);

        // 3) 失敗可選：先播 jumpscare（紅面板）
        if (playJumpscareFirst)
        {
            if (HintText != null)
            {
                HintText.gameObject.SetActive(true);
                HintText.text = string.IsNullOrEmpty(reason) ? "失敗…" : reason;
            }

            if (RedPanel != null) RedPanel.SetActive(true);
            yield return new WaitForSeconds(1f);
            if (RedPanel != null) RedPanel.SetActive(false);
        }

        // 4) 收 UI（你現成的）
        yield return StartCoroutine(CloseRoundUI());

        // 5) 淡出
        if (fader != null)
            yield return StartCoroutine(fader.FadeExposure(1.2f, 0.5f, -10f));

        // 6) 切場景
        SceneManager.LoadScene(sceneName);
    }
    private IEnumerator EndGameSuccess()//遊戲過關
    {
        yield return StartCoroutine(EndGameRoutine("success", false));
    }

    // 閃光 + 文字 + 若全部找完就成功
    private IEnumerator SpotCaptureFeedbackAndCheckEnd()
    {
        roundEnding = true; // 這裡先鎖一下，避免連點造成重複進入

        // 你要的「閃光+顯示文字」：用 fader
        if (HintText != null && spotManager != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = $"已保存異常 {spotManager.foundCount}/{spotManager.totalCount}";
        }

        if (fader != null)
        {
            float baseExposure = 0.5f;
            float flashExposure = 2.5f;
            yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
            yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));
        }

        // ✅ 判斷是否找完
        if (spotManager != null && spotManager.foundCount >= spotManager.totalCount)
        {
            yield return StartCoroutine(RoundSuccess());
            yield break;
        }

        // 還沒找完就放行繼續
        roundEnding = false;
    }
    private IEnumerator RoundSuccess()
    {
        roundEnding = true;

        // 停倒數
        if (timer != null) timer.ForceEnd();

        // 鎖 spot
        if (spotManager != null) spotManager.SetSpotsInteractable(false);

        // 讓玩家感覺「本 round 完成」
        yield return new WaitForSeconds(0.35f);

        // 收本 round 的 UI
        yield return StartCoroutine(CloseRoundUI());

        roundRunning = false;
        roundEnding = false;

        // ✅ 在「本 round 完整結束後」檢查是否達成總目標
        if (!gameEnding && totalCaptured >= winTotalCaptured)
        {
            StartCoroutine(EndGameSuccess());
        }
    }

    private IEnumerator CloseRoundUI()
    {
        // 停止拍照框跟隨
        followFrame = false;

        // 拍照框收掉
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);

        //計數
        spotManager.text.gameObject.SetActive(false);

        // Hint 收掉
        if (HintText != null) HintText.gameObject.SetActive(false);

        // Error UI 淡出
        if (ErrorPanel != null)
        {
            animationScript.Fade(ErrorPanel, 0.6f, 1f, 0f, null);
            yield return new WaitForSeconds(0.6f);
            ErrorPanel.SetActive(false);
        }
        if (ErrorPlace != null) ErrorPlace.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);

        // 計時 UI 收
        if (timer != null && timer.timerText != null)
            timer.timerText.gameObject.SetActive(false);

        StartGameButton.SetActive(true);
    }
    //public IEnumerator Act_WalkToFront()
    //{
    //    if (cControllScript == null || WalkToFrontPos == null) yield break;

    //    cControllScript.StartAutoMoveTo(WalkToFrontPos.position);

    //    yield return new WaitUntil(() => cControllScript.autoMoveFinished);
    //    yield return new WaitForSeconds(1f);
    //}


    public IEnumerator Act_HangUpPhone()
    {
        if (PhonePanel) PhonePanel.SetActive(false);

        //if (cControllScript.animator != null)
        //    cControllScript.animator.SetBool("phone", false);

        cControllScript.animator.Play("phone", 0, 1f);   // 從最後一幀開始
        cControllScript.animator.speed = -1f;          // 反向播放
        yield return new WaitUntil(() => cControllScript.animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 0f);
        cControllScript.animator.speed = 1f; // 記得還原


        yield return new WaitForSeconds(1f);
    }
    public IEnumerator Act_PickPhone()
    {
        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForAnimation(cControllScript.animator, "phone");

        if (PhonePanel != null) PhonePanel.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        if (PhonePanel != null) PhonePanel.SetActive(false);

        cControllScript.animator.SetBool("phone", false);
    }

    public IEnumerator Act_PickPhoneOn()
    {
        if (cControllScript == null || cControllScript.animator == null) yield break;

        cControllScript.animator.SetBool("phone", true);
        yield return WaitForAnimation(cControllScript.animator, "phone");
        if (PhonePanel != null) PhonePanel.SetActive(true);

        // 不關，交給後面劇情關（或你再做 PickPhoneOff）
        //cControllScript.animator.SetBool("phone", false);
    }

    public IEnumerator Act_BlackPanelOn()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_BlackPanelOn2()
    {
        if (BlackPanel22 == null || animationScript == null) yield break;

        BlackPanel22.SetActive(true);
        animationScript.Fade(BlackPanel22, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_BusShakeWithDamping(bool strong)
    {
        if (busRb == null) yield break;

        Vector3 originPos = busRb.position;
        Quaternion originRot = busRb.rotation;

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

    public IEnumerator Act_BlackPanelOff()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_LightDimDown()
    {
        if (fader != null)
        {
            yield return StartCoroutine(fader.FadeExposure(1.5f, 0.5f, -10f));
            yield return new WaitForSeconds(2f);
        }

    }


}
