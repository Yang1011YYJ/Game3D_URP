using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class CControll : MonoBehaviour
{
    [Header("動畫")]
    public Animator animator;
    public Sprite idle;
    [Tooltip("看手機")]public AnimationClip phone;
    [Tooltip("走路")] public AnimationClip walk;
    [Tooltip("轉身")] public AnimationClip turn;
    [Tooltip("遊戲失敗")] public AnimationClip die;
    [Tooltip("坐著睡著")] public AnimationClip sitsleep;
    [Tooltip("抓頭")] public AnimationClip Catch;
    [Tooltip("閉眼")] public AnimationClip eyeclose;
    [Tooltip("面相左邊")] public Sprite leftidle;
    [Tooltip("面左的看手機sprite")] public Sprite leftphoneidle;

    [Header("角色外表")]
    public GameObject PlayerAniAndSprite;
    public SpriteRenderer spriteRenderer;

    [Header("相機")]
    public Camera targetCamera;
    [Tooltip("鎖定X：避免上下仰俯（常用）")] public bool lockX = false; // 鎖定X：避免上下仰俯（常用）
    [Tooltip("鎖定Y：避免上下仰俯（常用）")] public bool lockY = false;
    [Tooltip("鎖定Z：避免上下仰俯（常用）")] public bool lockZ = false;

    public Rigidbody rig;

    [Header("移動參數")]
    public float moveSpeed = 5f;
    public float maxSpeed = 10f;
    [Tooltip("true = 玩家可以用方向鍵控制")]public bool playerControlEnabled = true;
    [Tooltip("是否正在自動移動")]public bool isAutoMoving = false;
    [Tooltip("自動移動的目標座標")] public Vector2 Target;
    [Tooltip("判定抵達目標的容許誤差")]public float arriveThreshold = 0.2f;
    [Tooltip("自動移動是否結束（給外部查詢用）")]public bool autoMoveFinished = false;
    float x; // 最後實際拿去移動用的輸入值

    [Header("腳本")]
    public First firstScript;

    void Awake()
    {
        // Animator
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null)
                animator = GetComponentInParent<Animator>();
        }

        // SpriteRenderer
        if (spriteRenderer == null)
        {
            spriteRenderer = PlayerAniAndSprite.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInParent<SpriteRenderer>();
        }

        // Rigidbody2D
        if (rig == null)
        {
            rig = GetComponent<Rigidbody>();
        }

        firstScript = FindAnyObjectByType<First>();
    }
    void Start()
    {

    }

    void LateUpdate()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        Vector3 camForward = targetCamera.transform.forward;

        // 如果你想要「只在水平面轉」（像站在地上），改用：
        camForward = Vector3.ProjectOnPlane(camForward, Vector3.up);

        Quaternion rot = Quaternion.LookRotation(camForward, Vector3.up);
        Vector3 e = rot.eulerAngles;

        if (lockX) e.x = transform.eulerAngles.x;
        if (lockY) e.y = transform.eulerAngles.y;
        if (lockZ) e.z = transform.eulerAngles.z;

        transform.rotation = Quaternion.Euler(e);
    }

    // Update is called once per frame
    void Update()
    {
        // 預設為 0，避免殘留
        x = 0f;

        if (isAutoMoving)
        {
            float diff = Target.x - transform.position.x;

            // 如果離目標很近，就算抵達
            if (Mathf.Abs(diff) <= arriveThreshold)
            {
                Debug.Log("離目標很近，算抵達");
                // 停下來
                x = 0f;
                isAutoMoving = false;
                autoMoveFinished = true;

                // 把座標修正到目標（避免物理晃一下）
                Vector3 pos = transform.position;
                pos.x = Target.x;
                transform.position = pos;
                // 把剛才累積的速度清空，避免滑出去
                rig.velocity = Vector2.zero;
            }
            else
            {
                // 按方向決定往左(-1)還是往右(1)
                x = Mathf.Sign(diff);
                autoMoveFinished = false;
            }
        }
        else if (playerControlEnabled)// 2. 平常玩家控制
        {
            x = Input.GetAxis("Horizontal");
            
        }
        else // 3. 其他情況（例如劇情中不給動）
        {
            x = 0f;
        }

        //動畫設定(判斷是否在走路)
        if (Mathf.Abs(x) > 0.01f)
        {
            animator.SetBool("walk", true);
            // 走路時使用側面圖
            //spriteRenderer.sprite = image_1;
            //if (x > 0)
            //{
            //    //sprite.flipX = true;
            //    //角色面向右邊
            //    spriteRenderer.flipX = true;
            //}
            //else
            //{
            //    spriteRenderer.flipX = false;
            //}
            spriteRenderer.flipX = x > 0;
        }
        else
        {
            animator.SetBool("walk", false);
            //spriteRenderer.sprite = image_0;
            // 沒在移動，用正面圖
            spriteRenderer.flipX = false;

        }
    }

    void FixedUpdate()
    {
        if (isAutoMoving)
        {
            // 自動移動：直接往目標靠近，不用 AddForce
            float step = moveSpeed * Time.fixedDeltaTime;
            Vector2 current = rig.position;
            Vector2 target = new Vector2(Target.x, current.y); // 只看 X

            Vector2 newPos = Vector2.MoveTowards(current, target, step);
            rig.MovePosition(newPos);  // 或 transform.position = newPos 也行

            // 這裡不要再 AddForce，也不要 clamp，完全自己控制
        }
        else
        {
            // 玩家控制：維持你原本的物理移動寫法
            // 角色移動 & 限速
            rig.AddForce(new Vector2(x * moveSpeed, 0f));

            rig.velocity = new Vector2(
                Mathf.Clamp(rig.velocity.x, -maxSpeed, maxSpeed),
                rig.velocity.y
            );
        }
        
    }


    /// 給外部呼叫，開始自動走到某個 X 位置
    public void StartAutoMoveTo(Vector2 Target)
    {
        this.Target = Target;
        Debug.Log("1");
        isAutoMoving = true;
        Debug.Log("2");
        autoMoveFinished = false;

        // 劇情時通常會關掉玩家控制，避免亂動
        playerControlEnabled = false;

        // 順便開啟走路動畫（Update 也會處理，不過這樣比較即時）
        animator.SetBool("walk", true);
    }

    /// 劇情結束後，恢復玩家控制
    public void EnablePlayerControl()
    {
        playerControlEnabled = true;
        isAutoMoving = false;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if(collider.tag == "ET1")//第一個觸發error地點
        {
            firstScript.eT1 = true;
        }
    }
}
