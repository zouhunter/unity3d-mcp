using UnityEngine;

public class BallController : MonoBehaviour
{
    private Rigidbody2D rb;
    public float speed = 5f;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 给球一个初始速度
        rb.velocity = new Vector2(Random.Range(-1f, 1f), -1f).normalized * speed;
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 如果撞到砖块，销毁砖块
        if (collision.gameObject.name.Contains("Brick"))
        {
            Destroy(collision.gameObject);
        }
        
        // 如果球掉到底部墙壁，重新开始
        if (collision.gameObject.name == "BottomWall")
        {
            transform.position = new Vector3(0, -2, 0);
            rb.velocity = new Vector2(Random.Range(-1f, 1f), -1f).normalized * speed;
        }
    }
}