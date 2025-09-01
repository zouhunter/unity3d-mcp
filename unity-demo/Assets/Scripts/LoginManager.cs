using UnityEngine;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    public InputField usernameField;
    public InputField passwordField;
    public Button loginButton;
    public Text messageText;

    [Header("Login Settings")]
    public string correctUsername = "admin";
    public string correctPassword = "123456";

    void Start()
    {
        // 为登录按钮添加点击事件
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
        }
    }

    public void OnLoginButtonClicked()
    {
        string username = usernameField?.text ?? "";
        string password = passwordField?.text ?? "";

        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("请输入用户名", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowMessage("请输入密码", Color.red);
            return;
        }

        // 验证登录信息
        if (username == correctUsername && password == correctPassword)
        {
            ShowMessage("登录成功！", Color.green);
            Debug.Log("用户登录成功: " + username);
            OnLoginSuccess();
        }
        else
        {
            ShowMessage("用户名或密码错误！", Color.red);
            Debug.Log("登录失败，用户名: " + username);
        }
    }

    private void OnLoginSuccess()
    {
        Debug.Log("执行登录成功后的操作");
        gameObject.SetActive(false);
    }

    private void ShowMessage(string message, Color color)
    {
        Debug.Log(message);
    }

    public void SetLoginCredentials(string username, string password)
    {
        correctUsername = username;
        correctPassword = password;
    }
}