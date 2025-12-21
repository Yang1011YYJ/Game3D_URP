using System;
using UnityEngine;

public enum RoundEndType
{
    FoundSpot,
    FailedRound
}
public class SpotManager : MonoBehaviour
{
    [Header("Game Rules")]
    public int totalRounds = 8;
    public int needFoundToWin = 5;
    public int mistakesPerRound = 2;

    [Header("Runtime (Read Only)")]
    [SerializeField] public int roundIndex = 0;     // 目前第幾回合（1~8）
    [SerializeField] public int totalFound = 0;     // 目前總共找到幾個（0~8）
    [SerializeField] public int mistakesLeft = 0;   // 本回合剩幾次機會（2->1->0）
    [SerializeField] private bool roundRunning = false;
    [SerializeField] private bool spotFoundThisRound = false;

    // 事件給 First/Second 接演出
    public event Action<int, int> OnRoundBegan;                  // (roundIndex, mistakesLeft)
    public event Action<int, int> OnMistakeChanged;              // (roundIndex, mistakesLeft)
    public event Action<int, int, RoundEndType> OnRoundEnded;    // (roundIndex, totalFound, endType)
    public event Action<int> OnTotalFoundChanged;                // (totalFound)

    // ✅ 劇情：開始一回合（不會自動開 timer）
    public void BeginRound()
    {
        if (IsGameEnded()) return;

        roundIndex++;
        mistakesLeft = mistakesPerRound;
        spotFoundThisRound = false;
        roundRunning = true;

        OnRoundBegan?.Invoke(roundIndex, mistakesLeft);
        OnMistakeChanged?.Invoke(roundIndex, mistakesLeft);
        OnTotalFoundChanged?.Invoke(totalFound);
    }

    // ✅ 劇情：如果你想「同一回合重來」(例如你做完演出後才允許繼續點)
    public void ResumeRound()
    {
        if (IsGameEnded()) return;
        if (spotFoundThisRound) return;
        roundRunning = true;
    }

    public void PauseRound()
    {
        roundRunning = false;
    }

    //// 入口1：點到 spot
    //public void OnSpotClicked()
    //{
    //    if (currentPhase == GamePhase.Teaching)
    //    {
    //        OnTeachSpotClicked();
    //        return;
    //    }

    //    if (currentPhase == GamePhase.Playing)
    //    {
    //        OnPlaySpotClick();
    //        return;
    //    }
        
    //}
    //遊戲的
    public void OnPlaySpotClick()
    {
        if (!roundRunning) return;
        if (spotFoundThisRound) return;

        spotFoundThisRound = true;
        roundRunning = false;

        totalFound++;
        OnTotalFoundChanged?.Invoke(totalFound);

        OnRoundEnded?.Invoke(roundIndex, totalFound, RoundEndType.FoundSpot);
    }

    //教學的
    //public void OnTeachSpotClicked()
    //{
    //    Debug.Log("[Teach] Spot clicked!");

        

    //    // 如果你的教學是用 Dialogue index 推進：
    //    // dialogueSystemGame00Script.NextLine();  // ← 依你系統實作
    //    Act_RequestTeach2(); // 或者你要接 Teach2
    //}


    // 入口2：點到背景
    public void OnBackgroundClicked()
    {
        if (!roundRunning) return;
        if (spotFoundThisRound) return;

        ApplyMistake();
    }

    // 入口3：超時（由 TimeControll.onTimeUp 呼叫進來）
    public void OnTimeout()
    {
        if (!roundRunning) return;
        if (spotFoundThisRound) return;

        ApplyMistake();
    }


    private void ApplyMistake()
    {
        mistakesLeft--;
        OnMistakeChanged?.Invoke(roundIndex, mistakesLeft);

        if (mistakesLeft > 0)
        {
            // ✅ 還有機會：回合仍在進行中，但「要不要立刻重新開始計時」由劇情決定
            // SpotManager 不碰 timer
            return;
        }

        // ✅ 本回合失敗
        roundRunning = false;
        OnRoundEnded?.Invoke(roundIndex, totalFound, RoundEndType.FailedRound);
    }

    // 狀態查詢
    public bool IsWin() => totalFound >= needFoundToWin;
    public bool IsGameEnded() => IsWin() || roundIndex >= totalRounds;

    public int RoundIndex => roundIndex;
    public int TotalFound => totalFound;
    public int MistakesLeft => mistakesLeft;
    public bool RoundRunning => roundRunning;
}
