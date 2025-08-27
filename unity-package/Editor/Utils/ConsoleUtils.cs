using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 负责Unity编辑器控制台的实际操作，包括读取和清空日志条目。
    /// 使用反射访问内部LogEntry方法/属性。
    /// </summary>
    public static class ConsoleUtils
    {
        // 用于访问内部LogEntry数据的反射成员
        private static MethodInfo _startGettingEntriesMethod;
        private static MethodInfo _endGettingEntriesMethod;
        private static MethodInfo _clearMethod;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryMethod;
        private static FieldInfo _modeField;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _instanceIdField;

        // 静态构造函数用于反射设置
        static ConsoleUtils()
        {
            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntries"
                );
                if (logEntriesType == null)
                    throw new Exception("Could not find internal type UnityEditor.LogEntries");

                // 包含NonPublic绑定标志，因为内部API可能会改变可访问性
                BindingFlags staticFlags =
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags instanceFlags =
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod(
                    "StartGettingEntries",
                    staticFlags
                );
                if (_startGettingEntriesMethod == null)
                    throw new Exception("Failed to reflect LogEntries.StartGettingEntries");

                _endGettingEntriesMethod = logEntriesType.GetMethod(
                    "EndGettingEntries",
                    staticFlags
                );
                if (_endGettingEntriesMethod == null)
                    throw new Exception("Failed to reflect LogEntries.EndGettingEntries");

                _clearMethod = logEntriesType.GetMethod("Clear", staticFlags);
                if (_clearMethod == null)
                    throw new Exception("Failed to reflect LogEntries.Clear");

                _getCountMethod = logEntriesType.GetMethod("GetCount", staticFlags);
                if (_getCountMethod == null)
                    throw new Exception("Failed to reflect LogEntries.GetCount");

                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", staticFlags);
                if (_getEntryMethod == null)
                    throw new Exception("Failed to reflect LogEntries.GetEntryInternal");

                Type logEntryType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntry"
                );
                if (logEntryType == null)
                    throw new Exception("Could not find internal type UnityEditor.LogEntry");

                _modeField = logEntryType.GetField("mode", instanceFlags);
                if (_modeField == null)
                    throw new Exception("Failed to reflect LogEntry.mode");

                _messageField = logEntryType.GetField("message", instanceFlags);
                if (_messageField == null)
                    throw new Exception("Failed to reflect LogEntry.message");

                _fileField = logEntryType.GetField("file", instanceFlags);
                if (_fileField == null)
                    throw new Exception("Failed to reflect LogEntry.file");

                _lineField = logEntryType.GetField("line", instanceFlags);
                if (_lineField == null)
                    throw new Exception("Failed to reflect LogEntry.line");

                _instanceIdField = logEntryType.GetField("instanceID", instanceFlags);
                if (_instanceIdField == null)
                    throw new Exception("Failed to reflect LogEntry.instanceID");
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError(
                    $"[ConsoleController] Static Initialization Failed: Could not setup reflection for LogEntries/LogEntry. Console reading/clearing will likely fail. Specific Error: {e.Message}"
                );
                // 将成员设置为null以防止后续的NullReferenceExceptions
                _startGettingEntriesMethod =
                    _endGettingEntriesMethod =
                    _clearMethod =
                    _getCountMethod =
                    _getEntryMethod =
                        null;
                _modeField = _messageField = _fileField = _lineField = _instanceIdField = null;
            }
        }

        /// <summary>
        /// 检查反射成员是否已正确初始化
        /// </summary>
        public static bool AreReflectionMembersInitialized()
        {
            return _startGettingEntriesMethod != null
                && _endGettingEntriesMethod != null
                && _clearMethod != null
                && _getCountMethod != null
                && _getEntryMethod != null
                && _modeField != null
                && _messageField != null
                && _fileField != null
                && _lineField != null
                && _instanceIdField != null;
        }

        /// <summary>
        /// 清空控制台日志
        /// </summary>
        public static void ClearConsole()
        {
            if (!AreReflectionMembersInitialized())
            {
                throw new InvalidOperationException("ConsoleController reflection members are not initialized. Cannot clear console logs.");
            }

            try
            {
                _clearMethod.Invoke(null, null); // 静态方法，无实例，无参数
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[ConsoleController] Failed to clear console: {e}");
                throw new InvalidOperationException($"Failed to clear console: {e.Message}", e);
            }
        }

        /// <summary>
        /// 获取控制台日志条目
        /// </summary>
        /// <param name="types">要获取的日志类型列表</param>
        /// <param name="count">限制获取的数量，null表示获取所有</param>
        /// <param name="filterText">文本过滤器</param>
        /// <param name="format">返回格式</param>
        /// <param name="includeStacktrace">是否包含堆栈跟踪</param>
        /// <returns>格式化的日志条目列表</returns>
        public static List<object> GetConsoleEntries(
            List<string> types,
            int? count,
            string filterText,
            string format,
            bool includeStacktrace
        )
        {
            if (!AreReflectionMembersInitialized())
            {
                throw new InvalidOperationException("ConsoleController reflection members are not initialized. Cannot access console logs.");
            }

            List<object> formattedEntries = new List<object>();
            int retrievedCount = 0;

            try
            {
                // LogEntries 需要在GetEntries/GetEntryInternal周围调用Start/Stop
                _startGettingEntriesMethod.Invoke(null, null);

                int totalEntries = (int)_getCountMethod.Invoke(null, null);
                // 创建实例传递给GetEntryInternal - 确保类型正确
                Type logEntryType = typeof(EditorApplication).Assembly.GetType(
                    "UnityEditor.LogEntry"
                );
                if (logEntryType == null)
                    throw new Exception(
                        "Could not find internal type UnityEditor.LogEntry during GetConsoleEntries."
                    );
                object logEntryInstance = Activator.CreateInstance(logEntryType);

                for (int i = 0; i < totalEntries; i++)
                {
                    // 使用反射将条目数据获取到我们的实例中
                    _getEntryMethod.Invoke(null, new object[] { i, logEntryInstance });

                    // 使用反射提取数据
                    int mode = (int)_modeField.GetValue(logEntryInstance);
                    string message = (string)_messageField.GetValue(logEntryInstance);
                    string file = (string)_fileField.GetValue(logEntryInstance);
                    int line = (int)_lineField.GetValue(logEntryInstance);

                    if (string.IsNullOrEmpty(message))
                        continue; // 跳过空消息

                    // --- 过滤 ---
                    // 按类型过滤
                    LogType currentType = GetLogTypeFromMode(mode);
                    if (!types.Contains(currentType.ToString().ToLowerInvariant()))
                    {
                        continue;
                    }

                    // 按文本过滤（不区分大小写）
                    if (
                        !string.IsNullOrEmpty(filterText)
                        && message.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0
                    )
                    {
                        continue;
                    }

                    // --- 格式化 ---
                    string stackTrace = includeStacktrace ? ExtractStackTrace(message) : null;
                    // 如果存在堆栈且被请求，获取第一行，否则使用完整消息
                    string messageOnly =
                        (includeStacktrace && !string.IsNullOrEmpty(stackTrace))
                            ? message.Split(
                                new[] { '\n', '\r' },
                                StringSplitOptions.RemoveEmptyEntries
                            )[0]
                            : message;

                    object formattedEntry = null;
                    switch (format)
                    {
                        case "plain":
                            formattedEntry = messageOnly;
                            break;
                        case "json":
                        case "detailed": // 将detailed视为json以返回结构化数据
                        default:
                            formattedEntry = new
                            {
                                type = currentType.ToString(),
                                message = messageOnly,
                                file = file,
                                line = line,
                                stackTrace = stackTrace, // 如果includeStacktrace为false或未找到堆栈，将为null
                            };
                            break;
                    }

                    formattedEntries.Add(formattedEntry);
                    retrievedCount++;

                    // 应用数量限制（过滤后）
                    if (count.HasValue && retrievedCount >= count.Value)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[ConsoleController] Error while retrieving log entries: {e}");
                // 即使在迭代期间出现错误，也要确保调用EndGettingEntries
                try
                {
                    _endGettingEntriesMethod.Invoke(null, null);
                }
                catch
                { /* 忽略嵌套异常 */ }
                throw new InvalidOperationException($"Error retrieving log entries: {e.Message}", e);
            }
            finally
            {
                // 确保我们总是调用EndGettingEntries
                try
                {
                    _endGettingEntriesMethod.Invoke(null, null);
                }
                catch (Exception e)
                {
                    if (McpConnect.EnableLog) Debug.LogError($"[ConsoleController] Failed to call EndGettingEntries: {e}");
                    // 这里不返回错误，因为我们可能有有效数据，但要记录它。
                }
            }

            return formattedEntries;
        }

        // --- 内部辅助方法 ---

        // LogEntry.mode位到LogType枚举的映射
        // 基于反编译的UnityEditor代码或常见模式。确切的位可能在Unity版本之间改变。
        private const int ModeBitError = 1 << 0;
        private const int ModeBitAssert = 1 << 1;
        private const int ModeBitWarning = 1 << 2;
        private const int ModeBitLog = 1 << 3;
        private const int ModeBitException = 1 << 4; // 经常与Error位组合
        private const int ModeBitScriptingError = 1 << 9;
        private const int ModeBitScriptingWarning = 1 << 10;
        private const int ModeBitScriptingLog = 1 << 11;
        private const int ModeBitScriptingException = 1 << 18;
        private const int ModeBitScriptingAssertion = 1 << 22;

        private static LogType GetLogTypeFromMode(int mode)
        {
            // 首先，基于原始逻辑确定类型（最严重的优先）
            LogType initialType;
            if (
                (
                    mode
                    & (
                        ModeBitError
                        | ModeBitScriptingError
                        | ModeBitException
                        | ModeBitScriptingException
                    )
                ) != 0
            )
            {
                initialType = LogType.Error;
            }
            else if ((mode & (ModeBitAssert | ModeBitScriptingAssertion)) != 0)
            {
                initialType = LogType.Assert;
            }
            else if ((mode & (ModeBitWarning | ModeBitScriptingWarning)) != 0)
            {
                initialType = LogType.Warning;
            }
            else
            {
                initialType = LogType.Log;
            }

            // 应用观察到的"低一级"校正
            switch (initialType)
            {
                case LogType.Error:
                    return LogType.Warning; // Error变成Warning
                case LogType.Warning:
                    return LogType.Log; // Warning变成Log
                case LogType.Assert:
                    return LogType.Assert; // Assert保持Assert（没有定义更低级别）
                case LogType.Log:
                    return LogType.Log; // Log保持Log
                default:
                    return LogType.Log; // 默认回退
            }
        }

        /// <summary>
        /// 尝试从日志消息中提取堆栈跟踪部分。
        /// Unity日志消息通常在主消息后附加堆栈跟踪，
        /// 从新行开始，通常缩进或以"at "开始。
        /// </summary>
        /// <param name="fullMessage">包含潜在堆栈跟踪的完整日志消息。</param>
        /// <returns>提取的堆栈跟踪字符串，如果没有找到则返回null。</returns>
        private static string ExtractStackTrace(string fullMessage)
        {
            if (string.IsNullOrEmpty(fullMessage))
                return null;

            // 分割成行，移除空行以优雅地处理不同的行结尾。
            string[] lines = fullMessage.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

            // 如果只有一行或更少，没有单独的堆栈跟踪。
            if (lines.Length <= 1)
                return null;

            int stackStartIndex = -1;

            // 从第二行开始检查。
            for (int i = 1; i < lines.Length; ++i)
            {
                string trimmedLine = lines[i].TrimStart();

                // 检查常见的堆栈跟踪模式。
                if (
                    trimmedLine.StartsWith("at ")
                    || trimmedLine.StartsWith("UnityEngine.")
                    || trimmedLine.StartsWith("UnityEditor.")
                    || trimmedLine.Contains("(at ")
                    || // 涵盖"(at Assets/..."模式
                       // 启发式：检查行是否以可能的命名空间/类模式开始（Uppercase.Something）
                    (
                        trimmedLine.Length > 0
                        && char.IsUpper(trimmedLine[0])
                        && trimmedLine.Contains('.')
                    )
                )
                {
                    stackStartIndex = i;
                    break; // 找到堆栈跟踪的可能开始
                }
            }

            // 如果找到了潜在的开始索引...
            if (stackStartIndex > 0)
            {
                // 使用标准换行符连接从堆栈开始索引往后的行。
                // 这重构了消息的堆栈跟踪部分。
                return string.Join("\n", lines.Skip(stackStartIndex));
            }

            // 基于模式没有找到明确的堆栈跟踪。
            return null;
        }
    }
}
