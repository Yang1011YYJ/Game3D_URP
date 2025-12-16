using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FadeInByExposure : MonoBehaviour
{
    public Volume volume;
    ColorAdjustments ca;

    void Awake()
    {
        if (volume && volume.profile) volume.profile.TryGet(out ca);
    }

    public IEnumerator FadeExposure(float duration, float from, float to)
    {
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
}
