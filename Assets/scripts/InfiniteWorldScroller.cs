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
    [Tooltip("安全段數")] int keepSegmentCount = 3;
    [SerializeField] float despawnWorldX = 5.5f; // 右側安全刪除線

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
        //TickInfiniteSegments();

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
        if (segmentWidth <= 0.001f) ResolveSegmentWidth();

        // ✅ 右邊：清掉太遠的段（世界往右推會把右邊推走）
        if (segments.Count > 0 && ShouldDespawnRight(segments[^1]))
        {
            DespawnRightMost();
        }

        // ✅ 左邊：補到固定段數
        while (segments.Count < keepSegmentCount)
        {
            SpawnToLeftOf(segments[0]);
        }
    }
    bool ShouldDespawnRight(Transform seg)
    {
        return GetBounds(seg).min.x > despawnWorldX;
    }

    void SpawnToLeftOf(Transform baseSeg)
    {
        GameObject go = Instantiate(segmentPrefab);
        Transform t = go.transform;
        t.SetParent(worldRoot, true);

        Bounds baseB = GetBounds(baseSeg);
        Bounds newB = GetBounds(t);

        // 讓 newSeg 的 max.x 貼到 baseSeg 的 min.x
        float dx = baseB.min.x - newB.max.x;

        // y/z 對齊
        float dy = baseSeg.position.y - t.position.y;
        float dz = baseSeg.position.z - t.position.z;

        t.position += new Vector3(dx, dy, dz);

        // 因為是最左邊補，所以插到 List 的最前面
        segments.Insert(0, t);
    }


    void DespawnRightMost()
    {
        if (segments.Count <= 1) return;

        int last = segments.Count - 1;
        Transform right = segments[last];
        segments.RemoveAt(last);
        if (right != null) Destroy(right.gameObject);
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
