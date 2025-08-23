using System.IO;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;

namespace UnityMcpBridge.Editor.Windows
{
    public class UnityMcpEditorWindow : EditorWindow
    {
        private bool isUnityBridgeRunning = false;
        private const int unityPort = 6400; // Hardcoded Unity port

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            GetWindow<UnityMcpEditorWindow>("MCP服务管理");
        }

        private async Task<bool> IsPortInUseAsync(int port)
        {
            try
            {
                using (var tcpClient = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync("localhost", port);
                    var timeoutTask = Task.Delay(500); // 500ms 超时
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    return completedTask == connectTask && tcpClient.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private async void OnEnable()
        {
            // 先设置为默认状态
            isUnityBridgeRunning = false;
            Repaint();

            // 异步检测
            bool unityBridge = await IsPortInUseAsync(unityPort);
            isUnityBridgeRunning = unityBridge;
            Repaint();
        }

        private void DrawStatusDot(Rect statusRect, Color statusColor)
        {
            Rect dotRect = new(statusRect.x + 6, statusRect.y + 4, 12, 12);
            Vector3 center = new(
                dotRect.x + (dotRect.width / 2),
                dotRect.y + (dotRect.height / 2),
                0
            );
            float radius = dotRect.width / 2;

            // Draw the main dot
            Handles.color = statusColor;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);

            // Draw the border
            Color borderColor = new(
                statusColor.r * 0.7f,
                statusColor.g * 0.7f,
                statusColor.b * 0.7f
            );
            Handles.color = borderColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private void OnGUI()
        {
            // Unity Bridge Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Unity MCP Bridge", EditorStyles.boldLabel);
            var installStatusRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            DrawStatusDot(installStatusRect, isUnityBridgeRunning ? Color.green : Color.red);
            EditorGUILayout.LabelField($"       Status: {(isUnityBridgeRunning ? "Running" : "Stopped")}");
            EditorGUILayout.LabelField($"Port: {unityPort}");
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge"))
            {
                ToggleUnityBridge();
            }
            EditorGUILayout.EndVertical();
        }
        private async void ToggleUnityBridge()
        {
            if (isUnityBridgeRunning)
            {
                UnityMcpBridge.Stop();
                isUnityBridgeRunning = false;
            }
            else
            {
                // 异步检查端口
                bool inUse = await IsPortInUseAsync(unityPort);
                if (inUse)
                {
                    EditorUtility.DisplayDialog("端口占用", $"端口 {unityPort} 已被占用，请检查是否有其他实例正在运行。", "确定");
                    return;
                }

                UnityMcpBridge.Start();
                isUnityBridgeRunning = true;
            }
            Repaint();
        }
    }
}
