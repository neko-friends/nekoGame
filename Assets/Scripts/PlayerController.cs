using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField,Header("プレイヤーの移動速度")]
    private float moveSpeed = 5f;//後々データから挿入できるようにする
    [SerializeField,Header("ジャンプ力")]
    private float jumpPower = 5f;
    [SerializeField]
    private GameObject PosePanel;

    private Rigidbody2D rb2d;

    private Vector2 moveInput;
    private bool isGround = true;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        PosePanel.SetActive(false);
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
    private void Move()
    {
        transform.Translate(moveInput*moveSpeed*Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && isGround)
        {
            rb2d.AddForce(Vector2.up * jumpPower, ForceMode2D.Impulse);
        }
    }
    public void OnPose(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            PosePanel.SetActive(true);
        }
    }
}
