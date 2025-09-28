using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Models;
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles batch function calls from MCP server.
    /// Executes multiple function calls sequentially and collects all results.
    /// </summary>
    public class ExtraBatchCalls : McpTool
    {
        public override string ToolName => "extra_batch_calls";

        /// <summary>
        /// Main handler for batch function calls.
        /// </summary>
        public override void HandleCommand(JObject cmd, Action<object> callback)
        {
            try
            {
                var funcsArray = cmd["args"] as JArray;

                if (funcsArray == null)
                {
                    callback(Response.Error("Required parameter 'funcs' is missing or not an array."));
                    return;
                }

                ExecuteFunctions(funcsArray, callback);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] Command execution failed: {e}");
                callback(Response.Error($"Internal error processing batch function calls: {e.Message}"));
                return;
            }
        }

        /// <summary>
        /// Executes multiple functions sequentially and collects results (异步版本).
        /// </summary>
        private void ExecuteFunctions(JArray funcsArray, Action<object> callback)
        {
            if (McpConnect.EnableLog)
                Debug.Log($"[FunctionsCall] Executing {funcsArray.Count} function calls asynchronously");

            var results = new List<object>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;

            try
            {
                // 确保方法已注册
                MethodsCall.EnsureMethodsRegisteredStatic();

                // 如果没有函数调用，直接返回
                if (totalCalls == 0)
                {
                    var emptyResponse = CreateBatchResponse(true, results, totalCalls, successfulCalls, failedCalls);
                    callback(emptyResponse);
                    return;
                }

                // 初始化结果列表
                for (int i = 0; i < totalCalls; i++)
                {
                    results.Add(null);
                }

                // 开始异步顺序执行
                ExecuteFunctionAtIndex(funcsArray, 0, results, totalCalls, callback);
            }
            catch (Exception e)
            {
                callback(CreateBatchResponse(false, results, totalCalls, 0, 1,
                    $"批量调用初始化过程中发生未预期错误: {e.Message}"));
                return;
            }
        }

        /// <summary>
        /// 异步顺序执行指定索引的函数，完成后递归执行下一个.
        /// </summary>
        private void ExecuteFunctionAtIndex(JArray funcsArray, int currentIndex, List<object> results,
            int totalCalls, Action<object> finalCallback)
        {
            // 如果所有函数都执行完毕，返回最终结果
            if (currentIndex >= totalCalls)
            {
                int successfulCalls = totalCalls;
                int failedCalls = 0;

                var finalResponse = CreateBatchResponse(true, results, totalCalls, successfulCalls, failedCalls);
                finalCallback(finalResponse);

                if (McpConnect.EnableLog)
                    Debug.Log($"[FunctionsCall] Batch execution completed: {successfulCalls}/{totalCalls} successful");
                return;
            }

            try
            {
                var funcCallToken = funcsArray[currentIndex];

                // 验证函数调用对象格式
                if (!(funcCallToken is JObject funcCall))
                {
                    string errorMsg = $"第{currentIndex + 1}个函数调用必须是对象类型";
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, totalCalls, finalCallback);
                    return;
                }

                // 提取func和args字段
                string funcName = funcCall["func"]?.ToString();
                var argsToken = funcCall["args"];

                // 验证func字段
                if (string.IsNullOrWhiteSpace(funcName))
                {
                    string errorMsg = $"第{currentIndex + 1}个函数调用的func字段无效或为空";
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, totalCalls, finalCallback);
                    return;
                }

                // 验证args字段（应该是对象）
                if (!(argsToken is JObject args))
                {
                    string errorMsg = $"第{currentIndex + 1}个函数调用的args字段必须是对象类型";
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, totalCalls, finalCallback);
                    return;
                }

                // 异步执行单个函数调用
                ExecuteSingleFunctionAsync(funcName, args, (singleResult) =>
                {
                    // 保存当前函数的执行结果
                    results[currentIndex] = singleResult;

                    if (McpConnect.EnableLog)
                    {
                        Debug.Log($"[FunctionsCall] Function {currentIndex + 1}/{totalCalls} ({funcName}) executed");
                    }

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, totalCalls, finalCallback);
                });
            }
            catch (Exception e)
            {
                string errorMsg = $"第{currentIndex + 1}个函数调用失败: {e.Message}";
                results[currentIndex] = null;

                if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                // 继续执行下一个函数
                ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, totalCalls, finalCallback);
            }
        }

        /// <summary>
        /// Executes a single function asynchronously with callback.
        /// </summary>
        private void ExecuteSingleFunctionAsync(string functionName, JObject args, Action<object> callback)
        {
            try
            {
                // 查找对应的工具方法
                var method = MethodsCall.GetRegisteredMethod(functionName);
                if (method == null)
                {
                    var availableMethods = string.Join(", ", MethodsCall.GetRegisteredMethodNames());
                    if (McpConnect.EnableLog) Debug.LogWarning($"Unknown method: '{functionName}'. Available methods: {availableMethods}");
                    callback(null);
                    return;
                }

                // 创建执行上下文
                var state = new StateTreeContext(args, new Dictionary<string, object>());

                // 异步执行方法
                method.ExecuteMethod(state);
                state.RegistComplete((result) =>
                {
                    try
                    {
                        // 成功执行，调用回调并传递结果
                        callback(result);
                    }
                    catch (Exception e)
                    {
                        // 执行过程中出现异常
                        if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] Exception in result callback for '{functionName}': {e}");
                        callback(null);
                    }
                });
            }
            catch (Exception e)
            {
                // 方法查找或执行设置过程中的异常
                if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] Exception setting up execution for '{functionName}': {e}");
                callback(null);
            }
        }

        /// <summary>
        /// Creates the batch response in the format expected by the Python layer.
        /// </summary>
        private object CreateBatchResponse(bool success, List<object> results,
            int totalCalls, int successfulCalls, int failedCalls, string globalError = null)
        {
            var responseData = new Dictionary<string, object>
            {
                ["success"] = success,
                ["results"] = JArray.FromObject(results) ?? new JArray(),
                ["total_calls"] = totalCalls,
                ["successful_calls"] = successfulCalls,
                ["failed_calls"] = failedCalls
            };

            if (!string.IsNullOrEmpty(globalError))
            {
                responseData["error"] = globalError;
            }

            // 返回包含data字段的Response格式，以便Python层可以提取data部分
            return Response.Success("Batch function calls completed", JObject.FromObject(responseData));
        }
    }
}
