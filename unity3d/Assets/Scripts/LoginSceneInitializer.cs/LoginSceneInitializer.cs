using UnityEngine;

public class LoginSceneInitializer : MonoBehaviour
{
    void Awake()
    {
        // 检查场景中是否已有登录界面
        LoginUI existingUI = FindObjectOfType<LoginUI>();
        if (existingUI == null)
        {
            // 如果没有登录界面，创建一个
            LoginUIGenerator generator = gameObject.AddComponent<LoginUIGenerator>();
            generator.CreateLoginUI();
        }
    }
}