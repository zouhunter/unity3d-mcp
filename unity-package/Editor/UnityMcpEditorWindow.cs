using System.IO;
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;
using UnityMcp.Tools;
using System.Linq;

namespace UnityMcp.Windows
{
    public class UnityMcpEditorWindow : EditorWindow
    {
        private bool isUnityBridgeRunning = false;
        private int unityPort => UnityMcp.unityPort; // Hardcoded Unity port

        // 工具方法列表相关变量
        private Dictionary<string, bool> methodFoldouts = new Dictionary<string, bool>();
        private Vector2 methodsScrollPosition;
        private Dictionary<string, double> methodClickTimes = new Dictionary<string, double>();
        private const double doubleClickTime = 0.3; // 双击判定时间（秒）

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            GetWindow<UnityMcpEditorWindow>("MCP服务管理");
        }

        private async Task<bool> IsPortInUseAsync(int port)
        {
            // 使用托管 API 判断是否有“正在监听”的端口，避免误把连接状态（TIME_WAIT/CLOSE_WAIT/ESTABLISHED）当作占用
            return await Task.Run(() =>
            {
                try
                {
                    IPGlobalProperties ipProps = IPGlobalProperties.GetIPGlobalProperties();
                    IPEndPoint[] listeners = ipProps.GetActiveTcpListeners();
                    foreach (var ep in listeners)
                    {
                        if (ep.Port == port)
                        {
                            return true; // 仅当端口处于 LISTENING 才认为被占用
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"检查端口占用时发生错误: {ex.Message}");
                    return false;
                }
            });
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

            // 标题行和日志开关在同一行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unity MCP Bridge", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            // 日志开关
            bool newEnableLog = EditorGUILayout.ToggleLeft("日志", UnityMcp.EnableLog, GUILayout.Width(60));
            if (newEnableLog != UnityMcp.EnableLog)
            {
                UnityMcp.EnableLog = newEnableLog;
                EditorPrefs.SetBool("mcp_enable_log", newEnableLog);
            }
            EditorGUILayout.EndHorizontal();
            var installStatusRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            DrawStatusDot(installStatusRect, isUnityBridgeRunning ? Color.green : Color.red);
            EditorGUILayout.LabelField($"       Status: {(isUnityBridgeRunning ? "Running" : "Stopped")}");
            EditorGUILayout.LabelField($"Port: {unityPort}");
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge"))
            {
                ToggleUnityBridge();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // 控制面板已移至标题行，不再单独显示
            // DrawControlPanel();

            // 添加工具方法列表
            EditorGUILayout.Space(10);
            DrawMethodsList();
        }
        private async void ToggleUnityBridge()
        {
            if (isUnityBridgeRunning)
            {
                UnityMcp.Stop();
                isUnityBridgeRunning = false;
            }
            else
            {
                // 异步检查端口
                bool inUse = await IsPortInUseAsync(unityPort);
                if (inUse)
                {
                    // 杀掉占用端口的进程，兼容macOS和Windows
                    if (EditorUtility.DisplayDialog("端口被占用", $"端口 {unityPort} 已被占用。是否尝试自动杀死占用该端口的进程？", "是", "否"))
                    {
#if UNITY_EDITOR_WIN
                        try
                        {
                            // 查询占用端口的PID
                            System.Diagnostics.Process netstat = new System.Diagnostics.Process();
                            netstat.StartInfo.FileName = "cmd.exe";
                            netstat.StartInfo.Arguments = $"/c netstat -ano | findstr :{unityPort}";
                            netstat.StartInfo.RedirectStandardOutput = true;
                            netstat.StartInfo.UseShellExecute = false;
                            netstat.StartInfo.CreateNoWindow = true;
                            netstat.Start();
                            string output = netstat.StandardOutput.ReadToEnd();
                            netstat.WaitForExit();

                            // 解析PID
                            string[] lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                            HashSet<int> pids = new HashSet<int>();
                            foreach (var line in lines)
                            {
                                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 5 && int.TryParse(parts[4], out int pid))
                                {
                                    pids.Add(pid);
                                }
                            }

                            foreach (int pid in pids)
                            {
                                Debug.Log($"kill pid: {pid} self: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                                if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) continue; // 不杀自己
                                System.Diagnostics.Process kill = new System.Diagnostics.Process();
                                kill.StartInfo.FileName = "taskkill";
                                kill.StartInfo.Arguments = $"/PID {pid} /F";
                                kill.StartInfo.CreateNoWindow = true;
                                kill.StartInfo.UseShellExecute = false;
                                kill.Start();
                                kill.WaitForExit();
                            }
                            EditorUtility.DisplayDialog("操作完成", $"已尝试杀死占用端口 {unityPort} 的进程。", "确定");
                        }
                        catch (System.Exception ex)
                        {
                            EditorUtility.DisplayDialog("错误", $"自动杀进程失败: {ex.Message}", "确定");
                        }
#elif UNITY_EDITOR_OSX
                       try
                       {
                           // lsof -i :端口号 | grep LISTEN | awk '{print $2}'
                           System.Diagnostics.Process lsof = new System.Diagnostics.Process();
                           lsof.StartInfo.FileName = "/bin/bash";
                           lsof.StartInfo.Arguments = $"-c \"lsof -i :{unityPort} -sTCP:LISTEN -t\"";
                           lsof.StartInfo.RedirectStandardOutput = true;
                           lsof.StartInfo.UseShellExecute = false;
                           lsof.StartInfo.CreateNoWindow = true;
                           lsof.Start();
                           string output = lsof.StandardOutput.ReadToEnd();
                           lsof.WaitForExit();

                           string[] pids = output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                           foreach (var pidStr in pids)
                           {
                               if (int.TryParse(pidStr, out int pid))
                               {
                                   if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) continue; // 不杀自己
                                   System.Diagnostics.Process kill = new System.Diagnostics.Process();
                                   kill.StartInfo.FileName = "/bin/bash";
                                   kill.StartInfo.Arguments = $"-c \"kill -9 {pid}\"";
                                   kill.StartInfo.CreateNoWindow = true;
                                   kill.StartInfo.UseShellExecute = false;
                                   kill.Start();
                                   kill.WaitForExit();
                               }
                           }
                           EditorUtility.DisplayDialog("操作完成", $"已尝试杀死占用端口 {unityPort} 的进程。", "确定");
                       }
                       catch (System.Exception ex)
                       {
                           EditorUtility.DisplayDialog("错误", $"自动杀进程失败: {ex.Message}", "确定");
                       }
#else
                        EditorUtility.DisplayDialog("不支持的平台", "自动杀进程仅支持Windows和macOS。", "确定");
#endif
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("启动失败", $"端口 {unityPort} 被占用，无法启动Bridge。", "确定");
                    }
                    return;
                }

                UnityMcp.Start();
                isUnityBridgeRunning = true;
            }
            EditorPrefs.SetBool("mcp_open_state", isUnityBridgeRunning);
            Repaint();
        }

        private void DrawControlPanel()
        {
            // 控制面板现在为空，因为日志开关已移至标题行
            // 如果将来需要添加更多控件，可以在此处添加
        }

        /// <summary>
        /// 绘制工具方法列表，支持折叠展开
        /// </summary>
        private void DrawMethodsList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("可用工具方法", EditorStyles.boldLabel);

            // 确保方法已注册
            FunctionCall.EnsureMethodsRegisteredStatic();
            var methodNames = FunctionCall.GetRegisteredMethodNames();

            // 滚动视图
            methodsScrollPosition = EditorGUILayout.BeginScrollView(methodsScrollPosition,
                GUILayout.MinHeight(200), GUILayout.MaxHeight(400));

            // 绘制每个方法
            foreach (var methodName in methodNames.OrderBy(m => m))
            {
                // 获取方法实例
                var method = FunctionCall.GetRegisteredMethod(methodName);
                if (method == null) continue;

                // 确保该方法在字典中有一个条目
                if (!methodFoldouts.ContainsKey(methodName))
                {
                    methodFoldouts[methodName] = false;
                }

                // 绘制折叠标题
                EditorGUILayout.BeginVertical("box");

                // 折叠标题栏样式
                GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };

                // 在一行中显示折叠标题和问号按钮
                EditorGUILayout.BeginHorizontal();

                // 绘制折叠标题
                Rect foldoutRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

                // 计算问号按钮的位置
                float helpButtonWidth = 20f;
                float helpButtonHeight = 18f;
                float padding = 2f;

                // 分离出问号按钮的区域
                Rect helpButtonRect = new Rect(
                    foldoutRect.xMax - helpButtonWidth - padding,
                    foldoutRect.y + (foldoutRect.height - helpButtonHeight) / 2,
                    helpButtonWidth,
                    helpButtonHeight
                );

                // 留给折叠标题的区域
                Rect actualFoldoutRect = new Rect(
                    foldoutRect.x,
                    foldoutRect.y,
                    foldoutRect.width - helpButtonWidth - padding * 2,
                    foldoutRect.height
                );

                // 绘制折叠标题
                methodFoldouts[methodName] = EditorGUI.Foldout(
                    actualFoldoutRect,
                    methodFoldouts[methodName],
                    methodName,
                    true,
                    foldoutStyle);

                // 绘制问号按钮
                GUIStyle helpButtonStyle = new GUIStyle(EditorStyles.miniButton);

                if (GUI.Button(helpButtonRect, "?", helpButtonStyle))
                {
                    // 处理按钮点击事件
                    HandleMethodHelpClick(methodName, method);
                }

                EditorGUILayout.EndHorizontal();

                // 如果展开，显示预览信息
                if (methodFoldouts[methodName])
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // 获取预览信息
                    string preview = method.Preview();

                    // 计算文本行数
                    int lineCount = 1;
                    if (!string.IsNullOrEmpty(preview))
                    {
                        lineCount = preview.Split('\n').Length;
                    }

                    // 显示预览信息
                    EditorGUILayout.SelectableLabel(preview, EditorStyles.wordWrappedLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * lineCount * 0.8f));
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        /// <summary>
        /// 处理方法帮助按钮的点击事件
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="method">方法实例</param>
        private void HandleMethodHelpClick(string methodName, IToolMethod method)
        {
            // 获取当前时间
            double currentTime = EditorApplication.timeSinceStartup;

            // 检查是否存在上次点击时间记录
            if (methodClickTimes.TryGetValue(methodName, out double lastClickTime))
            {
                // 判断是否为双击（时间间隔小于doubleClickTime）
                if (currentTime - lastClickTime < doubleClickTime)
                {
                    // 双击：打开脚本文件
                    OpenMethodScript(method);
                    // 重置点击时间，防止连续多次点击被判定为多个双击
                    methodClickTimes[methodName] = 0;
                    return;
                }
            }

            // 单击：定位到脚本文件
            PingMethodScript(method);
            // 记录本次点击时间
            methodClickTimes[methodName] = currentTime;
        }

        /// <summary>
        /// 在Project窗口中定位到方法所在的脚本文件
        /// </summary>
        /// <param name="method">方法实例</param>
        private void PingMethodScript(IToolMethod method)
        {
            // 获取方法类型
            Type methodType = method.GetType();

            // 查找脚本资源
            string scriptName = methodType.Name + ".cs";
            string[] guids = AssetDatabase.FindAssets(methodType.Name + " t:MonoScript");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(scriptName))
                {
                    // 在Project窗口中高亮显示该资源
                    UnityEngine.Object scriptObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (scriptObj != null)
                    {
                        EditorGUIUtility.PingObject(scriptObj);
                        return;
                    }
                }
            }

            // 如果没有找到脚本，尝试直接使用类型名称查找
            string[] allScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (string guid in allScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(scriptName))
                {
                    UnityEngine.Object scriptObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (scriptObj != null)
                    {
                        EditorGUIUtility.PingObject(scriptObj);
                        return;
                    }
                }
            }

            Debug.LogWarning($"无法在Project窗口中找到脚本: {scriptName}");
        }

        /// <summary>
        /// 打开方法所在的脚本文件
        /// </summary>
        /// <param name="method">方法实例</param>
        private void OpenMethodScript(IToolMethod method)
        {
            // 获取方法类型
            Type methodType = method.GetType();

            // 查找脚本资源
            string scriptName = methodType.Name + ".cs";
            string[] guids = AssetDatabase.FindAssets(methodType.Name + " t:MonoScript");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(scriptName))
                {
                    // 加载并打开脚本
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null)
                    {
                        AssetDatabase.OpenAsset(script);
                        return;
                    }
                }
            }

            // 如果没有找到脚本，尝试直接使用类型名称查找
            string[] allScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (string guid in allScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(scriptName))
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null)
                    {
                        AssetDatabase.OpenAsset(script);
                        return;
                    }
                }
            }

            Debug.LogWarning($"无法打开脚本: {scriptName}");
        }
    }
}
