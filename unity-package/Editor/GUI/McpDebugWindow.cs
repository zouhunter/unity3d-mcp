using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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
        private bool useAsyncExecution = true; // 默认使用异步执行
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

            // 执行模式选择
            GUILayout.BeginHorizontal();
            GUILayout.Label("执行模式:", GUILayout.Width(70));
            useAsyncExecution = EditorGUILayout.Toggle("异步执行", useAsyncExecution);
            if (!useAsyncExecution)
            {
                EditorGUILayout.HelpBox("同步执行可能会导致UI阻塞", MessageType.Warning);
            }
            GUILayout.EndHorizontal();

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

            // 创建输入框的滚动区域
            GUILayout.BeginVertical(EditorStyles.helpBox);
            inputScrollPosition = EditorGUILayout.BeginScrollView(
                inputScrollPosition,
               GUILayout.Height(inputHeight)
            );

            // 输入框
            inputJson = EditorGUILayout.TextArea(
                inputJson,
                codeStyle,
                GUILayout.ExpandHeight(true)
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
            // 第一行按钮
            GUILayout.BeginHorizontal();

            GUI.enabled = !isExecuting;
            if (GUILayout.Button("执行", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ExecuteCall();
            }

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
                string executingText = useAsyncExecution ? "异步执行中..." : "同步执行中...";
                GUILayout.Label(executingText, GUILayout.Width(100));
            }

            GUILayout.EndHorizontal();

            // 第二行按钮（剪贴板操作）
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("粘贴到输入框", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PasteFromClipboard();
            }

            if (GUILayout.Button("预览剪贴板", GUILayout.Height(25), GUILayout.Width(100)))
            {
                PreviewClipboard();
            }

            // 显示剪贴板状态
            string clipboardStatus = GetClipboardStatus();
            GUILayout.Label(clipboardStatus, EditorStyles.miniLabel);

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制结果显示区域（带滚动）
        /// </summary>
        private void DrawResultArea()
        {
            EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);

            // 创建结果显示的滚动区域
            GUILayout.BeginVertical(EditorStyles.helpBox);
            resultScrollPosition = EditorGUILayout.BeginScrollView(
                resultScrollPosition,
                GUILayout.Height(ResultAreaHeight)
            );

            // 结果文本区域
            EditorGUILayout.TextArea(
                resultText,
                codeStyle,
                GUILayout.ExpandHeight(true)
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

        private async void ExecuteCall()
        {
            if (string.IsNullOrWhiteSpace(inputJson))
            {
                EditorUtility.DisplayDialog("错误", "请输入JSON内容", "确定");
                return;
            }

            isExecuting = true;
            showResult = true;
            resultText = useAsyncExecution ? "正在异步执行..." : "正在同步执行...";

            try
            {
                DateTime startTime = DateTime.Now;
                object result;

                if (useAsyncExecution)
                {
                    result = await ExecuteJsonCallAsync();
                }
                else
                {
                    result = ExecuteJsonCall();
                }

                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                // 存储当前结果并格式化
                currentResult = result;
                string executionMode = useAsyncExecution ? "异步" : "同步";
                string formattedResult = FormatResult(result, duration, executionMode);
                resultText = formattedResult;

                // 刷新界面
                Repaint();
            }
            catch (Exception e)
            {
                string errorResult = $"执行错误:\n{e.Message}\n\n堆栈跟踪:\n{e.StackTrace}";
                resultText = errorResult;

                Debug.LogError($"[McpDebugWindow] 执行调用时发生错误: {e}");
            }
            finally
            {
                isExecuting = false;
            }
        }

        /// <summary>
        /// 执行批量函数调用（同步版本）
        /// </summary>
        private object ExecuteBatchCalls(JArray funcsArray)
        {
            var results = new List<object>();
            var errors = new List<string>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;

            for (int i = 0; i < funcsArray.Count; i++)
            {
                try
                {
                    var funcCall = funcsArray[i] as JObject;
                    if (funcCall == null)
                    {
                        errors.Add($"第{i + 1}个函数调用格式错误: 不是有效的JSON对象");
                        failedCalls++;
                        results.Add(null);
                        continue;
                    }

                    var functionCall = new FunctionCall();
                    object result = functionCall.HandleCommand(funcCall);
                    results.Add(result);
                    successfulCalls++;
                }
                catch (Exception e)
                {
                    string error = $"第{i + 1}个函数调用失败: {e.Message}";
                    errors.Add(error);
                    results.Add(null);
                    failedCalls++;
                }
            }

            // 返回类似functions_call的结果格式
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

        private object ExecuteJsonCall()
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

                return ExecuteBatchCalls(funcsArray);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                return functionCall.HandleCommand(inputObj);
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }

        /// <summary>
        /// 执行批量函数调用（异步版本）
        /// </summary>
        private async Task<object> ExecuteBatchCallsAsync(JArray funcsArray)
        {
            var results = new List<object>();
            var errors = new List<string>();
            int totalCalls = funcsArray.Count;
            int successfulCalls = 0;
            int failedCalls = 0;

            for (int i = 0; i < funcsArray.Count; i++)
            {
                try
                {
                    var funcCall = funcsArray[i] as JObject;
                    if (funcCall == null)
                    {
                        errors.Add($"第{i + 1}个函数调用格式错误: 不是有效的JSON对象");
                        failedCalls++;
                        results.Add(null);
                        continue;
                    }

                    var functionCall = new FunctionCall();
                    object result = await functionCall.HandleCommandAsync(funcCall);
                    results.Add(result);
                    successfulCalls++;
                }
                catch (Exception e)
                {
                    string error = $"第{i + 1}个函数调用失败: {e.Message}";
                    errors.Add(error);
                    results.Add(null);
                    failedCalls++;
                }
            }

            // 返回类似functions_call的结果格式
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

        private async Task<object> ExecuteJsonCallAsync()
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

                return await ExecuteBatchCallsAsync(funcsArray);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                return await functionCall.HandleCommandAsync(inputObj);
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }

        private string FormatResult(object result, TimeSpan duration, string executionMode = "")
        {
            string modeInfo = string.IsNullOrEmpty(executionMode) ? "" : $" ({executionMode}模式)";
            string formattedResult = $"执行时间: {duration.TotalMilliseconds:F2}ms{modeInfo}\n\n";

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
        private async void ExecuteClipboard()
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
                resultText = useAsyncExecution ? "正在异步执行剪贴板内容..." : "正在同步执行剪贴板内容...";

                try
                {
                    DateTime startTime = DateTime.Now;
                    object result;

                    if (useAsyncExecution)
                    {
                        result = await ExecuteJsonCallFromStringAsync(clipboardContent);
                    }
                    else
                    {
                        result = ExecuteJsonCallFromString(clipboardContent);
                    }

                    DateTime endTime = DateTime.Now;
                    TimeSpan duration = endTime - startTime;

                    // 存储当前结果并格式化
                    currentResult = result;
                    string executionMode = useAsyncExecution ? "异步" : "同步";
                    string formattedResult = FormatResult(result, duration, $"{executionMode} (剪贴板)");
                    resultText = $"📋 从剪贴板执行\n原始JSON:\n{clipboardContent}\n\n{formattedResult}";

                    // 刷新界面
                    Repaint();
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
        /// 从字符串执行JSON调用（同步版本）
        /// </summary>
        private object ExecuteJsonCallFromString(string jsonString)
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

                return ExecuteBatchCalls(funcsArray);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                return functionCall.HandleCommand(inputObj);
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
        }

        /// <summary>
        /// 从字符串执行JSON调用（异步版本）
        /// </summary>
        private async Task<object> ExecuteJsonCallFromStringAsync(string jsonString)
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

                return await ExecuteBatchCallsAsync(funcsArray);
            }
            else if (inputObj.ContainsKey("func"))
            {
                // 单个函数调用
                var functionCall = new FunctionCall();
                return await functionCall.HandleCommandAsync(inputObj);
            }
            else
            {
                throw new ArgumentException("输入的JSON必须包含 'func' 字段（单个调用）或 'funcs' 字段（批量调用）");
            }
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
    }
}