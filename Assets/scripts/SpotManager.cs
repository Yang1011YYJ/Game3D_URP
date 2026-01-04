using System;
using UnityEngine;
using UnityEngine.UI;

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
    //public int mistakesPerRound = 2;

    [Header("Lives (Global)")]
    public int totalLives = 2;                 // ✅ 整局只有兩命
    [SerializeField] public int livesLeft = 2; // ✅ 目前剩幾命

    [Header("Runtime (Read Only)")]
    [SerializeField] public int roundIndex = 0;     // 目前第幾回合（1~8）
    [SerializeField] public int totalFound = 0;     // 目前總共找到幾個（0~8）
    //[SerializeField] public int mistakesLeft = 0;   // 本回合剩幾次機會（2->1->0）
    [SerializeField] private bool roundRunning = false;
    [SerializeField] private bool spotFoundThisRound = false;
    [Header("Runtime (Read Only)")]
    [SerializeField] public int consecutiveFailedRounds = 0; // ✅連續失敗回合數
    [Tooltip("回合是否已結算")][SerializeField] private bool roundResolved = false;

    public bool IsConsecutiveFailGameOver(int failLimit = 2) => consecutiveFailedRounds >= failLimit;


    // 事件給 First/Second 接演出
    public event Action<int> OnRoundBegan;                  // (roundIndex, mistakesLeft)
    public event Action<int> OnLivesChanged;                      // ✅ (livesLeft)
    public event Action<int, int, RoundEndType> OnRoundEnded;    // (roundIndex, totalFound, endType)
    public event Action<int> OnTotalFoundChanged;                // (totalFound)
    public void ResetGame()
    {
        roundIndex = 0;
        totalFound = 0;
        livesLeft = totalLives;
        roundRunning = false;
        spotFoundThisRound = false;

        OnLivesChanged?.Invoke(livesLeft);
        OnTotalFoundChanged?.Invoke(totalFound);
    }

    // ✅ 劇情：開始一回合（不會自動開 timer）
    public void BeginRound()
    {
        if (IsGameEnded()) return;

        roundIndex++;
        spotFoundThisRound = false;
        roundResolved = false;   // ⭐重點
        roundRunning = true;

        OnRoundBegan?.Invoke(roundIndex);
        OnLivesChanged?.Invoke(livesLeft);
        OnTotalFoundChanged?.Invoke(totalFound);
    }

     //✅ 劇情：如果你想「同一回合重來」(例如你做完演出後才允許繼續點)
    public void ResumeRound()
    {
        if (IsGameEnded()) return;
        if (spotFoundThisRound) return;
        roundRunning = true;
        roundResolved = false;
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
        if (roundResolved) return;   // ⭐

        roundResolved = true;        // ⭐先鎖

        spotFoundThisRound = true;
        roundRunning = false;

        totalFound++;
        OnTotalFoundChanged?.Invoke(totalFound);

        //consecutiveFailedRounds = 0; // ✅成功就中斷連敗


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
        if (roundResolved) return;   // ⭐

        roundResolved = true;        // ⭐

        ApplyFailAction(); // ✅ 點錯就是一次失敗行為
    }

    // 入口3：超時（由 TimeControll.onTimeUp 呼叫進來）
    public void OnTimeout()
    {
        if (!roundRunning) return;
        if (spotFoundThisRound) return;
        if (roundResolved) return;   // ⭐

        //roundResolved = true;        // ⭐

        ApplyFailAction(); // ✅ 超時就是一次失敗行為
    }


    private void ApplyFailAction()
    {
        livesLeft--;
        // 發生失誤，立即停止回合運行，防止重複點擊
        roundRunning = false;
        roundResolved = true; // 先鎖住，直到 Second.cs 決定下一步
        OnLivesChanged?.Invoke(livesLeft);
    }

    // 新增一個方法供 Second.cs 在「再試一次」時呼叫
    public void ReadyToRetry()
    {
        roundResolved = false; // 演出完畢，正式解鎖「可點擊」狀態
        roundRunning = true;   // 開啟計時與點擊
    }

    public void ForceRoundEnd()
    {
        //if (roundResolved) return;

        roundResolved = true;
        roundRunning = false;

        OnRoundEnded?.Invoke(roundIndex, totalFound, RoundEndType.FailedRound);
    }


    // 狀態查詢
    public bool IsWin() => totalFound >= needFoundToWin;
    // ✅ 結束條件：贏、沒命、或 8 回合跑完
    public bool IsGameEnded() => IsWin() || livesLeft <= 0 || roundIndex >= totalRounds;

    public int RoundIndex => roundIndex;
    public int TotalFound => totalFound;
    public int LivesLeft => livesLeft;
    public bool RoundRunning => roundRunning;
}
