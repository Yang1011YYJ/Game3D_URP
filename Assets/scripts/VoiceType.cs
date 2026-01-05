using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceType : MonoBehaviour
{
    public enum SFXType
    {
        None,
        Click,
        Fail,//Loop
        Success,//Loop
        NarraTalk,
        Error,
        Photo,
        PlayerWalk,//Loop
        PhoneRing//Loop
    }
    public enum BGMType
    {
        None,
        Road,
        Drive
    }
}
