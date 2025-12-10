using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueSystemDes : MonoBehaviour
{
    [Header("UI")]
    public GameObject TextPanel;
    public TextMeshProUGUI DiaText;
    public Image FaceImage;
    public TextMeshProUGUI Name;
    //public RectTransform dialogueBoxRect;
    //RectTransform textRT;

    //[Header("對話框寬度")]
    //public float minWidth = 400f;
    //public float maxWidth = 900f;
    //[Tooltip("從 Scene 讀出來的 padding")] public float leftPadding;
    //[Tooltip("從 Scene 讀出來的 padding")] public float rightPadding;

    [Header("文本")]
    public TextAsset TextfileCurrent;
    [Tooltip("剩三分鐘進站")]public TextAsset Textfile01;

    [Header("劇情進度")]
    [Tooltip("車進站的時間")]public bool text01Finished = false;

    [Header("打字設定")]
    [Tooltip("讀到第幾行")]
    public int index = 0;
    [Tooltip("控制打字節奏（字元出現的間隔時間）")]public float TextSpeed = 0.06f;
    [Tooltip("繼續對話")] public bool KeepTalk;
    [Tooltip("對話中")] public bool IsTalking;

    [Header("自動播放設定")]
    [Tooltip("true 就自動下一行")] public bool autoNextLine = false;
    [Tooltip("每行播完後停多久再自動下一行")] public float autoNextDelay = 0.5f;

    [Header("控制設定")]
    [Tooltip("物件啟用時是否自動開始播放對話")]public bool playOnEnable = false;
    // 內部狀態
    private List<string> TextList = new List<string>();
    [Tooltip("標記是否正在打字")]public bool isTyping = false;
    private Coroutine typingRoutine;

    void Awake()
    {
        //textRT = DiaText.rectTransform;

        //// 讀 Scene 原本排好的距離
        //leftPadding = textRT.offsetMin.x;      // 左邊到父物件的距離
        //rightPadding = textRT.offsetMax.x;     // 右邊是負的，所以要取負號

        //// Debug 一下確認有抓到值
        //Debug.Log($"[Dialogue] padding L={leftPadding}, R={rightPadding}");

    }

    void Start()
    {
        TextPanel.SetActive(false);
    }

    void Update()
    {
        // 對話框沒開就不用理會
        if (TextPanel == null || !TextPanel.activeSelf) return;

        if (autoNextLine) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // 正在打字 → 直接補完這一行
                FinishCurrentLineImmediately();
                return;
            }
            // 這一行已經打完 → 換下一行或結束
            index++;
            if (index >= TextList.Count)
            {
                // 所有行數都播完 → 統一交給收尾函式
                HandleDialogueEnd();

            } 
            else
            {
                SetTextUI();
            }
        }
    }

    // 從 TextAsset 讀進所有行
    void GetTextFromFile(TextAsset file)
    {
        TextList.Clear();
        index = 0;

        if (file == null) return;

        var lineData = file.text.Split('\n');

        foreach (var line in lineData)
        {
            // 去掉尾巴的 \r，避免 Windows 換行造成奇怪字元
            TextList.Add(line.TrimEnd('\r'));
        }
    }
    // 從外部開始對話（可以指定要播哪個 TextAsset）
    public void StartDialogue(TextAsset textAsset)
    {
        playOnEnable = true;
        if (textAsset != null)
        {
            TextfileCurrent = textAsset;
            GetTextFromFile(TextfileCurrent);
        }

        if (TextList.Count == 0)
        {
            Debug.LogWarning("[DialogueSystemGame00] 目前 TextList 是空的，沒有東西可以播放。");
            return;
        }

        index = 0;
        TextPanel.SetActive(true);
        SetTextUI();
    }

    /// <summary>
    /// 顯示 index 對應的那一行，啟動打字機效果
    /// </summary>
    void SetTextUI()
    {
        if (index < 0 || index >= TextList.Count) return;

        string line = TextList[index];

        // 先依照這一行內容調整對話框的寬度
        //UpdateDialogueBoxWidth(line);

        // 如果之前有打字中的協程，先停掉
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        // 開始打字機，這次記得把協程存起來
        typingRoutine = StartCoroutine(TypeLine(line));
    }
    //打字機：一個字一個蹦出來
    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        DiaText.text = "";

        foreach (char c in line)
        {
            DiaText.text += c;
            yield return new WaitForSeconds(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;

        if (autoNextLine)
        {
            // 已經是最後一行了
            if (index >= TextList.Count-1)
            {
                // 如果這份對話是 Textfile01，可以在這裡做結束處理
                if (TextfileCurrent == Textfile01)
                {
                    HandleDialogueEnd();
                }
                yield break;
            }

            // 還有下一行 → 等一小段時間再播下一句
            yield return new WaitForSeconds(autoNextDelay);
            index++;
            SetTextUI();
        }
        else
        {
            // 手動模式：停在這裡，等玩家按空白
            typingRoutine = null;
        }
    }

    /// <summary>
    /// 正在打字時按 Space：立刻把這一行顯示完整
    /// </summary>
    void FinishCurrentLineImmediately()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (index < 0 || index >= TextList.Count) return;

        DiaText.text = TextList[index];
        isTyping = false;
    }

    // 播完所有對話時要做什麼：統一收尾都來這裡
    void HandleDialogueEnd()
    {
        // 收狀態
        isTyping = false;
        autoNextLine = false;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        // 這裡可以根據「是哪一份文本」決定不同行為
        if (TextfileCurrent == Textfile01)
        {
            text01Finished = true;
        }

        // 預設行為：關閉對話框、重置 index
        //TextPanel.SetActive(false);
        index = 0;
    }








    /// 依照目前這一行的文字長度調整對話框寬度
    /// （記得對話框背景圖請用 Sliced Sprite 才不會變形）
    //void UpdateDialogueBoxWidth(string line)
    //{
    //    if (dialogueBoxRect == null || DiaText == null)
    //    {
    //        Debug.LogError("[Dialogue] dialogueBoxRect 或 DiaText 是 null，沒有東西可以調寬！");
    //        return;
    //    }

    //    // 🔎 1. 進來時先印出現在的寬度
    //    Debug.Log($"[Dialogue] 呼叫 UpdateDialogueBoxWidth，目標台詞：\"{line}\"");
    //    Debug.Log($"[Dialogue] 進來前 dialogueBoxRect.sizeDelta.x = {dialogueBoxRect.sizeDelta.x}");

    //    // 1️⃣ 先算「完全不換行」時的理論寬度
    //    DiaText.enableWordWrapping = false;
    //    Vector2 prefNoWrap = DiaText.GetPreferredValues(line, Mathf.Infinity, Mathf.Infinity);
    //    float rawWidth = prefNoWrap.x;

    //    // 2️⃣ 算出文字可用的最大寬度（扣掉左右 padding）
    //    float innerMaxWidth = maxWidth - leftPadding - rightPadding;

    //    float textAreaWidth;
    //    bool willWrap;

    //    if (rawWidth <= innerMaxWidth)
    //    {
    //        // ✅ 可以一行顯示完
    //        willWrap = false;
    //        textAreaWidth = rawWidth;
    //    }
    //    else
    //    {
    //        // ❗太長了，一行裝不下，限制寬度讓它自動換行
    //        willWrap = true;
    //        textAreaWidth = innerMaxWidth;
    //    }

    //    float finalBoxWidth = Mathf.Clamp(
    //        textAreaWidth + leftPadding + rightPadding,
    //        minWidth,
    //        maxWidth
    //    );

    //    Debug.Log($"[Dialogue] rawWidth = {rawWidth}, innerMaxWidth = {innerMaxWidth}, textAreaWidth = {textAreaWidth}, finalBoxWidth = {finalBoxWidth}");

    //    // 4️⃣ 套用到背景框 RectTransform
    //    dialogueBoxRect.SetSizeWithCurrentAnchors(
    //        RectTransform.Axis.Horizontal,
    //        finalBoxWidth
    //    );

    //    Debug.Log($"[Dialogue] 設定後 dialogueBoxRect.sizeDelta.x = {dialogueBoxRect.sizeDelta.x}");

    //    // 5️⃣ 文字更新
    //    DiaText.enableWordWrapping = willWrap;
    //    DiaText.text = line;
    //    DiaText.ForceMeshUpdate();
    //}


}

