using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMoveControll : MonoBehaviour
{
    [Header("玩家位置")]
    public GameObject Player;
    public Transform target; // 角色的 Transform 組件

    [Header("相機控制")]
    public float LeftestCameraPosition = -6.09f;  // 右(我的左)邊界
    public float RightestCameraPosition = 5.85f; // 左(我的右)邊界

    [Header("自動移動相關")]
    public bool isAutoMovingCamera;


    public Camera cam;
    public GameObject camF;
    // Start is called before the first frame update
    private void Awake()
    {

    }
    void Start()
    {
        isAutoMovingCamera = false;
    }
    void LateUpdate()
    {
        if (target != null && !isAutoMovingCamera)
        {
            // 只跟 X 軸
            float clampedX = Mathf.Clamp(target.position.x, LeftestCameraPosition, RightestCameraPosition);
            //平滑跟隨
            float smoothX = Mathf.Lerp(camF.transform.position.x, clampedX, Time.deltaTime * 5f);
            camF.transform.position = new Vector3(smoothX, camF.transform.position.y, camF.transform.position.z);

        }
    }

    public IEnumerator MoveCameraTo(Vector3 targetPos, float moveSpeed)
    {
        isAutoMovingCamera = true;
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

        cam.transform.position = targetPos; // 保底對齊

        isAutoMovingCamera = false;
    }

}
