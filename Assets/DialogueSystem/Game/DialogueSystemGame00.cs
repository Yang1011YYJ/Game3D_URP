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
    public TextAsset TextfileGame01;
    public TextAsset TextfileLookPhone;

    [Header("打字設定")]
    [Tooltip("控制打字節奏（字元出現的間隔時間）")]public float TextSpeed = 0.06f;

    [Header("控制設定")]
    [Tooltip("物件啟用時是否自動開始播放對話")]public bool playOnEnable = false;
    [Tooltip("允許快速顯示內容")] public bool allowFastReveal;
    [Tooltip("第一段劇情撥完")] public bool FirstDiaFinished;

    [Header("跳過")]
    [Tooltip("跳過")] public bool skipRequested = false;
    public SceneCheckpoint currentCP = SceneCheckpoint.Start;
    public SceneCheckpoint skipToCP = SceneCheckpoint.Start;
    [Tooltip("允許跳過教學")] public bool allowSkipTeaching = false;
    public Dictionary<string, int> labelToIndex = new Dictionary<string, int>();
    public bool jumpRequested = false;
    public SceneCheckpoint jumpTarget = SceneCheckpoint.AfterDialogue;


    [Header("腳本")]
    public CControll cControllScript;
    public First firstScript;

    public enum LineCode { Player = 0, Action = 1, Narration = 2 }

    // ---- 內部資料結構：每一句 = (code, content)
    public struct DialogueEntry
    {
        public LineCode code;// 0 玩家, 1 動作, 2 旁白
        public string content;
        public DialogueEntry(LineCode c, string t) { code = c; content = t; }
    }


    [Header("內部狀態")]// 
    public List<DialogueEntry> TextList = new List<DialogueEntry>();
    [Tooltip("讀到第幾行")] public int index = 0;
    [Tooltip("標記是否正在打字")]public bool isTyping = false;
    private Coroutine typingRoutine;
    private bool isBusy = false; // 執行動作/等待中，先鎖住輸入
    // 旁白累加（讓幾句旁白不被洗掉）
    [Tooltip("是否進入「旁白保留模式」")]public bool narraSticky = false;      // 是否進入「旁白保留模式」
    [Tooltip("旁白顯示時用 append 而不是覆蓋")] public bool narraAppendMode = false;  // 旁白顯示時用 append 而不是覆蓋


    void Awake()
    {
        cControllScript = FindAnyObjectByType<CControll>();
        firstScript = FindAnyObjectByType<First>();
    }

    void Start()
    {
        SetPanels(false, false);
    }

    void Update()
    {
        // 有動作在跑就先不吃空白（避免玩家硬切）
        if (isBusy) return;
        if (!anyPanelOn()) return;

        //// 兩個面板都沒開就不處理
        //bool anyPanelOn =
        //    (TextPanel != null && TextPanel.activeSelf) ||
        //    (NarraTextPanel != null && NarraTextPanel.activeSelf);

        if (!Input.GetKeyDown(KeyCode.Space)) return;

        if (isTyping)
        {
            if (allowFastReveal) FinishCurrentLineImmediately();
            return;
        }

        index++;
        if (index >= TextList.Count)
        {
            FinishDialogue();
            return;
        }

        SetTextUI();
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

    [SerializeField] private string mandatoryLabelPrefix = "Label_MUST_";

    // 從目前 index 往後找「下一個必到 label」的位置
    public int FindNextMandatoryLabelIndex(int fromIndex)
    {
        int start = Mathf.Clamp(fromIndex + 1, 0, TextList.Count - 1);

        for (int i = start; i < TextList.Count; i++)
        {
            if (TextList[i].code != LineCode.Action) continue;

            string key = TextList[i].content.Trim();
            if (key.StartsWith(mandatoryLabelPrefix))
                return i;
        }
        return -1;
    }

    public bool SkipByEsc_ToNextMustOrEnd()
    {
        // 沒對話資料就不處理
        if (TextList == null || TextList.Count == 0) return false;

        // 停掉打字與正在跑的動作（避免卡狀態）
        StopTyping();
        isTyping = false;
        isBusy = false;

        // 找下一個 MUST label
        int target = FindNextMandatoryLabelIndex(index);

        if (target < 0)
        {
            // ✅ 沒有 MUST label：直接視為對話結束
            index = TextList.Count;     // 讓狀態符合你說的「index==count」
            FinishDialogue();
            return true;
        }

        // ✅ 有 MUST label：跳過去，但別卡在 label 本身
        // 先把面板收乾淨（避免殘影）
        SetPanels(false, false);

        index = target;

        // ⭐ 這一步很關鍵：避免停在 Label_ 那行需要再按空白
        index++;

        // 如果剛好 label 是最後一行（極少，但保險）
        if (index >= TextList.Count)
        {
            FinishDialogue();
            return true;
        }

        SetTextUI();  // 從 label 後一行開始繼續跑（例如 InTeach）
        return true;
    }


    //public bool SkipToNextMandatoryLabel()
    //{
    //    if (TextList == null || TextList.Count == 0) return false;

    //    int target = FindNextMandatoryLabelIndex(index);
    //    if (target < 0)
    //    {
    //        Debug.Log("[Dialogue] No next mandatory label found, skip ignored.");
    //        return false;
    //    }

    //    // 清掉打字與面板，避免殘影
    //    StopTyping();
    //    isTyping = false;
    //    isBusy = false;
    //    SetPanels(false, false);

    //    index = target;

    //    Debug.Log($"[Dialogue] Skip -> index={index} ({TextList[index].content})");
    //    SetTextUI(); // 讓它從 label 那行開始繼續跑（通常 label 本身是空動作）
    //    return true;
    //}


    // 從外部開始對話（可以指定要播哪個 TextAsset）
    public void StartDialogue(TextAsset textAsset)
    {
        if (!textAsset)
        {
            Debug.LogWarning("[DialogueSystemGame00] StartDialogue textAsset is null");
            return;
        }

        if (textAsset == TextfileGame00) FirstDiaFinished = false;

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
        List<string> cleaned = new();

        foreach (var l in lines)
        {
            string s = l.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(s)) continue; // 空行跳過（你想保留空行再改）
            cleaned.Add(s);
        }

        // 必須是偶數行：code, content, code, content……
        if (cleaned.Count % 2 != 0)
            Debug.LogWarning("[DialogueSystemGame00] 文本行數不是偶數，最後一行可能缺 content。會忽略最後一行。");

        for (int i = 0; i + 1 < cleaned.Count; i += 2)
        {
            if (!int.TryParse(cleaned[i], out int raw))
            {
                Debug.LogWarning($"[DialogueSystemGame00] 第 {i} 行代碼不是數字：{cleaned[i]}，預設當 0");
                raw = 0;
            }

            var code = (LineCode)Mathf.Clamp(raw, 0, 2);
            TextList.Add(new DialogueEntry(code, cleaned[i + 1]));
        }
        labelToIndex.Clear();
        for (int i = 0; i < TextList.Count; i++)
        {
            if (TextList[i].code == LineCode.Action)
            {
                string k = TextList[i].content.Trim();
                if (k.StartsWith("Label_"))
                    labelToIndex[k] = i;
            }
        }
    }

    public void JumpToLabel(string labelKey, bool runImmediately = true)
    {
        if (!labelToIndex.TryGetValue(labelKey, out int target))
        {
            Debug.LogWarning($"[Dialogue] label not found: {labelKey}");
            return;
        }

        // 收乾淨，避免打字/面板殘留
        StopTyping();
        isTyping = false;
        isBusy = false;
        SetPanels(false, false);

        index = target;

        if (runImmediately)
            SetTextUI();
    }


    public void StopTyping()
    {
        // 保險：停掉上一句打字
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;
    }

    // 顯示 index 對應的那一行，啟動打字機效果
    void SetTextUI()
    {
        Debug.Log($"[Dialogue] index={index}, code={TextList[index].code}, content={TextList[index].content}");

        if (index < 0 || index >= TextList.Count) { EndDialogue(); return; }

        StopTyping();

        var line = TextList[index];
        // 這三句旁白要「保留在對話框上」→ 開啟旁白累加模式
        if (line.code == LineCode.Narration)
        {
            if (line.content.StartsWith("雖然不明白發生了什麼") ||
                line.content.StartsWith("拍照、在時限內找出異常") ||
                line.content.StartsWith("成功找出異常後"))
            {
                narraSticky = true;
                narraAppendMode = true; // 這幾句開始用 append
            }
        }


        switch (line.code)
        {
            case LineCode.Player/*0*/: // 玩家對話
                ShowLineOnPlayer(line.content);
                break;

            case LineCode.Narration/*2*/: // 旁白
                ShowLineOnNarration(line.content);
                break;

            case LineCode.Action/*1*/: // 動作（做完自動下一句）
                SetPanels(false, false);
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

    // ---- 顯示/打字：集中處理，避免到處寫一樣的 code ----
    private void ShowLineOnPlayer(string content)
    {
        SetPanels(true, false);
        typingRoutine = StartCoroutine(TypeLine(DiaText, content));
    }

    private void ShowLineOnNarration(string content)
    {
        SetPanels(false, true);
        if (!NarraText)
        {
            Debug.LogWarning("[DialogueSystemGame00] NarraText 沒指定，旁白不顯示。");
            return;
        }

        // 旁白累加模式：不清空，直接換行加上去
        if (narraAppendMode)
        {
            StopTyping();
            isTyping = false;
            typingRoutine = StartCoroutine(TypeLineAppend(NarraText, content));
        }
        else
        {
            typingRoutine = StartCoroutine(TypeLine(NarraText, content));
        }
    }

    // 新增：旁白 append 的打字機
    IEnumerator TypeLineAppend(TextMeshProUGUI target, string line)
    {
        if (target == null) yield break;

        isTyping = true;

        // 已有文字就先換行
        if (!string.IsNullOrEmpty(target.text))
            target.text += "\n";

        foreach (char c in line)
        {
            target.text += c;
            yield return new WaitForSeconds(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;
    }


    public void SetPanels(bool playerOn, bool narraOn)
    {
        if (TextPanel) TextPanel.SetActive(playerOn);
        if (NarraTextPanel) NarraTextPanel.SetActive(narraOn);
    }

    // 正在打字時按 Space：立刻把這一行顯示完整
    void FinishCurrentLineImmediately()
    {

        StopTyping();

        if (index < 0 || index >= TextList.Count) return;

        var line = TextList[index];

        if (line.code == LineCode.Narration)
        {
            if (NarraText != null)
            {
                // ⭐ 關鍵：如果是 append 模式，就「補齊當前這一句」，不要洗掉前文
                if (narraAppendMode)
                {
                    // 如果目前正在打字，target.text 裡已經有「部分字」
                    // 我們只需要補上「還沒出現的剩餘字」
                    string current = NarraText.text;

                    // 找出最後一行已經顯示到哪
                    string[] lines = current.Split('\n');
                    string lastLine = lines[^1];

                    if (line.content.StartsWith(lastLine))
                    {
                        // 補上剩下的部分
                        string rest = line.content.Substring(lastLine.Length);
                        NarraText.text += rest;
                    }
                    else
                    {
                        // 保險：如果狀態怪了，至少不要洗掉前面
                        NarraText.text += "\n" + line.content;
                    }
                }
                else
                {
                    // 非 append（一般旁白）才覆蓋
                    NarraText.text = line.content;
                }
            }
        }
        else
        {
            if (DiaText != null)
                DiaText.text = line.content;
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
        if (index >= TextList.Count) FinishDialogue();
        else SetTextUI();
    }

    private IEnumerator DispatchAction(string actionText)
    {
        string key = actionText.Trim();
        int paren = key.IndexOf('(');
        if (paren >= 0) key = key.Substring(0, paren).Trim();

        if (key.StartsWith("Label_"))
            yield break;

        // TODO：你可以在這裡接動畫、音效、顯示手機 UI、角色移動等
        // 範例：如果文本包含「手機」，就等待 0.8 秒當作演出時間
        switch (key)
        {
            case "PickPhone":
                cControllScript.animator.SetBool("phone", true);
                yield return StartCoroutine(firstScript.WaitForAnimation(cControllScript.animator, "phone"));
                firstScript.PhonePanel.SetActive(true);
                yield return new WaitForSeconds(2f);
                firstScript.PhonePanel.SetActive(false);
                cControllScript.animator.SetBool("phone", false);
                break;

            case "WalkToFront":
                cControllScript.Target = new Vector3(-7.97f, 1.96f, -1.71f);
                cControllScript.autoMoveFinished = false;
                cControllScript.animator.SetBool("walk", true);
                cControllScript.isAutoMoving = true;
                yield return new WaitUntil(() => cControllScript.autoMoveFinished);

                cControllScript.animator.SetBool("walk", false);
                break;

            case "eyeclose":
                cControllScript.animator.SetBool("eyeclose", true);
                yield return new WaitForSeconds(3f);
                cControllScript.animator.SetBool("eyeclose", false);
                break;

            case "BlackPanelOn":
                //yield return StartCoroutine(firstScript.fader.FadeExposure(0.1f/*持續時間*/, 0.5f/*起始*/, -10f/*終點*/));
                firstScript.BlackPanel.SetActive(true);
                break;

            case "BlackPanelOff":
                //yield return StartCoroutine(firstScript.fader.FadeExposure(0.1f/*持續時間*/, 0.5f/*起始*/, -10f/*終點*/));
                firstScript.BlackPanel.SetActive(false);
                break;

            case "LeftAndRight":
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().sprite = cControllScript.leftidle;
                yield return new WaitForSeconds(0.5f);
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().flipX = true;//面向右邊
                yield return new WaitForSeconds(0.5f);
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().flipX = false;//面向左邊
                yield return new WaitForSeconds(0.5f);
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().flipX = true;//面向右邊
                yield return new WaitForSeconds(0.5f);
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().flipX = false;//面向左邊
                cControllScript.PlayerAniAndSprite.GetComponent<SpriteRenderer>().sprite = cControllScript.idle;
                break;

            case "PhoneOn":
                firstScript.PhonePanel.SetActive(true);
                break;

            case "PickPhoneOn":
                cControllScript.animator.SetBool("phone", true);
                yield return firstScript.WaitForAnimation(cControllScript.animator, "phone");
                firstScript.PhonePanel.SetActive(true);
                cControllScript.animator.SetBool("phone", false);
                break;

            // 1) WaitForTeach：教學中，等回到遊戲畫面才繼續
            case "WaitForTeach":
                if (firstScript != null)
                {
                    // 1) 先等教學真的開始（teachRoutine 變成非 null）
                    yield return new WaitUntil(() => firstScript.teachRoutine != null);

                    // 2) 再等教學結束（teachRoutine 回到 null）
                    yield return new WaitUntil(() => firstScript.teachRoutine == null);
                }

                break;

            // 2) Tutorial_AnomalyAppear：進入教學2，結束才回來繼續對話
            case "Tutorial_AnomalyAppear":
                if (firstScript != null)
                    firstScript.RequestTeach2(); // 只發請求，不在這裡跑協程
                break;

            // 3) Tutorial_ExplainInterrupted：把旁白框關掉、並解除「旁白保留模式」
            case "Tutorial_ExplainInterrupted":
                narraSticky = false;
                narraAppendMode = false;
                if (NarraText != null) NarraText.text = "";
                SetPanels(false, false); // 關掉旁白對話框
                break;

            // 4) LightOn：燈光恢復正常
            case "LightOn":
                if (firstScript != null && firstScript.fader != null)
                    yield return firstScript.fader.FadeExposure(0.8f, -10f, 0.5f);
                break;

            // 5) LightFlicker：燈光閃爍（你可自己決定閃幾次）
            case "LightFlicker":
                if (firstScript != null)
                    yield return firstScript.StartCoroutine(firstScript.LightFlickerOnce());
                break;

            // 6) LightDimDown：燈光暗下來（曝光 0.5 → -10）
            case "LightDimDown":
                if (firstScript != null && firstScript.fader != null)
                    yield return firstScript.fader.FadeExposure(0.8f, 0.5f, -10f);
                break;

            // 7) BusShake_Strong：車子搖晃（你做成相機震動或物件晃動）
            case "BusShake_Strong":
                if (firstScript != null)
                    //yield return firstScript.StartCoroutine(firstScript.BusShakeStrong(1.0f));
                    yield return null;
                break;

            // 8) ShowPhoto_S02_Photo_01b：照片出現
            case "ShowPhoto_S02_Photo_01b":
                if (firstScript != null)
                    firstScript.ShowPhoto("S02_Photo_01b");
                break;

            // 9) bigpicture：放大照片 target 區塊到指定大小
            case "bigpicture":
                if (firstScript != null)
                    yield return firstScript.StartCoroutine(firstScript.BigPictureZoom());
                break;

            // 10) TimeJump_1930：時間改變
            case "TimeJump_1930":
                if (firstScript != null)
                    firstScript.SetTimeText("19:30");
                break;

            // 12) ShowTitle_Game_Title：遊戲名稱顯示
            case "ShowTitle_Game_Title":
                if (firstScript != null)
                    yield return firstScript.StartCoroutine(firstScript.ShowGameTitle());
                break;

            case "InTeach":
                FirstDiaFinished = true;
                allowFastReveal = true;
                if (firstScript != null) firstScript.RequestTeach1();
                break;

            default:
                // 沒吃到的 key，就當作空動作（但建議 log 方便你抓拼字）
                Debug.LogWarning($"[DialogueSystemGame00] 未處理的 Action key: {key}");
                break;
        }
    }

    // ---- 面板/結束 ----
    private bool anyPanelOn()
    {
        return (TextPanel && TextPanel.activeSelf) || (NarraTextPanel && NarraTextPanel.activeSelf);
    }

    private void EndDialogue()
    {
        SetPanels(false, false);

        index = 0;
        isTyping = false;
        isBusy = false;

        StopTyping();
    }

    private void FinishDialogue()
    {
        //if (TextfileCurrent == TextfileGame00)
        //{
        //    FirstDiaFinished = true;
        //    allowFastReveal = true;
        //}
        EndDialogue();
    }
}

