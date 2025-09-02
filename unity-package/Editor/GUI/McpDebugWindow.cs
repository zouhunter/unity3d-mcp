using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// MCP调试客户端窗口 - 用于测试和调试MCP函数调用
    /// </summary>
    public class McpDebugWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            McpDebugWindow window = GetWindow<McpDebugWindow>("MCP Debug Client");
            window.minSize = new Vector2(500, 300);
            window.Show();
        }

        /// <summary>
        /// 打开调试窗口并预填充指定的JSON内容
        /// </summary>
        /// <param name="jsonContent">要预填充的JSON内容</param>
        public static void ShowWindowWithContent(string jsonContent)
        {
            McpDebugWindow window = GetWindow<McpDebugWindow>("MCP Debug Client");
            window.minSize = new Vector2(500, 300);
            window.SetInputJson(jsonContent);
            window.Show();
            window.Focus();
        }

        /// <summary>
        /// 设置输入框的JSON内容
        /// </summary>
        /// <param name="jsonContent">JSON内容</param>
        public void SetInputJson(string jsonContent)
        {
            if (!string.IsNullOrEmpty(jsonContent))
            {
                inputJson = jsonContent;
                ClearResults(); // 清空之前的结果
                Repaint(); // 刷新界面
            }
        }

        // UI状态变量
        private Vector2 inputScrollPosition;
        private Vector2 resultScrollPosition;
        private string inputJson = "{\n  \"func\": \"hierarchy_create\",\n  \"args\": {\n    \"from\": \"primitive\",\n    \"primitive_type\": \"Cube\",\n    \"name\": \"RedCube\",\n    \"position\": [\n      0,\n      0,\n      0\n    ]\n  }\n}";
        private string resultText = "";
        private bool showResult = false;
        private bool isExecuting = false;

        private object currentResult = null; // 存储当前执行结果

        // 布局参数
        private const float MinInputHeight = 100f;
        private const float MaxInputHeight = 300f;
        private const float LineHeight = 16f;
        private const float ResultAreaHeight = 200f;

        // 样式
        private GUIStyle headerStyle;
        private GUIStyle codeStyle;

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
                };
            }

            if (codeStyle == null)
            {
                codeStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = false,
                    fontSize = 12,
                    fontStyle = FontStyle.Normal
                };
            }
        }

        /// <summary>
        /// 根据文本内容动态计算输入框高度
        /// </summary>
        private float CalculateInputHeight()
        {
            if (string.IsNullOrEmpty(inputJson))
                return MinInputHeight;

            // 计算行数
            int lineCount = inputJson.Split('\n').Length;

            // 根据行数计算高度，加上一些padding
            float calculatedHeight = lineCount * LineHeight + 20f; // 20f为padding

            // 限制在最小和最大高度之间
            return Mathf.Clamp(calculatedHeight, MinInputHeight, MaxInputHeight);
        }

        private void OnGUI()
        {
            InitializeStyles();

            // 标题区域（不滚动）
            GUILayout.Label("Unity MCP Debug Client", headerStyle);
            GUILayout.Space(10);

            // 说明文字
            EditorGUILayout.HelpBox(
                "输入单个函数调用:\n{\"func\": \"function_name\", \"args\": {...}}\n\n" +
                "或批量调用:\n{\"funcs\": [{\"func\": \"...\", \"args\": {...}}, ...]}",
                MessageType.Info);

            GUILayout.Space(5);

            // JSON输入框区域（带滚动）
            DrawInputArea();

            GUILayout.Space(10);

            // 操作按钮区域（不滚动）
            DrawControlButtons();

            GUILayout.Space(10);

            // 结果显示区域（带滚动）
            if (showResult)
            {
                DrawResultArea();
            }
        }

        /// <summary>
        /// 绘制输入区域（带滚动和动态高度）
        /// </summary>
        private void DrawInputArea()
        {
            GUILayout.Label("MCP调用 (JSON格式):");

            float inputHeight = CalculateInputHeight();

            // 创建输入框的滚动区域，使用窗口宽度
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            inputScrollPosition = EditorGUILayout.BeginScrollView(
                inputScrollPosition,
                GUILayout.Height(inputHeight),
                GUILayout.ExpandWidth(true)
            );

            // 输入框，使用窗口宽度
            inputJson = EditorGUILayout.TextArea(
                inputJson,
                codeStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            );

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            // 显示行数信息
            int lineCount = inputJson?.Split('\n').Length ?? 0;
            GUILayout.Label($"行数: {lineCount} | 高度: {inputHeight:F0}px", EditorStyles.miniLabel);
        }

        /// <summary>
        /// 绘制控制按钮区域
        /// </summary>
        private void DrawControlButtons()
        {
            // 获取剪贴板可用性
            bool clipboardAvailable = IsClipboardAvailable();

            // 第一行按钮
            GUILayout.BeginHorizontal();

            GUI.enabled = !isExecuting;
            if (GUILayout.Button("执行", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ExecuteCall();
            }

            GUI.enabled = !isExecuting && clipboardAvailable;
            if (GUILayout.Button("执行剪贴板", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ExecuteClipboard();
            }
            GUI.enabled = true;

            if (GUILayout.Button("格式化JSON", GUILayout.Height(30), GUILayout.Width(120)))
            {
                FormatJson();
            }

            if (GUILayout.Button("清空", GUILayout.Height(30), GUILayout.Width(60)))
            {
                inputJson = "{}";
                ClearResults();
            }

            if (isExecuting)
            {
                GUILayout.Label("执行中...", GUILayout.Width(100));
            }

            GUILayout.EndHorizontal();

            // 第二行按钮（剪贴板操作）
            GUILayout.BeginHorizontal();

            // 剪贴板操作按钮 - 根据剪贴板内容动态启用/禁用
            GUI.enabled = clipboardAvailable;
            if (GUILayout.Button("粘贴到输入框", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PasteFromClipboard();
            }

            if (GUILayout.Button("预览剪贴板", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PreviewClipboard();
            }
            GUI.enabled = true;

            // 显示剪贴板状态 - 带颜色指示
            DrawClipboardStatus();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制结果显示区域（带滚动）
        /// </summary>
        private void DrawResultArea()
        {
            EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);

            // 创建结果显示的滚动区域，使用窗口宽度
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            resultScrollPosition = EditorGUILayout.BeginScrollView(
                resultScrollPosition,
                GUILayout.Height(ResultAreaHeight),
                GUILayout.ExpandWidth(true)
            );

            // 结果文本区域，使用窗口宽度
            EditorGUILayout.TextArea(
                resultText,
                codeStyle,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            );

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            // 结果操作按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复制结果", GUILayout.Width(80)))
            {
                EditorGUIUtility.systemCopyBuffer = resultText;
                EditorUtility.DisplayDialog("已复制", "结果已复制到剪贴板", "确定");
            }

            if (GUILayout.Button("清空结果", GUILayout.Width(80)))
            {
                ClearResults();
            }

            // 检查是否为批量结果，如果是则显示额外操作
            if (IsBatchResultDisplayed())
            {
                if (GUILayout.Button("复制统计", GUILayout.Width(80)))
                {
                    CopyBatchStatistics();
                }

                if (GUILayout.Button("仅显示错误", GUILayout.Width(80)))
                {
                    ShowOnlyErrors();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void FormatJson()
        {
            try
            {
                JObject jsonObj = JObject.Parse(inputJson);
                inputJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("JSON格式错误", $"无法解析JSON: {e.Message}", "确定");
            }
        }

        private void ExecuteCall()
        {
            if (string.IsNullOrWhiteSpace(inputJson))
            {
                EditorUtility.DisplayDialog("错误", "请输入JSON内容", "确定");
                return;
            }

            isExecuting = true;
            showResult = true;
            resultText = "正在执行...";

            try
            {
                DateTime startTime = DateTime.Now;
                object result = ExecuteJsonCall(startTime);

                // 如果结果为null，表示异步执行
                if (result == null)
                {
                    resultText = "异步执行中...";
                    // 刷新界面显示异步状态
                    Repaint();
                    // 注意：isExecuting保持为true，等待异步回调完成
                }
                else
                {
                    // 同步执行完成
                    DateTime endTime = DateTime.Now;
                    TimeSpan duration = endTime - startTime;
                    CompleteExecution(result, duration);
                }
            }
            catch (Exception e)
            {
                string errorResult = $"执行错误:\n{e.Message}\n\n堆栈跟踪:\n{e.StackTrace}";
                resultText = errorResult;
                isExecuting = false;

                Debug.LogError($"[McpDebugWindow] 执行调用时发生错误: {e}");
            }
        }

        /// <summary>
        /// 完成执行并更新UI显示
        /// </summary>
        private void CompleteExecution(object result, TimeSpan duration)
        {
            try
            {
                // 存储当前结果并格式化
                currentResult = result;
                string formattedResult = FormatResult(result, duration);
                resultText = formattedResult;

                // 刷新界面
                Repaint();
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// 执行批量函数调用，支持异步回调
        /// </summary>
        private object ExecuteBatchCalls(JArray funcsArray, DateTime startTime)
        {
            var results = new List<object>(new object[funcsArray.Count]); // 预分配容量防止越界
            var errors = new List<string>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;
            int completedCalls = 0;
            var lockObject = new object(); // 专用锁对象
            bool hasAsyncCalls = false;

            for (int i = 0; i < funcsArray.Count; i++)
            {
                try
                {
                    var funcCall = funcsArray[i] as JObject;
                    if (funcCall == null)
                    {
                        errors.Add($"第{i + 1}个函数调用格式错误: 不是有效的JSON对象");
                        failedCalls++;
                        results[i] = null; // 使用索引而非Add
                        completedCalls++;
                        continue;
                    }

                    var functionCall = new FunctionCall();
                    object callResult = null;
                    bool callbackExecuted = false;
                    int callIndex = i; // 捕获当前索引

                    functionCall.HandleCommand(funcCall, (result) =>
                    {
                        callResult = result;
                        callbackExecuted = true;

                        // 更新结果
                        lock (lockObject) // 线程安全
                        {
                            // 安全设置结果，防止越界
                            if (callIndex >= 0 && callIndex < results.Count)
                            {
                                results[callIndex] = result;
                            }

                            if (result != null && !IsErrorResponse(result))
                            {
                                successfulCalls++;
                            }
                            else
                            {
                                failedCalls++;
                                if (result != null)
                                {
                                    errors.Add($"第{callIndex + 1}个调用: {ExtractErrorMessage(result)}");
                                }
                            }

                            completedCalls++;

                            // 检查是否所有调用都完成了
                            if (completedCalls == totalCalls && isExecuting)
                            {
                                // 生成最终结果
                                var finalResult = new
                                {
                                    success = failedCalls == 0,
                                    results = results,
                                    errors = errors,
                                    total_calls = totalCalls,
                                    successful_calls = successfulCalls,
                                    failed_calls = failedCalls
                                };

                                DateTime endTime = DateTime.Now;
                                TimeSpan duration = endTime - startTime;
                                CompleteExecution(finalResult, duration);
                            }
                        }
                    });

                    // 设置结果位置
                    results[i] = callResult;

                    if (callbackExecuted)
                    {
                        // 同步执行
                        if (callResult != null && !IsErrorResponse(callResult))
                        {
                            successfulCalls++;
                        }
                        else
                        {
                            failedCalls++;
                            if (callResult != null)
                            {
                                errors.Add($"第{i + 1}个调用: {ExtractErrorMessage(callResult)}");
                            }
                        }
                        completedCalls++;
                    }
                    else
                    {
                        hasAsyncCalls = true;
                    }
                }
                catch (Exception e)
                {
                    string error = $"第{i + 1}个函数调用失败: {e.Message}";
                    errors.Add(error);
                    results[i] = null; // 使用索引而非Add
                    failedCalls++;
                    completedCalls++;
                }
            }

            // 如果所有调用都是同步的，直接返回结果
            if (!hasAsyncCalls)
            {
                return new
                {
                    success = failedCalls == 0,
                    results = results,
                    errors = errors,
                    total_calls = totalCalls,
                    successful_calls = successfulCalls,
                    failed_calls = failedCalls
                };
            }

            // 有异步调用，返回null等待回调完成
            return null;
        }

        private object ExecuteJsonCall(DateTime startTime)
        {
            JObject inputObj = JObject.Parse(inputJson);

            // 检查是否为批量调用
            if (inputObj.ContainsKey("funcs"))
            {
                // 批量调用 - 循环调用FunctionCall
                var funcsArray = inputObj["funcs"] as JArray;
                if (funcsArray == null)
                {
                    throw new ArgumentException("'funcs' 字段必须是一个数组");
                }

                return ExecuteBatchCalls(funcsArray, startTime);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                object callResult = null;
                bool callbackExecuted = false;

                functionCall.HandleCommand(inputObj, (result) =>
                {
                    callResult = result;
                    callbackExecuted = true;

                    // 如果是异步回调，更新UI
                    if (isExecuting)
                    {
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;
                        CompleteExecution(result, duration);
                    }
                });

                // 如果回调立即执行，返回结果；否则返回null表示异步执行
                return callbackExecuted ? callResult : null;
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }





        private string FormatResult(object result, TimeSpan duration)
        {
            string formattedResult = $"执行时间: {duration.TotalMilliseconds:F2}ms\n\n";

            if (result != null)
            {
                // 检查是否为批量调用结果
                if (IsBatchCallResult(result))
                {
                    formattedResult += FormatBatchCallResult(result);
                }
                else
                {
                    // 单个结果处理
                    if (result.GetType().Name == "Response" || result.ToString().Contains("\"success\""))
                    {
                        try
                        {
                            string jsonResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                            formattedResult += jsonResult;
                        }
                        catch
                        {
                            formattedResult += result.ToString();
                        }
                    }
                    else
                    {
                        formattedResult += result.ToString();
                    }
                }
            }
            else
            {
                formattedResult += "null";
            }

            return formattedResult;
        }

        /// <summary>
        /// 检查是否为批量调用结果
        /// </summary>
        private bool IsBatchCallResult(object result)
        {
            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                return resultObj.ContainsKey("results") &&
                       resultObj.ContainsKey("total_calls") &&
                       resultObj.ContainsKey("successful_calls") &&
                       resultObj.ContainsKey("failed_calls");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化批量调用结果，分条显示
        /// </summary>
        private string FormatBatchCallResult(object result)
        {
            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                var results = resultObj["results"] as JArray;
                var errors = resultObj["errors"] as JArray;
                var totalCalls = resultObj["total_calls"]?.Value<int>() ?? 0;
                var successfulCalls = resultObj["successful_calls"]?.Value<int>() ?? 0;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;
                var overallSuccess = resultObj["success"]?.Value<bool>() ?? false;

                var output = new StringBuilder();

                // 显示总体统计
                output.AppendLine("=== 批量调用执行结果 ===");
                output.AppendLine($"总调用数: {totalCalls}");
                output.AppendLine($"成功: {successfulCalls}");
                output.AppendLine($"失败: {failedCalls}");
                output.AppendLine($"整体状态: {(overallSuccess ? "成功" : "部分失败")}");
                output.AppendLine();

                // 分条显示每个结果
                if (results != null)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        output.AppendLine($"--- 调用 #{i + 1} ---");

                        var singleResult = results[i];
                        if (singleResult != null && !singleResult.Type.Equals(JTokenType.Null))
                        {
                            output.AppendLine("✅ 成功");
                            try
                            {
                                string formattedSingleResult = JsonConvert.SerializeObject(singleResult, Formatting.Indented);
                                output.AppendLine(formattedSingleResult);
                            }
                            catch
                            {
                                output.AppendLine(singleResult.ToString());
                            }
                        }
                        else
                        {
                            output.AppendLine("❌ 失败");
                            if (errors != null && i < errors.Count)
                            {
                                output.AppendLine($"错误信息: {errors[i]}");
                            }
                        }
                        output.AppendLine();
                    }
                }

                // 显示所有错误汇总
                if (errors != null && errors.Count > 0)
                {
                    output.AppendLine("=== 错误汇总 ===");
                    for (int i = 0; i < errors.Count; i++)
                    {
                        if (errors[i] != null && !string.IsNullOrEmpty(errors[i].ToString()))
                        {
                            output.AppendLine($"{i + 1}. {errors[i]}");
                        }
                    }
                }

                return output.ToString();
            }
            catch (Exception e)
            {
                return $"批量结果格式化失败: {e.Message}\n\n原始结果:\n{JsonConvert.SerializeObject(result, Formatting.Indented)}";
            }
        }

        /// <summary>
        /// 清理所有结果
        /// </summary>
        private void ClearResults()
        {
            resultText = "";
            showResult = false;
            currentResult = null;
        }

        /// <summary>
        /// 检查当前显示的是否为批量结果
        /// </summary>
        private bool IsBatchResultDisplayed()
        {
            return currentResult != null && IsBatchCallResult(currentResult);
        }

        /// <summary>
        /// 复制批量调用的统计信息
        /// </summary>
        private void CopyBatchStatistics()
        {
            if (currentResult == null || !IsBatchCallResult(currentResult))
                return;

            try
            {
                var resultJson = JsonConvert.SerializeObject(currentResult);
                var resultObj = JObject.Parse(resultJson);

                var totalCalls = resultObj["total_calls"]?.Value<int>() ?? 0;
                var successfulCalls = resultObj["successful_calls"]?.Value<int>() ?? 0;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;
                var overallSuccess = resultObj["success"]?.Value<bool>() ?? false;

                var statistics = $"批量调用统计:\n" +
                               $"总调用数: {totalCalls}\n" +
                               $"成功: {successfulCalls}\n" +
                               $"失败: {failedCalls}\n" +
                               $"整体状态: {(overallSuccess ? "成功" : "部分失败")}";

                EditorGUIUtility.systemCopyBuffer = statistics;
                EditorUtility.DisplayDialog("已复制", "统计信息已复制到剪贴板", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("复制失败", $"无法复制统计信息: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 仅显示错误信息
        /// </summary>
        private void ShowOnlyErrors()
        {
            if (currentResult == null || !IsBatchCallResult(currentResult))
                return;

            try
            {
                var resultJson = JsonConvert.SerializeObject(currentResult);
                var resultObj = JObject.Parse(resultJson);

                var errors = resultObj["errors"] as JArray;
                var failedCalls = resultObj["failed_calls"]?.Value<int>() ?? 0;

                var output = new StringBuilder();
                output.AppendLine("=== 错误信息汇总 ===");
                output.AppendLine($"失败调用数: {failedCalls}");
                output.AppendLine();

                if (errors != null && errors.Count > 0)
                {
                    for (int i = 0; i < errors.Count; i++)
                    {
                        if (errors[i] != null && !string.IsNullOrEmpty(errors[i].ToString()))
                        {
                            output.AppendLine($"错误 #{i + 1}:");
                            output.AppendLine($"  {errors[i]}");
                            output.AppendLine();
                        }
                    }
                }
                else
                {
                    output.AppendLine("没有发现错误信息。");
                }

                resultText = output.ToString();
            }
            catch (Exception e)
            {
                resultText = $"显示错误信息失败: {e.Message}";
            }
        }

        /// <summary>
        /// 执行剪贴板中的JSON内容
        /// </summary>
        private void ExecuteClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("错误", "剪贴板为空", "确定");
                    return;
                }

                // 验证JSON格式
                if (!ValidateClipboardJson(clipboardContent, out string errorMessage))
                {
                    EditorUtility.DisplayDialog("JSON格式错误", $"剪贴板内容不是有效的JSON:\n{errorMessage}", "确定");
                    return;
                }

                // 执行剪贴板内容
                isExecuting = true;
                showResult = true;
                resultText = "正在执行剪贴板内容...";

                try
                {
                    DateTime startTime = DateTime.Now;
                    object result = ExecuteJsonCallFromString(clipboardContent, startTime);

                    // 如果结果为null，表示异步执行
                    if (result == null)
                    {
                        resultText = "异步执行剪贴板内容中...";
                        // 刷新界面显示异步状态
                        Repaint();
                        // 注意：isExecuting保持为true，等待异步回调完成
                    }
                    else
                    {
                        // 同步执行完成
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;

                        // 存储当前结果并格式化
                        currentResult = result;
                        string formattedResult = FormatResult(result, duration);
                        resultText = $"📋 从剪贴板执行\n原始JSON:\n{clipboardContent}\n\n{formattedResult}";

                        // 刷新界面
                        Repaint();
                        isExecuting = false;
                    }
                }
                catch (Exception e)
                {
                    string errorResult = $"执行剪贴板内容错误:\n{e.Message}\n\n堆栈跟踪:\n{e.StackTrace}";
                    resultText = errorResult;

                    Debug.LogError($"[McpDebugWindow] 执行剪贴板内容时发生错误: {e}");
                }
                finally
                {
                    isExecuting = false;
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("执行失败", $"无法执行剪贴板内容: {e.Message}", "确定");
                isExecuting = false;
            }
        }

        /// <summary>
        /// 从字符串执行JSON调用，支持异步回调
        /// </summary>
        private object ExecuteJsonCallFromString(string jsonString, DateTime startTime)
        {
            JObject inputObj = JObject.Parse(jsonString);

            // 检查是否为批量调用
            if (inputObj.ContainsKey("funcs"))
            {
                // 批量调用 - 循环调用FunctionCall
                var funcsArray = inputObj["funcs"] as JArray;
                if (funcsArray == null)
                {
                    throw new ArgumentException("'funcs' 字段必须是一个数组");
                }

                return ExecuteBatchCallsForClipboard(funcsArray, startTime, jsonString);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                object callResult = null;
                bool callbackExecuted = false;

                functionCall.HandleCommand(inputObj, (result) =>
                {
                    callResult = result;
                    callbackExecuted = true;

                    // 如果是异步回调，更新UI（剪贴板格式）
                    if (isExecuting)
                    {
                        DateTime endTime = DateTime.Now;
                        TimeSpan duration = endTime - startTime;

                        // 存储当前结果并格式化
                        currentResult = result;
                        string formattedResult = FormatResult(result, duration);
                        resultText = $"📋 从剪贴板执行\n原始JSON:\n{jsonString}\n\n{formattedResult}";

                        // 刷新界面
                        Repaint();
                        isExecuting = false;
                    }
                });

                // 如果回调立即执行，返回结果；否则返回null表示异步执行
                return callbackExecuted ? callResult : null;
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }

        /// <summary>
        /// 执行剪贴板批量函数调用，支持异步回调
        /// </summary>
        private object ExecuteBatchCallsForClipboard(JArray funcsArray, DateTime startTime, string originalJson)
        {
            var results = new List<object>();
            var errors = new List<string>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;
            int completedCalls = 0;
            var lockObject = new object(); // 专用锁对象
            bool hasAsyncCalls = false;

            for (int i = 0; i < funcsArray.Count; i++)
            {
                try
                {
                    var funcCall = funcsArray[i] as JObject;
                    if (funcCall == null)
                    {
                        errors.Add($"第{i + 1}个函数调用格式错误: 不是有效的JSON对象");
                        failedCalls++;
                        results[i] = null; // 使用索引而非Add
                        completedCalls++;
                        continue;
                    }

                    var functionCall = new FunctionCall();
                    object callResult = null;
                    bool callbackExecuted = false;
                    int callIndex = i; // 捕获当前索引

                    functionCall.HandleCommand(funcCall, (result) =>
                    {
                        callResult = result;
                        callbackExecuted = true;

                        // 更新结果
                        lock (lockObject) // 线程安全
                        {
                            // 安全设置结果，防止越界
                            if (callIndex >= 0 && callIndex < results.Count)
                            {
                                results[callIndex] = result;
                            }

                            if (result != null && !IsErrorResponse(result))
                            {
                                successfulCalls++;
                            }
                            else
                            {
                                failedCalls++;
                                if (result != null)
                                {
                                    errors.Add($"第{callIndex + 1}个调用: {ExtractErrorMessage(result)}");
                                }
                            }

                            completedCalls++;

                            // 检查是否所有调用都完成了
                            if (completedCalls == totalCalls && isExecuting)
                            {
                                // 生成最终结果
                                var finalResult = new
                                {
                                    success = failedCalls == 0,
                                    results = results,
                                    errors = errors,
                                    total_calls = totalCalls,
                                    successful_calls = successfulCalls,
                                    failed_calls = failedCalls
                                };

                                DateTime endTime = DateTime.Now;
                                TimeSpan duration = endTime - startTime;

                                // 存储当前结果并格式化（剪贴板格式）
                                currentResult = finalResult;
                                string formattedResult = FormatResult(finalResult, duration);
                                resultText = $"📋 从剪贴板执行\n原始JSON:\n{originalJson}\n\n{formattedResult}";

                                // 刷新界面
                                Repaint();
                                isExecuting = false;
                            }
                        }
                    });

                    // 设置结果位置
                    results[i] = callResult;

                    if (callbackExecuted)
                    {
                        // 同步执行
                        if (callResult != null && !IsErrorResponse(callResult))
                        {
                            successfulCalls++;
                        }
                        else
                        {
                            failedCalls++;
                            if (callResult != null)
                            {
                                errors.Add($"第{i + 1}个调用: {ExtractErrorMessage(callResult)}");
                            }
                        }
                        completedCalls++;
                    }
                    else
                    {
                        hasAsyncCalls = true;
                    }
                }
                catch (Exception e)
                {
                    string error = $"第{i + 1}个函数调用失败: {e.Message}";
                    errors.Add(error);
                    results[i] = null; // 使用索引而非Add
                    failedCalls++;
                    completedCalls++;
                }
            }

            // 如果所有调用都是同步的，直接返回结果
            if (!hasAsyncCalls)
            {
                return new
                {
                    success = failedCalls == 0,
                    results = results,
                    errors = errors,
                    total_calls = totalCalls,
                    successful_calls = successfulCalls,
                    failed_calls = failedCalls
                };
            }

            // 有异步调用，返回null等待回调完成
            return null;
        }

        /// <summary>
        /// 粘贴剪贴板内容到输入框
        /// </summary>
        private void PasteFromClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("提示", "剪贴板为空", "确定");
                    return;
                }

                // 验证JSON格式
                if (!ValidateClipboardJson(clipboardContent, out string errorMessage))
                {
                    bool proceed = EditorUtility.DisplayDialog("JSON格式警告",
                        $"剪贴板内容可能不是有效的JSON:\n{errorMessage}\n\n是否仍要粘贴？",
                        "仍要粘贴", "取消");

                    if (!proceed) return;
                }

                inputJson = clipboardContent;
                EditorUtility.DisplayDialog("成功", "已粘贴剪贴板内容到输入框", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("粘贴失败", $"无法粘贴剪贴板内容: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 预览剪贴板内容
        /// </summary>
        private void PreviewClipboard()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    EditorUtility.DisplayDialog("剪贴板预览", "剪贴板为空", "确定");
                    return;
                }

                // 限制预览长度
                string preview = clipboardContent;
                if (preview.Length > 500)
                {
                    preview = preview.Substring(0, 500) + "\n...(内容过长，已截断)";
                }

                // 验证JSON格式
                string jsonStatus = ValidateClipboardJson(clipboardContent, out string errorMessage)
                    ? "✅ 有效的JSON格式"
                    : $"❌ JSON格式错误: {errorMessage}";

                EditorUtility.DisplayDialog("剪贴板预览",
                    $"格式状态: {jsonStatus}\n\n内容预览:\n{preview}", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("预览失败", $"无法预览剪贴板内容: {e.Message}", "确定");
            }
        }

        /// <summary>
        /// 获取剪贴板状态信息
        /// </summary>
        private string GetClipboardStatus()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    return "剪贴板: 空";
                }

                bool isValidJson = ValidateClipboardJson(clipboardContent, out _);
                string status = isValidJson ? "✅ JSON" : "❌ 非JSON";

                // 显示字符长度
                return $"剪贴板: {status} ({clipboardContent.Length} 字符)";
            }
            catch
            {
                return "剪贴板: 读取失败";
            }
        }

        /// <summary>
        /// 检查剪贴板是否可用（包含有效JSON内容）
        /// </summary>
        private bool IsClipboardAvailable()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                    return false;

                return ValidateClipboardJson(clipboardContent, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 绘制带颜色指示的剪贴板状态
        /// </summary>
        private void DrawClipboardStatus()
        {
            try
            {
                string clipboardContent = EditorGUIUtility.systemCopyBuffer;
                Color statusColor;
                string statusText;

                if (string.IsNullOrWhiteSpace(clipboardContent))
                {
                    statusColor = Color.red;
                    statusText = "剪切板: 空";
                }
                else
                {
                    bool isValidJson = ValidateClipboardJson(clipboardContent, out _);
                    if (isValidJson)
                    {
                        statusColor = Color.green;
                        statusText = $"剪切板: ✅ JSON ({clipboardContent.Length} 字符)";
                    }
                    else
                    {
                        statusColor = new Color(1f, 0.5f, 0f); // 橙色
                        statusText = $"剪切板: ❌ 非JSON ({clipboardContent.Length} 字符)";
                    }
                }

                // 显示带颜色的状态
                Color originalColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusText, EditorStyles.miniLabel);
                GUI.color = originalColor;
            }
            catch
            {
                Color originalColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("剪切板: 读取失败", EditorStyles.miniLabel);
                GUI.color = originalColor;
            }
        }

        /// <summary>
        /// 验证剪贴板JSON格式
        /// </summary>
        private bool ValidateClipboardJson(string content, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(content))
            {
                errorMessage = "内容为空";
                return false;
            }

            try
            {
                JObject.Parse(content);
                return true;
            }
            catch (JsonException e)
            {
                errorMessage = e.Message;
                return false;
            }
            catch (Exception e)
            {
                errorMessage = $"未知错误: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 检查结果是否为错误响应
        /// </summary>
        private bool IsErrorResponse(object result)
        {
            if (result == null) return true;

            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                // 检查是否有success字段且为false
                if (resultObj.ContainsKey("success"))
                {
                    return !resultObj["success"]?.Value<bool>() ?? true;
                }

                // 检查是否有error字段
                return resultObj.ContainsKey("error");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从错误响应中提取错误消息
        /// </summary>
        private string ExtractErrorMessage(object result)
        {
            if (result == null) return "结果为空";

            try
            {
                var resultJson = JsonConvert.SerializeObject(result);
                var resultObj = JObject.Parse(resultJson);

                // 尝试从error字段获取错误信息
                if (resultObj.ContainsKey("error"))
                {
                    return resultObj["error"]?.ToString() ?? "未知错误";
                }

                // 尝试从message字段获取错误信息
                if (resultObj.ContainsKey("message"))
                {
                    return resultObj["message"]?.ToString() ?? "未知错误";
                }

                return result.ToString();
            }
            catch
            {
                return result.ToString();
            }
        }
    }
}