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
    [Tooltip("讀到第幾行")]public int index = 0;
    [Tooltip("控制打字節奏（字元出現的間隔時間）")]public float TextSpeed = 0.06f;

    [Header("控制設定")]
    [Tooltip("物件啟用時是否自動開始播放對話")]public bool playOnEnable = false;

    // 內部狀態
    private List<string> TextList = new List<string>();
    [Tooltip("標記是否正在打字")]
    public bool isTyping = false;
    private Coroutine typingRoutine;

    void Awake()
    {

    }

    void Start()
    {
        TextPanel.SetActive(false);
        NarraTextPanel.SetActive(false);
    }

    void Update()
    {
        // 對話框沒開就不用理會
        if (TextPanel == null || !TextPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isTyping)
            {
                // 正在打字 → 直接補完這一行
                FinishCurrentLineImmediately();
            }
            else
            {
                // 這一行已經打完 → 換下一行或關閉
                index++;

                if (index == TextList.Count)
                {
                    if(TextfileCurrent == TextfileGame00)
                    {
                        TextPanel.SetActive(false);
                        isTyping = false;
                        index = 0;
                    }
                }
                else
                {
                    SetTextUI();
                }
            }
        }
    }

    /// <summary>
    /// 從 TextAsset 讀進所有行
    /// </summary>
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

    /// <summary>
    /// 從外部開始對話（可以指定要播哪個 TextAsset）
    /// </summary>
    public void StartDialogue(TextAsset textAsset)
    {
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
        if (index < 0 || index >= TextList.Count)
            return;

        string line = TextList[index];


        // 開始打字機
        StartCoroutine(TypeLine(line));
    }

    /// <summary>
    /// 打字機：一個字一個蹦出來
    /// </summary>
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










    
}

