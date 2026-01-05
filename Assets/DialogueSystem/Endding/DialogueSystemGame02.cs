using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueSystemGame02 : MonoBehaviour
{
    [Header("Fail Scene Owner")]
    public FailSceneController ownerFail;
    public void BindFail(FailSceneController f) => ownerFail = f;

    [Header("UI")]
    public GameObject TextPanel;
    public TextMeshProUGUI DiaText;

    [Header("旁白UI")]
    public GameObject NarraTextPanel;
    public TextMeshProUGUI NarraDiaText;

    [Header("文本")]
    public TextAsset TextfileCurrent;
    [Tooltip("遊戲失敗劇情")]public TextAsset TextfileGame03_1;

    [Header("打字")]
    public float TextSpeed = 0.06f;

    [Header("設定")]
    public bool playOnEnable = false;
    [Tooltip("允許快速顯示內容")] public bool allowFastReveal = true;
    [Tooltip("允許接收空白鍵")] public bool inputLocked = false;
    private bool dialogueRunning = false;

    [Header("自動播放設定")]
    [Tooltip("true 就自動下一行")] public bool autoNextLine = false;
    [Tooltip("每行播完後停多久再自動下一行")] public float autoNextDelay = 0.5f;

    [Header("旁白保留模式")]
    public bool narraSticky = false;        // 開著就不清空旁白
    public bool narraAppendMode = false;    // 開著就換行累積
    private TextMeshProUGUI typingTarget;
    private string typingFullLine;
    private int typingCharIndex;
    private bool typingAppendMode;
    public enum LineCode { Player = 0, Action = 1, Narration = 2 }

    [Header("腳本")]
    public AudioSettingsUI audioSettingsUI;
    public struct DialogueEntry
    {
        public LineCode code;
        public string content;
        public DialogueEntry(LineCode c, string t) { code = c; content = t; }
    }

    [Header("內部狀態")]
    public List<DialogueEntry> TextList = new List<DialogueEntry>();
    public int index = 0;
    public bool isTyping = false;

    private Coroutine typingRoutine;
    private bool isBusy = false;
    private void Awake()
    {
        audioSettingsUI = FindAnyObjectByType<AudioSettingsUI>();
    }

    void Start()
    {

    }

    void Update()
    {
        if (isBusy) return;
        if (!anyPanelOn()) return;

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
            return;
        }

        dialogueRunning = true;
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

    private void StopDialogueVoice()
    {
        if (audioSettingsUI != null)
            audioSettingsUI.StopLoopSFX();
    }

    private void ShowPlayer(string content)
    {
        SetPanels(true, false);
        typingRoutine = StartCoroutine(TypeLine(DiaText, content, overwrite: true));
    }

    private void ShowNarra(string content)
    {
        SetPanels(false, true);
        audioSettingsUI.PlayNarraTalk();

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



    private IEnumerator DoActionThenContinue(string actionKey)
    {
        isBusy = true;

        yield return StartCoroutine(DispatchAction(actionKey.Trim()));

        isBusy = false;

        index++;
        if (index >= TextList.Count) EndDialogue();
        else SetTextUI();
    }

    private IEnumerator DispatchAction(string actionKey)
    {
        // Label_ 直接忽略
        if (actionKey.StartsWith("Label_"))
            yield break;

        if (ownerFail == null)
        {
            Debug.LogWarning("[DialogueSystemGame00] ownerFail is null, action ignored: " + actionKey);
            yield break;
        }

        // ✅ 只保留 Second 會用到的 Act
        switch (actionKey)
        {
            //case "BlackPanelOn":
            //    yield return ownerFail.Act_BlackPanelOn();
            //    break;

            case "BlackPanelOff":
                yield return ownerFail.Act_BlackPanelOff();
                break;

            case "WaitforSecond1":
                if (ownerFail != null) yield return ownerFail.Act_WaitforSecond1();
                else yield return new WaitForSecondsRealtime(1f);
                break;

            case "StopInBlack":
                if (ownerFail != null) yield return ownerFail.Act_StopInBlack();
                break;

            case "ShowWordFail":
                if (ownerFail != null) yield return ownerFail.Act_ShowWordFail();
                break;




            default:
                Debug.LogWarning("[DialogueSystemGame00] Unhandled action key: " + actionKey);
                break;
        }
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


    public void SetPanels(bool playerOn, bool narraOn)
    {
        if (TextPanel) TextPanel.SetActive(playerOn);
        if (NarraTextPanel) NarraTextPanel.SetActive(narraOn);
    }

    private bool anyPanelOn()
    {
        return (TextPanel && TextPanel.activeSelf) || (NarraTextPanel && NarraTextPanel.activeSelf);
    }

    private void EndDialogue()
    {
        SetPanels(false, false);
        StopTyping();
        index = 0;
        isBusy = false;

        dialogueRunning = false;
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