using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField]
    private float E_moveSpeed = -3f;//後々データから挿入できるようにする
    [SerializeField]
    private Vector2 E_move;

    private Rigidbody2D E_rb2d;

    private float E_knockbackPower_back = 3f;
    private float E_knockbackPower_up = 1f;

    void Start()
    {
        E_rb2d = GetComponent<Rigidbody2D>();
        E_move.x = E_moveSpeed;
    }
   void Update()
    {
        EnemyMove();
    }

    private void EnemyMove()
    {
        transform.Translate(E_move * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        E_rb2d.AddForce(Vector2.right * E_knockbackPower_back, ForceMode2D.Impulse);
        E_rb2d.AddForce(Vector2.up *  E_knockbackPower_up, ForceMode2D.Impulse);
    }
}
