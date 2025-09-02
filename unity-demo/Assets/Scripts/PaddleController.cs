using UnityEngine;

public class PaddleController : MonoBehaviour
{
    public float speed = 10f;
    public float boundary = 5f;
    
    void Update()
    {
        float moveInput = Input.GetAxis("Horizontal");
        Vector3 newPosition = transform.position + Vector3.right * moveInput * speed * Time.deltaTime;
        
        // 限制挡板移动范围
        newPosition.x = Mathf.Clamp(newPosition.x, -boundary, boundary);
        transform.position = newPosition;
    }
}