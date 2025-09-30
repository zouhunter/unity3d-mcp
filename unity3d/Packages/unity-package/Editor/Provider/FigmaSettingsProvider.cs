using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Figma设置提供器，用于在Unity的ProjectSettings窗口中显示Figma相关设置
    /// </summary>
    [System.Serializable]
    public class FigmaSettingsProvider
    {
        private static Vector2 scrollPosition;
        private static bool apiSettingsFoldout = true;
        private static bool downloadSettingsFoldout = true;
        private static bool engineEffectsFoldout = true;
        private static bool helpInfoFoldout = false;

        /// <summary>
        /// Figma访问令牌（保存在EditorPrefs中，不会被提交到版本控制）
        /// </summary>
        public string figma_access_token
        {
            get
            {
                return EditorPrefs.GetString("UnityMcp.Figma.AccessToken", "");
            }
            set
            {
                EditorPrefs.SetString("UnityMcp.Figma.AccessToken", value);
            }
        }

        /// <summary>
        /// 默认下载路径
        /// </summary>
        public string default_download_path
        {
            get
            {
                if (string.IsNullOrEmpty(_default_download_path))
                    _default_download_path = "Assets/UI/Figma";
                return _default_download_path;
            }
            set { _default_download_path = value; }
        }
        [SerializeField] private string _default_download_path;

        /// <summary>
        /// Figma资产数据路径
        /// </summary>
        public string figma_assets_path
        {
            get
            {
                if (string.IsNullOrEmpty(_figma_assets_path))
                    _figma_assets_path = "Assets/FigmaAssets";
                return _figma_assets_path;
            }
            set { _figma_assets_path = value; }
        }
        [SerializeField] private string _figma_assets_path;

        /// <summary>
        /// 自动下载图片
        /// </summary>
        public bool auto_download_images = true;

        /// <summary>
        /// 图片缩放倍数
        /// </summary>
        public float image_scale = 2.0f;

        /// <summary>
        /// 自动转换图片为Sprite格式
        /// </summary>
        public bool auto_convert_to_sprite = true;

        /// <summary>
        /// 引擎支持效果
        /// </summary>
        public EngineSupportEffect engineSupportEffect;

        /// <summary>
        /// 引擎支持效果
        /// </summary>
        public class EngineSupportEffect
        {
            public bool roundCorner;
            public bool outLineImg;
            public bool gradientImg;
        }

        [SettingsProvider]
        public static SettingsProvider CreateFigmaSettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP/Figma", SettingsScope.Project)
            {
                label = "Figma",
                guiHandler = (searchContext) =>
                {
                    DrawFigmaSettings();
                },
                keywords = new[] { "Figma", "Design", "Token", "Download", "Images", "API", "File" }
            };

            return provider;
        }

        private static void DrawFigmaSettings()
        {
            var settings = McpSettings.Instance;
            if (settings.figmaSettings == null)
                settings.figmaSettings = new FigmaSettingsProvider();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Figma简介
            EditorGUILayout.LabelField("Figma 集成配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "配置与Figma的集成设置，包括访问令牌和下载选项。" +
                "这些设置将影响从Figma获取设计资源的行为。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // API设置
            apiSettingsFoldout = EditorGUILayout.Foldout(apiSettingsFoldout, "API设置", true, EditorStyles.foldoutHeader);

            if (apiSettingsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                settings.figmaSettings.figma_access_token = EditorGUILayout.PasswordField(
                    "Figma访问令牌",
                    settings.figmaSettings.figma_access_token);
                EditorGUILayout.LabelField("💾", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "访问令牌保存在本地编辑器设置中，不会被提交到版本控制。",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 下载设置
            downloadSettingsFoldout = EditorGUILayout.Foldout(downloadSettingsFoldout, "下载设置", true, EditorStyles.foldoutHeader);

            if (downloadSettingsFoldout)
            {
                EditorGUI.indentLevel++;

                settings.figmaSettings.default_download_path = EditorGUILayout.TextField(
                    "默认下载路径",
                    settings.figmaSettings.default_download_path);

                settings.figmaSettings.figma_assets_path = EditorGUILayout.TextField(
                    "Figma数据资产路径",
                    settings.figmaSettings.figma_assets_path);

                settings.figmaSettings.auto_download_images = EditorGUILayout.Toggle(
                    "自动下载图片",
                    settings.figmaSettings.auto_download_images);

                settings.figmaSettings.image_scale = EditorGUILayout.FloatField(
                    "图片缩放倍数",
                    settings.figmaSettings.image_scale);

                settings.figmaSettings.auto_convert_to_sprite = EditorGUILayout.Toggle(
                    "自动转换为Sprite",
                    settings.figmaSettings.auto_convert_to_sprite);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 引擎支持效果设置
            engineEffectsFoldout = EditorGUILayout.Foldout(engineEffectsFoldout, "引擎支持效果", true, EditorStyles.foldoutHeader);

            if (engineEffectsFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "配置Unity引擎对特定UI效果的支持。启用这些选项可以避免下载某些可以通过Unity原生组件实现的效果。",
                    MessageType.Info);

                // 初始化engineSupportEffect如果为null
                if (settings.figmaSettings.engineSupportEffect == null)
                    settings.figmaSettings.engineSupportEffect = new EngineSupportEffect();

                settings.figmaSettings.engineSupportEffect.roundCorner = EditorGUILayout.Toggle(
                    "圆角支持 (ProceduralUIImage)",
                    settings.figmaSettings.engineSupportEffect.roundCorner);

                settings.figmaSettings.engineSupportEffect.outLineImg = EditorGUILayout.Toggle(
                    "描边支持 (Outline组件)",
                    settings.figmaSettings.engineSupportEffect.outLineImg);

                settings.figmaSettings.engineSupportEffect.gradientImg = EditorGUILayout.Toggle(
                    "渐变支持 (UI Gradient)",
                    settings.figmaSettings.engineSupportEffect.gradientImg);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // 帮助信息
            helpInfoFoldout = EditorGUILayout.Foldout(helpInfoFoldout, "使用说明", true, EditorStyles.foldoutHeader);

            if (helpInfoFoldout)
            {
                EditorGUI.indentLevel++;

                // API设置说明
                EditorGUILayout.LabelField("API设置", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• 访问令牌：在Figma中生成个人访问令牌用于API访问\n" +
                    "• 获取方式：登录Figma → Settings → Personal access tokens → Generate new token\n" +
                    "• 安全性：访问令牌保存在本地EditorPrefs中，不会被提交到Git",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // 下载设置说明
                EditorGUILayout.LabelField("下载设置", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• 下载路径：图片和资源的本地保存位置\n" +
                    "• 数据资产路径：Figma节点数据和简化数据的保存位置\n" +
                    "• 缩放倍数：控制下载图片的分辨率（建议2.0用于高清显示）\n" +
                    "• 自动转换为Sprite：下载图片后自动设置为Sprite格式（推荐开启）",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // 引擎支持效果说明
                EditorGUILayout.LabelField("引擎支持效果", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "• 圆角支持：启用后，圆角矩形将使用ProceduralUIImage而非下载图片\n" +
                    "• 描边支持：启用后，描边效果将使用Outline组件而非下载图片\n" +
                    "• 渐变支持：启用后，渐变效果将使用UI Gradient组件而非下载图片\n" +
                    "• 优势：减少资源占用，提高性能，支持运行时动态调整",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // 使用流程说明
                EditorGUILayout.LabelField("使用流程", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "1. 配置Figma访问令牌\n" +
                    "2. 设置合适的下载路径和缩放倍数\n" +
                    "3. 根据项目需求启用引擎支持效果\n" +
                    "4. 在MCP工具中使用figma_manage下载设计资源\n" +
                    "5. 通过UI生成工具自动创建Unity UI组件",
                    MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
