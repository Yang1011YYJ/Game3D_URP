using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [Tooltip("前半段火車")] public GameObject TrainFront;
    [Tooltip("後半段火車")] public GameObject TrainBack;
    [Tooltip("火車整體group")] public GameObject Train;
    [Tooltip("起始位置（畫面外）")] public Transform trainStartPoint;
    [Tooltip("停下來的位置（月台前）")] public Transform trainStopPoint;
    [Tooltip("火車移動速度")] public float trainSpeed = 5f;
    [Space]
    [Tooltip("說明面板")] public GameObject DesPanel;
    [Tooltip("黑色遮罩")] public GameObject BlackPanel;//
    [Tooltip("顯示地點的文字")] public TextMeshProUGUI PlaceText;

    [Header("角色")]
    public GameObject Player;
    Animator PlayerAnimator;
    [Tooltip("主角位置")] public Transform playerTransform;
    [Tooltip("主角走路速度")] public float walkSpeed = 5f;
    [Tooltip("走到的「中間」位置")] public Transform middlePoint;
    [Tooltip("手機鈴聲01")] public GameObject ring01;
    [Tooltip("手機鈴聲02")] public GameObject ring02;

    [Header("腳本")]
    public AnimationScript animationScript;
    public CControll cControllScript;
    public SceneChange sceneChangeScript;
    public DialogueSystemDes dialogueSystemDesScript;
    private void Awake()
    {
        animationScript = GetComponent<AnimationScript>();
        cControllScript = Player.GetComponent<CControll>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();
        dialogueSystemDesScript = FindAnyObjectByType<DialogueSystemDes>();
    }
    private void Start()
    {
        if (cControllScript == null)
        {
            cControllScript = Player.GetComponent<CControll>();
        }

        PlayerAnimator = cControllScript != null ? cControllScript.animator : null;

        if (PlayerAnimator == null)
        {
            // 保險：自己找一次
            PlayerAnimator = Player.GetComponentInChildren<Animator>();
        }

        if (PlayerAnimator == null)
        {
            Debug.LogError("[desC] 找不到 Player 的 Animator，請檢查 Player 階層。", this);
        }
        BlackPanel.SetActive(false);
        PhoneMessage.SetActive(false);
        DesPanel.SetActive(false);
        PlaceText.gameObject.SetActive(false);
        ring01.SetActive(false);
        ring02.SetActive(false);
        StartCoroutine(SceneFlow());
    }
    //public void StartButton()
    //{
    //    animationScript.Fade(BlackPanel, 1.5f, "01"));
    //}
    //public void BackButton()
    //{
    //    StartCoroutine(animationScript.FadeOutAndChangeScene(BlackPanel.GetComponent<CanvasGroup>(), 1.5f, "menu"));
    //}

    IEnumerator SceneFlow()
    {
        //0.黑幕淡入
        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);

        //1.地點顯示
        PlaceText.gameObject.SetActive(true);
        animationScript.Fade(PlaceText.gameObject, 2f, 0f, 1f, null);
        yield return new WaitUntil(() => PlaceText.gameObject.GetComponent<CanvasGroup>().alpha == 1);
        yield return new WaitForSeconds(1f);
        animationScript.Fade(PlaceText.gameObject, 2f, 1f, 0f, null);
        yield return new WaitUntil(() => PlaceText.gameObject.GetComponent<CanvasGroup>().alpha == 0);
        yield return new WaitForSeconds(1f);
        PlaceText.gameObject.SetActive(false);

        // 2. 
        Debug.Log("主角走進場景");
        cControllScript.Target = new Vector3(-5.81f, -9f,-6.2f);
        cControllScript.StartAutoMoveTo(cControllScript.Target);

        // 等他走到指定 X
        yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        PlayerAnimator.SetBool("walk",false);

        //3.
        Debug.Log("播放拿手機的動畫");
        PlayerAnimator.SetBool("phone", true);
        yield return new WaitForSeconds(cControllScript.phone.length);

        //4.播放進站時間的對話
        Debug.Log("播放進站時間的對話");
        dialogueSystemDesScript.autoNextLine = true;
        dialogueSystemDesScript.StartDialogue(dialogueSystemDesScript.Textfile01);
        yield return new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => dialogueSystemDesScript.text01Finished);
        yield return new WaitForSeconds(2f);
        //對話結束，不看手機
        dialogueSystemDesScript.TextPanel.SetActive(false);
        PlayerAnimator.SetBool("phone", false);

        //5.播放皺眉動畫
        Debug.Log("皺眉");
        PlayerAnimator.SetBool("eyeclose", true);
        yield return new WaitForSeconds(cControllScript.eyeclose.length);
        yield return new WaitForSeconds(1.5f);

        //6.播放音樂響的動畫
        Debug.Log("播放音樂響的動畫");
        ring01.SetActive(true);
        ring02.SetActive(true);
        ring01.GetComponent<Animator>().SetBool("ring", true);
        ring02.GetComponent<Animator>().SetBool("ring", true);
        yield return new WaitForSeconds(3f);

        //7.不皺眉+看手機
        Debug.Log("不皺眉+看手機");
        PlayerAnimator.SetBool("eyeclose", false);
        PlayerAnimator.SetBool("phone", true);
        yield return new WaitForSeconds(cControllScript.phone.length);

        //    // 3. 
        //    Debug.Log("顯示圖片 Panel");
        //    PhoneMessage.SetActive(true);

        //    // 4. 
        //    Debug.Log("過幾秒後出現叉叉");
        //    yield return new WaitForSeconds(5f);
        //    CloseButton.SetActive(true);

        //    // 5. 
        //    Debug.Log("等玩家按關閉");
        //    bool closed = false;
        //    CloseButton.GetComponent<Button>().onClick.AddListener(() => closed = true);
        //    yield return new WaitUntil(() => closed);
        //    PlayerAnimator.SetBool("phone", false);

        //    //// 6. 黑幕淡入（0→1）
        //    //yield return StartCoroutine(animationScript.FadeIn(BlackPanel.GetComponent<CanvasGroup>(), 1f));

        //    // 7. 
        //    Debug.Log("火車進站（用移動而不是 Animator）");
        //    Train.transform.position = trainStartPoint.position; // 先放到起始點
        //    yield return StartCoroutine(MoveToPoint(Train.transform, trainStopPoint.position, trainSpeed));
        //    yield return new WaitUntil(() =>
        //Vector3.Distance(Train.transform.position, trainStopPoint.position) < 0.01f);


        //    // 8. 
        //    Debug.Log("主角走向火車（被前景 sprite 遮住，看起來像上車）");
        //    cControllScript.Target = new Vector2(-4.1f, 4f);
        //    cControllScript.StartAutoMoveTo(cControllScript.Target);
        //    // 等他走到指定 X
        //    yield return new WaitUntil(() => cControllScript.autoMoveFinished);
        //    PlayerAnimator.SetBool("walk", false);
        //    cControllScript.rig.bodyType = RigidbodyType2D.Kinematic;

        // 9. 
        Debug.Log("畫面再次淡出黑（可省略）");
        BlackPanel.SetActive(true);
        animationScript.Fade(
            BlackPanel, 
            1f,
            0f,
            1f,
            ()=>sceneChangeScript.SceneC("00"));
        yield return new WaitForSeconds(1f);
        BlackPanel.SetActive(false);

        // 劇情全部跑完
        Debug.Log("劇情全部跑完");
        cControllScript.EnablePlayerControl();
        // 10. 切換場景
        SceneManager.LoadScene("00");
    }
    //移動位置
    IEnumerator MoveToPoint(Transform obj, Vector3 targetPos, float speed)
    {
        targetPos.z = obj.position.z; // 2D 鎖 Z 軸，避免跑飛

        while (Vector3.Distance(obj.position, targetPos) > 0.01f)
        {
            obj.position = Vector3.MoveTowards(
                obj.position,
                targetPos,
                speed * Time.deltaTime
            );

            yield return null;
        }
        obj.position = targetPos; // 收尾精準貼到目標
    }

}
