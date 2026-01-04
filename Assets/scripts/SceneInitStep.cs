using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class SceneInitStep
{
    // Loading 上顯示的文字
    public string label;
    [Range(0.01f, 1f)]
    // 這一步占整體初始化的比例
    public float weight = 0.1f;          // 這一步占整體初始化的比例
    // 做完要不要讓一幀，避免卡
    public bool yieldAfter = true;       // 做完要不要讓一幀，避免卡

    [NonSerialized]
    // 這一步要做什麼（程式指定）
    public Func<IEnumerator> action;     // 這一步要做什麼（程式指定）
}
