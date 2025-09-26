using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityMcp.Tools
{
    /// <summary>
    /// MCP设置管理器，用于管理MCP相关的配置和偏好设置
    /// 保存到ProjectSettings/McpSettings.asset
    /// </summary>
    [FilePath("ProjectSettings/McpSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class McpSettings : ScriptableSingleton<McpSettings>
    {
        /// <summary>
        /// UI设置
        /// </summary>
        public McpUISettingsProvider uiSettings;

        /// <summary>
        /// Figma设置
        /// </summary>
        public FigmaSettingsProvider figmaSettings;

        /// <summary>
        /// 获取MCP设置实例
        /// </summary>
        public static McpSettings Instance => instance;

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            EditorUtility.SetDirty(this);
            Save(true);
        }
    }

}