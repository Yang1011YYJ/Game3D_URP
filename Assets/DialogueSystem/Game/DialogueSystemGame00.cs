using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class DialogueSystemGame00 : MonoBehaviour
{
    private First owner;
    public void BindOwner(First o) => owner = o;

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
    private Dictionary<string, int> labelToIndex = new Dictionary<string, int>();


    [Header("控制設定")]
    [Tooltip("物件啟用時是否自動開始播放對話")]public bool playOnEnable = false;
    [Tooltip("允許快速顯示內容")] public bool allowFastReveal;
    [Tooltip("第一段劇情撥完")] public bool FirstDiaFinished;
    [Tooltip("允許接收空白鍵")] public bool inputLocked = false;


    [Header("腳本")]
    public CControll cControllScript;
    public First firstScript;
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;
    public FadeInByExposure fader;
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

    [Header("自動播放設定")]
    [Tooltip("true 就自動下一行")] public bool autoNextLine = false;
    [Tooltip("每行播完後停多久再自動下一行")] public float autoNextDelay = 0.5f;

    [Header("旁白保留模式")]
    // 旁白累加（讓幾句旁白不被洗掉）
    [Tooltip("是否進入「旁白保留模式」")] public bool narraSticky = false;      // 是否進入「旁白保留模式」
    [Tooltip("旁白顯示時用 append 而不是覆蓋")] public bool narraAppendMode = false;  // 旁白顯示時用 append 而不是覆蓋
    private TextMeshProUGUI typingTarget;
    private string typingFullLine;
    private int typingCharIndex;
    private bool typingAppendMode;


    void Awake()
    {
        cControllScript = FindAnyObjectByType<CControll>();
        firstScript = FindAnyObjectByType<First>();
        animationScript = FindAnyObjectByType<AnimationScript>();
    }

    void Start()
    {
        SetPanels(false, false);
        StartDialogue(TextfileGame00);
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
        if (inputLocked) return;
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

    public void JumpToLabel(string labelKey, bool runImmediately = true)//快速跳到某index
    {
        if (!labelToIndex.TryGetValue(labelKey, out int target))
        {
            Debug.LogWarning($"[Dialogue] label not found: {labelKey}");
            return;
        }

        StopTyping();
        isTyping = false;
        isBusy = false;
        SetPanels(false, false);

        index = target;

        // ✅ 跳過 label 本身
        index++;

        if (index >= TextList.Count)
        {
            FinishDialogue();
            return;
        }

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
                NarraText.text = "";
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
    private IEnumerator TypeLine(TextMeshProUGUI target, string line, bool overwrite)
    {
        if (target == null) yield break;

        isTyping = true;

        typingTarget = target;
        typingFullLine = line;
        typingCharIndex = 0;
        typingAppendMode = false;

        if (overwrite) target.text = "";

        while (typingCharIndex < line.Length)
        {
            target.text += line[typingCharIndex];
            typingCharIndex++;
            yield return new WaitForSeconds(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;

        typingTarget = null;
        typingFullLine = null;

        if (autoNextLine)
        {
            yield return new WaitForSeconds(autoNextDelay);
            index++;
            if (index >= TextList.Count) EndDialogue();
            else SetTextUI();
        }
    }


    // ---- 顯示/打字：集中處理，避免到處寫一樣的 code ----
    private void ShowLineOnPlayer(string content)
    {
        SetPanels(true, false);
        typingRoutine = StartCoroutine(TypeLine(DiaText, content,true));
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
            typingRoutine = StartCoroutine(TypeLine(NarraText, content,true));
        }
    }

    // 新增：旁白 append 的打字機
    private IEnumerator TypeLineAppend(TextMeshProUGUI target, string line)
    {
        if (target == null) yield break;

        isTyping = true;

        typingTarget = target;
        typingFullLine = line;
        typingCharIndex = 0;
        typingAppendMode = true;

        // 只有在「開始新的一行」時才加換行
        if (!string.IsNullOrEmpty(target.text))
            target.text += "\n";

        while (typingCharIndex < line.Length)
        {
            target.text += line[typingCharIndex];
            typingCharIndex++;
            yield return new WaitForSeconds(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;

        typingTarget = null;
        typingFullLine = null;

        if (autoNextLine)
        {
            yield return new WaitForSeconds(autoNextDelay);
            index++;
            if (index >= TextList.Count) EndDialogue();
            else SetTextUI();
        }
    }



    public void SetPanels(bool playerOn, bool narraOn)
    {
        if (TextPanel) TextPanel.SetActive(playerOn);
        if (NarraTextPanel) NarraTextPanel.SetActive(narraOn);
    }

    // 正在打字時按 Space：立刻把這一行顯示完整
    private void FinishCurrentLineImmediately()
    {
        if (!isTyping) return;

        // 先把協程停掉，但不要重複 append 整句
        StopTyping();

        if (typingTarget == null || string.IsNullOrEmpty(typingFullLine))
            return;

        // ✅ 只補上「剩下還沒打完的字」
        if (typingCharIndex < typingFullLine.Length)
        {
            typingTarget.text += typingFullLine.Substring(typingCharIndex);
        }

        ForceLayoutNow(typingTarget);

        // 清理
        isTyping = false;
        typingTarget = null;
        typingFullLine = null;
        typingCharIndex = 0;
        typingAppendMode = false;
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

        if (owner == null)
        {
            Debug.LogWarning("[DialogueSystemGame00] owner(First) is null, action ignored: " + key);
            yield break;
        }

        yield return DispatchAction(key, actionText);
        // key 是乾淨命令，actionText 保留括號內容讓你解析參數用
    }

    public IEnumerator DispatchAction(string key, string raw)
    {
        switch (key)
        {
            case "LightOn":
                yield return owner.Act_LightOn();
                break;

            case "LightBlack":
                Debug.Log("1");
                yield return owner.Act_LightBlack();
                break;

            case "BusLightBright":
                yield return owner.Act_BusLightBright();
                break;

            case "eyeclose":
                if (cControllScript != null && cControllScript.animator != null)
                {
                    cControllScript.animator.SetBool("eyeclose", true);
                    yield return new WaitForSeconds(2f); // 你自己調
                }
                break;

            case "eyecloseBackToIdle":
                if (cControllScript != null && cControllScript.animator != null)
                {
                    cControllScript.animator.SetBool("eyeclose", false);
                    firstScript.Player.transform.position = firstScript.PlayerStartPos.position;
                    yield return new WaitForSeconds(1.5f);
                }
                break;

            case "LeftRight":
                yield return owner.Act_LeftRight();
                break;

            case "PickPhone":
                yield return owner.Act_PickPhone();
                break;

            case "PickPhoneOn":
                yield return owner.Act_PickPhoneOn();
                break;

            case "HangUpPhone":
                yield return owner.Act_HangUpPhone();
                break;

            case "BusShake":
                yield return owner.Act_BusShake(true);
                break;

            case "BusShakeWithDamping":
                if (firstScript != null)
                    yield return firstScript.Act_BusShakeWithDamping(true);
                break;

            case "BlackPanelOn":
                yield return owner.Act_BlackPanelOn();
                break;

            case "BlackPanelShutOff":
                yield return owner.Act_BlackPanelShutOff();
                break;

            case "BlackPanelOn2":
                yield return owner.Act_BlackPanelOn2();
                break;

            case "BlackPanelOff":
                yield return owner.Act_BlackPanelOff();
                break;

            case "InTeach01"://判斷教學
                owner.Act_RequestTeach1();
                break;

            case "GameStart"://開始遊戲
                yield return owner.Act_GameStart();
                break;

            case "InTeach02":
                owner.Act_RequestTeach2();
                break;

            case "Tutorial_ExplainInterrupted":
                //yield return owner.Act_TutorialExplainInterrupted();
                narraSticky = false;
                narraAppendMode = false;
                if (NarraText != null)
                    NarraText.text = "";

                SetPanels(false, false);
                yield return new WaitForSeconds(1.5f);
                break;

            case "TimeJump_1930":
                yield return owner.Act_SetTimeText("19:30");
                break;

            case "ShowPhoto_S02_Photo_01b":
                yield return owner.Act_ShowPhoto(firstScript.Picture02);
                break;
               
            case "photoclose":
                yield return owner.Act_photoclose();
                break;

            case "bigpicture":
                yield return owner.Act_BigPictureZoom();
                break;

            case "LightFlicker":
                yield return owner.Act_LightFlickerOnce();
                break;

            case "LightDimDown":
                yield return owner.Act_LightDimDown();
                break;

            case "ShowTitle_Game_Title":
                yield return owner.Act_ShowGameTitle();
                break;

            case "WalkToFront":
                yield return owner.Act_WalkToFront();
                break;

            default:
                Debug.LogWarning("[First] Unhandled action key: " + key + " raw=" + raw);
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
        if (TextfileCurrent == TextfileGame00)
        {
            FirstDiaFinished = true;
            //allowFastReveal = true;
            animationScript.Fade(firstScript.BlackPanel,1f,0f,1f, ()=> sceneChangeScript.SceneC("02"));
        }
        EndDialogue();
    }

    private void ForceLayoutNow(TextMeshProUGUI target)
    {
        if (!target) return;

        // 1) 讓 TMP 立刻更新字形/網格，算出正確 preferredWidth
        target.ForceMeshUpdate();

        // 2) 立刻讓 layout 系統重算（ContentSizeFitter / LayoutGroup 才會跟上）
        Canvas.ForceUpdateCanvases();

        // 3) 針對文字與它的父物件往上重建（背景框通常在父物件）
        LayoutRebuilder.ForceRebuildLayoutImmediate(target.rectTransform);

        var parentRT = target.rectTransform.parent as RectTransform;
        if (parentRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);
    }
}

