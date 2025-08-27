using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityMcp.Models; // For Response class
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles reading and clearing Unity Editor console log entries.
    /// Uses reflection to access internal LogEntry methods/properties.
    /// 对应方法名: read_console
    /// </summary>
    public class ReadConsole : StateMethodBase
    {
        // 注意：实际的控制台操作功能已移至 ConsoleController

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("get", HandleGetAction)
                    .Leaf("clear", HandleClearAction)
                .Build();
        }
        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理获取控制台日志的操作
        /// </summary>
        private object HandleGetAction(JObject args)
        {
            // 检查 ConsoleController 是否已正确初始化
            if (!ConsoleUtils.AreReflectionMembersInitialized())
            {
                if (UnityMcp.EnableLog) Debug.LogError(
                    "[ReadConsole] HandleGetAction called but ConsoleController reflection members are not initialized."
                );
                return Response.Error(
                    "ConsoleController failed to initialize due to reflection errors. Cannot access console logs."
                );
            }

            try
            {
                // 提取 'get' 操作的参数
                var types =
                    (args["types"] as JArray)?.Select(t => t.ToString().ToLower()).ToList()
                    ?? new List<string> { "error", "warning", "log" };
                int? count = args["count"]?.ToObject<int?>();
                string filterText = args["filterText"]?.ToString();
                string sinceTimestampStr = args["sinceTimestamp"]?.ToString();
                string format = (args["format"]?.ToString() ?? "detailed").ToLower();
                bool includeStacktrace = args["includeStacktrace"]?.ToObject<bool?>() ?? true;

                if (types.Contains("all"))
                {
                    types = new List<string> { "error", "warning", "log" };
                }

                if (!string.IsNullOrEmpty(sinceTimestampStr))
                {
                    if (UnityMcp.EnableLog) Debug.LogWarning(
                        "[ReadConsole] Filtering by 'since_timestamp' is not currently implemented."
                    );
                }

                LogInfo($"[ReadConsole] Getting console entries with types: [{string.Join(", ", types)}], count: {count?.ToString() ?? "all"}, filter: '{filterText ?? "none"}', format: {format}");

                // 使用 ConsoleController 获取控制台条目
                var entries = ConsoleUtils.GetConsoleEntries(types, count, filterText, format, includeStacktrace);
                return Response.Success(
                    $"Retrieved {entries.Count} log entries.",
                    entries
                );
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ReadConsole] Get action failed: {e}");
                return Response.Error($"Internal error processing get action: {e.Message}");
            }
        }

        /// <summary>
        /// 处理清空控制台的操作
        /// </summary>
        private object HandleClearAction(JObject args)
        {
            // 检查 ConsoleController 是否已正确初始化
            if (!ConsoleUtils.AreReflectionMembersInitialized())
            {
                if (UnityMcp.EnableLog) Debug.LogError(
                    "[ReadConsole] HandleClearAction called but ConsoleController reflection members are not initialized."
                );
                return Response.Error(
                    "ConsoleController failed to initialize due to reflection errors. Cannot clear console logs."
                );
            }

            try
            {
                LogInfo("[ReadConsole] Clearing console logs");
                ConsoleUtils.ClearConsole();
                return Response.Success("Console cleared successfully.");
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ReadConsole] Clear action failed: {e}");
                return Response.Error($"Internal error processing clear action: {e.Message}");
            }
        }

        /// <summary>
        /// 处理未知操作的回调方法
        /// </summary>
        private object HandleUnknownAction(JObject args)
        {
            string action = args["action"]?.ToString() ?? "null";
            return Response.Error($"Unknown action: '{action}' for read_console. Valid actions are 'get' or 'clear'.");
        }

        // --- Internal Helper Methods ---

        // 注意：原来的控制台操作实现已移动到 ConsoleController 中
    }
}

