using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class desC : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("顯示訊息")]public GameObject PhoneMessage;
    [Tooltip("關閉按鈕")] public GameObject CloseButton;
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
    [Tooltip("車子裡面模型")] public GameObject inside;

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
    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        cControllScript = Player.GetComponent<CControll>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();
        dialogueSystemDesScript = FindAnyObjectByType<DialogueSystemDes>();
        if (Player != null) cControllScript = FindAnyObjectByType<CControll>();
        if (fader == null) fader = FindAnyObjectByType<FadeInByExposure>();
        if (worldScrollerScript == null) worldScrollerScript = FindAnyObjectByType<WorldScroller>();

    }
    private void Start()
    {

        PlayerAnimator = cControllScript != null ? cControllScript.animator : null;

        if (PlayerAnimator == null)
        {
            // 保險：自己找一次
            PlayerAnimator = PlayerWithAnim.GetComponentInChildren<Animator>();
        }

        if (PlayerAnimator == null)
        {
            Debug.LogError("[desC] 找不到 Player 的 Animator，請檢查 Player 階層。", this);
        }
        // 初始 UI
        if (BlackPanel) BlackPanel.SetActive(false);
        if (PhoneMessage) PhoneMessage.SetActive(false);
        if (CloseButton) CloseButton.SetActive(false);
        if (DesPanel) DesPanel.SetActive(false);
        if (PlaceText) PlaceText.gameObject.SetActive(false);
        if (ring01) ring01.SetActive(false);
        if (ring02) ring02.SetActive(false);

        if (Bus) Bus.SetActive(false); // ✅ 等車階段不顯示

        // 直接開始跑劇本（你把完整劇本 TextAsset 拖給 DialogueSystemDes）
        if (dialogueSystemDesScript != null)
        {
            dialogueSystemDesScript.BindOwner(this);
            dialogueSystemDesScript.autoNextLine = false; // 你這段劇情比較像自動播放
            dialogueSystemDesScript.StartDialogue(dialogueSystemDesScript.Textfile01);
        }
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
        if (PhoneMessage) PhoneMessage.SetActive(false);
        if (CloseButton) CloseButton.SetActive(false);

        if (PlayerAnimator != null)
            PlayerAnimator.SetBool("phone", false);

        yield return new WaitForSeconds(1f);
    }

    public IEnumerator Act_PickUPPhone()
    {
        if (ring01) ring01.SetActive(false);
        if (ring02) ring02.SetActive(false);

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
        if (PhoneMessage) PhoneMessage.SetActive(false);
        //if (CloseButton) CloseButton.SetActive(false);
    }

    public IEnumerator Act_WalkInFromRight()
    {
        if (cControllScript == null || middlePoint == null) yield break;

        cControllScript.Target = middlePoint.position;
        cControllScript.StartAutoMoveTo(cControllScript.Target);
        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
    }

    public IEnumerator Act_BoardBus()
    {
        if (cControllScript == null || boardBusTarget == null) yield break;

        cControllScript.Target = boardBusTarget.position;
        cControllScript.autoMoveFinished = false;
        cControllScript.animator.SetBool("walk", true);
        cControllScript.isAutoMoving = true;

        yield return new WaitUntil(() => cControllScript.autoMoveFinished);

        // 🔒 關閉玩家控制，避免上車後亂動
        cControllScript.playerControlEnabled = false;

        // ✅ 重點：把玩家設成車的子物件
        Player.transform.SetParent(Bus.transform, true);

        cControllScript.animator.SetBool("walk", false);
    }

    public IEnumerator Act_PhoneRing()
    {
        if (ring01) ring01.SetActive(true);
        if (ring02) ring02.SetActive(true);

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
        yield return StartCoroutine(fader.FadeExposure(1, normalExposure, darkExposure));
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
        yield return new WaitForSeconds(2f);
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_NextScene(string sceneName)
    {
        StartCoroutine(fader.FadeExposure(1, normalExposure, darkExposure));
        yield return new WaitForSeconds(2f);
        sceneChangeScript.SceneC("01");
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
        if (inside) inside.SetActive(true);

        // 把玩家移到室內位置（兩種做法你選一個）

        // ✅做法1：瞬移（最穩、最不會卡）
        if (InsidePos != null && cControllScript != null)
        {
            Player.transform.position = InsidePos.position;
        }
        else if (InsidePos != null)
        {
            // 如果你玩家不是 cControllScript 那個物件，就改成你的 Player transform
            // playerTransform.position = InsidePos.position;
        }

        yield return new WaitForSeconds(1);
    }

    public IEnumerator Act_MoveToSit()
    {
        if (InsideSitPos == null || cControllScript == null) yield break;

        cControllScript.Target = InsideSitPos.position;
        cControllScript.StartAutoMoveTo(cControllScript.Target);
        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        yield return new WaitForSeconds(1);
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

}
