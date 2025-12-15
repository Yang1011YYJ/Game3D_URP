using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.UI;

public class First : MonoBehaviour
{
    [Header("腳本")]
    public AnimationScript animationScript;
    public CControll cControllScript;
    [Tooltip("場景中負責計算找錯誤數量的管理員")]public SpotManager spotManager;
    public DialogueSystemGame00 DSG00;
    public TimeControll timer;
    public CameraMoveControll cameraMoveControllScript;

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

    [Header("玩家")]
    public GameObject Player;
    [Tooltip("玩家教學用自動走到的位置")]public Vector2 teachTargetPos = new Vector2(19.3f, -4.3f);

    [Header("相機")]
    public Vector3 CurrentPos;
    public Vector3 TargetPos;
    public float MoveSpeed =5f;
    public float lightUPDuration = 1.2f; 

    [Header("教學")]
    [Tooltip("查看教學")] public bool CheckTeach = false;

    [Header("手機 UI")]
    [Tooltip("顯示在畫面上的手機介面 Panel")]
    public GameObject PhonePanel;
    [Tooltip("手機裡的『相機』按鈕")]
    public UnityEngine.UI.Button CameraButton;
    [Tooltip("紀錄玩家有沒有按相機")]public bool hasPressedCamera = false;

    [Header("其他")]
    public GameObject BlackPanel;//黑色遮罩
    [Tooltip("控制紅光閃爍的協程")] Coroutine warningCoroutine;
    [Header("遊戲失敗")]
    [Tooltip("紅色面板")]public GameObject RedPanel;
    [Tooltip("失敗次數")] public int Mistake;
    // 避免重複判定，用一個旗標
    [Tooltip("避免重複判定")]public bool errorResultHandled = false;
    

    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        DSG00 = FindAnyObjectByType<DialogueSystemGame00>();
        timer = FindAnyObjectByType<TimeControll>();
        cameraMoveControllScript = FindAnyObjectByType<CameraMoveControll>();
        if (cControllScript == null)
        {
            cControllScript = FindAnyObjectByType<CControll>();
        }
        if (spotManager == null)
        {
            spotManager = FindAnyObjectByType<SpotManager>();
        }
    }
    private void Start()
    {
        ErrorPanel.SetActive(false);
        ErrorPlace.SetActive(false);
        //CirclePlaceTeach.SetActive(false);
        ErrorPlaceTeach.SetActive(false);
        RedPanel.SetActive(false);
        ErrorLight.color = new Color(1, 0, 0, 0);

        if (PhonePanel != null)
            PhonePanel.SetActive(false);

        hasPressedCamera = false;

        eT1 = false;
        ErrorStart = false;

        // 整個場景流程交給協程控制，Start 只負責開頭
        StartCoroutine(SceneFlow());

    }

    IEnumerator SceneFlow()
    {
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


        // 2. 鏡頭回到公車內（先把鏡頭放在起點，再移到目標）
        cameraMoveControllScript.camera.transform.position = CurrentPos;
        yield return StartCoroutine(cameraMoveControllScript.MoveCameraTo(TargetPos, MoveSpeed));

        //3.車頂燈光閃爍


        //2.1紅光亮起
        redLight();
        yield return new WaitUntil(() => ErrorLight.color.a==1f);

        //2.1對話
        DSG00.StartDialogue(DSG00.TextfileLookPhone);
        yield return new WaitForSeconds(1f);

        //2.2看手機
        cControllScript.animator.SetBool("phone", true);
        yield return StartCoroutine(WaitForAnimation(cControllScript.animator, "phone"));
        //yield return new WaitForSeconds(0.5f);
        hasPressedCamera = false;
        // ⏳ 在這裡乖乖等玩家按
        yield return new WaitUntil(() => hasPressedCamera);

        // 玩家已經按了相機，可以收手機 UI、結束手機動畫
        PhonePanel.SetActive(false);
        cControllScript.animator.SetBool("phone", false);

        //3.errorpanel亮起
        // 🔥 紅光閃完 → 顯示異常提示 Panel
        Player.SetActive(false);
        openErrorPanel();

        //4.等error面板出現再開始倒數計時
        yield return new WaitUntil(() => ErrorPanel.GetComponent<CanvasGroup>().alpha == 1);

        //5.開始倒數計時
        timer.StartCountdown(15);

        //6.開始找錯
        ErrorStart = true;
        errorResultHandled = false;
    }

    // Update is called once per frame
    void Update()
    {
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
