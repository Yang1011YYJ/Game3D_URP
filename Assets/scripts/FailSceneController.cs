using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FailSceneController : MonoBehaviour
{
    [Header("WorldScroller (無限背景)")]
    public WorldScroller worldScroller;
    public AnimationScript animationScript;
    public DialogueSystemGame02 dialogue02;

    [Header("Fail UI")]
    public TextMeshProUGUI failWordText;      // 文字本體（可選）
    public string failWord = "END-迷途";
    public float wordFadeDuration = 1.2f;

    [Header("Replay Button")]
    public GameObject replayButton;
    public float replayAppearDelay = 1.0f;
    public float replayFadeDuration = 0.4f;

    [Header("其他UI")]
    [Tooltip("對話框不遮")]public GameObject BlackPanel;
    [Tooltip("全部都遮住")]public GameObject BlackPanel2;
    public GameObject SettingPanel;

    private void Awake()
    {
        if (dialogue02 == null) dialogue02 = FindAnyObjectByType<DialogueSystemGame02>();
        if (dialogue02 != null) dialogue02.BindFail(this);

        worldScroller = FindAnyObjectByType<WorldScroller>();
        animationScript = FindAnyObjectByType<AnimationScript>();

        // 初始都先藏起來

        if (replayButton != null)
        {
            replayButton.SetActive(false);
        }
        failWordText.gameObject.SetActive(false);
        BlackPanel.SetActive(true);
        BlackPanel2.SetActive(false);
        SettingPanel.SetActive(false);

        worldScroller.autoStartMove = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("1");
            SettingPanel.SetActive(!SettingPanel.activeSelf);
        }
    }

    // ====== 給 Dialogue Action 呼叫 ======

    public IEnumerator Act_WaitforSecond1()
    {
        yield return new WaitForSeconds(1f);
    }

    public IEnumerator Act_BlackPanelOff()
    {
        if (BlackPanel == null || animationScript == null) yield break;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);
        BlackPanel.SetActive(false);
    }

    public IEnumerator Act_StopInBlack()
    {
        worldScroller.BusMove = true;
        if (worldScroller != null)
            yield return worldScroller.StopInBlack(2.5f);

        yield return new WaitForSeconds(1f);
        BlackPanel.SetActive(true);
        animationScript.Fade(BlackPanel, 1, 0, 1, null);
        yield return new WaitForSeconds(1.5f);
    }

    public IEnumerator Act_ShowWordFail()
    {
        // 顯示文字
        if (failWordText != null) failWordText.text = failWord;

        animationScript.Fade(BlackPanel, 1f, 1f, 0f, null);
        yield return new WaitForSeconds(1.5f);

        if (failWordText != null)
        {
            failWordText.gameObject.SetActive(true);
            animationScript.Fade(failWordText.gameObject, wordFadeDuration, 0f, 1f,null);
            yield return new WaitForSeconds(1.5f);
        }

        // 等 1 秒再出現按鈕
        yield return new WaitForSeconds(replayAppearDelay);

        if (replayButton != null)
        {
            replayButton.SetActive(true);
            animationScript.Fade(replayButton, replayFadeDuration, 0f, 1f, null);
            yield return new WaitForSeconds(1.5f);
        }
    }

    public void FastOK()//快速通過劇情
    {
        dialogue02.allowFastReveal = !dialogue02.allowFastReveal;
        Debug.Log(dialogue02.allowFastReveal.ToString());
    }

    public void ResetGameG()
    {
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        // 淡出
        BlackPanel2.SetActive(true);
        animationScript.Fade(BlackPanel2, 1f, 0f, 1f, null);

        // 等淡出完成（時間要跟 Fade 一致）
        yield return new WaitForSeconds(2f);

        SceneManager.LoadScene("menu");
    }
}
