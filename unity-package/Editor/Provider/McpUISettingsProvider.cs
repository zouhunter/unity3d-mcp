using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace UnityMcp.Tools
{
    /// <summary>
    /// MCP UI设置提供器，用于在Unity的ProjectSettings窗口中显示UI相关设置
    /// </summary>
    [System.Serializable]
    public class McpUISettingsProvider
    {
        private static Vector2 scrollPosition;
        private static ReorderableList buildStepsList;
        private static ReorderableList preferredComponentsList;

        /// <summary>
        /// UI构建步骤
        /// </summary>
        public List<string> ui_build_steps
        {
            get
            {
                if (_ui_build_steps == null)
                    _ui_build_steps = GetDefaultBuildSteps();
                return _ui_build_steps;
            }
            set { _ui_build_steps = value; }
        }
        [SerializeField] private List<string> _ui_build_steps;

        /// <summary>
        /// UI构建环境
        /// </summary>
        public List<string> ui_build_enviroments
        {
            get
            {
                if (_ui_build_enviroments == null)
                    _ui_build_enviroments = GetDefaultBuildEnvironments();
                return _ui_build_enviroments;
            }
            set { _ui_build_enviroments = value; }
        }
        [SerializeField] private List<string> _ui_build_enviroments;

        /// <summary>
        /// 获取默认的UI构建步骤
        /// </summary>
        public static List<string> GetDefaultBuildSteps()
        {
            return new List<string>
            {
                "分析Figma设计稿结构",
                "确定UI层级和布局方案",
                "创建Canvas和根容器并设置好尺寸",
                "将Game尺寸和UI尺寸匹配",
                "回顾unity-mcp.mdc",
                "按照设计稿创建UI组件",
                "设置Layout和对齐方式",
                "配置组件属性和样式",
                "优化屏幕适配并记录更改方式到规则文件",
                "优化简化层级数量并记录更改方式到规则文件",
                "再次检查Layout，确保还原度",
                "基于figma-mcp下载并准备相关图片资源",
                "将本地图片或下载好的图加载到UI上"
            };
        }

        /// <summary>
        /// 获取默认的UI环境说明
        /// </summary>
        public static List<string> GetDefaultBuildEnvironments()
        {
            return new List<string>
            {
                "基于UI界面",
                "支持TMP字体",
                "优先使用TMP输入框等"
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateMcpUISettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP/UI", SettingsScope.Project)
            {
                label = "UI",
                guiHandler = (searchContext) =>
                {
                    DrawMcpUISettings();
                },
                keywords = new[] { "UI", "UI", "Generation", "Rules", "Figma", "Canvas", "Button", "Text", "Image" }
            };

            return provider;
        }

        private static void DrawMcpUISettings()
        {
            var settings = McpSettings.Instance;
            if (settings.uiSettings == null)
                settings.uiSettings = new McpUISettingsProvider();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // UI简介
            EditorGUILayout.LabelField("UI 生成规则配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "配置Unity UI系统的自动生成规则和偏好设置。" +
                "这些设置将影响通过MCP工具生成的UI组件的默认行为和结构。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 初始化ReorderableList
            if (buildStepsList == null)
            {
                buildStepsList = new ReorderableList(settings.uiSettings.ui_build_steps, typeof(string), true, true, true, true);
                buildStepsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "UI构建步骤");

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "重置"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", "确定要重置UI构建步骤为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_steps = McpUISettingsProvider.GetDefaultBuildSteps();
                            settings.SaveSettings();
                        }
                    }
                };
                buildStepsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    settings.uiSettings.ui_build_steps[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_steps[index]);
                };
                buildStepsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_steps.Add("新步骤？");
                };
            }

            if (preferredComponentsList == null)
            {
                preferredComponentsList = new ReorderableList(settings.uiSettings.ui_build_enviroments, typeof(string), true, true, true, true);
                preferredComponentsList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "UI环境说明");

                    // 添加重置按钮
                    Rect resetButtonRect = new Rect(rect.width - 60, rect.y, 60, rect.height);
                    if (GUI.Button(resetButtonRect, "重置"))
                    {
                        if (EditorUtility.DisplayDialog("确认重置", "确定要重置UI环境说明为默认值吗？", "确定", "取消"))
                        {
                            settings.uiSettings.ui_build_enviroments = McpUISettingsProvider.GetDefaultBuildEnvironments();
                            settings.SaveSettings();
                        }
                    }
                };
                preferredComponentsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    settings.uiSettings.ui_build_enviroments[index] = EditorGUI.TextField(rect, settings.uiSettings.ui_build_enviroments[index]);
                };
                preferredComponentsList.onAddCallback = (ReorderableList list) =>
                {
                    settings.uiSettings.ui_build_enviroments.Add("");
                };
            }

            // 绘制UI构建步骤列表
            EditorGUILayout.LabelField("UI构建步骤", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("定义UI生成的步骤流程，按顺序执行。", MessageType.Info);
            buildStepsList.DoLayoutList();

            EditorGUILayout.Space(10);

            // 绘制偏好组件列表
            EditorGUILayout.LabelField("偏好组件", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("配置UI生成时优先使用的组件类型。", MessageType.Info);
            preferredComponentsList.DoLayoutList();

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
