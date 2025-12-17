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

    [Header("旁白UI")]
    public GameObject NarraTextPanel;
    public TextMeshProUGUI NarraDiaText;

    [Header("文本")]
    public TextAsset TextfileCurrent;
    [Tooltip("主線劇情")]public TextAsset Textfile01;

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

    // ===== 旁白保留（神話編織者那種）=====
    [Header("旁白保留模式")]
    public bool narraSticky = false;        // 開著就不清空旁白
    public bool narraAppendMode = false;    // 開著就換行累積
    private TextMeshProUGUI typingTarget;
    private string typingFullLine;
    private int typingCharIndex;
    private bool typingAppendMode;

    [Header("腳本")]
    public WorldScroller worldScrollerScript;

    // ===== 內部資料 =====
    public enum LineCode { Player = 0, Action = 1, Narration = 2 }
    public struct DialogueEntry
    {
        public LineCode code;
        public string content;
        public DialogueEntry(LineCode c, string t) { code = c; content = t; }
    }

    private List<DialogueEntry> TextList = new();
    public bool isTyping = false;
    private Coroutine typingRoutine;
    private bool isBusy = false;

    private desC owner;  // desC 注入（BindOwner）

    public void BindOwner(desC o) => owner = o;

    void Awake()
    {
        //textRT = DiaText.rectTransform;

        //// 讀 Scene 原本排好的距離
        //leftPadding = textRT.offsetMin.x;      // 左邊到父物件的距離
        //rightPadding = textRT.offsetMax.x;     // 右邊是負的，所以要取負號

        //// Debug 一下確認有抓到值
        //Debug.Log($"[Dialogue] padding L={leftPadding}, R={rightPadding}");
        worldScrollerScript = FindAnyObjectByType<WorldScroller>();
    }

    void Start()
    {
        SetPanels(false, false);
    }

    void Update()
    {
        if (isBusy) return;
        if (!AnyPanelOn()) return;

        if (!autoNextLine && Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                FinishCurrentLineImmediately();
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
    }

    // 從 TextAsset 讀進所有行
    // 從外部開始對話（可以指定要播哪個 TextAsset）
    public void StartDialogue(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogWarning("[DialogueSystemDes] StartDialogue textAsset is null");
            return;
        }

        TextfileCurrent = textAsset;
        ParseFileToTextList(TextfileCurrent);

        if (TextList.Count == 0)
        {
            Debug.LogWarning("[DialogueSystemDes] TextList 是空的。");
            return;
        }

        index = 0;
        SetTextUI();
    }

    private void ParseFileToTextList(TextAsset file)
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
            Debug.LogWarning("[DialogueSystemDes] 文本行數不是偶數，最後一行可能缺 content，會忽略最後一行。");

        for (int i = 0; i + 1 < cleaned.Count; i += 2)
        {
            if (!int.TryParse(cleaned[i], out int raw))
                raw = 0;

            var code = (LineCode)Mathf.Clamp(raw, 0, 2);
            TextList.Add(new DialogueEntry(code, cleaned[i + 1].Trim()));
        }
    }
    // 顯示 index 對應的那一行，啟動打字機效果
    void SetTextUI()
    {
        if (index < 0 || index >= TextList.Count) { EndDialogue(); return; }

        StopTyping();

        var line = TextList[index];
        Debug.Log($"[DesDialogue] index={index}, code={line.code}, content={line.content}");

        switch (line.code)
        {
            case LineCode.Player:
                ShowPlayer(line.content);
                break;

            case LineCode.Narration:
                ShowNarra(line.content);
                break;

            case LineCode.Action:
                SetPanels(false, false);
                StartCoroutine(DoActionThenContinue(line.content));
                break;
        }
    }

    private void ShowPlayer(string content)
    {
        SetPanels(true, false);
        typingRoutine = StartCoroutine(TypeLine(DiaText, content, overwrite: true));
    }

    private void ShowNarra(string content)
    {
        SetPanels(false, true);

        // sticky + append：累積
        if (narraSticky && narraAppendMode)
            typingRoutine = StartCoroutine(TypeLineAppend(NarraDiaText, content));
        else
            typingRoutine = StartCoroutine(TypeLine(NarraDiaText, content, overwrite: true));
    }
    //打字機：一個字一個蹦出來
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


    // 正在打字時按 Space：立刻把這一行顯示完整
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


    private void FinishCurrentLineImmediately()//立刻顯示文字
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


    private IEnumerator DoActionThenContinue(string actionText)
    {
        isBusy = true;

        yield return StartCoroutine(DispatchAction(actionText));

        isBusy = false;

        index++;
        if (index >= TextList.Count) EndDialogue();
        else SetTextUI();
    }

    private IEnumerator DispatchAction(string actionText)
    {
        // 把 "waitforSecs((等兩秒)" 這種括號註解切掉，只抓 key
        string key = actionText.Trim();
        int paren = key.IndexOf('(');
        if (paren >= 0) key = key.Substring(0, paren).Trim();

        // ===== 依你的劇情支援的 Action =====
        switch (key)
        {
            case "phonesprite":
                if (owner != null) yield return owner.Act_PhoneSprite();
                break;

            case "WalkInFromRight":
                if (owner != null) yield return owner.Act_WalkInFromRight();
                break;

            case "showPlace":
                if (owner != null) yield return owner.Act_showPlace();
                break;

            case "PhoneRing":
                if (owner != null) yield return owner.Act_PhoneRing();
                break;

            case "HangUpCall":
                if (owner != null) yield return owner.Act_HangUpCall();
                break;

            case "PickUPPhone":
                if (owner != null) yield return owner.Act_PickUPPhone();
                break;

            case "BusCome":
                if (owner != null) yield return owner.Act_BusCome();
                break;

            case "BoardBus":
                if (owner != null) yield return owner.Act_BoardBus();
                break;

            case "BusGo":
                if (owner != null) yield return owner.Act_BusGo();
                break;

            case "eyeclose":
                if (owner != null) yield return owner.Act_EyeClose(0.8f);
                break;

            case "Inside":
                if (owner != null) yield return owner.Act_Inside();
                break;

            case "MoveToSit":
                if (owner != null) yield return owner.Act_MoveToSit();
                break;

            case "Sleep":
                if (owner != null) yield return owner.Act_Sleep();
                break;

            case "waitforSecs":
                // 從 actionText 抓第一個數字，抓不到就預設 2 秒
                float sec = ExtractFirstNumber(actionText, 2f);
                yield return new WaitForSeconds(sec);
                break;

            case "LightDimDown":
                // ✅ 這一刻開始旁白保留（你要的功能）
                narraSticky = true;
                narraAppendMode = true;

                if (owner != null) yield return owner.Act_LightDimDown();
                break;

            case "LightOn":
                if (owner != null) yield return owner.Act_LightOn();
                break;

            case "BlackPanelOn":
                if (owner != null) yield return owner.Act_BlackPanelOn(0.8f);
                break;

            case "BlackPanelOff":
                if (owner != null) yield return owner.Act_BlackPanelOff(0.8f);
                break;

            case "NextScene":
                // ✅ 切車上前：關掉保留模式並清空旁白
                narraSticky = false;
                narraAppendMode = false;
                if (NarraDiaText != null) NarraDiaText.text = "";
                SetPanels(false, false);

                if (owner != null) yield return owner.Act_NextScene("01");
                break;

            case "LeaveMessageOn":
                if (NarraDiaText != null) NarraDiaText.text = "";
                narraSticky = true;
                narraAppendMode = true;
                // 可選：確保旁白面板開著（如果你希望一開就顯示）
                // SetPanels(false, true);
                worldScrollerScript.StopMove();
                break;

            case "LeaveMessageOff":
                narraSticky = false;
                narraAppendMode = false;
                if (NarraDiaText != null) NarraDiaText.text = "";

                // ✅ 可選 1：只關保留，不清空（保留畫面上已累積的字）
                // 不做任何事

                // ✅ 可選 2：關掉保留 + 清空旁白（你如果希望 off 就收掉留言）
                // if (NarraDiaText != null) NarraDiaText.text = "";

                break;




            default:
                Debug.LogWarning($"[DialogueSystemDes] 未處理的 Action key: {key}");
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

    private void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        isTyping = false;
    }

    private void EndDialogue()
    {
        StopTyping();
        isBusy = false;
        index = 0;
        SetPanels(false, false);
    }

    private void SetPanels(bool playerOn, bool narraOn)
    {
        if (TextPanel) TextPanel.SetActive(playerOn);
        if (NarraTextPanel) NarraTextPanel.SetActive(narraOn);
    }

    private bool AnyPanelOn()
    {
        return (TextPanel && TextPanel.activeSelf) || (NarraTextPanel && NarraTextPanel.activeSelf);
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

