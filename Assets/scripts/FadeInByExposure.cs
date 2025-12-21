using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FadeInByExposure : MonoBehaviour
{
    public Volume volume;
    private ColorAdjustments ca;

    void Awake()
    {
        Cache();
    }

    private void Cache()
    {
        if (volume == null) { Debug.LogError("[FadeInByExposure] volume is null"); return; }
        if (volume.profile == null) { Debug.LogError("[FadeInByExposure] volume.profile is null"); return; }

        bool ok = volume.profile.TryGet(out ca);
        Debug.Log($"[FadeInByExposure] Cache ok={ok}, volume={volume.name}, profile={(volume.profile != null ? volume.profile.name : "null")}");
    }


    public IEnumerator FadeExposure(float duration, float from, float to)
    {
        if (ca == null) Cache();
        if (ca == null) yield break;

        float t = 0f;
        ca.postExposure.value = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            ca.postExposure.value = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        ca.postExposure.value = to;
    }
    public IEnumerator FlashByExposure(
    float flashValue = 8f,   // 閃光有多亮：6~10 通常很像閃燈
    float hold = 0.02f,      // 亮著停留多久：0.01~0.04
    float returnTime = 0.12f // 回到原本的時間：0.08~0.18
)
    {
        float original = GetExposure(); // ✅抓當下的曝光（很重要）

        // 瞬間拉亮
        SetExposureImmediate(flashValue);
        yield return new WaitForSeconds(hold);

        // 很快回原本（用你已有的 FadeExposure）
        yield return StartCoroutine(FadeExposure(returnTime, flashValue, original));

        // 保險：避免被其他協程干擾後卡住
        SetExposureImmediate(original);
    }


    public void SetExposureImmediate(float value)
    {
        if (ca == null) Cache();
        if (ca == null) return;

        ca.postExposure.overrideState = true;   // 保險：強制覆寫
        ca.active = true;

        ca.postExposure.value = value;
        Debug.Log($"[Exposure SET] -> {value}, now={ca.postExposure.value}, frame={Time.frameCount}");
    }

    public float GetExposure()
    {
        if (ca == null) Cache();
        if (ca == null) return 0f; // 取不到就回傳 0，避免爆炸
        return ca.postExposure.value;
    }
}
