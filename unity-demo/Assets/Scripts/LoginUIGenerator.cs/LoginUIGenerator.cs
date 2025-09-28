using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUIGenerator : MonoBehaviour
{
    void Start()
    {
        CreateLoginUI();
    }

    public void CreateLoginUI()
    {
        // 创建Canvas
        GameObject canvasObj = new GameObject("LoginCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 创建登录面板
        GameObject panelObj = new GameObject("LoginPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 350);

        // 创建标题
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "用户登录";
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1);
        titleRect.anchorMax = new Vector2(0.5f, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -40);
        titleRect.sizeDelta = new Vector2(200, 50);

        // 创建用户名输入框
        GameObject usernameObj = new GameObject("UsernameInput");
        usernameObj.transform.SetParent(panelObj.transform, false);
        Image usernameImage = usernameObj.AddComponent<Image>();
        usernameImage.color = Color.white;
        TMP_InputField usernameInput = usernameObj.AddComponent<TMP_InputField>();
        RectTransform usernameRect = usernameObj.GetComponent<RectTransform>();
        usernameRect.anchorMin = new Vector2(0.5f, 0.5f);
        usernameRect.anchorMax = new Vector2(0.5f, 0.5f);
        usernameRect.pivot = new Vector2(0.5f, 0.5f);
        usernameRect.anchoredPosition = new Vector2(0, 30);
        usernameRect.sizeDelta = new Vector2(300, 50);

        // 创建用户名输入框的文本
        GameObject usernamePlaceholderObj = new GameObject("Placeholder");
        usernamePlaceholderObj.transform.SetParent(usernameObj.transform, false);
        TextMeshProUGUI usernamePlaceholder = usernamePlaceholderObj.AddComponent<TextMeshProUGUI>();
        usernamePlaceholder.text = "请输入用户名";
        usernamePlaceholder.fontSize = 18;
        usernamePlaceholder.color = new Color(0.5f, 0.5f, 0.5f, 1);
        RectTransform usernamePlaceholderRect = usernamePlaceholderObj.GetComponent<RectTransform>();
        usernamePlaceholderRect.anchorMin = Vector2.zero;
        usernamePlaceholderRect.anchorMax = Vector2.one;
        usernamePlaceholderRect.offsetMin = new Vector2(10, 0);
        usernamePlaceholderRect.offsetMax = new Vector2(-10, 0);

        GameObject usernameTextObj = new GameObject("Text");
        usernameTextObj.transform.SetParent(usernameObj.transform, false);
        TextMeshProUGUI usernameText = usernameTextObj.AddComponent<TextMeshProUGUI>();
        usernameText.fontSize = 18;
        usernameText.color = Color.black;
        RectTransform usernameTextRect = usernameTextObj.GetComponent<RectTransform>();
        usernameTextRect.anchorMin = Vector2.zero;
        usernameTextRect.anchorMax = Vector2.one;
        usernameTextRect.offsetMin = new Vector2(10, 0);
        usernameTextRect.offsetMax = new Vector2(-10, 0);

        usernameInput.textComponent = usernameText;
        usernameInput.placeholder = usernamePlaceholder;

        // 创建密码输入框
        GameObject passwordObj = new GameObject("PasswordInput");
        passwordObj.transform.SetParent(panelObj.transform, false);
        Image passwordImage = passwordObj.AddComponent<Image>();
        passwordImage.color = Color.white;
        TMP_InputField passwordInput = passwordObj.AddComponent<TMP_InputField>();
        passwordInput.contentType = TMP_InputField.ContentType.Password;
        RectTransform passwordRect = passwordObj.GetComponent<RectTransform>();
        passwordRect.anchorMin = new Vector2(0.5f, 0.5f);
        passwordRect.anchorMax = new Vector2(0.5f, 0.5f);
        passwordRect.pivot = new Vector2(0.5f, 0.5f);
        passwordRect.anchoredPosition = new Vector2(0, -30);
        passwordRect.sizeDelta = new Vector2(300, 50);

        // 创建密码输入框的文本
        GameObject passwordPlaceholderObj = new GameObject("Placeholder");
        passwordPlaceholderObj.transform.SetParent(passwordObj.transform, false);
        TextMeshProUGUI passwordPlaceholder = passwordPlaceholderObj.AddComponent<TextMeshProUGUI>();
        passwordPlaceholder.text = "请输入密码";
        passwordPlaceholder.fontSize = 18;
        passwordPlaceholder.color = new Color(0.5f, 0.5f, 0.5f, 1);
        RectTransform passwordPlaceholderRect = passwordPlaceholderObj.GetComponent<RectTransform>();
        passwordPlaceholderRect.anchorMin = Vector2.zero;
        passwordPlaceholderRect.anchorMax = Vector2.one;
        passwordPlaceholderRect.offsetMin = new Vector2(10, 0);
        passwordPlaceholderRect.offsetMax = new Vector2(-10, 0);

        GameObject passwordTextObj = new GameObject("Text");
        passwordTextObj.transform.SetParent(passwordObj.transform, false);
        TextMeshProUGUI passwordText = passwordTextObj.AddComponent<TextMeshProUGUI>();
        passwordText.fontSize = 18;
        passwordText.color = Color.black;
        RectTransform passwordTextRect = passwordTextObj.GetComponent<RectTransform>();
        passwordTextRect.anchorMin = Vector2.zero;
        passwordTextRect.anchorMax = Vector2.one;
        passwordTextRect.offsetMin = new Vector2(10, 0);
        passwordTextRect.offsetMax = new Vector2(-10, 0);

        passwordInput.textComponent = passwordText;
        passwordInput.placeholder = passwordPlaceholder;

        // 创建状态文本
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.fontSize = 16;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = Color.red;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0, -70);
        statusRect.sizeDelta = new Vector2(300, 30);

        // 创建登录按钮
        GameObject loginBtnObj = new GameObject("LoginButton");
        loginBtnObj.transform.SetParent(panelObj.transform, false);
        Image loginBtnImage = loginBtnObj.AddComponent<Image>();
        loginBtnImage.color = new Color(0.2f, 0.6f, 1f, 1f);
        Button loginBtn = loginBtnObj.AddComponent<Button>();
        loginBtn.targetGraphic = loginBtnImage;
        RectTransform loginBtnRect = loginBtnObj.GetComponent<RectTransform>();
        loginBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        loginBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        loginBtnRect.pivot = new Vector2(0.5f, 0.5f);
        loginBtnRect.anchoredPosition = new Vector2(0, -120);
        loginBtnRect.sizeDelta = new Vector2(200, 50);

        // 创建登录按钮的文本
        GameObject loginBtnTextObj = new GameObject("Text");
        loginBtnTextObj.transform.SetParent(loginBtnObj.transform, false);
        TextMeshProUGUI loginBtnText = loginBtnTextObj.AddComponent<TextMeshProUGUI>();
        loginBtnText.text = "登录";
        loginBtnText.fontSize = 20;
        loginBtnText.alignment = TextAlignmentOptions.Center;
        loginBtnText.color = Color.white;
        RectTransform loginBtnTextRect = loginBtnTextObj.GetComponent<RectTransform>();
        loginBtnTextRect.anchorMin = Vector2.zero;
        loginBtnTextRect.anchorMax = Vector2.one;
        loginBtnTextRect.offsetMin = Vector2.zero;
        loginBtnTextRect.offsetMax = Vector2.zero;

        // 添加事件系统
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 添加LoginUI组件并设置引用
        LoginUI loginUI = panelObj.AddComponent<LoginUI>();
        loginUI.usernameInput = usernameInput;
        loginUI.passwordInput = passwordInput;
        loginUI.loginButton = loginBtn;
        loginUI.statusText = statusText;

        Debug.Log("登录界面创建完成！");
    }
}