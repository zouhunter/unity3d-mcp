using UnityEngine;
using UnityEditor;

namespace UnityMcp.Tools
{
    /// <summary>
    /// MCP设置提供器，用于在Unity的ProjectSettings窗口中显示MCP设置
    /// </summary>
    public static class McpSettingsProvider
    {
        private static bool isInitialized = false;

        [SettingsProvider]
        public static SettingsProvider CreateMcpSettingsProvider()
        {
            var provider = new SettingsProvider("Project/MCP", SettingsScope.Project)
            {
                label = "MCP",
                guiHandler = (searchContext) =>
                {
                    DrawMcpSettings();
                },
                activateHandler = (searchContext, rootElement) =>
                {
                    // 在激活时初始化GUI状态
                    if (!isInitialized)
                    {
                        McpConnectGUI.Initialize();
                        isInitialized = true;
                    }
                },
                keywords = new[] { "MCP", "Settings", "Configuration", "Debug", "Bridge", "Server" }
            };

            return provider;
        }

        private static void DrawMcpSettings()
        {
            var settings = McpSettings.Instance;

            EditorGUILayout.LabelField("MCP (Model Context Protocol)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "MCP是一个强大的Unity扩展工具，提供了智能的UI生成、代码管理和项目优化功能。" +
                "通过与AI模型的深度集成，MCP能够帮助开发者快速创建高质量的Unity项目。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 绘制完整的MCP管理GUI
            McpConnectGUI.DrawGUI();

            // 自动保存
            if (GUI.changed)
            {
                settings.SaveSettings();
            }
        }
    }
}
