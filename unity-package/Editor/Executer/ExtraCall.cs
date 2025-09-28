using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles single function calls from MCP server.
    /// Routes function calls to appropriate tool classes via reflection and name matching.
    /// </summary>
    public class ExtraCall : McpTool
    {
        public override string ToolName => "extra_call";

        /// <summary>
        /// Main handler for function calls (同步版本).
        /// </summary>
        public override void HandleCommand(JObject cmd, Action<object> callback)
        {
            try
            {
                string functionName = cmd["func"]?.ToString();
                string argsJson = cmd["args"]?.ToString() ?? "{}";

                if (string.IsNullOrWhiteSpace(functionName))
                {
                    callback(Response.Error("Required parameter 'func' is missing or empty."));
                    return;
                }

                ExecuteFunction(functionName, argsJson, callback);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[FunctionCall] Command execution failed: {e}");
                callback(Response.Error($"Internal error processing function call: {e.Message}"));
                return;
            }
        }

        /// <summary>
        /// Executes a specific function by routing to the appropriate method (同步版本).
        /// </summary>
        private void ExecuteFunction(string functionName, string argsJson, Action<object> callback)
        {
            if (McpConnect.EnableLog)
                Debug.Log($"[FunctionCall] Executing function: {functionName}->{argsJson}");
            try
            {
                // 确保方法已注册
                MethodsCall.EnsureMethodsRegisteredStatic();

                // 解析参数
                JObject args = JObject.Parse(argsJson);

                // 查找对应的工具方法
                var method = MethodsCall.GetRegisteredMethod(functionName);
                if (method == null)
                {
                    callback(Response.Error($"Unknown method: '{functionName}'. Available methods: {string.Join(", ", MethodsCall.GetRegisteredMethodNames())}"));
                    return;
                }

                // 调用工具的ExecuteMethod方法
                var state = new StateTreeContext(args, new System.Collections.Generic.Dictionary<string, object>());
                method.ExecuteMethod(state);
                state.RegistComplete(callback);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[FunctionCall] Failed to execute function '{functionName}': {e}");
                callback(Response.Error($"Error executing function '{functionName}->{argsJson}': {e.Message}"));
            }
        }






    }
}