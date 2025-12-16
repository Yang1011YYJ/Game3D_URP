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
        if (volume != null && volume.profile != null)
            volume.profile.TryGet(out ca);
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

    public void SetExposureImmediate(float value)
    {
        if (ca == null) Cache();
        if (ca == null) return;

        ca.postExposure.value = value;
    }
    public float GetExposure()
    {
        if (ca == null) Cache();
        if (ca == null) return 0f; // 取不到就回傳 0，避免爆炸
        return ca.postExposure.value;
    }
}
