using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIToolkit.SimpleUI
{
    /// <summary>
    /// SimpleUI控制器 - 管理UI Toolkit界面的逻辑和交互
    /// </summary>
    public class SimpleUIController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("样式表")]
        [SerializeField] private StyleSheet styleSheet;

        [Header("图片资源")]
        [SerializeField] private Texture2D image1Texture;
        [SerializeField] private Texture2D image2Texture;
        [SerializeField] private Texture2D image3Texture;

        // UI元素引用
        private VisualElement rootContainer;
        private Label titleLabel;
        private VisualElement imagesContainer;
        private VisualElement image1Element;
        private VisualElement image2Element;
        private VisualElement image3Element;
        private VisualElement decorationElement;

        void Start()
        {
            LoadStyleSheet();
            InitializeUI();
            BindEvents();
            LoadImages();
        }

        /// <summary>
        /// 加载USS样式表
        /// </summary>
        private void LoadStyleSheet()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                // 方法1：如果在Inspector中设置了styleSheet
                if (styleSheet != null)
                {
                    if (!uiDocument.rootVisualElement.styleSheets.Contains(styleSheet))
                    {
                        uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
                        Debug.Log("[SimpleUI] 通过Inspector加载样式表完成");
                    }
                }
                else
                {
                    // 方法2：自动从Resources加载SimpleUI样式表
                    StyleSheet autoStyleSheet = Resources.Load<StyleSheet>("SimpleUI");
                    if (autoStyleSheet != null)
                    {
                        if (!uiDocument.rootVisualElement.styleSheets.Contains(autoStyleSheet))
                        {
                            uiDocument.rootVisualElement.styleSheets.Add(autoStyleSheet);
                            Debug.Log("[SimpleUI] 自动加载Resources中的样式表完成");
                        }
                    }
                    else
                    {
                        // 方法3：尝试通过AssetDatabase加载（仅在Editor中工作）
#if UNITY_EDITOR
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("SimpleUI t:StyleSheet");
                        if (guids.Length > 0)
                        {
                            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            StyleSheet editorStyleSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(assetPath);
                            if (editorStyleSheet != null)
                            {
                                if (!uiDocument.rootVisualElement.styleSheets.Contains(editorStyleSheet))
                                {
                                    uiDocument.rootVisualElement.styleSheets.Add(editorStyleSheet);
                                    Debug.Log($"[SimpleUI] 通过AssetDatabase加载样式表完成: {assetPath}");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[SimpleUI] 未找到SimpleUI样式表，请在Inspector中手动设置styleSheet字段，或将SimpleUI.uss放入Resources文件夹");
                        }
#else
                        Debug.LogWarning("[SimpleUI] 运行时未找到样式表，请在Inspector中设置styleSheet字段");
#endif
                    }
                }
            }
        }

        /// <summary>
        /// 初始化UI元素引用
        /// </summary>
        private void InitializeUI()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            var root = uiDocument.rootVisualElement;

            // 获取UI元素引用
            rootContainer = root.Q<VisualElement>("root-container");
            titleLabel = root.Q<Label>("title-text");
            imagesContainer = root.Q<VisualElement>("images-container");
            image1Element = root.Q<VisualElement>("image-1");
            image2Element = root.Q<VisualElement>("image-2");
            image3Element = root.Q<VisualElement>("image-3");
            decorationElement = root.Q<VisualElement>("vector-decoration");

            Debug.Log("[SimpleUI] UI元素初始化完成");
        }

        /// <summary>
        /// 绑定事件处理
        /// </summary>
        private void BindEvents()
        {
            // 图片点击事件
            if (image1Element != null)
            {
                image1Element.RegisterCallback<ClickEvent>(OnImage1Click);
                image1Element.RegisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image1Element.RegisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }

            if (image2Element != null)
            {
                image2Element.RegisterCallback<ClickEvent>(OnImage2Click);
                image2Element.RegisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image2Element.RegisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }

            if (image3Element != null)
            {
                image3Element.RegisterCallback<ClickEvent>(OnImage3Click);
                image3Element.RegisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image3Element.RegisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }

            Debug.Log("[SimpleUI] 事件绑定完成");
        }

        /// <summary>
        /// 加载图片资源
        /// </summary>
        private void LoadImages()
        {
            // 设置图片背景
            if (image1Element != null && image1Texture != null)
            {
                image1Element.style.backgroundImage = new StyleBackground(image1Texture);
            }

            if (image2Element != null && image2Texture != null)
            {
                image2Element.style.backgroundImage = new StyleBackground(image2Texture);
            }

            if (image3Element != null && image3Texture != null)
            {
                image3Element.style.backgroundImage = new StyleBackground(image3Texture);
            }

            Debug.Log("[SimpleUI] 图片资源加载完成");
        }

        #region 事件处理方法

        private void OnImage1Click(ClickEvent evt)
        {
            Debug.Log("[SimpleUI] Image 1 被点击");
            // 在这里添加图片1的点击逻辑
        }

        private void OnImage2Click(ClickEvent evt)
        {
            Debug.Log("[SimpleUI] Image 2 被点击");
            // 在这里添加图片2的点击逻辑
        }

        private void OnImage3Click(ClickEvent evt)
        {
            Debug.Log("[SimpleUI] Image 3 被点击");
            // 在这里添加图片3的点击逻辑
        }

        private void OnImageMouseEnter(MouseEnterEvent evt)
        {
            var element = evt.target as VisualElement;
            if (element != null)
            {
                // 鼠标悬停效果 - 轻微缩放
                element.style.scale = new StyleScale(new Scale(new Vector2(1.05f, 1.05f)));
                element.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0.2f) });
            }
        }

        private void OnImageMouseLeave(MouseLeaveEvent evt)
        {
            var element = evt.target as VisualElement;
            if (element != null)
            {
                // 恢复原始大小
                element.style.scale = new StyleScale(new Scale(new Vector2(1f, 1f)));
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新标题文本
        /// </summary>
        /// <param name="newText">新的标题文本</param>
        public void UpdateTitleText(string newText)
        {
            if (titleLabel != null)
            {
                titleLabel.text = newText;
            }
        }

        /// <summary>
        /// 设置图片纹理
        /// </summary>
        /// <param name="imageIndex">图片索引 (1-3)</param>
        /// <param name="texture">纹理资源</param>
        public void SetImageTexture(int imageIndex, Texture2D texture)
        {
            VisualElement targetElement = imageIndex switch
            {
                1 => image1Element,
                2 => image2Element,
                3 => image3Element,
                _ => null
            };

            if (targetElement != null && texture != null)
            {
                targetElement.style.backgroundImage = new StyleBackground(texture);
                Debug.Log($"[SimpleUI] 图片 {imageIndex} 纹理已更新");
            }
        }

        /// <summary>
        /// 显示/隐藏装饰元素
        /// </summary>
        /// <param name="visible">是否显示</param>
        public void SetDecorationVisible(bool visible)
        {
            if (decorationElement != null)
            {
                decorationElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// 手动重新加载样式表
        /// </summary>
        public void ReloadStyleSheet()
        {
            LoadStyleSheet();
        }

        /// <summary>
        /// 设置自定义样式表
        /// </summary>
        /// <param name="customStyleSheet">自定义样式表</param>
        public void SetCustomStyleSheet(StyleSheet customStyleSheet)
        {
            if (customStyleSheet != null && uiDocument != null && uiDocument.rootVisualElement != null)
            {
                // 清除现有样式表
                uiDocument.rootVisualElement.styleSheets.Clear();

                // 添加新样式表
                uiDocument.rootVisualElement.styleSheets.Add(customStyleSheet);

                Debug.Log("[SimpleUI] 自定义样式表设置完成");
            }
        }

        #endregion

        void OnDestroy()
        {
            // 清理事件监听
            if (image1Element != null)
            {
                image1Element.UnregisterCallback<ClickEvent>(OnImage1Click);
                image1Element.UnregisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image1Element.UnregisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }

            if (image2Element != null)
            {
                image2Element.UnregisterCallback<ClickEvent>(OnImage2Click);
                image2Element.UnregisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image2Element.UnregisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }

            if (image3Element != null)
            {
                image3Element.UnregisterCallback<ClickEvent>(OnImage3Click);
                image3Element.UnregisterCallback<MouseEnterEvent>(OnImageMouseEnter);
                image3Element.UnregisterCallback<MouseLeaveEvent>(OnImageMouseLeave);
            }
        }
    }
}
