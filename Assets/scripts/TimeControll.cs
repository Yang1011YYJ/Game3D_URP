using System.Collections;
using TMPro;
using UnityEngine;

public class TimeControll : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI timerText;
    private Coroutine countdownRoutine;

    [Header("時間")]
    public bool isRunning = false;
    public int currentTime = 0;
    public System.Action onTimeUp;

    private void Start()
    {
        if (timerText != null) timerText.gameObject.SetActive(false);
    }

    public void StartCountdown(int seconds)//開始
    {
        ForceEnd();
        if (timerText != null) timerText.gameObject.SetActive(true);
        countdownRoutine = StartCoroutine(Countdown(seconds));
    }

    public void ForceEnd()//停止
    {
        if (countdownRoutine != null)
            StopCoroutine(countdownRoutine);
        if (timerText != null) timerText.gameObject.SetActive(false);
        ResetTimer();
    }

    private IEnumerator Countdown(int totalSeconds)//計時功能
    {
        isRunning = true;
        currentTime = totalSeconds;
        timerText.color = Color.black;

        while (currentTime > 0)
        {
            if (timerText != null)
                timerText.text = FormatTime(currentTime);

            yield return new WaitForSeconds(1f);
            currentTime--;

            if (currentTime <= 5)
                timerText.color = Color.red;
        }

        if (timerText != null)
            timerText.text = FormatTime(0);

        OnCountdownEnd();
        ResetTimer();
    }

    private void ResetTimer()//重置
    {
        isRunning = false;
        currentTime = 0;

        if (timerText != null)
            timerText.text = FormatTime(0);
        if (timerText != null) timerText.gameObject.SetActive(false);
        countdownRoutine = null;
    }

    protected virtual void OnCountdownEnd()//到屬結束
    {
        Debug.Log("倒數結束！");
        onTimeUp?.Invoke();
    }
    string FormatTime(int totalSeconds)//文字設計
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

}
