using System.IO;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;

namespace UnityMcpBridge.Editor.Windows
{
    public class UnityMcpEditorWindow : EditorWindow
    {
        private bool isUnityBridgeRunning = false;
        private Vector2 scrollPosition;
        private string pythonServerInstallationStatus = "Not Installed";
        private Color pythonServerInstallationStatusColor = Color.red;
        private const int unityPort = 6400; // Hardcoded Unity port
        private const int mcpPort = 6500; // Hardcoded MCP port

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            GetWindow<UnityMcpEditorWindow>("MCP Editor");
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
            pythonServerInstallationStatus = "检测中...";
            pythonServerInstallationStatusColor = Color.yellow;
            Repaint();

            // 异步检测
            bool unityBridge = await IsPortInUseAsync(unityPort);
            bool mcpServer = true;//

            isUnityBridgeRunning = unityBridge;
            pythonServerInstallationStatus = mcpServer ? "Running" : "Stopped";
            pythonServerInstallationStatusColor = mcpServer ? Color.green : Color.red;
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
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            // Title with improved styling
            Rect titleRect = EditorGUILayout.GetControlRect(false, 30);
            EditorGUI.DrawRect(
                new Rect(titleRect.x, titleRect.y, titleRect.width, titleRect.height),
                new Color(0.2f, 0.2f, 0.2f, 0.1f)
            );
            GUI.Label(
                new Rect(titleRect.x + 10, titleRect.y + 6, titleRect.width - 20, titleRect.height),
                "MCP Editor",
                EditorStyles.boldLabel
            );
            EditorGUILayout.Space(10);

            // Python Server Installation Status Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Python Server Status", EditorStyles.boldLabel);

            // Status indicator with colored dot
            Rect installStatusRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            DrawStatusDot(installStatusRect, pythonServerInstallationStatusColor);
            EditorGUILayout.LabelField("       " + pythonServerInstallationStatus);
            EditorGUILayout.EndHorizontal();

            //EditorGUILayout.LabelField($"Unity Port: {unityPort}");
            EditorGUILayout.LabelField($"MCP Port: {mcpPort}");
            EditorGUILayout.HelpBox(
                "Your MCP client (e.g. Cursor or Claude Desktop) will start the server automatically when you start it.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Unity Bridge Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Unity MCP Bridge", EditorStyles.boldLabel);
            installStatusRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            DrawStatusDot(installStatusRect, pythonServerInstallationStatusColor);
            EditorGUILayout.LabelField($"       Status: {(isUnityBridgeRunning ? "Running" : "Stopped")}");
            EditorGUILayout.LabelField($"Port: {unityPort}");
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge"))
            {
                ToggleUnityBridge();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        private async void ToggleUnityBridge()
        {
            if (pythonServerInstallationStatus != "Running")
            {
                EditorUtility.DisplayDialog("Python Server Not Running", "Please start the Python server before starting the Unity MCP Bridge.", "OK");
                return;
            }

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
