using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingManager : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup loadingGroup;
    public Slider progressBar;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI tipText;

    [Header("Config")]
    public float fadeDuration = 0.35f;

    [Tooltip("載入場景佔總進度比例（剩下比例給場景初始化）")]
    [Range(0.1f, 0.9f)] public float sceneLoadWeight = 0.7f;

    [Header("Tips")]
    [TextArea] public string[] tips;

    [SerializeField] private bool autoBootToMenu = false;
    private static bool _bootedOnce = false;

    public static LoadingManager Instance { get; private set; }

    private string _pendingScene;


    private static string _nextSceneName;

    
    private void Awake()
    {
        if (Instance != null) { Destroy(transform.root.gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
    }
    // 給外部呼叫：開始載入某個場景
    public void BeginLoad(string sceneName)
    {
        _pendingScene = sceneName;
        if (string.IsNullOrEmpty(_pendingScene))
        {
            Debug.LogError("[LoadingManager] next scene is null. Did you call BeginLoad() ?");
            return;
        }
        
        StopAllCoroutines();
        ShowLoading();
        SetTip();
        StartCoroutine(LoadFlow(sceneName));
    }

    private void Start()
    {
        if (!_bootedOnce && SceneManager.GetActiveScene().name == "LoadingScene")
        {
            _bootedOnce = true;
            BeginLoad("menu");
        }
    }

    private IEnumerator LoadFlow(string targetScene)
    {
        // 0%：開始
        SetProgress(0f, "準備載入…");
        yield return null;

        // 1) Async 載入目標場景（先不啟用）
        var op = SceneManager.LoadSceneAsync(targetScene);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float p01 = Mathf.Clamp01(op.progress / 0.9f);       // 0~1
            float weighted = p01 * sceneLoadWeight;               // 0~sceneLoadWeight
            SetProgress(weighted, "載入場景資源…");
            yield return null;
        }

        // 2) 啟用場景（進入目標場景，但 loading UI 仍在）
        SetProgress(sceneLoadWeight, "啟用場景…");
        op.allowSceneActivation = true;

        // 等待場景切換完成一點點（至少一幀）
        // 等到 ActiveScene 真的切成 targetScene
        yield return new WaitUntil(() => SceneManager.GetSceneByName(targetScene).isLoaded);

        var scene = SceneManager.GetSceneByName(targetScene);
        SceneManager.SetActiveScene(scene);
        yield return null;

        var initializer = FindInitializerInActiveScene(scene);
        if (initializer == null)
        {
            // 找不到也不要卡死，直接淡出
            Debug.LogWarning("[LoadingManager] No ISceneInitializable found in scene.");
            SetProgress(1f, "完成！");
            yield return new WaitForSeconds(0.2f);
            yield return FadeOutLoading();
            yield break;
        }

        // 4) 跑場景初始化步驟（剩下進度）
        List<SceneInitStep> steps = initializer.BuildInitSteps();
        if (steps == null || steps.Count == 0)
        {
            SetProgress(1f, "完成！");
            yield return new WaitForSeconds(0.2f);
            yield return FadeOutLoading();
            yield break;
        }

        float initStart = sceneLoadWeight;
        float initBudget = 1f - sceneLoadWeight;

        float totalWeight = 0f;
        foreach (var s in steps) totalWeight += Mathf.Max(0.0001f, s.weight);

        float accumulated = 0f;

        foreach (var step in steps)
        {
            float w = Mathf.Max(0.0001f, step.weight);
            float stepStart = initStart + initBudget * (accumulated / totalWeight);
            float stepEnd = initStart + initBudget * ((accumulated + w) / totalWeight);
            accumulated += w;

            SetProgress(stepStart, step.label);
            yield return null;

            if (step.action != null)
            {
                IEnumerator it = step.action.Invoke();
                // 讓進度在這一步期間平滑往 stepEnd 靠近
                while (it.MoveNext())
                {
                    float cur = progressBar ? progressBar.value : stepStart;
                    float next = Mathf.MoveTowards(cur, stepEnd, Time.unscaledDeltaTime * 0.6f);
                    SetProgress(next, step.label);
                    yield return it.Current;
                }
            }

            SetProgress(stepEnd, step.label);
            if (step.yieldAfter) yield return null;
        }

        // 5) 完成 → 淡出
        SetProgress(1f, "完成！");
        yield return new WaitForSeconds(0.08f);
        yield return FadeOutLoading();
    }

    private ISceneInitializable FindInitializerInActiveScene(Scene scene)
    {
        // 找場景中任何 MonoBehaviour 有實作介面即可
        // Resources.FindObjectsOfTypeAll 可以找 inactive，但比較重；
        // 這裡用 FindObjectsByType（Unity 2022+）最輕巧。

        //Scene active = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        foreach (var root in roots)
        {
            // 找 root 底下所有 MonoBehaviour（含 inactive 也可選）
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb is ISceneInitializable init)
                    return init;
            }
        }

        return null;
    }

    // ==== UI helpers ====
    private void ShowLoading()
    {
        if (!loadingGroup) return;
        loadingGroup.alpha = 1;
        loadingGroup.blocksRaycasts = true;
        loadingGroup.interactable = true;
    }

    private IEnumerator FadeOutLoading()
    {
        if (!loadingGroup) yield break;

        float t = 0f;
        float start = loadingGroup.alpha;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            loadingGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }

        loadingGroup.alpha = 0f;
        loadingGroup.blocksRaycasts = false;
        loadingGroup.interactable = false;
    }

    private void SetProgress(float value01, string msg)
    {
        if (progressBar) progressBar.value = Mathf.Clamp01(value01);
        if (statusText) statusText.text = msg;
    }

    private void SetTip()
    {
        if (!tipText || tips == null || tips.Length == 0) return;
        tipText.text = tips[Random.Range(0, tips.Length)];
    }
}
