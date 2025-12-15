using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueSystemGame00 : MonoBehaviour
{
    [Header("UI")]
    public GameObject TextPanel;
    public TextMeshProUGUI DiaText;

    [Header("旁白對話框")] public GameObject NarraTextPanel;
    public TextMeshProUGUI NarraText;

    [Header("文本")]
    public TextAsset TextfileCurrent;
    public TextAsset TextfileGame00;
    public TextAsset TextfileLookPhone;

    [Header("打字設定")]
    [Tooltip("控制打字節奏（字元出現的間隔時間）")]public float TextSpeed = 0.06f;

    [Header("控制設定")]
    [Tooltip("物件啟用時是否自動開始播放對話")]public bool playOnEnable = false;
    [Tooltip("允許快速顯示內容")] public bool allowFastReveal;

    [Header("腳本")]
    public CControll cControllScript;
    public First firstScript;

    // ---- 內部資料結構：每一句 = (code, content)
    public struct DialogueEntry
    {
        public int code;      // 0 玩家, 1 動作, 2 旁白
        public string content;

        public DialogueEntry(int c, string t)
        {
            code = c;
            content = t;
        }
    }

    [Header("內部狀態")]// 
    public List<DialogueEntry> TextList = new List<DialogueEntry>();
    [Tooltip("讀到第幾行")] public int index = 0;
    [Tooltip("標記是否正在打字")]public bool isTyping = false;
    private Coroutine typingRoutine;
    private bool isBusy = false; // 執行動作/等待中，先鎖住輸入

    void Awake()
    {
        cControllScript = FindAnyObjectByType<CControll>();
        firstScript = FindAnyObjectByType<First>();
    }

    void Start()
    {
        if (TextPanel) TextPanel.SetActive(false);
        if (NarraTextPanel) NarraTextPanel.SetActive(false);
    }

    void Update()
    {
        // 有動作在跑就先不吃空白（避免玩家硬切）
        if (isBusy) return;

        // 兩個面板都沒開就不處理
        bool anyPanelOn =
            (TextPanel != null && TextPanel.activeSelf) ||
            (NarraTextPanel != null && NarraTextPanel.activeSelf);

        if (!anyPanelOn) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // 正在打字 → 直接補完這一行
                // 打字中：允許才快顯
                if (allowFastReveal)
                    FinishCurrentLineImmediately();
            }
            else
            {
                // 這一行已經打完 → 換下一行或關閉
                index++;

                if (index >= TextList.Count)
                {
                    if(TextfileCurrent == TextfileGame00)
                    {
                        EndDialogue();
                    }
                }
                else
                {
                    SetTextUI();
                }
            }
        }
    }

    //// 從 TextAsset 讀進所有行
    //void ParseFileToEntries(TextAsset file)
    //{
    //    TextList.Clear();
    //    index = 0;

    //    if (file == null) return;

    //    var lineData = file.text.Split('\n');

    //    foreach (var line in lineData)
    //    {
    //        // 去掉尾巴的 \r，避免 Windows 換行造成奇怪字元
    //        TextList.Add(line.TrimEnd('\r'));
    //    }
    //}

    // 從外部開始對話（可以指定要播哪個 TextAsset）
    public void StartDialogue(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogWarning("[DialogueSystemGame00] StartDialogue textAsset is null");
            return;
        }

        TextfileCurrent = textAsset;
        ParseFileToEntries(TextfileCurrent);

        if (TextList.Count == 0)
        {
            Debug.LogWarning("[DialogueSystemGame00] 目前 TextList 是空的，沒有東西可以播放。");
            return;
        }

        index = 0;
        SetTextUI();
    }

    // ---------- 文本解析：每 2 行為一組 (code + content) ----------
    private void ParseFileToEntries(TextAsset file)
    {
        TextList.Clear();
        index = 0;

        string[] lines = file.text.Split('\n');
        List<string> cleaned = new List<string>();

        foreach (var l in lines)
        {
            string s = l.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(s)) continue; // 空行跳過（你想保留空行再改）
            cleaned.Add(s);
        }

        // 必須是偶數行：code, content, code, content...
        if (cleaned.Count % 2 != 0)
        {
            Debug.LogWarning("[DialogueSystemGame00] 文本行數不是偶數，最後一行可能缺 content。會忽略最後一行。");
        }

        for (int i = 0; i + 1 < cleaned.Count; i += 2)
        {
            int code = 0;
            if (!int.TryParse(cleaned[i], out code))
            {
                Debug.LogWarning($"[DialogueSystemGame00] 第 {i} 行代碼不是數字：{cleaned[i]}，預設當 0");
                code = 0;
            }

            string content = cleaned[i + 1];
            TextList.Add(new DialogueEntry(code, content));
        }
    }

    // 顯示 index 對應的那一行，啟動打字機效果
    void SetTextUI()
    {
        if (index < 0 || index >= TextList.Count) { EndDialogue(); return; }

        // 保險：停掉上一句打字
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;

        DialogueEntry line = TextList[index];

        switch (line.code)
        {
            case 0: // 玩家對話
                ShowPlayerPanel();
                typingRoutine = StartCoroutine(TypeLine(DiaText, line.content));
                break;

            case 2: // 旁白
                ShowNarrationPanel();
                if (NarraText == null)
                {
                    Debug.LogWarning("[DialogueSystemGame00] NarraText 沒有指定，旁白不會顯示文字");
                }
                else
                {
                    typingRoutine = StartCoroutine(TypeLine(NarraText, line.content));
                }
                break;

            case 1: // 動作（做完自動下一句）
                if (TextPanel) TextPanel.SetActive(false);
                if (NarraTextPanel) NarraTextPanel.SetActive(false);
                StartCoroutine(DoActionThenContinue(line.content));
                break;

            default:
                Debug.LogWarning($"[DialogueSystemGame00] 未知的 code：{line.code}，內容：{line.content}");
                break;
        }
    }


    // 打字機：一個字一個蹦出來
    IEnumerator TypeLine(TextMeshProUGUI target, string line)
    {
        if (target == null) yield break;

        isTyping = true;
        target.text = "";

        foreach (char c in line)
        {
            target.text += c;
            yield return new WaitForSeconds(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;
    }

    private void ShowPlayerPanel()
    {
        if (TextPanel) TextPanel.SetActive(true);
        if (NarraTextPanel) NarraTextPanel.SetActive(false);
    }

    private void ShowNarrationPanel()
    {
        if (TextPanel) TextPanel.SetActive(false);
        if (NarraTextPanel) NarraTextPanel.SetActive(true);
    }

    // 正在打字時按 Space：立刻把這一行顯示完整
    void FinishCurrentLineImmediately()
    {

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (index < 0 || index >= TextList.Count) return;

        DialogueEntry line = TextList[index];

        // 依照目前 code，把文字塞到對的面板
        if (line.code == 2)
        {
            if (NarraText != null) NarraText.text = line.content;
        }
        else
        {
            if (DiaText != null) DiaText.text = line.content;
        }

        isTyping = false;
    }

    // ---------- 動作系統：你可以在這裡擴充 ----------
    private IEnumerator DoActionThenContinue(string actionText)
    {
        isBusy = true;

        // 動作期間，通常不想讓玩家快切（你也可以改成 allowFastReveal = false）
        bool prevAllow = allowFastReveal;
        allowFastReveal = false;

        // 這裡做一個最簡單的 Dispatcher：依字串決定要做什麼
        // 你例子是 "(拿起了手機)"，我建議你用更穩的 key，例如 "PickPhone"
        yield return StartCoroutine(DispatchAction(actionText));

        allowFastReveal = prevAllow;
        isBusy = false;

        // 動作完成 → 自動下一句
        index++;
        if (index >= TextList.Count) EndDialogue();
        else SetTextUI();
    }

    private IEnumerator DispatchAction(string actionText)
    {
        // TODO：你可以在這裡接動畫、音效、顯示手機 UI、角色移動等
        // 範例：如果文本包含「手機」，就等待 0.8 秒當作演出時間
        if (actionText.Contains("PickPhone"))
        {
            cControllScript.animator.SetBool("phone", true);
            yield return StartCoroutine(firstScript.WaitForAnimation(cControllScript.animator, "phone"));
        }
        else
        {
            // 預設動作時間（避免瞬間跳過看不到）
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void EndDialogue()
    {
        if (TextPanel) TextPanel.SetActive(false);
        if (NarraTextPanel) NarraTextPanel.SetActive(false);

        index = 0;
        isTyping = false;
        isBusy = false;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
    }







}

