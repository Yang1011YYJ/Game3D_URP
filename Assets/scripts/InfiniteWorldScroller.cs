using System.Collections.Generic;
using UnityEngine;

public class WorldScroller : MonoBehaviour
{
    public enum MoveMode
    {
        Speed,      // 一直以速度移動（無限場景常用）
        ToTarget    // 移到指定 target（用來做 BusGo 移動一段很方便）
    }

    [Header("世界根節點（你所有 segment 的父物件）")]
    public Transform worldRoot;

    [Header("移動設定")]
    public bool BusMove = false;
    public MoveMode moveMode = MoveMode.Speed;

    [Tooltip("世界往右移動的速度（+X）")]
    public float moveSpeed = 5f;

    [Tooltip("玩家上車前移動的目標")] public Transform StopTarget;
    [Tooltip("玩家上車後移動的目標")]public Transform GoTarget;

    [Tooltip("目標模式：到達誤差")]
    public float targetEpsilon = 0.02f;

    [Header("無限場景（方案二：一個 Segment = 地板+背景 的組合 Prefab）")]
    [Tooltip("你的 segment prefab，內含 Floor+BG（兩塊都放同一個 prefab 裡）")]
    public GameObject segmentPrefab;

    [Tooltip("場景上現在已經擺好的 segment（從左到右排序）")]
    public List<Transform> segments = new();

    [Tooltip("相機（用來判斷何時露底）")]
    public Camera targetCamera;

    [Tooltip("螢幕右側要多留多少 buffer 才生成新段（避免看到空）")]
    public float spawnBuffer = 2f;

    [Tooltip("螢幕左側超出多少就刪除舊段")]
    public float despawnBuffer = 2f;

    public enum TargetType { Stop, Go }
    private TargetType currentTargetType;

    // ---- internal ----
    float segmentWidth = 0f;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (worldRoot == null) worldRoot = transform;
    }

    void Start()
    {
        // 自動抓 segment 寬度（不用你知道地板多寬）
        ResolveSegmentWidth();
    }

    void Update()
    {
        // 1) 世界移動（只讓這支腳本控制）
        TickMove();
        TickInfiniteSegments();

        // 2) 無限場景維護（生成/移除）
        TickInfiniteSegments();
    }

    // =========================
    // 移動控制
    // =========================
    void TickMove()
    {
        if (!BusMove) return;
        if (worldRoot == null) return;

        if (moveMode == MoveMode.Speed)
        {
            worldRoot.position += Vector3.right * (moveSpeed * Time.deltaTime);
            return;
        }

        // === ToTarget 模式 ===
        Transform target = null;

        if (currentTargetType == TargetType.Stop)
            target = StopTarget;
        else if (currentTargetType == TargetType.Go)
            target = GoTarget;

        if (target == null) return;

        worldRoot.position = Vector3.MoveTowards(
            worldRoot.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(worldRoot.position, target.position) <= targetEpsilon)
        {
            BusMove = false;
        }
    }

    public void StartMove_Speed(float speed)
    {
        moveMode = MoveMode.Speed;
        moveSpeed = speed;
        BusMove = true;
    }

    public void StartMove_ToStop(float speed)
    {
        moveMode = MoveMode.ToTarget;
        currentTargetType = TargetType.Stop;
        moveSpeed = speed;
        BusMove = true;
    }
    public void StartMove_ToGo(float speed)
    {
        moveMode = MoveMode.ToTarget;
        currentTargetType = TargetType.Go;
        moveSpeed = speed;
        BusMove = true;
    }

    public void StopMove()
    {
        BusMove = false;
    }

    // =========================
    // 無限場景（生成 / 刪除）
    // =========================
    void TickInfiniteSegments()
    {
        if (segmentPrefab == null) return;
        if (segments == null || segments.Count == 0) return;
        if (targetCamera == null) return;
        if (segmentWidth <= 0.001f) ResolveSegmentWidth();

        // 以相機的 viewport 右/左邊界換算世界座標
        float camZ = Mathf.Abs(targetCamera.transform.position.z - worldRoot.position.z);
        Vector3 rightWorld = targetCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, camZ));
        Vector3 leftWorld = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, camZ));

        // 取得目前最右段、最左段的 bounds（用 Renderer bounds，比你手算可靠）
        Transform rightSeg = segments[segments.Count - 1];
        Transform leftSeg = segments[0];

        Bounds rightB = GetBounds(rightSeg);
        Bounds leftB = GetBounds(leftSeg);

        // 1) 右邊快露底 → 在最右邊生成一段
        if (rightB.max.x < rightWorld.x + spawnBuffer)
        {
            SpawnToRightOf(rightSeg);
        }

        // 2) 左邊整段超出畫面太多 → 移除最左段
        if (leftB.max.x < leftWorld.x - despawnBuffer)
        {
            DespawnLeftMost();
        }
    }

    void SpawnToRightOf(Transform baseSeg)
    {
        if (segmentPrefab == null) return;

        GameObject go = Instantiate(segmentPrefab, worldRoot);
        Transform t = go.transform;

        // 放到 baseSeg 的右邊
        Bounds b = GetBounds(baseSeg);
        Vector3 pos = t.position;
        pos.x = b.max.x + (segmentWidth * 0.5f);
        t.position = pos;

        segments.Add(t);
    }

    void DespawnLeftMost()
    {
        if (segments.Count <= 1) return;

        Transform left = segments[0];
        segments.RemoveAt(0);
        if (left != null) Destroy(left.gameObject);
    }

    // =========================
    // 工具：自動算寬 / bounds
    // =========================
    void ResolveSegmentWidth()
    {
        if (segments == null || segments.Count == 0)
        {
            // 沒有手動指定 segments，就用 prefab 自己算
            if (segmentPrefab == null) return;
            segmentWidth = GetPrefabWidth(segmentPrefab);
            return;
        }

        segmentWidth = GetBounds(segments[0]).size.x;

        // 如果抓不到（例如沒 Renderer），fallback prefab
        if (segmentWidth <= 0.001f && segmentPrefab != null)
            segmentWidth = GetPrefabWidth(segmentPrefab);
    }

    Bounds GetBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);

        return b;
    }

    float GetPrefabWidth(GameObject prefab)
    {
        // 暫時生成到場景外算 bounds 寬度，再刪掉
        GameObject tmp = Instantiate(prefab);
        tmp.transform.position = new Vector3(99999, 99999, 99999);

        Renderer[] rs = tmp.GetComponentsInChildren<Renderer>();
        float w = 0f;
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);

            w = b.size.x;
        }

        Destroy(tmp);
        return w;
    }
}
