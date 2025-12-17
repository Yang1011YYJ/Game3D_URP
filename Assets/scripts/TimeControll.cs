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

    public void StartCountdown(int seconds)
    {
        ForceEnd();
        countdownRoutine = StartCoroutine(Countdown(seconds));
    }

    public void ForceEnd()
    {
        if (countdownRoutine != null)
            StopCoroutine(countdownRoutine);

        ResetTimer();
    }

    private IEnumerator Countdown(int totalSeconds)
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

    private void ResetTimer()
    {
        isRunning = false;
        currentTime = 0;

        if (timerText != null)
            timerText.text = FormatTime(0);

        countdownRoutine = null;
    }

    protected virtual void OnCountdownEnd()
    {
        Debug.Log("倒數結束！");
        onTimeUp?.Invoke();
    }
    string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

}
