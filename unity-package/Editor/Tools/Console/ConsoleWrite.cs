using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles writing different types of log messages to Unity Editor console.
    /// Supports Error, Warning, Log, Assert, and Exception message types.
    /// 对应方法名: console_write
    /// </summary>
    [ToolName("console_write", "开发工具")]
    public class ConsoleWrite : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：error, warning, log, assert, exception", false),
                new MethodKey("message", "要写入的日志消息内容", false),
                new MethodKey("tag", "日志标签，用于分类和过滤，可选", true),
                new MethodKey("context", "上下文对象名称，用于在控制台中定位相关GameObject，可选", true),
                new MethodKey("condition", "断言条件表达式（仅用于assert类型），可选", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("error", HandleWriteError)
                    .Leaf("warning", HandleWriteWarning)
                    .Leaf("log", HandleWriteLog)
                    .Leaf("assert", HandleWriteAssert)
                    .Leaf("exception", HandleWriteException)
                    .DefaultLeaf(HandleUnknownAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理写入错误日志的操作
        /// </summary>
        private object HandleWriteError(JObject args)
        {
            try
            {
                string message = ExtractMessage(args);
                string tag = ExtractTag(args);
                UnityEngine.Object context = ExtractContext(args);

                string formattedMessage = FormatMessage(message, tag, "ERROR");

                LogInfo($"[ConsoleWrite] Writing error log: {formattedMessage}");

                if (context != null)
                {
                    Debug.LogError(formattedMessage, context);
                }
                else
                {
                    Debug.LogError(formattedMessage);
                }

                return Response.Success($"Error log written successfully: {formattedMessage}");
            }
            catch (Exception e)
            {
                LogError($"[ConsoleWrite] Failed to write error log: {e.Message}");
                return Response.Error($"Failed to write error log: {e.Message}");
            }
        }

        /// <summary>
        /// 处理写入警告日志的操作
        /// </summary>
        private object HandleWriteWarning(JObject args)
        {
            try
            {
                string message = ExtractMessage(args);
                string tag = ExtractTag(args);
                UnityEngine.Object context = ExtractContext(args);

                string formattedMessage = FormatMessage(message, tag, "WARNING");

                LogInfo($"[ConsoleWrite] Writing warning log: {formattedMessage}");

                if (context != null)
                {
                    Debug.LogWarning(formattedMessage, context);
                }
                else
                {
                    Debug.LogWarning(formattedMessage);
                }

                return Response.Success($"Warning log written successfully: {formattedMessage}");
            }
            catch (Exception e)
            {
                LogError($"[ConsoleWrite] Failed to write warning log: {e.Message}");
                return Response.Error($"Failed to write warning log: {e.Message}");
            }
        }

        /// <summary>
        /// 处理写入普通日志的操作
        /// </summary>
        private object HandleWriteLog(JObject args)
        {
            try
            {
                string message = ExtractMessage(args);
                string tag = ExtractTag(args);
                UnityEngine.Object context = ExtractContext(args);

                string formattedMessage = FormatMessage(message, tag, "LOG");

                LogInfo($"[ConsoleWrite] Writing log: {formattedMessage}");

                if (context != null)
                {
                    Debug.Log(formattedMessage, context);
                }
                else
                {
                    Debug.Log(formattedMessage);
                }

                return Response.Success($"Log written successfully: {formattedMessage}");
            }
            catch (Exception e)
            {
                LogError($"[ConsoleWrite] Failed to write log: {e.Message}");
                return Response.Error($"Failed to write log: {e.Message}");
            }
        }

        /// <summary>
        /// 处理写入断言日志的操作
        /// </summary>
        private object HandleWriteAssert(JObject args)
        {
            try
            {
                string message = ExtractMessage(args);
                string tag = ExtractTag(args);
                string condition = ExtractCondition(args);
                UnityEngine.Object context = ExtractContext(args);

                string formattedMessage = FormatAssertMessage(message, tag, condition);

                LogInfo($"[ConsoleWrite] Writing assert log: {formattedMessage}");

                if (context != null)
                {
                    Debug.LogAssertion(formattedMessage, context);
                }
                else
                {
                    Debug.LogAssertion(formattedMessage);
                }

                return Response.Success($"Assertion log written successfully: {formattedMessage}");
            }
            catch (Exception e)
            {
                LogError($"[ConsoleWrite] Failed to write assertion log: {e.Message}");
                return Response.Error($"Failed to write assertion log: {e.Message}");
            }
        }

        /// <summary>
        /// 处理写入异常日志的操作
        /// </summary>
        private object HandleWriteException(JObject args)
        {
            try
            {
                string message = ExtractMessage(args);
                string tag = ExtractTag(args);
                UnityEngine.Object context = ExtractContext(args);

                string formattedMessage = FormatMessage(message, tag, "EXCEPTION");

                LogInfo($"[ConsoleWrite] Writing exception log: {formattedMessage}");

                // 创建一个简单的异常对象用于日志
                var exception = new System.Exception(formattedMessage);

                if (context != null)
                {
                    Debug.LogException(exception, context);
                }
                else
                {
                    Debug.LogException(exception);
                }

                return Response.Success($"Exception log written successfully: {formattedMessage}");
            }
            catch (Exception e)
            {
                LogError($"[ConsoleWrite] Failed to write exception log: {e.Message}");
                return Response.Error($"Failed to write exception log: {e.Message}");
            }
        }

        /// <summary>
        /// 处理未知操作的回调方法
        /// </summary>
        private object HandleUnknownAction(JObject args)
        {
            string action = args["action"]?.ToString() ?? "null";
            return Response.Error($"Unknown action: '{action}' for console_write. Valid actions are 'error', 'warning', 'log', 'assert', or 'exception'.");
        }

        // --- Parameter Extraction Helper Methods ---

        /// <summary>
        /// 提取消息参数
        /// </summary>
        private string ExtractMessage(JObject args)
        {
            string message = args["message"]?.ToString();
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("Message parameter is required and cannot be empty.");
            }
            return message;
        }

        /// <summary>
        /// 提取标签参数
        /// </summary>
        private string ExtractTag(JObject args)
        {
            return args["tag"]?.ToString();
        }

        /// <summary>
        /// 提取条件参数（用于断言）
        /// </summary>
        private string ExtractCondition(JObject args)
        {
            return args["condition"]?.ToString();
        }

        /// <summary>
        /// 提取上下文对象
        /// </summary>
        private UnityEngine.Object ExtractContext(JObject args)
        {
            string contextName = args["context"]?.ToString();
            if (string.IsNullOrEmpty(contextName))
            {
                return null;
            }

            try
            {
                // 尝试找到GameObject
                GameObject go = GameObject.Find(contextName);
                if (go != null)
                {
                    return go;
                }

                // 尝试通过Resources加载资源
                UnityEngine.Object asset = Resources.Load(contextName);
                if (asset != null)
                {
                    return asset;
                }

                LogInfo($"[ConsoleWrite] Context object '{contextName}' not found, using null context");
                return null;
            }
            catch (Exception e)
            {
                LogInfo($"[ConsoleWrite] Failed to find context object '{contextName}': {e.Message}");
                return null;
            }
        }

        // --- Message Formatting Helper Methods ---

        /// <summary>
        /// 格式化日志消息
        /// </summary>
        private string FormatMessage(string message, string tag, string logType)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                return $"[{tag}][{logType}] {message}";
            }
            return $"[MCP][{logType}] {message}";
        }

        /// <summary>
        /// 格式化断言日志消息
        /// </summary>
        private string FormatAssertMessage(string message, string tag, string condition)
        {
            string baseMessage = FormatMessage(message, tag, "ASSERT");

            if (!string.IsNullOrEmpty(condition))
            {
                return $"{baseMessage} | Condition: {condition}";
            }

            return baseMessage;
        }
    }
}
