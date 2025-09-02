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
using UnityMcp;
using Newtonsoft.Json;

namespace UnityMcp.Windows
{
    public class McpConnectEditorWindow : EditorWindow
    {
        private bool isUnityBridgeRunning = false;
        private int unityPortStart => McpConnect.unityPortStart;
        private int unityPortEnd => McpConnect.unityPortEnd;
        private int currentPort => McpConnect.currentPort;

        // 工具方法列表相关变量
        private Dictionary<string, bool> methodFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> assemblyFoldouts = new Dictionary<string, bool>(); // 程序集折叠状态
        private Vector2 methodsScrollPosition;
        private Dictionary<string, double> methodClickTimes = new Dictionary<string, double>();
        private const double doubleClickTime = 0.3; // 双击判定时间（秒）

        // 客户端连接状态相关变量
        private Vector2 clientsScrollPosition;
        private bool showClientDetails = false;

        [MenuItem("Window/Unity MCP")]
        public static void ShowWindow()
        {
            GetWindow<McpConnectEditorWindow>("MCP服务管理");
        }

        private async Task<bool> IsPortInUseAsync(int port)
        {
            // 使用托管 API 判断是否有"正在监听"的端口，避免误把连接状态（TIME_WAIT/CLOSE_WAIT/ESTABLISHED）当作占用
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

        private async Task<bool> IsAnyPortInRangeInUse()
        {
            for (int port = unityPortStart; port <= unityPortEnd; port++)
            {
                if (await IsPortInUseAsync(port))
                {
                    return true;
                }
            }
            return false;
        }

        private async void OnEnable()
        {
            // 先设置为默认状态
            isUnityBridgeRunning = false;
            Repaint();

            // 异步检测 - 检查是否有任何端口在使用，并且是否是当前Unity进程
            bool anyPortInUse = await IsAnyPortInRangeInUse();
            isUnityBridgeRunning = anyPortInUse && McpConnect.IsRunning;
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
            // 使用垂直布局管理整个窗口，确保充分利用空间
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            // Unity Bridge Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行和日志开关在同一行
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unity MCP Bridge", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            // 日志开关
            bool newEnableLog = EditorGUILayout.ToggleLeft("日志", McpConnect.EnableLog, GUILayout.Width(60));
            if (newEnableLog != McpConnect.EnableLog)
            {
                McpConnect.EnableLog = newEnableLog;
                EditorPrefs.SetBool("mcp_enable_log", newEnableLog);
            }
            EditorGUILayout.EndHorizontal();
            var installStatusRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            DrawStatusDot(installStatusRect, isUnityBridgeRunning ? Color.green : Color.red);
            EditorGUILayout.LabelField($"       Status: {(isUnityBridgeRunning ? "Running" : "Stopped")}");

            // 显示端口信息
            if (isUnityBridgeRunning && currentPort != -1)
            {
                EditorGUILayout.LabelField($"Port: {currentPort} (Range: {unityPortStart}-{unityPortEnd})");
            }
            else
            {
                EditorGUILayout.LabelField($"Port Range: {unityPortStart}-{unityPortEnd}");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(isUnityBridgeRunning ? "Stop Bridge" : "Start Bridge"))
            {
                ToggleUnityBridge();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            // 客户端连接状态部分
            if (isUnityBridgeRunning)
            {
                DrawClientConnectionStatus();
            }

            // 控制面板已移至标题行，不再单独显示
            // DrawControlPanel();

            // 添加工具方法列表 - 让它填充剩余空间
            EditorGUILayout.Space(10);
            DrawMethodsList();

            // 结束主垂直布局
            EditorGUILayout.EndVertical();
        }
        private async void ToggleUnityBridge()
        {
            if (isUnityBridgeRunning)
            {
                McpConnect.Stop();
                isUnityBridgeRunning = false;
            }
            else
            {
                // 尝试启动 - Unity MCP 会自动选择可用端口
                bool hasConflicts = false;
                List<int> conflictPorts = new List<int>();

                // 检查端口范围内是否有冲突
                for (int port = unityPortStart; port <= unityPortEnd; port++)
                {
                    bool inUse = await IsPortInUseAsync(port);
                    if (inUse)
                    {
                        // 检查是否是外部进程占用
                        bool isExternalProcess = await IsPortUsedByExternalProcess(port);
                        if (isExternalProcess)
                        {
                            hasConflicts = true;
                            conflictPorts.Add(port);
                        }
                    }
                }

                // 如果有外部进程占用端口，询问用户是否清理
                if (hasConflicts)
                {
                    string conflictPortsStr = string.Join(", ", conflictPorts);
                    if (EditorUtility.DisplayDialog("端口冲突",
                        $"端口 {conflictPortsStr} 被外部进程占用。\n\n" +
                        "选择'清理'将尝试终止占用进程，\n" +
                        "选择'继续'将使用其他可用端口启动。", "清理", "继续"))
                    {
                        // 用户选择清理冲突端口
                        await ClearConflictPorts(conflictPorts);
                    }
                }

                // 尝试启动Unity MCP，它会自动选择可用端口
                McpConnect.Start();

                // 检查启动是否成功
                if (McpConnect.IsRunning)
                {
                    isUnityBridgeRunning = true;
                    Debug.Log($"Unity MCP Bridge 已启动，使用端口: {McpConnect.currentPort}");
                }
                else
                {
                    isUnityBridgeRunning = false;
                    EditorUtility.DisplayDialog("启动失败",
                        $"无法在端口范围 {unityPortStart}-{unityPortEnd} 内启动Unity MCP Bridge。\n" +
                        "请检查是否有其他进程占用了所有端口。", "确定");
                }
            }
            EditorPrefs.SetBool("mcp_open_state", isUnityBridgeRunning);
            Repaint();
        }

        private async Task<bool> IsPortUsedByExternalProcess(int port)
        {
            return await Task.Run(() =>
            {
                try
                {
                    int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

#if UNITY_EDITOR_WIN
                    // Windows: 使用netstat检查端口占用
                    System.Diagnostics.Process netstat = new System.Diagnostics.Process();
                    netstat.StartInfo.FileName = "cmd.exe";
                    netstat.StartInfo.Arguments = $"/c netstat -ano | findstr :{port}";
                    netstat.StartInfo.RedirectStandardOutput = true;
                    netstat.StartInfo.UseShellExecute = false;
                    netstat.StartInfo.CreateNoWindow = true;
                    netstat.Start();
                    string output = netstat.StandardOutput.ReadToEnd();
                    netstat.WaitForExit();

                    string[] lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && int.TryParse(parts[4], out int pid))
                        {
                            if (pid != currentProcessId && line.Contains("LISTENING"))
                            {
                                return true; // 外部进程占用
                            }
                        }
                    }
#elif UNITY_EDITOR_OSX
                    // macOS: 使用lsof检查端口占用
                    System.Diagnostics.Process lsof = new System.Diagnostics.Process();
                    lsof.StartInfo.FileName = "/bin/bash";
                    lsof.StartInfo.Arguments = $"-c \"lsof -i :{port} -sTCP:LISTEN\"";
                    lsof.StartInfo.RedirectStandardOutput = true;
                    lsof.StartInfo.UseShellExecute = false;
                    lsof.StartInfo.CreateNoWindow = true;
                    lsof.Start();
                    string output = lsof.StandardOutput.ReadToEnd();
                    lsof.WaitForExit();

                    string[] lines = output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("LISTEN"))
                        {
                            var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && int.TryParse(parts[1], out int pid))
                            {
                                if (pid != currentProcessId)
                                {
                                    return true; // 外部进程占用
                                }
                            }
                        }
                    }
#endif
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"检查端口是否被外部进程占用时发生错误: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task ClearConflictPorts(List<int> conflictPorts)
        {
            await Task.Run(() =>
            {
                try
                {
                    int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

                    foreach (int port in conflictPorts)
                    {
#if UNITY_EDITOR_WIN
                        // Windows: 查找并杀死占用端口的进程
                        System.Diagnostics.Process netstat = new System.Diagnostics.Process();
                        netstat.StartInfo.FileName = "cmd.exe";
                        netstat.StartInfo.Arguments = $"/c netstat -ano | findstr :{port}";
                        netstat.StartInfo.RedirectStandardOutput = true;
                        netstat.StartInfo.UseShellExecute = false;
                        netstat.StartInfo.CreateNoWindow = true;
                        netstat.Start();
                        string output = netstat.StandardOutput.ReadToEnd();
                        netstat.WaitForExit();

                        string[] lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                        HashSet<int> pids = new HashSet<int>();
                        foreach (var line in lines)
                        {
                            var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5 && int.TryParse(parts[4], out int pid))
                            {
                                if (pid != currentProcessId && line.Contains("LISTENING"))
                                {
                                    pids.Add(pid);
                                }
                            }
                        }

                        foreach (int pid in pids)
                        {
                            System.Diagnostics.Process kill = new System.Diagnostics.Process();
                            kill.StartInfo.FileName = "taskkill";
                            kill.StartInfo.Arguments = $"/PID {pid} /F";
                            kill.StartInfo.CreateNoWindow = true;
                            kill.StartInfo.UseShellExecute = false;
                            kill.Start();
                            kill.WaitForExit();
                        }
#elif UNITY_EDITOR_OSX
                        // macOS: 查找并杀死占用端口的进程
                        System.Diagnostics.Process lsof = new System.Diagnostics.Process();
                        lsof.StartInfo.FileName = "/bin/bash";
                        lsof.StartInfo.Arguments = $"-c \"lsof -i :{port} -sTCP:LISTEN -t\"";
                        lsof.StartInfo.RedirectStandardOutput = true;
                        lsof.StartInfo.UseShellExecute = false;
                        lsof.StartInfo.CreateNoWindow = true;
                        lsof.Start();
                        string output = lsof.StandardOutput.ReadToEnd();
                        lsof.WaitForExit();

                        string[] pids = output.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var pidStr in pids)
                        {
                            if (int.TryParse(pidStr, out int pid) && pid != currentProcessId)
                            {
                                System.Diagnostics.Process kill = new System.Diagnostics.Process();
                                kill.StartInfo.FileName = "/bin/bash";
                                kill.StartInfo.Arguments = $"-c \"kill -9 {pid}\"";
                                kill.StartInfo.CreateNoWindow = true;
                                kill.StartInfo.UseShellExecute = false;
                                kill.Start();
                                kill.WaitForExit();
                            }
                        }
#endif
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"清理冲突端口时发生错误: {ex.Message}");
                }
            });
        }

        private void DrawControlPanel()
        {
            // 控制面板现在为空，因为日志开关已移至标题行
            // 如果将来需要添加更多控件，可以在此处添加
        }

        /// <summary>
        /// 动态计算工具方法列表的可用高度
        /// </summary>
        private float CalculateAvailableMethodsHeight()
        {
            // 获取当前窗口总高度
            float windowHeight = position.height;

            // 估算已占用的空间
            float usedHeight = 0f;

            // Unity Bridge Section 估算高度 (约 120-140px)
            usedHeight += 100;

            // 客户端连接状态部分（如果显示）
            if (isUnityBridgeRunning)
            {
                int clientCount = McpConnect.ConnectedClientCount;
                if (clientCount > 0)
                {
                    // 如果显示详细信息，额外增加滚动视图高度
                    if (showClientDetails)
                    {
                        usedHeight += 80f;
                        // 滚动视图：最小80px，每个客户端约占100px，最大220px，再加上边距
                        usedHeight += Mathf.Max(80f, Mathf.Min(clientCount * 100f, 220f)) + 10f;
                    }
                }
            }

            // 工具方法列表标题和间距 (约 50px)
            usedHeight += 50f;

            // 窗口边距和滚动条等 (约 30px)
            usedHeight += 30f;

            // 计算剩余可用高度，至少保留 150px
            float availableHeight = Mathf.Max(windowHeight - usedHeight, 150f);

            return availableHeight;
        }

        /// <summary>
        /// 绘制工具方法列表，支持折叠展开，按程序集分类显示
        /// </summary>
        private void DrawMethodsList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            // 标题栏：左侧显示标题，右侧显示调试按钮
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("可用工具方法", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            // 调试窗口按钮
            GUIStyle titleDebugButtonStyle = new GUIStyle(EditorStyles.miniButton);
            Color titleOriginalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f); // 淡蓝色背景

            if (GUILayout.Button("调试窗口", titleDebugButtonStyle, GUILayout.Width(70)))
            {
                // 打开调试窗口（不预填充内容）
                McpDebugWindow.ShowWindow();
            }

            GUI.backgroundColor = titleOriginalColor;
            EditorGUILayout.EndHorizontal();

            // 确保方法已注册
            FunctionCall.EnsureMethodsRegisteredStatic();
            var methodNames = FunctionCall.GetRegisteredMethodNames();

            // 按程序集分组方法
            var methodsByAssembly = new Dictionary<string, List<(string methodName, IToolMethod method)>>();

            foreach (var methodName in methodNames)
            {
                var method = FunctionCall.GetRegisteredMethod(methodName);
                if (method == null) continue;

                // 获取程序集名称
                string assemblyName = GetAssemblyDisplayName(method.GetType().Assembly);

                if (!methodsByAssembly.ContainsKey(assemblyName))
                {
                    methodsByAssembly[assemblyName] = new List<(string, IToolMethod)>();
                }

                methodsByAssembly[assemblyName].Add((methodName, method));
            }

            // 动态计算可用高度并应用到滚动视图
            float availableHeight = CalculateAvailableMethodsHeight();
            methodsScrollPosition = EditorGUILayout.BeginScrollView(methodsScrollPosition,
                GUILayout.Height(availableHeight));

            // 按程序集名称排序并绘制
            foreach (var assemblyGroup in methodsByAssembly.OrderBy(kvp => kvp.Key))
            {
                string assemblyName = assemblyGroup.Key;
                var methods = assemblyGroup.Value.OrderBy(m => m.methodName).ToList();

                // 确保程序集在折叠字典中有条目
                if (!assemblyFoldouts.ContainsKey(assemblyName))
                {
                    assemblyFoldouts[assemblyName] = false;
                }

                // 绘制程序集折叠标题
                EditorGUILayout.BeginVertical("box");

                GUIStyle assemblyFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };

                EditorGUILayout.BeginHorizontal();
                assemblyFoldouts[assemblyName] = EditorGUILayout.Foldout(
                    assemblyFoldouts[assemblyName],
                    $"{assemblyName} ({methods.Count})",
                    true,
                    assemblyFoldoutStyle
                );
                EditorGUILayout.EndHorizontal();

                // 如果程序集展开，显示其中的方法
                if (assemblyFoldouts[assemblyName])
                {
                    EditorGUILayout.BeginVertical();
                    EditorGUI.indentLevel++;

                    foreach (var (methodName, method) in methods)
                    {
                        // 确保该方法在字典中有一个条目
                        if (!methodFoldouts.ContainsKey(methodName))
                        {
                            methodFoldouts[methodName] = false;
                        }

                        // 绘制方法折叠标题
                        EditorGUILayout.BeginVertical("box");

                        // 折叠标题栏样式
                        GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
                        {
                            fontStyle = FontStyle.Bold
                        };

                        // 在一行中显示折叠标题、问号按钮和调试按钮
                        EditorGUILayout.BeginHorizontal();

                        // 绘制折叠标题
                        Rect foldoutRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

                        // 计算按钮的位置
                        float buttonWidth = 20f;
                        float buttonHeight = 18f;
                        float padding = 2f;
                        float totalButtonsWidth = (buttonWidth + padding) * 2; // 两个按钮的总宽度

                        // 分离出调试按钮的区域
                        Rect debugButtonRect = new Rect(
                            foldoutRect.xMax - buttonWidth - padding,
                            foldoutRect.y + (foldoutRect.height - buttonHeight) / 2,
                            buttonWidth,
                            buttonHeight
                        );

                        // 分离出问号按钮的区域
                        Rect helpButtonRect = new Rect(
                            foldoutRect.xMax - (buttonWidth + padding) * 2,
                            foldoutRect.y + (foldoutRect.height - buttonHeight) / 2,
                            buttonWidth,
                            buttonHeight
                        );

                        // 留给折叠标题的区域
                        Rect actualFoldoutRect = new Rect(
                            foldoutRect.x,
                            foldoutRect.y,
                            foldoutRect.width - totalButtonsWidth - padding,
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

                        // 绘制调试按钮
                        GUIStyle debugButtonStyle = new GUIStyle(EditorStyles.miniButton);
                        Color originalColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f); // 淡蓝色背景

                        if (GUI.Button(debugButtonRect, "T", debugButtonStyle))
                        {
                            // 处理调试按钮点击事件
                            HandleMethodDebugClick(methodName, method);
                        }

                        GUI.backgroundColor = originalColor;

                        EditorGUILayout.EndHorizontal();

                        // 如果展开，显示预览信息
                        if (methodFoldouts[methodName])
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                            // === 参数Keys信息部分 ===
                            EditorGUILayout.BeginVertical("box");

                            var keys = method.Keys;
                            if (keys != null && keys.Length > 0)
                            {
                                foreach (var key in keys)
                                {
                                    // 创建参数行的样式
                                    EditorGUILayout.BeginHorizontal();
                                    // 参数名称 - 必需参数用粗体，可选参数用普通字体
                                    GUIStyle keyStyle = EditorStyles.miniBoldLabel;
                                    originalColor = GUI.color;

                                    // 必需参数用红色标记，可选参数用灰色标记
                                    GUI.color = key.Optional ? Color.red : Color.green;
                                    // 参数名称
                                    EditorGUILayout.SelectableLabel(key.Key, keyStyle, GUILayout.Width(120), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                    GUI.color = originalColor;

                                    // 参数描述
                                    EditorGUILayout.SelectableLabel(key.Desc, keyStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            else
                            {
                                EditorGUILayout.LabelField("无参数", EditorStyles.centeredGreyMiniLabel);
                            }

                            EditorGUILayout.EndVertical();

                            // 添加一些间距
                            EditorGUILayout.Space(3);

                            // === 状态树结构部分 ===
                            EditorGUILayout.BeginVertical("box");

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
                            EditorGUILayout.EndVertical();
                        }

                        EditorGUILayout.EndVertical();
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 获取程序集的显示名称
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns>程序集显示名称</returns>
        private string GetAssemblyDisplayName(System.Reflection.Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name;

            // Return all names in English
            if (assemblyName.StartsWith("Assembly-CSharp"))
            {
                return "Main Project Assembly";
            }
            else if (assemblyName.StartsWith("UnityMcp"))
            {
                return "Unity MCP";
            }
            else if (assemblyName.StartsWith("Unity."))
            {
                return $"Unity Built-in ({assemblyName.Replace("Unity.", "")})";
            }
            else if (assemblyName == "mscorlib" || assemblyName == "System" || assemblyName.StartsWith("System."))
            {
                return ".NET System Library";
            }
            else
            {
                return assemblyName;
            }
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

        /// <summary>
        /// 处理方法调试按钮的点击事件
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="method">方法实例</param>
        private void HandleMethodDebugClick(string methodName, IToolMethod method)
        {
            try
            {
                // 生成方法调用的示例JSON
                string exampleJson = GenerateMethodExampleJson(methodName, method);

                // 打开McpDebugWindow并预填充示例
                McpDebugWindow.ShowWindowWithContent(exampleJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityMcpEditorWindow] 生成调试示例时发生错误: {e}");
                EditorUtility.DisplayDialog("错误", $"无法生成调试示例: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 生成方法调用的示例JSON
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="method">方法实例</param>
        /// <returns>示例JSON字符串</returns>
        private string GenerateMethodExampleJson(string methodName, IToolMethod method)
        {
            try
            {
                var exampleCall = new
                {
                    func = methodName,
                    args = GenerateExampleArgs(method)
                };

                return JsonConvert.SerializeObject(exampleCall, Formatting.Indented);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"生成示例JSON失败，使用基础模板: {e.Message}");

                // 如果生成失败，返回基础模板
                var basicCall = new
                {
                    func = methodName,
                    args = new { }
                };

                return JsonConvert.SerializeObject(basicCall, Formatting.Indented);
            }
        }

        /// <summary>
        /// 生成方法的示例参数
        /// </summary>
        /// <param name="method">方法实例</param>
        /// <returns>示例参数对象</returns>
        private object GenerateExampleArgs(IToolMethod method)
        {
            var exampleArgs = new Dictionary<string, object>();
            var keys = method.Keys;

            if (keys != null && keys.Length > 0)
            {
                foreach (var key in keys)
                {
                    // 根据参数名和描述生成示例值
                    object exampleValue = GenerateExampleValue(key.Key, key.Desc, key.Optional);
                    if (exampleValue != null)
                    {
                        exampleArgs[key.Key] = exampleValue;
                    }
                }
            }

            return exampleArgs;
        }

        /// <summary>
        /// 根据参数信息生成示例值
        /// </summary>
        /// <param name="keyName">参数名</param>
        /// <param name="description">参数描述</param>
        /// <param name="isOptional">是否可选</param>
        /// <returns>示例值</returns>
        private object GenerateExampleValue(string keyName, string description, bool isOptional)
        {
            // 转换为小写用于模式匹配
            string lowerKey = keyName.ToLower();
            string lowerDesc = description?.ToLower() ?? "";

            // 根据参数名和描述推断类型和示例值
            switch (lowerKey)
            {
                case "action":
                    return "modify"; // 默认操作

                case "from":
                    return "primitive";

                case "primitive_type":
                    return "Cube";

                case "name":
                    return "ExampleObject";

                case "path":
                    if (lowerDesc.Contains("material"))
                        return "Assets/Materials/ExampleMaterial.mat";
                    if (lowerDesc.Contains("prefab"))
                        return "Assets/Prefabs/ExamplePrefab.prefab";
                    if (lowerDesc.Contains("script"))
                        return "Assets/Scripts/ExampleScript.cs";
                    if (lowerDesc.Contains("texture"))
                        return "Assets/Textures/ExampleTexture.png";
                    return "Assets/Example.asset";

                case "target":
                    return "ExampleTarget";

                case "position":
                    return new float[] { 0, 0, 0 };

                case "rotation":
                    return new float[] { 0, 0, 0 };

                case "scale":
                    return new float[] { 1, 1, 1 };

                case "shader":
                    return "Standard";

                case "properties":
                    if (lowerDesc.Contains("color") || lowerKey.Contains("color"))
                        return new { _Color = new { r = 1.0f, g = 0.0f, b = 0.0f, a = 1.0f } };
                    return new { };

                case "active":
                    return true;

                case "tag":
                    return "Untagged";

                case "layer":
                    return "Default";

                case "component_type":
                    return "Rigidbody";

                case "search_method":
                    return "by_name";

                case "url":
                    return "https://httpbin.org/get";

                case "timeout":
                    return 30;

                case "build_index":
                    return 0;

                case "texture_type":
                    return "Sprite";

                case "mesh_type":
                    return "cube";

                default:
                    // 根据描述内容推断
                    if (lowerDesc.Contains("bool") || lowerDesc.Contains("是否"))
                        return !isOptional; // 必需参数默认true，可选参数默认false

                    if (lowerDesc.Contains("array") || lowerDesc.Contains("list") || lowerDesc.Contains("数组"))
                        return new object[] { };

                    if (lowerDesc.Contains("number") || lowerDesc.Contains("int") || lowerDesc.Contains("数字"))
                        return 0;

                    if (lowerDesc.Contains("float") || lowerDesc.Contains("浮点"))
                        return 0.0f;

                    // 如果是可选参数且无法推断类型，返回null（不添加到参数中）
                    if (isOptional)
                        return null;

                    // 必需参数默认返回空字符串
                    return "";
            }
        }

        /// <summary>
        /// 绘制客户端连接状态
        /// </summary>
        private void DrawClientConnectionStatus()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 客户端连接状态标题
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("客户端连接状态", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            // 显示连接数量
            int clientCount = McpConnect.ConnectedClientCount;
            Color countColor = clientCount > 0 ? Color.green : Color.gray;
            GUIStyle countStyle = new GUIStyle(EditorStyles.label);
            countStyle.normal.textColor = countColor;
            countStyle.fontStyle = FontStyle.Bold;

            EditorGUILayout.LabelField($"连接数: {clientCount}", countStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (clientCount > 0)
            {
                // 详细信息折叠控制
                showClientDetails = EditorGUILayout.Foldout(showClientDetails, "显示详细信息", true);

                if (showClientDetails)
                {
                    EditorGUILayout.Space(5);

                    // 客户端列表滚动视图
                    clientsScrollPosition = EditorGUILayout.BeginScrollView(clientsScrollPosition,
                        GUILayout.MinHeight(80), GUILayout.MaxHeight(220));

                    var clients = McpConnect.GetConnectedClients();
                    foreach (var client in clients)
                    {
                        EditorGUILayout.BeginVertical("box");

                        // 客户端基本信息
                        EditorGUILayout.LabelField($"端点: {client.EndPoint}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"连接时间: {client.ConnectedAt:HH:mm:ss}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"最后活动: {client.LastActivity:HH:mm:ss}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"命令数: {client.CommandCount}", EditorStyles.miniLabel);

                        // 计算连接持续时间
                        TimeSpan duration = DateTime.Now - client.ConnectedAt;
                        string durationText = duration.TotalMinutes < 1
                            ? $"{duration.Seconds}秒"
                            : $"{(int)duration.TotalMinutes}分{duration.Seconds}秒";
                        EditorGUILayout.LabelField($"连接时长: {durationText}", EditorStyles.miniLabel);

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                EditorGUILayout.LabelField("暂无客户端连接", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
