using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class First : MonoBehaviour
{
    [Header("腳本")]
    public AnimationScript animationScript;
    public CControll cControllScript;
    [Tooltip("場景中負責計算找錯誤數量的管理員")]public SpotManager spotManager;
    public DialogueSystemGame00 DSG00;
    public TimeControll timer;
    public CameraMoveControll cameraMoveControllScript;
    public FadeInByExposure fader;
    public DialogueSystemGame00 dialogueSystemGame00Script;

    [Header("異常相關")]
    [Tooltip("異常畫面的背景")]public GameObject ErrorPanel;
    [Tooltip("設定異常的位置組")] public GameObject ErrorPlace;
    [Tooltip("教學-設定異常的位置組")] public GameObject ErrorPlaceTeach;
    [Tooltip("設定異常的圈圈")] public GameObject CirclePlace;
    //[Tooltip("教學-設定異常的圈圈")] public GameObject CirclePlaceTeach;
    [Tooltip("異常光線")] public Light ErrorLight;
    public bool StartError;
    [Tooltip("開始找錯")] public bool ErrorStart;
    [Tooltip("異常觸發")] public bool eT1;
    [Tooltip("異常數量")]public int errorTotal = 10;
    [Tooltip("笑臉")]public Transform smileTf;

    [Header("玩家")]
    public GameObject Player;
    [Tooltip("玩家教學用自動走到的位置")]public Vector2 teachTargetPos = new Vector2(19.3f, -4.3f);
    public Transform targetPoint;


    [Header("相機")]
    public Transform TargetPoint;
    public Transform StartPoint;
    public float MoveSpeed =5f;
    public float lightUPDuration = 1.2f;

    [Header("燈光")]
    public GameObject BusUpLightTotal;

    [Header("教學")]
    [Tooltip("查看教學")] public bool CheckTeach = false;
    [Tooltip("系統提是文字")] public TextMeshProUGUI HintText;
    [Header("教學拍照框 (UI)")]
    [Tooltip("拍照框 Image（UI）")] public RectTransform PhotoFrameRect;
    [Tooltip("拍照框的 Image（用來開關）")] public Image PhotoFrameImage;
    [Tooltip("教學：拍照框先自動移動到的目標（UI Transform）")] public Transform PhotoFrameTeachTarget;
    [Tooltip("拍照框自動移動速度")] public float photoFrameAutoMoveSpeed = 900f;
    [Tooltip("拍照框跟隨指標速度")] public float photoFrameFollowSpeed = 18f;

    [Header("UI Raycaster")]
    public Canvas UICanvas;                       // 你的 UI Canvas
    public GraphicRaycaster uiRaycaster;          // Canvas 上的 GraphicRaycaster
    public EventSystem eventSystem;               // 場景 EventSystem

    [Header("拍照結果面板")]
    public GameObject WrongPhotoPanel;            // 可選：如果你也想有錯誤面板


    [Header("手機 UI")]
    [Tooltip("顯示在畫面上的手機介面 Panel")]
    public GameObject PhonePanel;
    [Tooltip("手機裡的『相機』按鈕")]
    public UnityEngine.UI.Button CameraButton;
    [Tooltip("紀錄玩家有沒有按相機")]public bool hasPressedCamera = false;

    [Header("拍照流程")]
    public GameObject CorrectPhotoPanel;          // 正確照片顯示的 Panel
    public Button ShutterButton;                  // 快門按鈕（拍照按鈕）
    public bool hasPressedShutter = false;        // 玩家有沒有按快門
    public TextAsset TextfileAfterPhoto;          // 拍照後要接的對話腳本（可選）


    [Header("其他")]
    public GameObject BlackPanel;//黑色遮罩
    [Tooltip("控制紅光閃爍的協程")] Coroutine warningCoroutine;
    [Header("遊戲失敗")]
    [Tooltip("紅色面板")]public GameObject RedPanel;
    [Tooltip("失敗次數")] public int Mistake;
    // 避免重複判定，用一個旗標
    [Tooltip("避免重複判定")]public bool errorResultHandled = false;

    private Coroutine sceneFlowRoutine;
    private bool skipRequested = false;

    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        DSG00 = FindAnyObjectByType<DialogueSystemGame00>();
        timer = FindAnyObjectByType<TimeControll>();
        cameraMoveControllScript = FindAnyObjectByType<CameraMoveControll>();
        fader = FindAnyObjectByType<FadeInByExposure>();

        dialogueSystemGame00Script = FindAnyObjectByType<DialogueSystemGame00>();
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

    }
    private void Start()
    {
        ErrorPanel.SetActive(false);
        ErrorPlace.SetActive(false);
        //CirclePlaceTeach.SetActive(false);
        ErrorPlaceTeach.SetActive(false);
        CirclePlace.SetActive(false);
        RedPanel.SetActive(false);
        BlackPanel.SetActive(false);
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


        // 整個場景流程交給協程控制，Start 只負責開頭
        sceneFlowRoutine = StartCoroutine(SceneFlow());


    }

    public void OnShutterButtonClicked()
    {
        Debug.Log("[First] 玩家按下快門（拍照）");
        hasPressedShutter = true;
    }

    public IEnumerator PhotoStoryFlow()
    {
        // A) 打開 error 面板（你原本就有）
        openErrorPanel();

        // B) 等面板淡入完成（你原本也是用 alpha 等）
        var cg = ErrorPanel.GetComponent<CanvasGroup>();
        if (cg != null)
            yield return new WaitUntil(() => cg.alpha >= 1f);

        // C) 系統提示文字顯示（看你文字在哪裡，這裡用對話系統示範）
        // 你可以換成 ErrorPanel 裡的 TMP 文字也行
        // 例：dialogueSystemGame00Script.StartDialogue(dialogueSystemGame00Script.TextfileXXX);
        // yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);

        // D) 等玩家按下快門
        hasPressedShutter = false;
        if (ShutterButton != null)
            ShutterButton.interactable = true;

        yield return new WaitUntil(() => hasPressedShutter);

        if (ShutterButton != null)
            ShutterButton.interactable = false;

        // E) 閃光：fader 持續 1 秒（0.1 秒爆亮 + 0.9 秒回復）
        // 你可以依你專案曝光值微調
        float baseExposure = 0.5f;   // 你前面 FadeExposure 結束是 0.5
        float flashExposure = 2.5f;  // 閃光更亮

        // 先瞬間變亮（很短）
        yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
        // 再回復（把剩下時間用掉）
        yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));

        // F) 正確照片 Panel 出現
        if (CorrectPhotoPanel != null)
        {
            CorrectPhotoPanel.SetActive(true);
            // 你如果想淡入，也可以用 animationScript.Fade(CorrectPhotoPanel, ...)
        }

        // G) 繼續對話
        if (TextfileAfterPhoto != null)
        {
            dialogueSystemGame00Script.StartDialogue(TextfileAfterPhoto);
            yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);
        }
    }

    IEnumerator AbnormalCaptureFlow(Transform targetTf, bool darkFirst, bool showSmile, float waitSmileSeconds)
    {
        // 記錄原本狀態（用來恢復）
        bool playerWasActive = Player != null && Player.activeSelf;
        bool prevControl = cControllScript != null && cControllScript.playerControlEnabled;
        float prevAlpha = ErrorLight != null ? ErrorLight.color.a : 0f;

        // 先鎖玩家、藏玩家
        if (Player != null) Player.SetActive(false);
        if (cControllScript != null) cControllScript.playerControlEnabled = false;
        if (PhonePanel != null) PhonePanel.SetActive(false);

        float baseExposure = 0.5f;     // 你目前流程就是用 0.5 當正常亮度
        float darkExposure = -2.0f;    // 第二段要暗下來用

        // 1) 需要暗下來才做（第二段）
        if (darkFirst)
            yield return StartCoroutine(fader.FadeExposure(1.0f, baseExposure, darkExposure));

        // 2) 打開 error panel
        openErrorPanel();

        // 等淡入完成
        var cg = ErrorPanel != null ? ErrorPanel.GetComponent<CanvasGroup>() : null;
        if (cg != null)
            yield return new WaitUntil(() => cg.alpha >= 1f);

        // 3) 第二段：等 3 秒後 smile 出現
        if (showSmile && smileTf != null)
        {
            smileTf.gameObject.SetActive(false);
            yield return new WaitForSeconds(waitSmileSeconds);
            smileTf.gameObject.SetActive(true);

            // 第二段目標就是 smile
            targetTf = smileTf;
        }

        // 4) 顯示提示文字（你可以改文案）
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = showSmile
                ? "盡快點擊異常！"
                : "對準異常，按下快門";
        }

        // 5) 指引框出現 + 移到指定目標
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(true);

        if (PhotoFrameRect != null && targetTf != null)
            yield return StartCoroutine(MoveFrameToTargetAny(PhotoFrameRect, targetTf, photoFrameAutoMoveSpeed));

        // 6) 等玩家點擊框（教學內不跟指標，純等點）
        bool clicked = false;
        while (!clicked)
        {
            if (PointerDownThisFrame() && IsPointerOverUI(PhotoFrameImage != null ? PhotoFrameImage.gameObject : null))
                clicked = true;

            yield return null;
        }

        // 7) 點框 = 快門 → 閃光（每次都要）
        if (HintText != null) HintText.text = "影像鎖定…";

        float flashExposure = 2.5f;
        yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
        yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));

        // 8) 正確面板顯示 3 秒（每次都要）
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(true);
        if (HintText != null) HintText.text = "✔ 異常影像已成功保存";

        yield return new WaitForSeconds(3f);

        // 9) 收尾：全部消失、回復燈光/曝光、玩家回來
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);

        if (HintText != null) HintText.gameObject.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (showSmile && smileTf != null) smileTf.gameObject.SetActive(false);

        if (ErrorPanel != null)
        {
            animationScript.Fade(ErrorPanel, 0.6f, 1f, 0f, null);
            yield return new WaitForSeconds(0.6f);
            ErrorPanel.SetActive(false);
        }
        if (ErrorPlaceTeach != null) ErrorPlaceTeach.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);

        // 光線恢復到進來前的 alpha
        if (ErrorLight != null)
            yield return StartCoroutine(AbnormalLight(0.6f, ErrorLight.color.a, prevAlpha));

        // 第二段暗下來的話，曝光恢復
        if (darkFirst)
            yield return StartCoroutine(fader.FadeExposure(1.0f, darkExposure, baseExposure));

        if (Player != null) Player.SetActive(playerWasActive);
        if (cControllScript != null) cControllScript.playerControlEnabled = prevControl;
    }


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
            yield return null;
        }
    }


    void FollowPointer(RectTransform frame)
    {
        if (frame == null || UICanvas == null) return;

        Vector2 screenPos = Input.mousePosition;
        if (Input.touchCount > 0) screenPos = Input.GetTouch(0).position;

        RectTransform canvasRect = UICanvas.transform as RectTransform;
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, UICanvas.worldCamera, out localPoint))
        {
            frame.anchoredPosition = Vector2.Lerp(frame.anchoredPosition, localPoint, photoFrameFollowSpeed * Time.deltaTime);
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
        if (ErrorPlaceTeach != null) ErrorPlaceTeach.SetActive(false);

        if (ErrorPanel != null)
        {
            // 你要淡出也行，我這邊直接關
            ErrorPanel.SetActive(false);
        }
    }


    IEnumerator SceneFlow()
    {
        //0.鎖連續跳過劇情
        dialogueSystemGame00Script.allowFastReveal = false;

        // 1. 黑幕淡出
        //if (BlackPanel != null)
        //{
        //    BlackPanel.SetActive(true);
        //    animationScript.Fade(
        //        BlackPanel,
        //        1.5f/*持續時間*/,
        //        1f,
        //        0f,
        //        null
        //    );

        //    yield return new WaitForSeconds(1.5f);
        //    BlackPanel.SetActive(false);
        //}
        //yield return StartCoroutine(postExposureFaderScript.FadeExposure(lightUPDuration/*持續時間*/, -3f, 0f));
        yield return StartCoroutine(fader.FadeExposure(1.5f/*持續時間*/, -10f/*起始*/, 0.5f/*終點*/));
        yield return new WaitForSeconds(2);


        // 2. 鏡頭回到公車內（先把鏡頭放在起點，再移到目標）
        //cameraMoveControllScript.camera.transform.position = CurrentPos;
        Debug.Log($"[CameraMove] TargetPos (field) = {TargetPoint}");
        Debug.Log($"[CameraMove] Cam before = {cameraMoveControllScript.cam.transform.position}");
        cameraMoveControllScript.cam.transform.position = StartPoint.position;
        yield return StartCoroutine(cameraMoveControllScript.MoveCameraTo(TargetPoint.position, MoveSpeed));

        Debug.Log($"[CameraMove] Cam after  = {cameraMoveControllScript.cam.transform.position}");
        
        yield return new WaitForSeconds(2);

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

        //4.對話開始
        dialogueSystemGame00Script.StartDialogue(dialogueSystemGame00Script.TextfileGame00);
        // ⏳ 等到整段對話播完
        yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);

        Player.SetActive(false);
        BlackPanel.SetActive(false);
        cControllScript.playerControlEnabled = false;
        PhonePanel.SetActive(false);
        // ✅ 跑教學拍照流程
        // 第一段：把框移到你指定的異常目標
        yield return StartCoroutine(AbnormalTeach_1());



        //// ✅ 教學結束後，再開始找錯倒數（如果你還需要）
        //timer.StartCountdown(15);
        //ErrorStart = true;
        //errorResultHandled = false;

        Debug.Log("中間劇情");

        // 第二段：暗下來 + smile
        yield return StartCoroutine(AbnormalTeach_2());


        ////2.1紅光亮起
        //redLight();
        //yield return new WaitUntil(() => ErrorLight.color.a==1f);

        //////2.1對話
        ////DSG00.StartDialogue(DSG00.TextfileLookPhone);
        ////yield return new WaitForSeconds(1f);

        ////2.2看手機
        //cControllScript.animator.SetBool("phone", true);
        //yield return StartCoroutine(WaitForAnimation(cControllScript.animator, "phone"));
        ////yield return new WaitForSeconds(0.5f);
        //hasPressedCamera = false;
        //// ⏳ 在這裡乖乖等玩家按
        //yield return new WaitUntil(() => hasPressedCamera);

        //// 玩家已經按了相機，可以收手機 UI、結束手機動畫
        //PhonePanel.SetActive(false);
        //cControllScript.animator.SetBool("phone", false);

        ////3.errorpanel亮起
        //// 🔥 紅光閃完 → 顯示異常提示 Panel
        //Player.SetActive(false);
        //openErrorPanel();

        ////4.等error面板出現再開始倒數計時
        //yield return new WaitUntil(() => ErrorPanel.GetComponent<CanvasGroup>().alpha == 1);

        ////5.開始倒數計時
        //timer.StartCountdown(15);

        ////6.開始找錯
        //ErrorStart = true;
        //errorResultHandled = false;
    }

    //第一段：你給一個異常目標 Transform（你說你會自己拉）
    public IEnumerator AbnormalTeach_1()
    {
        // 不暗、不顯示 smile、不等待
        yield return StartCoroutine(AbnormalCaptureFlow(PhotoFrameTeachTarget, darkFirst: false, showSmile: false, waitSmileSeconds: 0f));
    }

    //第二段：固定是 smile（暗下來 → 開面板 → 等 3 秒 smile 出現 → 移到 smile）
    public IEnumerator AbnormalTeach_2()
    {
        yield return StartCoroutine(AbnormalCaptureFlow(null, darkFirst: true, showSmile: true, waitSmileSeconds: 3f));
    }


    // Update is called once per frame
    void Update()
    {
        // 跳過劇情（你也可以改成 UI Button 來呼叫 SkipToTeaching()）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SkipToTeaching();
        }

        timer.timerText.gameObject.SetActive(ErrorPanel.activeSelf);
        if (spotManager == null) return;

        if (!ErrorStart || errorResultHandled) return;
        // 🔎 檢查目前找到幾個異常

        //1. 成功條件：找到全部，且時間還沒負數
        if (ErrorStart && spotManager.foundCount >= spotManager.totalCount && timer.currentTime >= 0f)
        {
            errorResultHandled = true;
            ErrorStart = false;   // 關閉這一輪檢查
            timer.ForceEnd();
            StartCoroutine(OnErrorComplete()); // 通關
        }
        else if(ErrorStart && timer.currentTime <= 0f && spotManager.foundCount < spotManager.totalCount)//2. 失敗條件：時間 < 0 且還沒找完
        {
            //遊戲失敗
            errorResultHandled = true;
            ErrorStart = false;
            timer.ForceEnd();
            StartCoroutine(ErrorMistake());   // 失敗
        }
    }

    public void SkipToTeaching()
    {
        if (skipRequested) return; // 防連按
        skipRequested = true;

        // 停掉 SceneFlow（避免後面又跑回來接著演）
        if (sceneFlowRoutine != null)
        {
            StopCoroutine(sceneFlowRoutine);
            sceneFlowRoutine = null;
        }

        // 停掉這個 First 身上所有協程（鏡頭、燈光閃爍、教學前演出等）
        StopAllCoroutines();

        // 停掉 Dialogue（避免它還在打字 / 忙碌）
        if (dialogueSystemGame00Script != null)
        {
            dialogueSystemGame00Script.SetPanels(false, false);
            dialogueSystemGame00Script.StopTyping();
            dialogueSystemGame00Script.isTyping = false;
            dialogueSystemGame00Script.FirstDiaFinished = true; // 保險：避免任何 WaitUntil 卡住
        }

        // 停掉紅光閃爍（你目前 warningCoroutine 沒 Stop，但至少清掉旗標）
        warningCoroutine = null;

        // 直接進入「教學前」狀態，然後開始教學
        EnterTeachingStateAndStart();
    }

    private void EnterTeachingStateAndStart()
    {
        // 1) 把會干擾教學的 UI 全部收乾淨
        if (BlackPanel != null) BlackPanel.SetActive(false);
        if (PhonePanel != null) PhonePanel.SetActive(false);

        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);

        if (HintText != null) HintText.gameObject.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);

        // smile 是第二段才用，先收掉
        if (smileTf != null) smileTf.gameObject.SetActive(false);

        // 2) 玩家先隱藏/鎖控制（跟你原本教學流程一致）
        if (Player != null) Player.SetActive(false);
        if (cControllScript != null) cControllScript.playerControlEnabled = false;

        // 3) 把「光線/曝光」設回你教學開始時希望的基準
        // 你 SceneFlow 開頭結束在 exposure=0.5，所以我們直接把它設到這裡
        if (fader != null) StartCoroutine(fader.FadeExposure(0.1f, -10f, 0.5f)); // 快速拉回可見
        if (ErrorLight != null)
        {
            // 教學前通常不需要紅光殘留
            StartCoroutine(AbnormalLight(0.2f, ErrorLight.color.a, 0f));
        }

        // 4) ErrorPanel 先確保是關的（TeachPhotoFlow 會自己 openErrorPanel）
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (ErrorPlaceTeach != null) ErrorPlaceTeach.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);

        // 5) 最重要：直接開始教學（第一段）
        StartCoroutine(AbnormalTeach_1());

        // 如果你想跳過直接到第二段教學，改成：
        // StartCoroutine(TeachSmileStep_InSceneFlow());
    }

    public IEnumerator AbnormalLight(float duration,float start, float end)//讓窗外異常光線啟動（瞬間變紅、變亮）
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

            
            if (ErrorPlaceTeach != null)
                ErrorPlaceTeach.SetActive(false); // 關閉異常提示界面
            spotManager.ClearAllCircles();
            ErrorStart = false;
            CirclePlace.SetActive(false);
            animationScript.Fade(ErrorPanel,2f,1f,0f,null);
            yield return new WaitForSeconds(2f);
            ErrorPanel.SetActive(false);
            Player.SetActive(true);

            // 恢復正常光線
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(AbnormalLight(2f,1f,0f));
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
        if (ErrorPlace != null)
        {
            Debug.Log("[First] 顯示異常提示畫面 ErrorPlace");
            ErrorPlaceTeach.SetActive(true);
            ErrorPanel.SetActive(true);
            animationScript.Fade(ErrorPanel, 2f, 0f, 1f, null);
            CirclePlace.SetActive(true);
            spotManager.RefreshActiveSpots();
            
        }
    }

    public void OnCameraButtonClicked()
    {
        Debug.Log("[First] 玩家按下手機裡的相機按鈕");
        hasPressedCamera = true;
    }

    public IEnumerator ErrorMistake()//遊戲失敗一次
    {
        Mistake += 1;
        RedPanel.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        RedPanel.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        RedPanel.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        RedPanel.SetActive(false);
        yield return new WaitForSeconds(1.5f);
        ErrorPanel.SetActive(false);
        Player.SetActive(true);
        cControllScript.animator.SetBool("die", true);
    }
}
