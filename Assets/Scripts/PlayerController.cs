using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField,Header("プレイヤーの移動速度")]
    private float moveSpeed = 5f;//後々データから挿入できるようにする
    [SerializeField,Header("ジャンプ力")]
    private float jumpPower = 5f;

    private Rigidbody2D rb2d;

    private Vector2 moveInput;
    private bool isGround = true;

    private GameObject Item_obj;

    private Transform Posepanel;

    private void Awake()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            Posepanel = canvas.transform.Find("PosePanel");
           
        }
    }
    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        Posepanel.gameObject.SetActive(false);
    }
    void Update()
    {
        Move();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Ground"))
        {
            isGround = true;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGround = false;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("当たった！");
        if(collision.gameObject.CompareTag("Item"))
        {
            Debug.Log("アイテムを拾った！");
            Item item = collision.gameObject.GetComponent<Item>();

            if (item != null)
            {
                // 生成するPrefabを記憶
                Item_obj = item.ItemPrefab;

                // 元のItemを削除
                Destroy(collision.gameObject);
            }
        }
    }
    private void Move()
    {
        transform.Translate(moveInput*moveSpeed*Time.deltaTime);
    }
    private void ThrowItem()
    {
        if (Item_obj == null)
        {
            return;
        }
        GameObject item = Instantiate(Item_obj,transform.position,Quaternion.identity);
        Rigidbody2D rb2d_item = item.GetComponent<Rigidbody2D>();

        if(rb2d_item != null)
        {
            rb2d.linearVelocity = Vector2.right * 10f;
        }
        Item_obj = null;    
    }

    public void OnMove(InputAction.CallbackContext context)//移動
    {
        moveInput = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)//ジャンプ
    {
        if (context.started && isGround)
        {
            rb2d.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
        }
    }
    public void OnPose(InputAction.CallbackContext context)//ポーズ
    {
        if(context.started)
        {
            Posepanel.gameObject.SetActive(true);
        }
    }
    public void OnThrow(InputAction.CallbackContext context)//投げ
    {
        if (context.started)
        {
            ThrowItem();
        }

    }
}
