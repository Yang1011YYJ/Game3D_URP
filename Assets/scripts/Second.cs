using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class Second : MonoBehaviour
{
    [Header("Phase")]
    public GamePhase currentPhase = GamePhase.Playing;//一定要設值不然會是0或是第一個

    [Header("腳本")]
    public SpotManager spotManager;
    public TimeControll timer;
    public DialogueSystemGame01 dialogueSystemGame01Script;
    public FadeInByExposure fader;
    public CControll cControllScript;
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;

    [Header("遊戲用UI")]
    public GameObject ErrorPanel;
    public GameObject RedPanel;
    public GameObject PhotoFrameImage;
    public TextMeshProUGUI HintText;
    [Tooltip("Round UI")]
    public TextMeshProUGUI countText; // 顯示「找到 X / >5」

    [Header("題庫")]
    public List<SpotDef> spotPool = new();          // Inspector 拖 8 題
    private List<SpotDef> bag = new();              // 抽題用袋子（不放回）
    private SpotDef currentDef;

    [Header("ErrorPanel 圖片顯示")]
    public Image errorPictureImage;                 // ErrorPanel 上用來顯示題目的 Image（Inspector拖）


    [Header("其他UI")]
    public GameObject PhonePanel;
    [Tooltip("玩家對話框還看的到")] public GameObject BlackPanel;
    [Tooltip("玩家對話框看不到")] public GameObject BlackPanel22;
    [Tooltip("遊戲名字")] public GameObject GameName;

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
    [Tooltip("跑到前面去看司機")] public Transform WalkToFrontPos;
    public Transform PlayerStartPos;

    [Header("車子相關")]
    [Tooltip("車子本體")] public Rigidbody busRb;

    [Header("Game Settings")]
    public int roundSeconds = 15;
    // ===== Round gate: 讓 Act_GameStart 等到回合結束才放行 =====
    private bool _roundFinished = false;
    private bool _roundFlowDone = false;
    private RoundEndType _lastEndType;

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
        CleanupUI();//關掉遊戲用UI
        CleanupRoundUI();
        
        if (dialogueSystemGame01Script != null)
            dialogueSystemGame01Script.BindOwner(this);

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
        // 每次開始一回合前，重置閘門
        _roundFinished = false;
        _roundFlowDone = false;

        ErrorPanel.SetActive(true);
        PhotoFrameImage.gameObject.SetActive(true);
        PhonePanel.gameObject.SetActive(false);

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

    private IEnumerator StartNormalRound()//開始正式遊戲
    {
        Debug.Log("[First] Normal round start");

        SetupTargets();      // ← 唯一差異點（教學 vs 隨機）
        OpenErrorPanel();     // ✅ 保證 ErrorPanel 會亮
        // ✅ 保證拍照框會出現且跟隨
        EnablePhotoFrameFollow();
        HideTeachHint();

        // 開回合
        spotManager.BeginRound();

        // Timer 超時 = 當成失誤
        timer.onTimeUp = () =>
        {
            Debug.Log("[First] Timeout");
            spotManager.OnTimeout();
        };

        timer.StartCountdown(roundSeconds);
        yield return null;
    }
    private void SetupTargets()
    {
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
        if (errorPictureImage != null)
            errorPictureImage.sprite = currentDef.questionSprite;

        // 6) （可選）清理 UI 或提示文字
        if (HintText != null)
        {
            HintText.gameObject.SetActive(true);
            HintText.text = "在時間內找出異常！";
        }
    }

    private void OpenErrorPanel()
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(true);
    }

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

        countText.gameObject.SetActive(true); // 要不要顯示你可控
        countText.text = $"{totalFound} / {spotManager.totalRounds}";
    }

    // =====================================================
    // 🎉 成功 / 失敗演出
    // =====================================================

    private IEnumerator HandleSuccess()//點到正確位置
    {
        Debug.Log("[First] Success feedback");

        // TODO：成功閃光 / 正確提示
        yield return new WaitForSeconds(1.2f);

        CleanupRoundUI();
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

        CleanupRoundUI();
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
    }

    private void CleanupRoundUI()//關掉error面板
    {
        if (ErrorPanel != null) ErrorPanel.SetActive(false);
        // ✅ 建議加這行：關掉本題 spot，避免殘留到下一回合
        if (currentDef != null && currentDef.spotRoot != null)
            currentDef.spotRoot.SetActive(false);
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
    public IEnumerator Act_WalkToFront()
    {
        if (cControllScript == null || WalkToFrontPos == null) yield break;

        cControllScript.StartAutoMoveTo(WalkToFrontPos.position);

        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        yield return new WaitForSeconds(1f);
    }
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
    public IEnumerator Act_BlackPanelOn()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 0f, 1f, null);
        yield return new WaitForSeconds(1.5f);
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
    public IEnumerator Act_LeftRight()
    {
        //玩家裝有animator那個物件切換sprite到leftidle，然後用flip.x=true去做往右看的樣子，左右左右左右
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_SetTimeText(string time)
    {
        //指定ShowTimeText為19:30
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_ShowPhoto(string pictureName)
    {
        //顯示圖片
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_BigPictureZoom()
    {
        //放大圖片某處，放大位置我會放一個transform，朝著那裏放大就可以了
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_LightFlickerOnce()
    {
        //
        yield return new WaitForSeconds(1.5f);
    }
    public IEnumerator Act_ShowGameTitle()
    {
        GameName.SetActive(true);
        yield return new WaitForSeconds(1.5f);
    }
}
