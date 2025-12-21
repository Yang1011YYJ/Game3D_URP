using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class SpotDef//題庫
{
    public int id;                     // 1..8
    public Sprite questionSprite;      // 要放到 ErrorPanel 上的圖片
    public GameObject spotRoot;        // 這題的目標 spot（或這題所有spot的父物件）
    // 也可以改成 DifferenceSpot target; 但用 root 更好控管（可塞一堆子物件）
}
