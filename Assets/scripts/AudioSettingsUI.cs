using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using static VoiceType;
using static Unity.Collections.AllocatorManager;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Mixer")]
    public AudioMixer mixer;

    [Header("Sliders (0~1)")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("音效")]
    public AudioSource SFX;
    public AudioSource loopSfxSource;
    public AudioSource loopBGMSource;


    // 你在 AudioMixer Exposed Parameters 設的名字
    public const string MUSIC_PARAM = "MusicVol";
    public const string SFX_PARAM = "SFXVol";

    private SFXType currentLoop = SFXType.None;
    private BGMType BGMcurrentLoop = BGMType.None;

    [System.Serializable]
    public class SFXEntry
    {
        public SFXType type;
        public AudioClip clip;
    }
    [System.Serializable]
    public class BGMEntry
    {
        public BGMType type;
        public AudioClip clip;
    }

    public List<SFXEntry> sfxTable;

    public List<BGMEntry> BGMTable;

    private void Start()
    {
        
    }

    public void ApplyMusic(float value01)
    {
        mixer.SetFloat(MUSIC_PARAM, LinearToDb(value01));
        PlayerPrefs.SetFloat(MUSIC_PARAM, value01);
    }

    public void ApplySFX(float value01)
    {
        mixer.SetFloat(SFX_PARAM, LinearToDb(value01));
        PlayerPrefs.SetFloat(SFX_PARAM, value01);
    }

    // 0~1 轉成 dB：0 代表靜音，1 代表 0dB（原音量）
    public float LinearToDb(float value)
    {
        if (value <= 0.0001f) return -80f; // 幾乎靜音（Mixer 常用最低）
        return Mathf.Log10(value) * 20f;
    }

    public void PlaySFX(SFXType type)
    {
        var entry = sfxTable.Find(e => e.type == type);
        if (entry != null)
            SFX.PlayOneShot(entry.clip);
    }

    public void PlayLoopSFX(SFXType type)
    {
        var entry = sfxTable.Find(e => e.type == type);
        if (entry == null || loopSfxSource == null) return;

        // 已經在播同一個 loop，就不用重播
        if (loopSfxSource.isPlaying && currentLoop == type)
            return;

        loopSfxSource.Stop(); // 換之前先停
        loopSfxSource.clip = entry.clip;
        loopSfxSource.loop = true;
        loopSfxSource.Play();

        currentLoop = type;
    }

    public void StopLoopSFX()
    {
        if (loopSfxSource == null) return;

        if (loopSfxSource.isPlaying)
            loopSfxSource.Stop();

        loopSfxSource.clip = null;
        currentLoop = SFXType.None;
    }

    public void PlayLoopBGM(BGMType type)
    {
        var entry = BGMTable.Find(e => e.type == type);
        if (entry == null || loopBGMSource == null) return;

        // 已經在播同一個 loop，就不用重播
        if (loopBGMSource.isPlaying && BGMcurrentLoop == type)
            return;

        loopBGMSource.Stop(); // 換之前先停
        loopBGMSource.clip = entry.clip;
        loopBGMSource.loop = true;
        loopBGMSource.Play();

        BGMcurrentLoop = type;
    }

    public void StopLoopBGM()
    {
        if (loopBGMSource == null) return;

        if (loopBGMSource.isPlaying)
            loopBGMSource.Stop();

        loopBGMSource.clip = null;
        BGMcurrentLoop = BGMType.None;
    }

    public void PlayClick()
    {
        PlaySFX(SFXType.Click);
    }

    public void PlayError()
    {
        PlaySFX(SFXType.Error);
    }

    public void PlaySuccess()
    {
        PlaySFX(SFXType.Success);
    }

    public void PlayPhoto()
    {
        PlaySFX(SFXType.Photo);
    }

    public void PlayPlayerWalk()//Loop
    {
        PlayLoopSFX(SFXType.PlayerWalk);
    }

    public void PlayNarraTalk()//Loop
    {
        PlayLoopSFX(SFXType.NarraTalk);
    }

    public void PlayPhoneRing()//Loop
    {
        PlayLoopSFX(SFXType.PhoneRing);
    }
    
    public void PlayFail()//Loop
    {
        PlayLoopSFX(SFXType.Fail);
    }

    public void StopLoop()
    {
        StopLoopSFX();
    }

    public void PlayDrive()//Loop
    {
        PlayLoopBGM(BGMType.Drive);
    }
    public void StopBGMLoop()
    {
        StopLoopBGM();
    }

}
