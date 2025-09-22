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
    public class FunctionsCall : McpTool
    {
        public override string ToolName => "functions_call";

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
            var errors = new List<string>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;

            try
            {
                // 确保方法已注册
                FunctionCall.EnsureMethodsRegisteredStatic();

                // 如果没有函数调用，直接返回
                if (totalCalls == 0)
                {
                    var emptyResponse = CreateBatchResponse(true, results, errors, totalCalls, successfulCalls, failedCalls);
                    callback(emptyResponse);
                    return;
                }

                // 初始化结果和错误列表
                for (int i = 0; i < totalCalls; i++)
                {
                    results.Add(null);
                    errors.Add(null);
                }

                // 开始异步顺序执行
                ExecuteFunctionAtIndex(funcsArray, 0, results, errors, totalCalls, callback);
            }
            catch (Exception e)
            {
                callback(CreateBatchResponse(false, results, errors, totalCalls, 0, 1,
                    $"批量调用初始化过程中发生未预期错误: {e.Message}"));
                return;
            }
        }

        /// <summary>
        /// 异步顺序执行指定索引的函数，完成后递归执行下一个.
        /// </summary>
        private void ExecuteFunctionAtIndex(JArray funcsArray, int currentIndex, List<object> results, List<string> errors,
            int totalCalls, Action<object> finalCallback)
        {
            // 如果所有函数都执行完毕，返回最终结果
            if (currentIndex >= totalCalls)
            {
                int successfulCalls = 0;
                int failedCalls = 0;

                // 统计成功和失败的调用数
                for (int i = 0; i < totalCalls; i++)
                {
                    if (errors[i] == null)
                        successfulCalls++;
                    else
                        failedCalls++;
                }

                var finalResponse = CreateBatchResponse(failedCalls == 0, results, errors, totalCalls, successfulCalls, failedCalls);
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
                    errors[currentIndex] = errorMsg;
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, errors, totalCalls, finalCallback);
                    return;
                }

                // 提取func和args字段
                string funcName = funcCall["func"]?.ToString();
                var argsToken = funcCall["args"];

                // 验证func字段
                if (string.IsNullOrWhiteSpace(funcName))
                {
                    string errorMsg = $"第{currentIndex + 1}个函数调用的func字段无效或为空";
                    errors[currentIndex] = errorMsg;
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, errors, totalCalls, finalCallback);
                    return;
                }

                // 验证args字段（应该是对象）
                if (!(argsToken is JObject args))
                {
                    string errorMsg = $"第{currentIndex + 1}个函数调用的args字段必须是对象类型";
                    errors[currentIndex] = errorMsg;
                    results[currentIndex] = null;

                    if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, errors, totalCalls, finalCallback);
                    return;
                }

                // 异步执行单个函数调用
                ExecuteSingleFunctionAsync(funcName, args, (singleResult, singleError) =>
                {
                    // 保存当前函数的执行结果
                    results[currentIndex] = singleResult;
                    errors[currentIndex] = singleError;

                    if (McpConnect.EnableLog)
                    {
                        if (singleError == null)
                            Debug.Log($"[FunctionsCall] Function {currentIndex + 1}/{totalCalls} ({funcName}) executed successfully");
                        else
                            Debug.LogError($"[FunctionsCall] Function {currentIndex + 1}/{totalCalls} ({funcName}) failed: {singleError}");
                    }

                    // 继续执行下一个函数
                    ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, errors, totalCalls, finalCallback);
                });
            }
            catch (Exception e)
            {
                string errorMsg = $"第{currentIndex + 1}个函数调用失败: {e.Message}";
                errors[currentIndex] = errorMsg;
                results[currentIndex] = null;

                if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] {errorMsg}");

                // 继续执行下一个函数
                ExecuteFunctionAtIndex(funcsArray, currentIndex + 1, results, errors, totalCalls, finalCallback);
            }
        }

        /// <summary>
        /// Executes a single function asynchronously with callback.
        /// </summary>
        private void ExecuteSingleFunctionAsync(string functionName, JObject args, Action<object, string> callback)
        {
            try
            {
                // 查找对应的工具方法
                var method = FunctionCall.GetRegisteredMethod(functionName);
                if (method == null)
                {
                    var availableMethods = string.Join(", ", FunctionCall.GetRegisteredMethodNames());
                    string errorMsg = $"Unknown method: '{functionName}'. Available methods: {availableMethods}";
                    callback(null, errorMsg);
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
                        // 成功执行，调用回调并传递结果和null错误
                        callback(result, null);
                    }
                    catch (Exception e)
                    {
                        // 执行过程中出现异常
                        if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] Exception in result callback for '{functionName}': {e}");
                        callback(null, $"Result processing error: {e.Message}");
                    }
                });
            }
            catch (Exception e)
            {
                // 方法查找或执行设置过程中的异常
                if (McpConnect.EnableLog) Debug.LogError($"[FunctionsCall] Exception setting up execution for '{functionName}': {e}");
                callback(null, $"Execution setup error: {e.Message}");
            }
        }

        /// <summary>
        /// Creates the batch response in the format expected by the Python layer.
        /// </summary>
        private object CreateBatchResponse(bool success, List<object> results, List<string> errors,
            int totalCalls, int successfulCalls, int failedCalls, string globalError = null)
        {
            var responseData = new Dictionary<string, object>
            {
                ["success"] = success,
                ["results"] = results ?? new List<object>(),
                ["errors"] = errors ?? new List<string>(),
                ["total_calls"] = totalCalls,
                ["successful_calls"] = successfulCalls,
                ["failed_calls"] = failedCalls
            };

            if (!string.IsNullOrEmpty(globalError))
            {
                // 如果有全局错误，将其添加到errors列表的开头
                var errorList = new List<string> { globalError };
                if (errors != null) errorList.AddRange(errors);
                responseData["errors"] = errorList;
            }

            // 返回包含data字段的Response格式，以便Python层可以提取data部分
            return Response.Success("Batch function calls completed", responseData);
        }
    }
}
