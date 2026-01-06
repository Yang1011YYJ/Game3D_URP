using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueSystemGame01 : MonoBehaviour
{
    [Header("ownerSecond")]
    public Second ownerSecond; // ✅ 拖 Second 進來
    public void BindOwner(Second o) => ownerSecond = o;

    [Header("UI")]
    public GameObject TextPanel;
    public TextMeshProUGUI DiaText;

    [Header("旁白對話框")]
    public GameObject NarraTextPanel;
    public TextMeshProUGUI NarraText;

    [Header("文本")]
    public TextAsset TextfileCurrent;
    [Tooltip("關卡開始前的劇情")]public TextAsset TextfileGame01;
    [Tooltip("遊戲失敗的劇情")] public TextAsset TextfileGame03;
    [Tooltip("遊戲通關的劇情")] public TextAsset TextfileGame04;

    [Header("打字")]
    public float TextSpeed = 0.06f;

    [Header("控制設定")]
    [Tooltip("允許快速顯示內容")] public bool allowFastReveal = true;
    [Tooltip("允許接收空白鍵")] public bool inputLocked = false;
    private bool dialogueRunning = false;

    [Header("自動播放設定")]
    [Tooltip("true 就自動下一行")] public bool autoNextLine = false;
    [Tooltip("每行播完後停多久再自動下一行")] public float autoNextDelay = 0.5f;

    [Header("劇情控制")]
    public bool keepTalk = false;       // 開關：是否要接續劇情
    public TextAsset nextDialogue;      // 要接續的劇情檔

    [Header("旁白保留模式")]
    // 旁白累加（讓幾句旁白不被洗掉）
    [Tooltip("是否進入「旁白保留模式」")] public bool narraSticky = false;        // 開著就不清空旁白
    [Tooltip("旁白顯示時用 append 而不是覆蓋")] public bool narraAppendMode = false;    // 開著就換行累積
    private TextMeshProUGUI typingTarget;
    private string typingFullLine;
    private int typingCharIndex;
    private bool typingAppendMode;
    public enum LineCode { Player = 0, Action = 1, Narration = 2 }

    public struct DialogueEntry
    {
        public LineCode code;
        public string content;
        public DialogueEntry(LineCode c, string t) { code = c; content = t; }
    }

    [Header("內部狀態")]
    public List<DialogueEntry> TextList = new List<DialogueEntry>();
    [Tooltip("讀到第幾行")] public int index = 0;
    [Tooltip("標記是否正在打字")] public bool isTyping = false;

    private Coroutine typingRoutine;
    public bool isBusy = false;// 執行動作/等待中，先鎖住輸入

    [Header("腳本")]
    public CControll cControllScript;
    public AnimationScript animationScript;
    public SceneChange sceneChangeScript;
    public FadeInByExposure fader;
    public AudioSettingsUI audioSettingsUI;
    void Awake()
    {
        cControllScript = FindAnyObjectByType<CControll>();
        animationScript = FindAnyObjectByType<AnimationScript>();
        audioSettingsUI = FindAnyObjectByType<AudioSettingsUI>();
        sceneChangeScript = FindAnyObjectByType<SceneChange>();
        fader = FindAnyObjectByType<FadeInByExposure>();
    }
    void Start()
    {
    }

    void Update()
    {
        if (isBusy)
        {
            return;
        }
        if (!anyPanelOn() && !keepTalk) return;// 如果任何面板開著，才接受空白鍵跳過或下一行

        if (keepTalk)
        {
            keepTalk = false; // 避免重複觸發
            StartDialogue(TextfileCurrent);
            return;
        }

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
            EndDialogue();
            return;
        }

        SetTextUI();
    }

    public void StartDialogue(TextAsset textAsset)
    {
        if (dialogueRunning)
        {
            Debug.LogWarning("[DialogueSystemGame01] StartDialogue called while dialogue is running. Ignored.");
            ForceEndDialogue(); // ⭐ 關鍵在這
            
        }

        dialogueRunning = true;
        Debug.Log($"[StartDialogue] instance={GetInstanceID()} frame={Time.frameCount}");


        if (!textAsset)
        {
            Debug.LogWarning("[DialogueSystemGame00] textAsset is null");
            dialogueRunning = false;
            return;
        }
        inputLocked = false;

        TextfileCurrent = textAsset;
        ParseFileToEntries(TextfileCurrent);

        if (TextList.Count == 0)
        {
            Debug.LogWarning("[DialogueSystemGame00] TextList is empty");
            return;
        }

        index = 0;
        SetTextUI();
    }

    void ForceEndDialogue()
    {
        // 停掉所有對話協程
        StopAllCoroutines();

        // 重置狀態
        dialogueRunning = false;
        inputLocked = false;

        // 清資料
        TextList.Clear();
        index = 0;

        // 關 UI（依你實際用的）
        SetPanels(false, false);
        StopTyping();

        Debug.Log("[DialogueSystemGame01] Dialogue force ended.");
    }


    private void ParseFileToEntries(TextAsset file)
    {
        TextList.Clear();
        index = 0;

        string[] lines = file.text.Split('\n');
        List<string> cleaned = new();

        foreach (var l in lines)
        {
            string s = l.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(s)) continue;
            cleaned.Add(s);
        }

        if (cleaned.Count % 2 != 0)
            Debug.LogWarning("[DialogueSystemGame00] 文本行數不是偶數，最後一行可能缺 content。");

        for (int i = 0; i + 1 < cleaned.Count; i += 2)
        {
            if (!int.TryParse(cleaned[i], out int raw))
                raw = 0;

            var code = (LineCode)Mathf.Clamp(raw, 0, 2);
            TextList.Add(new DialogueEntry(code, cleaned[i + 1]));
        }
    }

    private void SetTextUI()
    {
        Debug.Log($"[Dialogue] index={index}, code={TextList[index].code}, content={TextList[index].content}");

        if (index < 0 || index >= TextList.Count) { EndDialogue(); return; }

        StopTyping();

        var line = TextList[index];

        switch (line.code)
        {
            case LineCode.Player:
                ShowLineOnPlayer(line.content);
                break;

            case LineCode.Narration:
                ShowLineOnNarration(line.content);
                break;

            case LineCode.Action:
                SetPanels(false, false);
                StartCoroutine(DoActionThenContinue(line.content));
                break;
        }
    }
    // 打字機：一個字一個蹦出來
    private IEnumerator TypeLine(TextMeshProUGUI target, string line, bool overwrite)
    {
        Debug.Log($"[TypeLine START] instance={GetInstanceID()} frame={Time.frameCount}");


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
            yield return new WaitForSecondsRealtime(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;

        typingTarget = null;
        typingFullLine = null;
        // ✅ 停掉旁白 / 角色 loop 音效
        StopDialogueVoice();
        if (autoNextLine)
        {
            yield return new WaitForSecondsRealtime(autoNextDelay);
            index++;
            if (index >= TextList.Count) EndDialogue();
            else SetTextUI();
        }
    }
    private void StopDialogueVoice()
    {
        if (audioSettingsUI != null)
            audioSettingsUI.StopLoopSFX();
    }

    // ---- 顯示/打字：集中處理，避免到處寫一樣的 code ----
    private void ShowLineOnPlayer(string content)
    {
        SetPanels(true, false);
        typingRoutine = StartCoroutine(TypeLine(DiaText, content, true));
    }

    private void ShowLineOnNarration(string content)
    {
        SetPanels(false, true);
        audioSettingsUI.PlayNarraTalk();
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
            typingRoutine = StartCoroutine(TypeLine(NarraText, content, true));
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
            yield return new WaitForSecondsRealtime(TextSpeed);
        }

        isTyping = false;
        typingRoutine = null;

        typingTarget = null;
        typingFullLine = null;
        // ✅ 停掉旁白 / 角色 loop 音效
        StopDialogueVoice();
        if (autoNextLine)
        {
            yield return new WaitForSecondsRealtime(autoNextDelay);
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

        // ✅ 停掉旁白 / 角色 loop 音效
        StopDialogueVoice();
    }

    // ---------- 動作系統：你可以在這裡擴充 ----------
    private IEnumerator DoActionThenContinue(string actionKey)
    {
        isBusy = true;

        // 動作期間，通常不想讓玩家快切（你也可以改成 allowFastReveal = false）
        bool prevAllow = allowFastReveal;
        allowFastReveal = false;

        yield return StartCoroutine(DispatchAction(actionKey.Trim()));

        allowFastReveal = prevAllow;
        isBusy = false;

        index++;
        if (index >= TextList.Count) EndDialogue();
        else SetTextUI();
    }

    private IEnumerator DispatchAction(string actionKey)
    {
        string key = actionKey.Trim();
        int paren = key.IndexOf('(');
        if (paren >= 0) key = key.Substring(0, paren).Trim();

        // Label_ 直接忽略
        if (key.StartsWith("Label_"))
            yield break;

        if (ownerSecond == null)
        {
            Debug.LogWarning("[DialogueSystemGame00] ownerSecond(Second) is null, action ignored: " + key);
            yield break;
        }
        yield return DispatchAction(key, actionKey);
        // key 是乾淨命令，actionText 保留括號內容讓你解析參數用
    }

    public IEnumerator DispatchAction(string key, string raw)
    {

        // ✅ 只保留 Second 會用到的 Act
        switch (key)
        {
            //燈光控制
            case "LightOn":
                yield return ownerSecond.Act_LightOn();
                break;

            case "LightDimDown":
                yield return ownerSecond.Act_LightDimDown();
                break;

            case "RedLight":
                yield return ownerSecond.Act_RedLight();
                break;

            case "BuslightCloseOneByOne":
                yield return ownerSecond.Act_BuslightCloseOneByOne();
                break;
            //燈光控制

            //黑畫面與節奏控制與畫面控制
            case "BlackPanelOn":
                yield return ownerSecond.Act_BlackPanelOn();
                break;

            case "BlackPanelShutOff":
                yield return ownerSecond.Act_BlackPanelShutOff();
                break;

            case "BlackPanelOff":
                yield return ownerSecond.Act_BlackPanelOff();
                break;

            case "WaitForSecond1":
                yield return ownerSecond.Act_WaitForSecondsCoroutine(2.0f); // 或直接寫在 Second 裡
                break;

            case "CameraBack":
                yield return ownerSecond.Act_CameraBack();
                break;
            //黑畫面與節奏控制

            //遊戲相關
            case "SelectRound": 
                yield return ownerSecond.Act_SelectRound(); 
                break;

            case "GameStart":
                yield return ownerSecond.SelectAndStartRoutine();
                break;

            case "FailScene":
                yield return ownerSecond.Act_FailScene();
                break;

            case "SuccessScene":
                yield return ownerSecond.Act_SuccessScene();
                break;
            //遊戲相關

            //手機系統
            case "PickPhone":
                yield return ownerSecond.Act_PickPhone();
                break;

            case "PickPhoneOn":
                yield return ownerSecond.Act_PickPhoneOn();
                break;

            case "HangUpPhone":
                yield return ownerSecond.Act_HangUpPhone();
                break;

            case "PhoneRing":
                if (ownerSecond != null) yield return ownerSecond.Act_PhoneRing();
                break;
            //手機系統

            //公車相關
            case "BusShake":
                yield return ownerSecond.Act_BusShake(true);
                break;
            //公車相關

            //角色相關
            case "LeftRight":
                yield return ownerSecond.Act_LeftRight();
                break;

            case "idle":
                yield return ownerSecond.Act_idle();
                break;
                
            case "PlayerToDie":
                yield return ownerSecond.Act_PlayerToDie();
                break;
            //角色相關

            default:
                Debug.LogWarning("[DialogueSystemGame00] Unhandled action key: " + key);
                break;
        }
    }

    private float ExtractFirstNumber(string s, float fallback)
    {
        // 找到第一段連續數字（含小數點）
        bool found = false;
        string num = "";
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsDigit(c) || (c == '.' && found))
            {
                found = true;
                num += c;
            }
            else if (found)
            {
                break;
            }
        }

        if (float.TryParse(num, out float v))
            return v;

        return fallback;
    }

    public void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;
    }


    private bool anyPanelOn()
    {
        //Debug.Log("anyPanelOn");
        //if(TextPanel.activeSelf) Debug.Log(TextPanel.activeSelf);
        //if (NarraTextPanel.activeSelf) Debug.Log(NarraTextPanel.activeSelf);
        return (TextPanel && TextPanel.activeSelf) || (NarraTextPanel && NarraTextPanel.activeSelf);
    }

    private void EndDialogue()
    {
        SetPanels(false, false);
        StopTyping();
        index = 0;
        isTyping = false;
        isBusy = false;

        dialogueRunning = false; // ✅ 核心
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