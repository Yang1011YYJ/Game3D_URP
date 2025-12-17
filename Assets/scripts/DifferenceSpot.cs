using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifferenceSpot : MonoBehaviour
{
    [Header("圈圈預置物")]
    public GameObject circlePrefab;
    [Tooltip("圈圈要放在哪個 UI 爸爸下面")] public RectTransform canvasRect;

    [Header("回合判定")]
    public bool requireInFrame = true;    // 需要拍照框罩住才算

    bool found = false;

    [Tooltip("計算圈圈數量")]
    public SpotManager manager;
    public Second second;                 // 由 Second/Manager 綁進來

    public void OnClickSpot()
    {
        if (found) return;

        // ✅ 需要框內才算
        if (requireInFrame && second != null)
        {
            if (!second.IsSpotInsideFrame(this))
            {
                second.ConsumeLife("沒對準！");
                return;
            }
        }

        found = true;

        // ✅ 找到後鎖住按鈕避免重複點
        var btn = GetComponent<Button>();
        if (btn != null) btn.interactable = false;

        // 通知統計
        if (manager != null) manager.OnSpotFound(this);

        // 觸發「閃光+文字+可能結束」
        if (second != null) second.OnSpotCaptured(this);
    }

    public void ResetSpot()
    {
        found = false;
    }
}
