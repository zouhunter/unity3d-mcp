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

        /// <summary>
        /// Figma访问令牌
        /// </summary>
        public string figma_access_token
        {
            get
            {
                if (string.IsNullOrEmpty(_figma_access_token))
                    _figma_access_token = "";
                return _figma_access_token;
            }
            set { _figma_access_token = value; }
        }
        [SerializeField] private string _figma_access_token;

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
            EditorGUILayout.LabelField("API设置", EditorStyles.boldLabel);

            settings.figmaSettings.figma_access_token = EditorGUILayout.PasswordField(
                "Figma访问令牌",
                settings.figmaSettings.figma_access_token);

            EditorGUILayout.Space(10);

            // 下载设置
            EditorGUILayout.LabelField("下载设置", EditorStyles.boldLabel);

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

            EditorGUILayout.Space(10);

            // 帮助信息
            EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. 访问令牌：在Figma中生成个人访问令牌用于API访问\n" +
                "2. 下载路径：图片和资源的本地保存位置\n" +
                "3. 数据资产路径：Figma节点数据和简化数据的保存位置\n" +
                "4. 缩放倍数：控制下载图片的分辨率（建议2.0用于高清显示）\n" +
                "5. 自动转换为Sprite：下载图片后自动设置为Sprite格式（推荐开启）",
                MessageType.Info);

            EditorGUILayout.EndScrollView();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
