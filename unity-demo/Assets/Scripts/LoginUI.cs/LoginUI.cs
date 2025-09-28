using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [Header("UI引用")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public TextMeshProUGUI statusText;

    // 用户名和密码（实际项目中应从服务器获取或加密存储）
    private string correctUsername = "admin";
    private string correctPassword = "password";

    private void Start()
    {
        // 确保引用已设置
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClick);
        }
        
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    public void OnLoginButtonClick()
    {
        if (usernameInput == null || passwordInput == null || statusText == null)
        {
            Debug.LogError("UI引用未设置！");
            return;
        }

        string username = usernameInput.text;
        string password = passwordInput.text;

        // 简单的登录验证
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            statusText.text = "请输入用户名和密码";
            statusText.color = Color.red;
            return;
        }

        // 验证用户名和密码
        if (username == correctUsername && password == correctPassword)
        {
            statusText.text = "登录成功！";
            statusText.color = Color.green;
            Debug.Log("登录成功：" + username);
            
            // 在实际项目中，这里可以跳转到主界面
            // SceneManager.LoadScene("MainScene");
        }
        else
        {
            statusText.text = "用户名或密码错误";
            statusText.color = Color.red;
            Debug.LogWarning("登录失败：" + username);
        }
    }

    // 清除输入框和状态文本
    public void ClearInputs()
    {
        if (usernameInput != null)
        {
            usernameInput.text = "";
        }

        if (passwordInput != null)
        {
            passwordInput.text = "";
        }

        if (statusText != null)
        {
            statusText.text = "";
        }
    }
}