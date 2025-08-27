using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp.Tools;

namespace UnityMcp
{
    [InitializeOnLoad]
    public static partial class UnityMcp
    {
        private static TcpListener listener;
        private static bool isRunning = false;
        private static readonly object lockObj = new();
        private static Dictionary<
            string,
            (string commandJson, TaskCompletionSource<string> tcs)
        > commandQueue = new();
        public static readonly int unityPortStart = 6400; // Start of port range
        public static readonly int unityPortEnd = 6405;   // End of port range
        public static int currentPort = -1; // Currently used port
        public static bool IsRunning => isRunning;

        // 客户端连接状态跟踪
        private static readonly Dictionary<string, ClientInfo> connectedClients = new();
        private static readonly object clientsLock = new();

        public static int ConnectedClientCount
        {
            get
            {
                lock (clientsLock)
                {
                    return connectedClients.Count;
                }
            }
        }

        public static List<ClientInfo> GetConnectedClients()
        {
            lock (clientsLock)
            {
                return connectedClients.Values.ToList();
            }
        }

        // 客户端信息类
        public class ClientInfo
        {
            public string Id { get; set; }
            public string EndPoint { get; set; }
            public DateTime ConnectedAt { get; set; }
            public DateTime LastActivity { get; set; }
            public int CommandCount { get; set; }
        }

        // 缓存McpTool类型和实例，静态工具类型
        private static readonly Dictionary<string, McpTool> mcpToolInstanceCache = new();
        // 在 UnityMcp 类中添加日志开关
        public static bool EnableLog = false;

        // 统一的日志输出方法
        private static void Log(string message)
        {
            if (EnableLog) Debug.Log(message);
        }

        private static void LogWarning(string message)
        {
            if (EnableLog) Debug.LogWarning(message);
        }

        private static void LogError(string message)
        {
            if (EnableLog) Debug.LogError(message);
        }

        public static bool FolderExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fullPath = Path.Combine(
                Application.dataPath,
                path.StartsWith("Assets/") ? path[7..] : path
            );
            return Directory.Exists(fullPath);
        }

        static UnityMcp()
        {
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            if (EditorPrefs.HasKey("mcp_open_state") && EditorPrefs.GetBool("mcp_open_state"))
            {
                Start();
            }
        }

        public static void Start()
        {
            // 从EditorPrefs读取日志设置，默认为false
            UnityMcp.EnableLog = EditorPrefs.GetBool("mcp_enable_log", false);
            Log($"[UnityMcp] 正在启动UnityMcp...");
            Stop();

            if (isRunning)
            {
                Log($"[UnityMcp] 服务已在运行中");
                return;
            }

            // Try to start listener on available port in range
            bool started = false;
            SocketException lastException = null;

            for (int port = unityPortStart; port <= unityPortEnd; port++)
            {
                try
                {
                    Log($"[UnityMcp] 尝试在端口 {port} 启动TCP监听器...");
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    currentPort = port;
                    isRunning = true;
                    started = true;
                    Log($"[UnityMcp] TCP监听器已成功启动，端口: {port}");

                    // Start the listener loop and command processing
                    Task.Run(ListenerLoop);
                    EditorApplication.update += ProcessCommands;
                    Log($"[UnityMcp] 启动完成，监听循环已开始，命令处理已注册");
                    break;
                }
                catch (SocketException ex)
                {
                    lastException = ex;
                    if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        Log($"[UnityMcp] 端口 {port} 已被占用，尝试下一个端口...");
                    }
                    else
                    {
                        LogWarning($"[UnityMcp] 端口 {port} 启动失败: {ex.Message}，尝试下一个端口...");
                    }

                    // Clean up failed listener
                    try
                    {
                        listener?.Stop();
                    }
                    catch { }
                    listener = null;
                }
            }

            if (!started)
            {
                LogError($"[UnityMcp] 无法在端口范围 {unityPortStart}-{unityPortEnd} 内启动TCP监听器。最后错误: {lastException?.Message}");
                if (lastException?.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    LogError("[UnityMcp] 所有端口都被占用。请确保没有其他Unity MCP实例正在运行。");
                }
            }
        }

        public static void Stop()
        {
            if (!isRunning)
            {
                return;
            }

            Log($"[UnityMcp] 正在停止UnityMcp...");

            try
            {
                listener?.Stop();
                listener = null;
                isRunning = false;
                currentPort = -1; // Reset current port

                // 清空客户端连接信息
                lock (clientsLock)
                {
                    connectedClients.Clear();
                }

                EditorApplication.update -= ProcessCommands;
                Log($"[UnityMcp] 服务已停止，TCP监听器已关闭，命令处理已注销");
            }
            catch (Exception ex)
            {
                LogError($"[UnityMcp] 停止服务时发生错误: {ex.Message}");
            }
        }

        private static async Task ListenerLoop()
        {
            Log($"[UnityMcp] 监听循环已启动");

            while (isRunning)
            {
                try
                {
                    Log($"[UnityMcp] 等待客户端连接...");
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    // Enable basic socket keepalive
                    client.Client.SetSocketOption(
                        SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive,
                        true
                    );

                    // Set longer receive timeout to prevent quick disconnections
                    client.ReceiveTimeout = 60000; // 60 seconds

                    Log($"[UnityMcp] 客户端连接配置完成：KeepAlive=true, ReceiveTimeout=60s");

                    // Fire and forget each client connection
                    _ = HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        LogError($"[UnityMcp] 监听器错误: {ex.Message}");
                    }
                    else
                    {
                        Log($"[UnityMcp] 监听器已停止");
                    }
                }
            }

            Log($"[UnityMcp] 监听循环已结束");
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            string clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            string clientId = Guid.NewGuid().ToString();
            Log($"[UnityMcp] 客户端已连接: {clientEndpoint} (ID: {clientId})");

            // 添加客户端到连接列表
            var clientInfo = new ClientInfo
            {
                Id = clientId,
                EndPoint = clientEndpoint,
                ConnectedAt = DateTime.Now,
                LastActivity = DateTime.Now,
                CommandCount = 0
            };

            lock (clientsLock)
            {
                connectedClients[clientId] = clientInfo;
            }

            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[8192];
                while (isRunning)
                {
                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Log($"[UnityMcp] 客户端已断开连接: {clientEndpoint}");
                            break; // Client disconnected
                        }

                        string commandText = System.Text.Encoding.UTF8.GetString(
                            buffer,
                            0,
                            bytesRead
                        );
                        Log($"[UnityMcp] 接收到命令 from {clientEndpoint}: {commandText}");

                        // 更新客户端活动状态
                        lock (clientsLock)
                        {
                            if (connectedClients.TryGetValue(clientId, out var existingClient))
                            {
                                existingClient.LastActivity = DateTime.Now;
                                existingClient.CommandCount++;
                            }
                        }

                        string commandId = Guid.NewGuid().ToString();
                        TaskCompletionSource<string> tcs = new();

                        // Special handling for ping command to avoid JSON parsing
                        if (commandText.Trim() == "ping")
                        {
                            Log($"[UnityMcp] 处理ping命令 from {clientEndpoint}");
                            // Direct response to ping without going through JSON parsing
                            byte[] pingResponseBytes = System.Text.Encoding.UTF8.GetBytes(
                                /*lang=json,strict*/
                                "{\"status\":\"success\",\"result\":{\"message\":\"pong\"}}"
                            );
                            await stream.WriteAsync(pingResponseBytes, 0, pingResponseBytes.Length);
                            Log($"[UnityMcp] ping响应已发送 to {clientEndpoint}");
                            continue;
                        }

                        lock (lockObj)
                        {
                            commandQueue[commandId] = (commandText, tcs);
                            Log($"[UnityMcp] 命令已加入队列 ID: {commandId}");
                        }

                        string response = await tcs.Task;
                        byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        Log($"[UnityMcp] 响应已发送 to {clientEndpoint}, ID: {commandId}, Response: {response}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"[UnityMcp] 客户端处理错误 {clientEndpoint}: {ex.Message}");
                        break;
                    }
                }
            }

            // 从连接列表中移除客户端
            lock (clientsLock)
            {
                connectedClients.Remove(clientId);
            }

            Log($"[UnityMcp] 客户端连接已关闭: {clientEndpoint} (ID: {clientId})");
        }

        private static void ProcessCommands()
        {
            List<string> processedIds = new();
            lock (lockObj)
            {
                if (commandQueue.Count > 0)
                {
                    Log($"[UnityMcp] 开始处理命令队列，队列长度: {commandQueue.Count}");
                }

                foreach (
                    KeyValuePair<
                        string,
                        (string commandJson, TaskCompletionSource<string> tcs)
                    > kvp in commandQueue.ToList()
                )
                {
                    string id = kvp.Key;
                    string commandText = kvp.Value.commandJson;
                    TaskCompletionSource<string> tcs = kvp.Value.tcs;

                    Log($"[UnityMcp] 处理命令 ID: {id}");

                    try
                    {
                        // Special case handling
                        if (string.IsNullOrEmpty(commandText))
                        {
                            LogWarning($"[UnityMcp] 接收到空命令 ID: {id}");
                            var emptyResponse = new
                            {
                                status = "error",
                                error = "Empty command received",
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(emptyResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Trim the command text to remove any whitespace
                        commandText = commandText.Trim();

                        // Non-JSON direct commands handling (like ping)
                        if (commandText == "ping")
                        {
                            Log($"[UnityMcp] 处理ping命令 ID: {id}");
                            var pingResponse = new
                            {
                                status = "success",
                                result = new { message = "pong" },
                            };
                            string pingResponseJson = JsonConvert.SerializeObject(pingResponse);
                            tcs.SetResult(pingResponseJson);
                            Log($"[UnityMcp] ping命令处理完成 ID: {id}");
                            processedIds.Add(id);
                            continue;
                        }

                        // Check if the command is valid JSON before attempting to deserialize
                        if (!IsValidJson(commandText))
                        {
                            LogError($"[UnityMcp] 无效JSON格式 ID: {id}, Content: {commandText}");
                            var invalidJsonResponse = new
                            {
                                status = "error",
                                error = "Invalid JSON format",
                                receivedText = commandText.Length > 50
                                    ? commandText[..50] + "..."
                                    : commandText,
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(invalidJsonResponse));
                            processedIds.Add(id);
                            continue;
                        }

                        // Normal JSON command processing
                        Log($"[UnityMcp] 开始解析JSON命令 ID: {id}");
                        Command command = JsonConvert.DeserializeObject<Command>(commandText);
                        if (command == null)
                        {
                            LogError($"[UnityMcp] 命令反序列化为null ID: {id}");
                            var nullCommandResponse = new
                            {
                                status = "error",
                                error = "Command deserialized to null",
                                details = "The command was valid JSON but could not be deserialized to a Command object",
                            };
                            tcs.SetResult(JsonConvert.SerializeObject(nullCommandResponse));
                        }
                        else
                        {
                            Log($"[UnityMcp] 执行命令 ID: {id}, Type: {command.type}");
                            string responseJson = ExecuteCommand(command);
                            tcs.SetResult(responseJson);
                            Log($"[UnityMcp] 命令执行完成 ID: {id}, Type: {command.type}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"[UnityMcp] 处理命令时发生错误 ID: {id}: {ex.Message}\n{ex.StackTrace}");

                        var response = new
                        {
                            status = "error",
                            error = ex.Message,
                            commandType = "Unknown (error during processing)",
                            receivedText = commandText?.Length > 50
                                ? commandText[..50] + "..."
                                : commandText,
                        };
                        string responseJson = JsonConvert.SerializeObject(response);
                        tcs.SetResult(responseJson);
                        Log($"[UnityMcp] 错误响应已设置 ID: {id}");
                    }

                    processedIds.Add(id);
                }

                foreach (string id in processedIds)
                {
                    commandQueue.Remove(id);
                }

                if (processedIds.Count > 0)
                {
                    Log($"[UnityMcp] 命令队列处理完成，已处理: {processedIds.Count} 个命令");
                }
            }
        }

        // Helper method to check if a string is valid JSON
        private static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (
                (text.StartsWith("{") && text.EndsWith("}"))
                || // Object
                (text.StartsWith("[") && text.EndsWith("]"))
            ) // Array
            {
                try
                {
                    JToken.Parse(text);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string ExecuteCommand(Command command)
        {
            Log($"[UnityMcp] 开始执行命令: Type={command.type}");

            try
            {
                if (string.IsNullOrEmpty(command.type))
                {
                    LogError($"[UnityMcp] 命令类型为空");
                    var errorResponse = new
                    {
                        status = "error",
                        error = "Command type cannot be empty",
                        details = "A valid command type is required for processing",
                    };
                    return JsonConvert.SerializeObject(errorResponse);
                }

                // Handle ping command for connection verification
                if (command.type.Equals("ping", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"[UnityMcp] 处理ping命令");
                    var pingResponse = new
                    {
                        status = "success",
                        result = new { message = "pong" },
                    };
                    string pingResult = JsonConvert.SerializeObject(pingResponse);
                    Log($"[UnityMcp] ping命令执行成功");
                    return pingResult;
                }

                // Use JObject for args as the new handlers likely expect this
                JObject paramsObject = command.cmd ?? new JObject();
                Log($"[UnityMcp] 命令参数: {paramsObject}");

                Log($"[UnityMcp] 获取McpTool实例: {command.type}");
                var tool = GetMcpTool(command.type);
                if (tool == null)
                {
                    LogError($"[UnityMcp] 未找到工具: {command.type}");
                    throw new ArgumentException($"Unknown or unsupported command type: {command.type}");
                }

                Log($"[UnityMcp] 找到工具: {tool.GetType().Name}，开始处理命令");
                object result = tool.HandleCommand(paramsObject);
                Log($"[UnityMcp] 工具执行完成，结果类型: {result?.GetType().Name ?? "null"}");

                // Standard success response format
                var response = new { status = "success", result };
                string responseJson = JsonConvert.SerializeObject(response);
                Log($"[UnityMcp] 命令执行成功: Type={command.type}");
                return responseJson;
            }
            catch (Exception ex)
            {
                // Log the detailed error in Unity for debugging
                LogError(
                    $"[UnityMcp] 执行命令时发生错误 '{command?.type ?? "Unknown"}': {ex.Message}\n{ex.StackTrace}"
                );

                // Standard error response format
                var response = new
                {
                    status = "error",
                    error = ex.Message, // Provide the specific error message
                    command = command?.type ?? "Unknown", // Include the command type if available
                    stackTrace = ex.StackTrace, // Include stack trace for detailed debugging
                    paramsSummary = command?.cmd != null
                        ? GetParamsSummary(command.cmd)
                        : "No args", // Summarize args for context
                };
                string errorResult = JsonConvert.SerializeObject(response);
                Log($"[UnityMcp] 错误响应已生成: Type={command?.type ?? "Unknown"}");
                return errorResult;
            }
        }
        /// <summary>
        /// 获取McpTool实例
        /// </summary>
        /// <param name="toolName"></param>
        /// <returns></returns>
        private static McpTool GetMcpTool(string toolName)
        {
            Log($"[UnityMcp] 请求获取工具: {toolName}");

            if (mcpToolInstanceCache.Count == 0)
            {
                Log($"[UnityMcp] 工具缓存为空，开始反射查找工具实例");
                // 没有缓存则反射查找并缓存
                var toolType = typeof(McpTool);
                var toolInstances = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => !t.IsAbstract && toolType.IsAssignableFrom(t)).Select(t => Activator.CreateInstance(t) as McpTool);

                int cacheCount = 0;
                foreach (var toolInstance in toolInstances)
                {
                    mcpToolInstanceCache[toolInstance.ToolName] = toolInstance;
                    cacheCount++;
                    Log($"[UnityMcp] 缓存工具: {toolInstance.ToolName} ({toolInstance.GetType().Name})");
                }
                Log($"[UnityMcp] 工具缓存完成，共缓存 {cacheCount} 个工具");
            }

            if (mcpToolInstanceCache.TryGetValue(toolName, out var tool))
            {
                Log($"[UnityMcp] 从缓存中获取到工具: {toolName} ({tool.GetType().Name})");
                return tool;
            }

            LogError($"[UnityMcp] 未找到工具: {toolName}，可用工具: [{string.Join(", ", mcpToolInstanceCache.Keys)}]");
            return null;
        }
        // Helper method to get a summary of args for error reporting
        private static string GetParamsSummary(JObject cmd)
        {
            try
            {
                return cmd == null || !cmd.HasValues
                    ? "No args"
                    : string.Join(
                        ", ",
                       cmd
                            .Properties()
                            .Select(static p =>
                                $"{p.Name}: {p.Value?.ToString()?[..Math.Min(20, p.Value?.ToString()?.Length ?? 0)]}"
                            )
                    );
            }
            catch
            {
                return "Could not summarize args";
            }
        }
    }
}
