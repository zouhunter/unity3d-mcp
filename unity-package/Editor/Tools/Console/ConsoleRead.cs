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
    /// 对应方法名: console_reader
    /// </summary>
    [ToolName("console_read", "开发工具")]
    public class ConsoleRead : StateMethodBase
    {
        // 注意：实际的控制台操作功能已移至 ConsoleController

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：get(无堆栈跟踪), get_full(包含堆栈跟踪), clear(清空控制台)", false),
                new MethodKey("types", "消息类型列表：error, warning, log，默认全部类型", true),
                new MethodKey("count", "最大返回消息数，不设置则获取全部", true),
                new MethodKey("filterText", "文本过滤器，过滤包含指定文本的日志", true),
                new MethodKey("format", "输出格式：plain, detailed, json，默认detailed", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Branch("get")
                        .OptionalKey("count")
                            .OptionalLeaf("filterText", HandleGetPartialWithFilter)
                            .DefaultLeaf(HandleGetPartialWithoutFilter)
                        .Up()
                        .OptionalLeaf("filterText", HandleGetAllWithFilter)
                        .DefaultLeaf(HandleGetAllWithoutFilter)
                    .Up()
                    .Branch("get_full")
                        .OptionalKey("count")
                            .OptionalLeaf("filterText", HandleGetFullPartialWithFilter)
                            .DefaultLeaf(HandleGetFullPartialWithoutFilter)
                        .Up()
                        .OptionalLeaf("filterText", HandleGetFullAllWithFilter)
                        .DefaultLeaf(HandleGetFullAllWithoutFilter)
                    .Up()
                    .Leaf("clear", HandleClearAction)
                .Build();
        }
        // --- State Tree Action Handlers for GET (不包含堆栈跟踪) ---

        /// <summary>
        /// 处理获取全部控制台日志（无过滤，不包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetAllWithoutFilter(JObject args)
        {
            return GetConsoleEntriesInternal(args, null, null, false, "all log entries (no filter, no stacktrace)");
        }

        /// <summary>
        /// 处理获取全部控制台日志（有过滤，不包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetAllWithFilter(JObject args)
        {
            string filterText = args["filterText"]?.ToString();
            return GetConsoleEntriesInternal(args, null, filterText, false, $"all log entries (filtered by '{filterText}', no stacktrace)");
        }

        /// <summary>
        /// 处理获取部分控制台日志（无过滤，不包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetPartialWithoutFilter(JObject args)
        {
            int count = args["count"]?.ToObject<int>() ?? 10;
            return GetConsoleEntriesInternal(args, count, null, false, $"{count} log entries (no filter, no stacktrace)");
        }

        /// <summary>
        /// 处理获取部分控制台日志（有过滤，不包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetPartialWithFilter(JObject args)
        {
            int count = args["count"]?.ToObject<int>() ?? 10;
            string filterText = args["filterText"]?.ToString();
            return GetConsoleEntriesInternal(args, count, filterText, false, $"{count} log entries (filtered by '{filterText}', no stacktrace)");
        }

        // --- State Tree Action Handlers for GET_FULL (包含堆栈跟踪) ---

        /// <summary>
        /// 处理获取全部控制台日志（无过滤，包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetFullAllWithoutFilter(JObject args)
        {
            return GetConsoleEntriesInternal(args, null, null, true, "all log entries (no filter, with stacktrace)");
        }

        /// <summary>
        /// 处理获取全部控制台日志（有过滤，包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetFullAllWithFilter(JObject args)
        {
            string filterText = args["filterText"]?.ToString();
            return GetConsoleEntriesInternal(args, null, filterText, true, $"all log entries (filtered by '{filterText}', with stacktrace)");
        }

        /// <summary>
        /// 处理获取部分控制台日志（无过滤，包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetFullPartialWithoutFilter(JObject args)
        {
            int count = args["count"]?.ToObject<int>() ?? 10;
            return GetConsoleEntriesInternal(args, count, null, true, $"{count} log entries (no filter, with stacktrace)");
        }

        /// <summary>
        /// 处理获取部分控制台日志（有过滤，包含堆栈跟踪）的操作
        /// </summary>
        private object HandleGetFullPartialWithFilter(JObject args)
        {
            int count = args["count"]?.ToObject<int>() ?? 10;
            string filterText = args["filterText"]?.ToString();
            return GetConsoleEntriesInternal(args, count, filterText, true, $"{count} log entries (filtered by '{filterText}', with stacktrace)");
        }

        /// <summary>
        /// 统一的控制台日志获取逻辑
        /// </summary>
        private object GetConsoleEntriesInternal(JObject args, int? count, string filterText, bool includeStacktrace, string description)
        {
            // 检查 ConsoleController 是否已正确初始化
            if (!ConsoleUtils.AreReflectionMembersInitialized())
            {
                if (McpConnect.EnableLog) Debug.LogError(
                    "[ReadConsole] GetConsoleEntriesInternal called but ConsoleController reflection members are not initialized."
                );
                return Response.Error(
                    "ConsoleController failed to initialize due to reflection errors. Cannot access console logs."
                );
            }

            try
            {
                // 提取参数
                var types = ExtractTypes(args);
                string format = ExtractFormat(args);

                LogInfo($"[ReadConsole] Getting {description}");

                // 使用 ConsoleController 获取控制台条目
                var entries = ConsoleUtils.GetConsoleEntries(types, count, filterText, format, includeStacktrace);
                return Response.Success(
                    $"Retrieved {entries.Count} log entries ({description}).",
                    entries
                );
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[ReadConsole] GetConsoleEntriesInternal failed: {e}");
                return Response.Error($"Internal error processing console entries: {e.Message}");
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
                if (McpConnect.EnableLog) Debug.LogError(
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
                if (McpConnect.EnableLog) Debug.LogError($"[ReadConsole] Clear action failed: {e}");
                return Response.Error($"Internal error processing clear action: {e.Message}");
            }
        }

        // --- Parameter Extraction Helper Methods ---

        /// <summary>
        /// 提取消息类型参数
        /// </summary>
        private List<string> ExtractTypes(JObject args)
        {
            var types = (args["types"] as JArray)?.Select(t => t.ToString().ToLower()).ToList()
                ?? new List<string> { "error", "warning", "log" };

            if (types.Contains("all"))
            {
                types = new List<string> { "error", "warning", "log" };
            }

            return types;
        }

        /// <summary>
        /// 提取格式参数
        /// </summary>
        private string ExtractFormat(JObject args)
        {
            return (args["format"]?.ToString() ?? "detailed").ToLower();
        }

        /// <summary>
        /// 处理未知操作的回调方法
        /// </summary>
        private object HandleUnknownAction(JObject args)
        {
            string action = args["action"]?.ToString() ?? "null";
            return Response.Error($"Unknown action: '{action}' for read_console. Valid actions are 'get', 'get_full', or 'clear'.");
        }

        // --- Internal Helper Methods ---

        // 注意：原来的控制台操作实现已移动到 ConsoleController 中
    }
}

