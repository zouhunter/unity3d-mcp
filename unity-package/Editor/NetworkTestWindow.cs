using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityMcp.Tools;

namespace UnityMcp.Testing
{
    /// <summary>
    /// 网络工具测试窗口
    /// </summary>
    public class NetworkTestWindow : EditorWindow
    {
        private string testUrl = "https://httpbin.org/status/200";
        private string testResult = "";

        [MenuItem("Unity MCP/Test Network Tool")]
        public static void ShowWindow()
        {
            GetWindow<NetworkTestWindow>("Network Tool Test");
        }

        private void OnGUI()
        {
            GUILayout.Label("Network Tool Test", EditorStyles.boldLabel);

            GUILayout.Space(10);

            GUILayout.Label("Test URL:");
            testUrl = EditorGUILayout.TextField(testUrl);

            GUILayout.Space(10);

            if (GUILayout.Button("Test Ping"))
            {
                TestPing();
            }

            if (GUILayout.Button("Test GET Request"))
            {
                TestGetRequest();
            }

            GUILayout.Space(10);

            GUILayout.Label("Result:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(testResult, GUILayout.Height(200));
        }

        private void TestPing()
        {
            try
            {
                var networkTool = new ManageHttp();
                var args = new JObject
                {
                    ["action"] = "ping",
                    ["url"] = testUrl,
                    ["timeout"] = 10
                };

                var result = networkTool.ExecuteMethod(args);
                testResult = $"Ping Test Result:\n{result}";
            }
            catch (System.Exception e)
            {
                testResult = $"Ping Test Error:\n{e.Message}";
            }
        }

        private void TestGetRequest()
        {
            try
            {
                var networkTool = new ManageHttp();
                var args = new JObject
                {
                    ["action"] = "get",
                    ["url"] = testUrl,
                    ["timeout"] = 10
                };

                var result = networkTool.ExecuteMethod(args);
                testResult = $"GET Test Result:\n{result}";
            }
            catch (System.Exception e)
            {
                testResult = $"GET Test Error:\n{e.Message}";
            }
        }
    }
}
