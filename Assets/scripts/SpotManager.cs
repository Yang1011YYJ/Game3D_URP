using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.UI;

public class SpotManager : MonoBehaviour
{
    [Header("Round Owner")]
    public Second second; // 在 Inspector 拖 Second 進來


    public List<DifferenceSpot> activeSpots = new List<DifferenceSpot>();// 自動抓，不用手動填

    public int totalCount;          // 總共有幾個 spot
    public int foundCount;          // 已經找到幾個
                                    // Start is called before the first frame update
                                    // 自動抓場景中所有 DifferenceSpot（包含沒啟用的，用 true）
    [Header("UI")]
    public TextMeshProUGUI text;//計算數量

    void Awake()
    {
        text.gameObject.SetActive(false);
    }
    public void RefreshActiveSpots()
    {
        activeSpots.Clear();

        DifferenceSpot[] allSpots = FindObjectsOfType<DifferenceSpot>(true);

        foreach (var s in allSpots)
        {
            if (s.gameObject.activeInHierarchy)
            {
                activeSpots.Add(s);

                s.manager = this;
                s.second = second;   // ✅ 綁 Second
                s.ResetSpot();
            }
        }

        totalCount = activeSpots.Count;
        foundCount = 0;

        if (text != null)
            text.text = $"{foundCount} / {totalCount}";

        Debug.Log($"[SpotManager] 總共有 {totalCount} 個可以找的地方");
    }

    //被 DifferenceSpot 呼叫：某個 spot 被找到時進來
    public void OnSpotFound(DifferenceSpot spot)
    {
        if (!activeSpots.Contains(spot))
        {
            Debug.Log($"[SpotManager] 不在 active 列表內的 spot：{spot.name}");
            return;
        }

        foundCount++;

        if (text != null)
            text.text = $"{foundCount} / {totalCount}";

        Debug.Log($"[SpotManager] 找到第 {foundCount} 個，進度：{foundCount}/{totalCount}");

        if (foundCount >= totalCount)
        {
            Debug.Log("[SpotManager] 全部找完啦！");

        }
    }

    public void ClearAllCircles()
    {
        CircleFill[] circles = FindObjectsOfType<CircleFill>(true);

        foreach (var c in circles)
        {
            Destroy(c.gameObject);
        }

        Debug.Log("[SpotManager] 已清掉所有標記圈圈");
    }
    public void SetSpotsInteractable(bool on)
    {
        foreach (var s in activeSpots)
        {
            var btn = s.GetComponent<Button>();
            if (btn != null) btn.interactable = on;
        }
    }
}
