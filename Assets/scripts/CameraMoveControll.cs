using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMoveControll : MonoBehaviour
{
    public Camera cam;
    // Start is called before the first frame update
    void Start()
    {
    }

    public IEnumerator MoveCameraTo(Vector3 targetPos, float moveSpeed)
    {

        if (cam == null)
        {
            Debug.LogError("[CameraMoveControll] Camera is NULL, please assign it in Inspector.");
            yield break;
        }

        while (Vector3.Distance(cam.transform.position, targetPos) > 0.01f)
        {
            cam.transform.position = Vector3.MoveTowards(
                cam.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        cam.transform.position = targetPos; // «O©³¹ï»ô
    }

}
