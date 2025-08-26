using UnityEngine;
using UnityEditor;
using UnityMcp;

namespace UnityMcp.Tests
{
    /// <summary>
    /// 测试日志控制功能的脚本
    /// </summary>
    public class TestLogControl : EditorWindow
    {
        [MenuItem("Window/Unity MCP/测试日志控制")]
        public static void ShowWindow()
        {
            GetWindow<TestLogControl>("日志控制测试");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity MCP 日志控制测试", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 显示当前日志状态
            EditorGUILayout.LabelField($"当前日志状态: {(UnityMcp.EnableLog ? "启用" : "禁用")}");

            EditorGUILayout.Space();

            // 测试按钮
            if (GUILayout.Button("测试日志输出"))
            {
                TestLogOutput();
            }

            EditorGUILayout.Space();

            // 切换日志状态
            bool newEnableLog = EditorGUILayout.Toggle("启用日志", UnityMcp.EnableLog);
            if (newEnableLog != UnityMcp.EnableLog)
            {
                UnityMcp.EnableLog = newEnableLog;
                EditorPrefs.SetBool("mcp_enable_log", newEnableLog);
                Debug.Log($"[TestLogControl] 日志状态已切换为: {(newEnableLog ? "启用" : "禁用")}");
            }

            EditorGUILayout.Space();

            // 说明
            EditorGUILayout.HelpBox(
                "说明：\n" +
                "1. 当日志启用时，所有UnityMcp相关的Debug.Log输出都会显示\n" +
                "2. 当日志禁用时，所有UnityMcp相关的Debug.Log输出都会被抑制\n" +
                "3. 设置会自动保存到EditorPrefs中\n" +
                "4. 重启Unity后设置会保持",
                MessageType.Info
            );
        }

        private void TestLogOutput()
        {
            Debug.Log("[TestLogControl] 这是一条测试日志消息");
            Debug.LogWarning("[TestLogControl] 这是一条测试警告消息");
            Debug.LogError("[TestLogControl] 这是一条测试错误消息");

            // 测试UnityMcp的日志方法
            if (UnityMcp.EnableLog)
            {
                Debug.Log("[TestLogControl] UnityMcp日志已启用，应该能看到上面的测试消息");
            }
            else
            {
                Debug.Log("[TestLogControl] UnityMcp日志已禁用，上面的测试消息应该被抑制");
            }
        }
    }
}
