using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;



public enum SceneCheckpoint
{
    Start = 0,
    AfterFadeIn = 10,
    AfterCameraMove = 20,
    AfterRoofBlink = 30,
    AfterDialogue = 40,
    AfterTeach1 = 50,
    AfterTeach2 = 60
}

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

    [Header("拍照框點擊狀態")]
    public bool photoFrameClicked = false;

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
    public Coroutine teachRoutine;
    private bool requestTeach1 = false;
    private bool requestTeach2 = false;
    public void RequestTeach1() => requestTeach1 = true;
    public void RequestTeach2() => requestTeach2 = true;


    [Header("UI Raycaster")]
    public Canvas UICanvas;                       // 你的 UI Canvas
    public GraphicRaycaster uiRaycaster;          // Canvas 上的 GraphicRaycaster
    public EventSystem eventSystem;               // 場景 EventSystem

    [Header("拍照結果面板")]
    public GameObject WrongPhotoPanel;            // 可選：如果你也想有錯誤面板


    [Header("手機 UI")]
    [Tooltip("顯示在畫面上的手機介面 Panel")]public GameObject PhonePanel;
    [Tooltip("手機裡的『相機』按鈕")]public UnityEngine.UI.Button CameraButton;
    [Tooltip("紀錄玩家有沒有按相機")]public bool hasPressedCamera = false;

    [Header("拍照流程")]
    [Tooltip("正確照片顯示的 Panel")] public GameObject CorrectPhotoPanel;          // 正確照片顯示的 Panel
    [Tooltip("快門按鈕（拍照按鈕）")] public Button ShutterButton;                  // 快門按鈕（拍照按鈕）
    [Tooltip("玩家有沒有按快門")] public bool hasPressedShutter = false;        // 玩家有沒有按快門
    [Tooltip("拍照後要接的對話腳本（可選）")] public TextAsset TextfileAfterPhoto;          // 拍照後要接的對話腳本（可選）
    [Header("照片顯示")]
    [Tooltip("顯示照片的 Panel（可選，用來整組開關）")]
    public GameObject PhotoPanel;

    [Tooltip("照片槽位：元素0=01、元素1=02...（把 picture01、picture02 依序拖進來）")]
    public Image[] pictures;


    [Header("其他")]
    public GameObject BlackPanel;//黑色遮罩
    [Tooltip("控制紅光閃爍的協程")] Coroutine warningCoroutine;
    [Header("遊戲失敗")]
    [Tooltip("紅色面板")]public GameObject RedPanel;
    [Tooltip("失敗次數")] public int Mistake;
    // 避免重複判定，用一個旗標
    [Tooltip("避免重複判定")]public bool errorResultHandled = false;

    private enum FlowStage
    {
        Cutscene,        // 劇情段（可跳）
        Teaching,        // 教學段（先不給跳）
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
            // 你如果想淡入，也可以用 animationScript.Fade(CorrectPhotoPanel, ……)
        }

        // G) 繼續對話
        if (TextfileAfterPhoto != null)
        {
            dialogueSystemGame00Script.StartDialogue(TextfileAfterPhoto);
            yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);
        }
    }

    private void ApplyCheckpointState(SceneCheckpoint cp)
    {
        // --- 共通：把一些可能殘留的協程效果停掉/歸零 ---
        if (warningCoroutine != null) { StopCoroutine(warningCoroutine); warningCoroutine = null; }

        // UI/面板先收乾淨（避免跳過時殘留）
        if (HintText != null) HintText.gameObject.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (ErrorPlace != null) ErrorPlace.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);
        if (PhonePanel != null) PhonePanel.SetActive(false);
        if (smileTf != null) smileTf.gameObject.SetActive(false);

        // 黑幕一定關（你現在流程是曝光淡入）
        if (BlackPanel != null) BlackPanel.SetActive(false);

        // 車頂燈：劇情跑完閃爍後是亮的
        if (BusUpLightTotal != null) BusUpLightTotal.SetActive(true);

        // 紅光：一般劇情到 Teach1 結束後應該回到原本（你預設 alpha=0）
        if (ErrorLight != null)
        {
            var c = ErrorLight.color;
            c.a = 0f;
            ErrorLight.color = c;
        }

        // 曝光：你的 SceneFlow 開頭最後是 0.5
        // ⚠️ 最穩：直接把曝光設到終點（不要再 FadeExposure）
        if (fader != null) fader.SetExposureImmediate(0.5f);
        // ↑ 你需要在 FadeInByExposure 裡加一個 SetExposureImmediate(float v)（下面我給你）

        // 相機：劇情正常走完鏡頭會到 TargetPoint
        if (cameraMoveControllScript != null && cameraMoveControllScript.cam != null && TargetPoint != null)
        {
            cameraMoveControllScript.cam.transform.position = TargetPoint.position;
        }

        // --- 依 checkpoint 決定玩家/控制狀態 ---
        // 在你原本流程：對話後會把玩家藏起來、鎖控制，然後進 Teach1/Teach2
        if (cp < SceneCheckpoint.AfterTeach2)
        {
            if (Player != null) Player.SetActive(false);
            if (cControllScript != null) cControllScript.playerControlEnabled = false;
        }
        else
        {
            if (Player != null) Player.SetActive(true);
            if (cControllScript != null) cControllScript.playerControlEnabled = true;
        }

        dialogueSystemGame00Script.currentCP = cp;
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

        //// 1) 需要暗下來才做（第二段）
        //if (darkFirst)
        //    yield return StartCoroutine(fader.FadeExposure(1.0f, baseExposure, darkExposure));

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

        //if (PhotoFrameRect != null && targetTf != null)
        //    yield return StartCoroutine(MoveFrameToTargetAny(PhotoFrameRect, targetTf, photoFrameAutoMoveSpeed));

        // 6) 等玩家點擊拍照框（改成 Button OnClick）
        photoFrameClicked = false;

        // 拿到 Button
        var btn = PhotoFrameImage != null ? PhotoFrameImage.GetComponent<Button>() : null;

        // 在「移動期間」先鎖住，避免還沒到位就被點
        if (btn != null) btn.interactable = false;

        // 只移動一次：移到指定目標
        if (PhotoFrameRect != null && targetTf != null)
            yield return StartCoroutine(MoveFrameToTargetAny(PhotoFrameRect, targetTf, photoFrameAutoMoveSpeed));

        // 移完再允許點
        if (btn != null) btn.interactable = true;

        photoFrameClicked = false;
        yield return new WaitUntil(() => photoFrameClicked);

        if (btn != null) btn.interactable = false;



        // 7) 點框 = 快門 → 閃光
        if (HintText != null) HintText.text = "影像鎖定…";

        float flashExposure = 2.5f;
        yield return StartCoroutine(fader.FadeExposure(0.1f, baseExposure, flashExposure));
        yield return StartCoroutine(fader.FadeExposure(0.9f, flashExposure, baseExposure));

        // 8) 顯示正確面板並等待 3 秒（期間不恢復、不淡出）
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(true);
        if (HintText != null) HintText.text = "異常影像已成功保存";

        yield return new WaitForSeconds(3f);

        // 9) 3 秒後才開始淡出（面板淡出）
        if (CorrectPhotoPanel != null)
        {
            // 如果你 CorrectPhotoPanel 也想淡出，前提：它有 CanvasGroup
            CanvasGroup correctCg = CorrectPhotoPanel.GetComponent<CanvasGroup>();
            if (correctCg != null)
            {
                // 先確保 alpha 是 1
                correctCg.alpha = 1f;
                animationScript.Fade(CorrectPhotoPanel, 0.6f, 1f, 0f, null);
                yield return new WaitForSeconds(0.6f);
            }
            CorrectPhotoPanel.SetActive(false);
        }

        // Hint 也收掉
        if (HintText != null) HintText.gameObject.SetActive(false);

        // 拍照框與 smile 收掉
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (showSmile && smileTf != null) smileTf.gameObject.SetActive(false);

        // ErrorPanel 淡出（你要「恢復原本狀況」= 這裡才開始）
        if (ErrorPanel != null)
        {
            animationScript.Fade(ErrorPanel, 0.6f, 1f, 0f, null);
            yield return new WaitForSeconds(0.6f);
            ErrorPanel.SetActive(false);
        }
        if (ErrorPlace != null) ErrorPlace.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);

        //// 光線恢復到進來前的 alpha
        //if (ErrorLight != null)
        //    yield return StartCoroutine(AbnormalLight(0.6f, ErrorLight.color.a, prevAlpha));

        //// 第二段暗下來的話，曝光恢復
        //if (darkFirst)
        //    yield return StartCoroutine(fader.FadeExposure(1.0f, darkExposure, baseExposure));

        // 玩家回來
        if (Player != null) Player.SetActive(true);
        if (cControllScript != null) cControllScript.playerControlEnabled = prevControl;

        photoFrameClicked = false;

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
            Debug.Log($"[MoveFrameToTargetAny] dist={Vector2.Distance(frame.anchoredPosition, localPoint)} localPoint={localPoint}");

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
        if (ErrorPlace != null) ErrorPlace.SetActive(false);

        if (ErrorPanel != null)
        {
            // 你要淡出也行，我這邊直接關
            ErrorPanel.SetActive(false);
        }
    }


    IEnumerator SceneFlow()
    {
        Debug.Log($"[SceneFlow] Start id={GetInstanceID()}");
        stage = FlowStage.Cutscene;
        //0.鎖連續跳過劇情
        stage = FlowStage.Cutscene;
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
        // 1) 曝光淡入
        if (!dialogueSystemGame00Script. skipRequested)
        {
            yield return StartCoroutine(fader.FadeExposure(1.5f, -10f, 0.5f));
            yield return new WaitForSeconds(2f);
        }
        else
        {
            // 直接把曝光設到「看完劇情後」一致
            fader.SetExposureImmediate(0.5f);
        }
        ApplyCheckpointState(SceneCheckpoint.AfterFadeIn);


        // 2) 鏡頭移動
        if (!dialogueSystemGame00Script.skipRequested)
        {
            cameraMoveControllScript.cam.transform.position = StartPoint.position;
            yield return StartCoroutine(cameraMoveControllScript.MoveCameraTo(TargetPoint.position, MoveSpeed));
            yield return new WaitForSeconds(2f);
        }
        else
        {
            // 直接到位
            cameraMoveControllScript.cam.transform.position = TargetPoint.position;
        }
        ApplyCheckpointState(SceneCheckpoint.AfterCameraMove);

        // 3) 車頂燈閃
        if (!dialogueSystemGame00Script.skipRequested)
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
        }
        else
        {
            // 跳過後，燈光要跟「劇情看完」一致：最後是亮著
            BusUpLightTotal.SetActive(true);
        }
        ApplyCheckpointState(SceneCheckpoint.AfterRoofBlink);


        // 4) 對話
        if (!dialogueSystemGame00Script.skipRequested)
        {
            dialogueSystemGame00Script.StartDialogue(dialogueSystemGame00Script.TextfileGame00);
            yield return new WaitUntil(() => dialogueSystemGame00Script.FirstDiaFinished);
        }
        else
        {
            // 跳過後：把對話系統收掉，並避免 WaitUntil 卡住
            if (dialogueSystemGame00Script != null)
            {
                dialogueSystemGame00Script.StopTyping();
                dialogueSystemGame00Script.isTyping = false;
                dialogueSystemGame00Script.SetPanels(false, false);
                dialogueSystemGame00Script.FirstDiaFinished = true;
            }
        }
        ApplyCheckpointState(SceneCheckpoint.AfterDialogue);

        // ---- 到這裡：劇情段結束，切到教學段（先不給跳） ----
        stage = FlowStage.Teaching;

        Player.SetActive(false);
        BlackPanel.SetActive(false);
        cControllScript.playerControlEnabled = false;
        PhonePanel.SetActive(false);
        // 5) Teach1
        // 教學前「標準狀態」統一一下，確保跳過跟乖乖看完一樣
        ApplyBeforeTeachingState();

        // Teach 1
        teachRoutine = StartCoroutine(AbnormalTeach_1());
        yield return teachRoutine;
        teachRoutine = null;
        ApplyCheckpointState(SceneCheckpoint.AfterTeach1);

        Debug.Log("中間劇情");

        // Teach 2
        teachRoutine = StartCoroutine(AbnormalTeach_2());
        yield return teachRoutine;
        teachRoutine = null;
        ApplyCheckpointState(SceneCheckpoint.AfterTeach2);


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

    private void ApplyBeforeTeachingState()
    {
        // 你原本在 SceneFlow 進教學前做的那些狀態統一在這裡
        if (BlackPanel != null) BlackPanel.SetActive(false);
        if (PhonePanel != null) PhonePanel.SetActive(false);

        // 你的教學流程本來就是先把玩家藏起來並鎖控制
        if (Player != null) Player.SetActive(false);
        if (cControllScript != null) cControllScript.playerControlEnabled = false;

        // 教學 UI 預設收乾淨（避免跳過殘留）
        if (HintText != null) HintText.gameObject.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);
        if (smileTf != null) smileTf.gameObject.SetActive(false);

        // Error 面板類
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (ErrorPlace != null) ErrorPlace.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);

        // 光線/曝光對齊到你「劇情完畢」常態
        if (fader != null) fader.SetExposureImmediate(0.5f);
        if (ErrorLight != null)
        {
            var c = ErrorLight.color;
            c.a = 0f;
            ErrorLight.color = c;
        }

        // 鏡頭保險到位（防跳過）
        if (cameraMoveControllScript != null && cameraMoveControllScript.cam != null && TargetPoint != null)
            cameraMoveControllScript.cam.transform.position = TargetPoint.position;

        // 劇情跳過後不要再影響後面
        dialogueSystemGame00Script.skipRequested = false;
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
            // 只允許劇情段跳過；教學段你想給跳再改條件
            if (stage == FlowStage.Cutscene)
            {
                // 先把教學/面板殘留收乾淨（很重要）
                ApplyBeforeTeachingState();

                // 叫對話系統自己找下一個必到 label
                dialogueSystemGame00Script.SkipToNextMandatoryLabel();
                return;
            }
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
        //如果被請求，且目前沒有教學在跑，就開教學
        if (requestTeach1)
        {
            requestTeach1 = false;
            teachRoutine = StartCoroutine(AbnormalTeach_1());
            StartCoroutine(ClearTeachRoutineWhenDone(teachRoutine));
        }
        else if (requestTeach2)
        {
            requestTeach2 = false;
            teachRoutine = StartCoroutine(AbnormalTeach_2());
            StartCoroutine(ClearTeachRoutineWhenDone(teachRoutine));
        }
    }

    private IEnumerator ClearTeachRoutineWhenDone(Coroutine routine)
    {
        yield return routine;
        teachRoutine = null;
    }

    public void SkipToTeaching()
    {
        if (dialogueSystemGame00Script.skipRequested) return;
        dialogueSystemGame00Script.skipRequested = true;

        // 你想跳到哪？你目前需求是：「Teach1結束後還要繼續印中間劇情、跑Teach2」
        dialogueSystemGame00Script.skipToCP = SceneCheckpoint.AfterTeach1;

        // 強制把畫面對齊到「AfterTeach1」的標準狀態
        ApplyCheckpointState(dialogueSystemGame00Script.skipToCP);

        // 不要 StopAllCoroutines()
        // 不要 StopCoroutine(sceneFlowRoutine)
    }


    private void EnterTeachingStateOnly()
    {
        if (BlackPanel != null) BlackPanel.SetActive(false);
        if (PhonePanel != null) PhonePanel.SetActive(false);

        if (CorrectPhotoPanel != null) CorrectPhotoPanel.SetActive(false);
        if (WrongPhotoPanel != null) WrongPhotoPanel.SetActive(false);
        if (HintText != null) HintText.gameObject.SetActive(false);
        if (PhotoFrameImage != null) PhotoFrameImage.gameObject.SetActive(false);
        if (smileTf != null) smileTf.gameObject.SetActive(false);

        if (Player != null) Player.SetActive(false);
        if (cControllScript != null) cControllScript.playerControlEnabled = false;

        if (fader != null) StartCoroutine(fader.FadeExposure(0.1f, -10f, 0.5f));
        if (ErrorLight != null) StartCoroutine(AbnormalLight(0.2f, ErrorLight.color.a, 0f));

        // 鏡頭強制到位
        if (cameraMoveControllScript != null && cameraMoveControllScript.cam != null && TargetPoint != null)
            cameraMoveControllScript.cam.transform.position = TargetPoint.position;

        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        if (ErrorPlace != null) ErrorPlace.SetActive(false);
        if (CirclePlace != null) CirclePlace.SetActive(false);
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

            
            if (ErrorPlace != null)
                ErrorPlace.SetActive(false); // 關閉異常提示界面
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
            ErrorPlace.SetActive(true);
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

    public void OnPhotoFrameClicked()
    {
        Debug.Log("[First] 玩家點擊拍照指引框");
        photoFrameClicked = true;
    }

    // 燈光閃爍一次（你可自己調節節奏）
    public IEnumerator LightFlickerOnce()
    {
        if (BusUpLightTotal == null) yield break;

        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.06f);
        BusUpLightTotal.SetActive(true);
        yield return new WaitForSeconds(0.06f);
        BusUpLightTotal.SetActive(false);
        yield return new WaitForSeconds(0.12f);
        BusUpLightTotal.SetActive(true);
    }

    //// 車子強烈搖晃（最簡單先做相機震動；你也可以改成整台車的 root 在抖）
    //public IEnumerator BusShakeStrong(float duration)
    //{
    //    if (cameraMoveControllScript == null || cameraMoveControllScript.cam == null)
    //        yield break;

    //    Transform camTf = cameraMoveControllScript.cam.transform;
    //    Vector3 origin = camTf.position;

    //    float t = 0f;
    //    while (t < duration)
    //    {
    //        t += Time.deltaTime;
    //        camTf.position = origin + (Vector3)Random.insideUnitCircle * 0.15f;
    //        yield return null;
    //    }

    //    camTf.position = origin;
    //}
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
        // TODO: 做放大演出
        yield return new WaitForSeconds(0.8f);
    }

    // 時間跳轉：你如果有時間 UI 文字就改它
    public void SetTimeText(string timeText)
    {
        Debug.Log($"[First] TimeJump -> {timeText}");
        // TODO: timerText 或手機時間欄位改成 timeText
    }

    // 顯示遊戲標題（你可以做一個 TitlePanel）
    public IEnumerator ShowGameTitle()
    {
        Debug.Log("[First] ShowGameTitle");
        // TODO: TitlePanel.SetActive(true); 淡入淡出
        yield return new WaitForSeconds(1.2f);
    }

    public IEnumerator RunTeach1()
    {
        ApplyBeforeTeachingState();
        teachRoutine = StartCoroutine(AbnormalTeach_1());
        yield return teachRoutine;
        teachRoutine = null;
    }

    public IEnumerator RunTeach2()
    {
        ApplyBeforeTeachingState();
        teachRoutine = StartCoroutine(AbnormalTeach_2());
        yield return teachRoutine;
        teachRoutine = null;
    }

}
