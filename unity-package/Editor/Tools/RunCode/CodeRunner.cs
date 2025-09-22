using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;
using UnityEditor.Compilation;
using CompilationAssembly = UnityEditor.Compilation.Assembly;
using ReflectionAssembly = System.Reflection.Assembly;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles C# code execution including compilation and running arbitrary C# methods.
    /// 对应方法名: code_runner
    /// </summary>
    [ToolName("code_runner", "开发工具")]
    public class CodeRunner : StateMethodBase
    {
        // Code execution tracking
        private class CodeOperation
        {
            public TaskCompletionSource<object> CompletionSource { get; set; }
            public string Code { get; set; }
            public string MethodName { get; set; }
            public List<ExecutionResult> Results { get; set; } = new List<ExecutionResult>();
        }

        private class ExecutionResult
        {
            public string MethodName { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; }
            public string Output { get; set; }
            public string StackTrace { get; set; }
            public double Duration { get; set; }
            public object ReturnValue { get; set; }
        }

        // Queue of active code operations
        private readonly List<CodeOperation> _activeOperations = new List<CodeOperation>();

        // Flag to track if the update callback is registered
        private bool _updateCallbackRegistered = false;

        private object validationResult;
        private object executionResult;

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: execute, validate", false),
                new MethodKey("code", "C# code content to execute", true),
                new MethodKey("class_name", "Class name, default is CodeClass", true),
                new MethodKey("method_name", "Method name to execute, default is Run", true),
                new MethodKey("namespace", "Namespace, default is CodeNamespace", true),
                new MethodKey("includes", "Referenced using statements list, JSON array format", true),
                new MethodKey("parameters", "Method parameters, JSON array format", true),
                new MethodKey("timeout", "Execution timeout (seconds), default 30 seconds", true),
                new MethodKey("cleanup", "Whether to clean up temporary files after execution, default true", true),
                new MethodKey("return_output", "Whether to capture and return console output, default true", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("execute", HandleExecuteCode)
                    .Leaf("validate", HandleValidateCode)
                    .DefaultLeaf(HandleExecuteCode)
                .Build();
        }

        // --- 代码执行操作处理方法 ---

        /// <summary>
        /// 处理执行代码操作
        /// </summary>
        private object HandleExecuteCode(StateTreeContext ctx)
        {
            LogInfo("[CodeRunner] Executing C# code");
            return ctx.AsyncReturn(ExecuteCodeCoroutine(ctx.JsonData));
        }

        /// <summary>
        /// 处理验证代码操作
        /// </summary>
        private object HandleValidateCode(StateTreeContext ctx)
        {
            LogInfo("[CodeRunner] Validating C# code");
            return ctx.AsyncReturn(ValidateCodeCoroutine(ctx.JsonData));
        }

        // --- 异步执行方法 ---

        /// <summary>
        /// 异步执行代码协程
        /// </summary>
        private IEnumerator ExecuteCodeCoroutine(JObject args)
        {
            string tempFilePath = null;
            string tempAssemblyPath = null;

            try
            {
                string code = args["code"]?.ToString();
                if (string.IsNullOrEmpty(code))
                {
                    yield return Response.Error("code parameter is required");
                    yield break;
                }

                string className = args["class_name"]?.ToString() ?? "CodeClass";
                string methodName = args["method_name"]?.ToString() ?? "Run";
                string namespaceName = args["namespace"]?.ToString() ?? "CodeNamespace";
                var includes = args["includes"]?.ToObject<string[]>() ?? GetDefaultIncludes();
                var parameters = args["parameters"]?.ToObject<object[]>() ?? new object[0];
                int timeout = args["timeout"]?.ToObject<int>() ?? 30;
                bool cleanup = args["cleanup"]?.ToObject<bool>() ?? true;
                bool returnOutput = args["return_output"]?.ToObject<bool>() ?? true;

                LogInfo($"[CodeRunner] Executing method: {namespaceName}.{className}.{methodName}");

                // 使用协程执行代码
                yield return ExecuteCodeCoroutineInternal(code, className, methodName, namespaceName, includes, parameters, timeout, cleanup, returnOutput,
                    (tFilePath, tAssemblyPath) => { tempFilePath = tFilePath; tempAssemblyPath = tAssemblyPath; });
                yield return executionResult;
            }
            finally
            {
                // 清理临时目录
                if (!string.IsNullOrEmpty(tempFilePath))
                {
                    var tempDir = Path.GetDirectoryName(tempFilePath);
                    EditorApplication.delayCall += () => CleanupTempDirectory(tempDir);
                }
            }
        }

        /// <summary>
        /// 验证代码协程
        /// </summary>
        private IEnumerator ValidateCodeCoroutine(JObject args)
        {
            string tempFilePath = null;
            string tempAssemblyPath = null;

            try
            {
                string code = args["code"]?.ToString();
                if (string.IsNullOrEmpty(code))
                {
                    yield return Response.Error("code parameter is required");
                    yield break;
                }

                string className = args["class_name"]?.ToString() ?? "CodeClass";
                string methodName = args["method_name"]?.ToString() ?? "Run";
                string namespaceName = args["namespace"]?.ToString() ?? "CodeNamespace";
                var includes = args["includes"]?.ToObject<string[]>() ?? GetDefaultIncludes();

                LogInfo($"[CodeRunner] Validating code class: {namespaceName}.{className}");

                // 在协程外部处理异常
                validationResult = null;
                string fullCode = code;

                bool failed = false;
                try
                {
                    fullCode = GenerateFullCode(code, className, methodName, namespaceName, includes);
                    LogInfo($"[CodeRunner] Generated code for validation");
                }
                catch (Exception e)
                {
                    LogError($"[CodeRunner] Failed to generate validation code: {e.Message}");
                    validationResult = Response.Error($"Failed to generate code: {e.Message}");
                    failed = true;
                }
                if (failed)
                {
                    yield return validationResult;
                    yield break;
                }

                // 使用协程编译验证
                yield return CompileCodeCoroutine(fullCode,
                    (tFilePath, tAssemblyPath) => { tempFilePath = tFilePath; tempAssemblyPath = tAssemblyPath; },
                    (success, assembly, errors) =>
                    {
                        if (success)
                        {
                            validationResult = Response.Success(
                                "Code syntax is valid", new
                                {
                                    operation = "validate",
                                    class_name = className,
                                    method_name = methodName,
                                    namespace_name = namespaceName,
                                    generated_code = fullCode
                                });
                        }
                        else
                        {
                            validationResult = Response.Error("Code syntax validation failed", new
                            {
                                operation = "validate",
                                errors = string.Join("\n", errors ?? new string[] { "Unknown validation error" })
                            });
                        }
                    });

                yield return validationResult;
            }
            finally
            {
                // 清理临时目录
                if (!string.IsNullOrEmpty(tempFilePath))
                {
                    var tempDir = Path.GetDirectoryName(tempFilePath);
                    EditorApplication.delayCall += () => CleanupTempDirectory(tempDir);
                }
            }
        }


        /// <summary>
        /// 执行代码的内部协程
        /// </summary>
        private IEnumerator ExecuteCodeCoroutineInternal(string code, string className, string methodName, string namespaceName, string[] includes, object[] parameters, int timeout, bool cleanup, bool returnOutput, System.Action<string, string> onTempFilesCreated = null)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            executionResult = null;

            // 生成完整的代码
            string fullCode = code;
            bool failed = false;
            try
            {
                fullCode = GenerateFullCode(code, className, methodName, namespaceName, includes);
                LogInfo($"[CodeRunner] Generated complete code");
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] Failed to generate code: {e.Message}");
                executionResult = Response.Error($"Failed to generate code: {e.Message}");
                failed = true;
            }
            if (failed)
            {
                yield return executionResult;
                yield break;
            }

            // 使用协程编译代码
            yield return CompileCodeCoroutine(fullCode,
                onTempFilesCreated,
                (success, assembly, errors) =>
                {
                    if (success)
                    {
                        try
                        {
                            // 检查是否是完整代码，如果是则需要动态查找执行方法
                            bool isCompleteCode = fullCode.Contains("using ") || fullCode.Contains("namespace ") ||
                                                 (fullCode.Contains("public class") || fullCode.Contains("public static class"));

                            List<ExecutionResult> executionResults;
                            if (isCompleteCode)
                            {
                                // 对于完整代码，尝试查找并执行所有可执行的静态方法
                                executionResults = ExecuteCompleteCode(assembly, parameters, returnOutput);
                            }
                            else
                            {
                                // 对于代码片段，按原有方式执行
                                executionResults = ExecuteCompiledCode(assembly, namespaceName, className, methodName, parameters, returnOutput);
                            }

                            var result = executionResults.FirstOrDefault() ?? new ExecutionResult
                            {
                                MethodName = methodName,
                                Success = false,
                                Message = "No execution result",
                                Output = "",
                                Duration = 0
                            };

                            executionResult = Response.Success(
                                result.Success ? "Code execution completed successfully" : "Code execution completed with errors",
                                new
                                {
                                    operation = "execute",
                                    class_name = className,
                                    method_name = methodName,
                                    namespace_name = namespaceName,
                                    success = result.Success,
                                    message = result.Message,
                                    output = result.Output,
                                    return_value = result.ReturnValue?.ToString() ?? "null",
                                    duration = result.Duration,
                                    stack_trace = result.StackTrace
                                }
                            );
                        }
                        catch (Exception e)
                        {
                            LogError($"[CodeRunner] Code execution failed: {e.Message}");
                            executionResult = Response.Error($"Failed to execute compiled code: {e.Message}");
                        }
                    }
                    else
                    {
                        executionResult = Response.Error("Code compilation failed", new
                        {
                            operation = "execute",
                            errors = string.Join("\n", errors ?? new string[] { "Unknown compilation error" })
                        });
                    }
                });
            yield return executionResult;
        }


        /// <summary>
        /// 生成完整的代码
        /// </summary>
        private string GenerateFullCode(string code, string className, string methodName, string namespaceName, string[] includes)
        {
            // 检查代码是否已经是完整的（包含using、namespace、class等）
            bool isCompleteCode = code.Contains("using ") || code.Contains("namespace ") ||
                                 (code.Contains("public class") || code.Contains("public static class"));

            if (isCompleteCode)
            {
                // 如果是完整代码，直接返回，不添加任何包装
                LogInfo("[CodeRunner] 检测到完整代码，直接使用");
                return code;
            }

            var sb = new StringBuilder();

            // 添加using语句
            foreach (var include in includes)
            {
                sb.AppendLine($"using {include};");
            }

            sb.AppendLine();

            // 添加命名空间和类
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            // 检查用户代码是否已经包含方法定义
            bool hasMethodDefinition = code.Contains("public") && (code.Contains("static") || code.Contains("void") || code.Contains("string") || code.Contains("int") || code.Contains("bool"));

            if (hasMethodDefinition)
            {
                // 如果用户代码已包含方法定义，直接缩进并添加
                var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    sb.AppendLine($"        {line}");
                }
            }
            else
            {
                // 如果用户代码只是代码片段，包装在指定的方法中
                sb.AppendLine($"        public static object {methodName}()");
                sb.AppendLine("        {");
                sb.AppendLine("            try");
                sb.AppendLine("            {");

                var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    sb.AppendLine($"                {line}");
                }

                // 如果代码不包含return语句，添加默认返回值
                if (!code.ToLower().Contains("return"))
                {
                    sb.AppendLine("                return \"Execution completed\";");
                }

                sb.AppendLine("            }");
                sb.AppendLine("            catch (System.Exception ex)");
                sb.AppendLine("            {");
                sb.AppendLine("                UnityEngine.Debug.LogException(ex);");
                sb.AppendLine("                throw;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// 协程版本的代码编译
        /// </summary>
        private IEnumerator CompileCodeCoroutine(string code, System.Action<string, string> onTempFilesCreated, System.Action<bool, ReflectionAssembly, string[]> callback)
        {
            // 创建基于代码内容的临时目录
            var baseDir = Path.Combine(Application.temporaryCachePath, "CodeRunner");
            var codeHash = GetCodeHash(code);
            var tempDir = Path.Combine(baseDir, codeHash);
            string tempFilePath = null;
            string tempAssemblyPath = null;
            AssemblyBuilder assemblyBuilder = null;

            // 在协程外部处理初始化异常
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var tempFileName = "TestClass.cs";
                tempFilePath = Path.Combine(tempDir, tempFileName);
                tempAssemblyPath = Path.Combine(tempDir, "TestClass.dll");

                // 通知上层函数临时文件路径
                onTempFilesCreated?.Invoke(tempFilePath, tempAssemblyPath);

                // 检查是否已经存在编译好的程序集
                if (File.Exists(tempAssemblyPath))
                {
                    LogInfo($"[CodeRunner] 发现已编译的程序集，直接加载: {tempAssemblyPath}");
                    try
                    {
                        var assemblyBytes = File.ReadAllBytes(tempAssemblyPath);
                        if (assemblyBytes.Length > 0)
                        {
                            var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                            LogInfo($"[CodeRunner] 程序集重用成功: {assemblyBytes.Length} bytes");
                            callback(true, loadedAssembly, null);
                            yield break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"[CodeRunner] 加载已存在程序集失败，将重新编译: {ex.Message}");
                        // 删除损坏的程序集文件
                        try { File.Delete(tempAssemblyPath); } catch { }
                    }
                }

                // 写入代码到临时文件
                File.WriteAllText(tempFilePath, code);
                LogInfo($"[CodeRunner] 临时文件路径: {tempFilePath}");
                LogInfo($"[CodeRunner] 目标程序集路径: {tempAssemblyPath}");
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] 初始化失败: {e.Message}");
                callback(false, null, new[] { $"Initialization failed: {e.Message}" });
                yield break;
            }

            // 设置编译器
            try
            {
                assemblyBuilder = new AssemblyBuilder(tempAssemblyPath, new[] { tempFilePath });

                // 收集程序集引用
                var references = new List<string>();
                LogInfo("[CodeRunner] 开始收集程序集引用...");

                foreach (var assembly in CompilationPipeline.GetAssemblies())
                {
                    references.AddRange(assembly.compiledAssemblyReferences);
                    if (!string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath))
                    {
                        references.Add(assembly.outputPath);
                    }
                }

                foreach (var loadedAssembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(loadedAssembly.Location) && File.Exists(loadedAssembly.Location))
                        {
                            references.Add(loadedAssembly.Location);
                        }
                    }
                    catch { }
                }

                // 添加基础引用
                references.Add(typeof(object).Assembly.Location);
                references.Add(typeof(System.Linq.Enumerable).Assembly.Location);
                references.Add(typeof(UnityEngine.Debug).Assembly.Location);
                references.Add(typeof(UnityEditor.EditorApplication).Assembly.Location);

                var uniqueReferences = references.Distinct().Where(r => !string.IsNullOrEmpty(r) && File.Exists(r)).ToArray();
                LogInfo($"[CodeRunner] 收集到 {uniqueReferences.Length} 个有效引用");

                assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                assemblyBuilder.additionalReferences = uniqueReferences;

                // 记录详细的编译参数
                LogInfo($"[CodeRunner] 编译参数:");
                LogInfo($"  - 源文件: {tempFilePath}");
                LogInfo($"  - 目标程序集: {tempAssemblyPath}");
                LogInfo($"  - 引用选项: {assemblyBuilder.referencesOptions}");
                LogInfo($"  - 额外引用数量: {uniqueReferences.Length}");
                LogInfo($"  - 前20个引用: {string.Join(", ", uniqueReferences.Take(477).Select(Path.GetFileName))}");
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] 编译器设置失败: {e.Message}");
                callback(false, null, new[] { $"Compiler setup failed: {e.Message}" });
                yield break;
            }

            // 启动编译
            LogInfo("[CodeRunner] 尝试启动编译...");
            bool started = false;

            // 用于存储编译消息的变量
            CompilerMessage[] compilationMessages = null;
            bool compilationFinished = false;

            // 添加编译事件监听
            System.Action<object> buildStarted = (context) =>
            {
                EditorApplication.delayCall += () => LogInfo($"[CodeRunner] 编译开始");
            };

            System.Action<string, CompilerMessage[]> buildFinished = (assemblyPath, messages) =>
            {
                compilationMessages = messages;
                compilationFinished = true;
                EditorApplication.delayCall += () =>
                {
                    LogInfo($"[CodeRunner] 编译完成: {assemblyPath}");
                    if (messages != null && messages.Length > 0)
                    {
                        LogInfo($"[CodeRunner] 收到 {messages.Length} 条编译消息");
                        foreach (var msg in messages)
                        {
                            var logLevel = msg.type == CompilerMessageType.Error ? "ERROR" :
                                         msg.type == CompilerMessageType.Warning ? "WARNING" : "INFO";
                            LogInfo($"[CodeRunner] {logLevel}: {msg.message} (Line: {msg.line}, Column: {msg.column})");
                        }
                    }
                    else
                    {
                        LogInfo("[CodeRunner] 没有收到编译消息");
                    }
                };
            };

            CompilationPipeline.compilationStarted += buildStarted;
            CompilationPipeline.assemblyCompilationFinished += buildFinished;

            try
            {
                // 在启动编译前，先验证源文件内容
                LogInfo($"[CodeRunner] 验证源文件: {tempFilePath}");
                if (File.Exists(tempFilePath))
                {
                    var sourceContent = File.ReadAllText(tempFilePath);
                    LogInfo($"[CodeRunner] 源文件大小: {sourceContent.Length} 字符");
                    LogInfo($"[CodeRunner] 源文件前200字符: {sourceContent.Substring(0, Math.Min(200, sourceContent.Length))}");
                }

                started = assemblyBuilder.Build();
                LogInfo($"[CodeRunner] 编译启动结果: {started}");

                // 记录AssemblyBuilder的初始状态
                LogInfo($"[CodeRunner] 初始编译状态: {assemblyBuilder.status}");
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] 编译启动异常: {e.Message}");
                LogError($"[CodeRunner] 异常堆栈: {e.StackTrace}");
                callback(false, null, new[] { $"Failed to start compilation: {e.Message}", $"Stack trace: {e.StackTrace}" });
                yield break;
            }
            finally
            {
                // 移除事件监听
                CompilationPipeline.compilationStarted -= buildStarted;
                CompilationPipeline.assemblyCompilationFinished -= buildFinished;
            }

            if (!started)
            {
                LogError("[CodeRunner] 无法启动编译");
                callback(false, null, new[] { "Failed to start compilation" });
                yield break;
            }

            LogInfo("[CodeRunner] 编译已启动，等待完成...");

            // 使用协程等待编译完成
            float timeout = 30f;
            float elapsedTime = 0f;
            var lastStatus = assemblyBuilder.status;

            while (assemblyBuilder.status == AssemblyBuilderStatus.IsCompiling && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                // 监控状态变化
                if (assemblyBuilder.status != lastStatus)
                {
                    LogInfo($"[CodeRunner] 编译状态变化: {lastStatus} -> {assemblyBuilder.status}");
                    lastStatus = assemblyBuilder.status;
                }

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    LogInfo($"[CodeRunner] 编译中... 状态: {assemblyBuilder.status}, 已等待: {elapsedTime:F1}s");
                }
            }

            // 额外等待编译消息（有时消息会稍晚到达）
            float messageWaitTime = 0f;
            const float maxMessageWaitTime = 2f;
            while (!compilationFinished && messageWaitTime < maxMessageWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                messageWaitTime += 0.1f;
                LogInfo($"[CodeRunner] 等待编译消息... {messageWaitTime:F1}s");
            }

            LogInfo($"[CodeRunner] 编译完成, 最终状态: {assemblyBuilder.status}, 消息已接收: {compilationFinished}");

            // 处理编译结果
            if (assemblyBuilder.status == AssemblyBuilderStatus.Finished)
            {
                LogInfo($"[CodeRunner] 编译状态为Finished，开始验证结果...");

                // 立即检查文件是否存在
                var assemblyPath = assemblyBuilder.assemblyPath;
                LogInfo($"[CodeRunner] 预期程序集路径: {assemblyPath}");
                LogInfo($"[CodeRunner] 文件是否存在: {File.Exists(assemblyPath)}");

                if (File.Exists(assemblyPath))
                {
                    var fileInfo = new FileInfo(assemblyPath);
                    LogInfo($"[CodeRunner] 文件大小: {fileInfo.Length} bytes, 修改时间: {fileInfo.LastWriteTime}");
                }

                yield return HandleCompilationSuccess(assemblyBuilder, callback);
            }
            else if (elapsedTime >= timeout)
            {
                LogError("[CodeRunner] 编译超时");
                callback(false, null, new[] { "Compilation timeout" });
            }
            else
            {
                LogError($"[CodeRunner] 编译失败, 状态: {assemblyBuilder.status}");

                // 收集详细的错误信息
                var errorMessages = new List<string>();
                errorMessages.Add($"Compilation failed with status: {assemblyBuilder.status}");

                // 首先检查是否有编译消息
                if (compilationMessages != null && compilationMessages.Length > 0)
                {
                    LogInfo($"[CodeRunner] 处理 {compilationMessages.Length} 条编译消息");
                    var errorMsgs = new List<string>();
                    var warningMsgs = new List<string>();

                    foreach (var msg in compilationMessages)
                    {
                        var msgText = $"Line {msg.line}, Column {msg.column}: {msg.message}";
                        if (msg.type == CompilerMessageType.Error)
                        {
                            errorMsgs.Add(msgText);
                            LogError($"[CodeRunner] 编译错误: {msgText}");
                        }
                        else if (msg.type == CompilerMessageType.Warning)
                        {
                            warningMsgs.Add(msgText);
                            LogWarning($"[CodeRunner] 编译警告: {msgText}");
                        }
                    }

                    if (errorMsgs.Count > 0)
                    {
                        errorMessages.Add($"Compilation errors ({errorMsgs.Count}):");
                        errorMessages.AddRange(errorMsgs);
                    }

                    if (warningMsgs.Count > 0)
                    {
                        errorMessages.Add($"Compilation warnings ({warningMsgs.Count}):");
                        errorMessages.AddRange(warningMsgs);
                    }
                }
                else
                {
                    LogWarning("[CodeRunner] 没有收到编译消息，尝试其他方法获取错误信息");

                    // 尝试获取编译错误信息（保留原有逻辑作为后备）
                    try
                    {
                        var tempDirPath = Path.GetDirectoryName(tempFilePath);
                        var logFiles = new string[]
                        {
                            Path.Combine(tempDirPath, "CompilerOutput.log"),
                            Path.ChangeExtension(tempAssemblyPath, ".log"),
                            tempAssemblyPath + ".log"
                        };

                        foreach (var logFile in logFiles)
                        {
                            if (File.Exists(logFile))
                            {
                                var logContent = File.ReadAllText(logFile);
                                if (!string.IsNullOrEmpty(logContent))
                                {
                                    errorMessages.Add($"Log from {Path.GetFileName(logFile)}: {logContent}");
                                    LogError($"[CodeRunner] 编译日志: {logContent}");
                                }
                            }
                        }

                        // 检查临时目录中的所有文件
                        if (Directory.Exists(tempDirPath))
                        {
                            var allFiles = Directory.GetFiles(tempDirPath);
                            LogInfo($"[CodeRunner] 临时目录文件: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                            errorMessages.Add($"Files in temp directory: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"[CodeRunner] 获取编译错误信息失败: {ex.Message}");
                        errorMessages.Add($"Failed to get compilation error details: {ex.Message}");
                    }
                }

                callback(false, null, errorMessages.ToArray());
            }

            // 清理由上层函数负责
        }

        /// <summary>
        /// 处理编译成功后的程序集加载
        /// </summary>
        private IEnumerator HandleCompilationSuccess(AssemblyBuilder assemblyBuilder, System.Action<bool, ReflectionAssembly, string[]> callback)
        {
            var assemblyPath = assemblyBuilder.assemblyPath;
            var tempDir = Path.GetDirectoryName(assemblyPath);
            float waitTime = 0f;
            const float maxWaitTime = 2f;

            LogInfo($"[CodeRunner] 等待程序集文件生成: {assemblyPath}");

            // 等待文件存在
            while (!File.Exists(assemblyPath) && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;

                // 每0.5秒检查一次临时目录状态
                if (waitTime % 0.5f < 0.1f)
                {
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            var files = Directory.GetFiles(tempDir);
                            LogInfo($"[CodeRunner] 临时目录文件 ({waitTime:F1}s): {string.Join(", ", files.Select(Path.GetFileName))}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"[CodeRunner] 检查临时目录失败: {ex.Message}");
                    }
                }
            }

            if (File.Exists(assemblyPath))
            {
                // 等待文件写入完成
                yield return new WaitForSeconds(0.1f);

                try
                {
                    var assemblyBytes = File.ReadAllBytes(assemblyPath);
                    LogInfo($"[CodeRunner] 程序集文件大小: {assemblyBytes.Length} bytes");

                    if (assemblyBytes.Length > 0)
                    {
                        var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                        LogInfo($"[CodeRunner] 程序集加载成功: {assemblyBytes.Length} bytes");
                        callback(true, loadedAssembly, null);
                    }
                    else
                    {
                        LogError("[CodeRunner] 程序集文件为空");
                        callback(false, null, new[] { "Assembly file is empty" });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[CodeRunner] 无法加载程序集: {ex.Message}");
                    callback(false, null, new[] { $"Failed to load assembly: {ex.Message}" });
                }
            }
            else
            {
                LogError($"[CodeRunner] 程序集文件不存在: {assemblyPath}");

                // 收集详细的调试信息
                var errorMessages = new List<string>();
                errorMessages.Add("Assembly file not found after compilation");
                errorMessages.Add($"Expected path: {assemblyPath}");

                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        var allFiles = Directory.GetFiles(tempDir);
                        LogError($"[CodeRunner] 临时目录内容: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                        errorMessages.Add($"Files in temp directory: {string.Join(", ", allFiles.Select(Path.GetFileName))}");

                        // 检查是否有其他dll文件
                        var dllFiles = allFiles.Where(f => f.EndsWith(".dll")).ToArray();
                        if (dllFiles.Length > 0)
                        {
                            LogInfo($"[CodeRunner] 找到其他DLL文件: {string.Join(", ", dllFiles.Select(Path.GetFileName))}");
                            errorMessages.Add($"Other DLL files found: {string.Join(", ", dllFiles.Select(Path.GetFileName))}");
                        }

                        // 检查是否有日志文件
                        var logFiles = allFiles.Where(f => f.EndsWith(".log") || f.Contains("log")).ToArray();
                        foreach (var logFile in logFiles)
                        {
                            try
                            {
                                var logContent = File.ReadAllText(logFile);
                                LogError($"[CodeRunner] 日志文件 {Path.GetFileName(logFile)}: {logContent}");
                                errorMessages.Add($"Log file {Path.GetFileName(logFile)}: {logContent}");
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        LogError($"[CodeRunner] 临时目录不存在: {tempDir}");
                        errorMessages.Add($"Temp directory does not exist: {tempDir}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[CodeRunner] 收集调试信息失败: {ex.Message}");
                    errorMessages.Add($"Failed to collect debug info: {ex.Message}");
                }

                callback(false, null, errorMessages.ToArray());
            }
        }

        /// <summary>
        /// 原始的同步编译代码（保留作为后备）
        /// </summary>
        private (bool success, ReflectionAssembly assembly, string[] errors) CompileCode(string code)
        {
            try
            {
                // 创建基于代码内容的临时目录
                var baseDir = Path.Combine(Application.temporaryCachePath, "CodeRunner");
                var codeHash = GetCodeHash(code);
                var tempDir = Path.Combine(baseDir, codeHash);

                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var tempFileName = "TestClass.cs";
                var tempFilePath = Path.Combine(tempDir, tempFileName);
                var tempAssemblyPath = Path.Combine(tempDir, "TestClass.dll");

                try
                {
                    // 检查是否已经存在编译好的程序集
                    if (File.Exists(tempAssemblyPath))
                    {
                        LogInfo($"[CodeRunner] 发现已编译的程序集，直接加载: {tempAssemblyPath}");
                        try
                        {
                            var assemblyBytes = File.ReadAllBytes(tempAssemblyPath);
                            if (assemblyBytes.Length > 0)
                            {
                                var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                                LogInfo($"[CodeRunner] 程序集重用成功: {assemblyBytes.Length} bytes");
                                return (true, loadedAssembly, null);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"[CodeRunner] 加载已存在程序集失败，将重新编译: {ex.Message}");
                            // 删除损坏的程序集文件
                            try { File.Delete(tempAssemblyPath); } catch { }
                        }
                    }

                    // 写入代码到临时文件
                    File.WriteAllText(tempFilePath, code);

                    LogInfo($"[CodeRunner] 临时文件路径: {tempFilePath}");
                    LogInfo($"[CodeRunner] 目标程序集路径: {tempAssemblyPath}");
                    LogInfo($"[CodeRunner] 代码长度: {code.Length} 字符");

                    // 使用Unity编译流水线编译
                    var assemblyBuilder = new AssemblyBuilder(tempAssemblyPath, new[] { tempFilePath });

                    // 添加必要的引用
                    var references = new List<string>();

                    LogInfo("[CodeRunner] 开始收集程序集引用...");

                    // 获取所有程序集引用，包括Assembly-CSharp和所有package程序集
                    foreach (var assembly in CompilationPipeline.GetAssemblies())
                    {
                        LogInfo($"[CodeRunner] 处理程序集: {assembly.name}");

                        // 添加程序集的所有引用
                        references.AddRange(assembly.compiledAssemblyReferences);

                        // 如果程序集已经编译，也添加它的路径（包括Assembly-CSharp等）
                        if (!string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath))
                        {
                            references.Add(assembly.outputPath);
                            LogInfo($"[CodeRunner] 添加编译后程序集: {assembly.outputPath}");
                        }
                    }

                    // 添加当前域中已加载的所有程序集（包括package程序集）
                    foreach (var loadedAssembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(loadedAssembly.Location) && File.Exists(loadedAssembly.Location))
                            {
                                references.Add(loadedAssembly.Location);
                                LogInfo($"[CodeRunner] 添加运行时程序集: {loadedAssembly.GetName().Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略无法访问的程序集
                            LogInfo($"[CodeRunner] 无法访问程序集 {loadedAssembly.GetName().Name}: {ex.Message}");
                        }
                    }

                    // 添加基础引用
                    references.Add(typeof(object).Assembly.Location); // mscorlib
                    references.Add(typeof(System.Linq.Enumerable).Assembly.Location); // System.Core
                    references.Add(typeof(UnityEngine.Debug).Assembly.Location); // UnityEngine
                    references.Add(typeof(UnityEditor.EditorApplication).Assembly.Location); // UnityEditor

                    LogInfo($"[CodeRunner] 总共收集到 {references.Count} 个程序集引用");

                    // 去除重复引用
                    var uniqueReferences = references.Distinct().Where(r => !string.IsNullOrEmpty(r) && File.Exists(r)).ToArray();
                    LogInfo($"[CodeRunner] 去除重复后有 {uniqueReferences.Length} 个有效引用");

                    assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                    assemblyBuilder.additionalReferences = uniqueReferences;

                    // 开始编译，添加重试机制
                    bool started = false;
                    int retryCount = 0;
                    const int maxRetries = 3;

                    while (!started && retryCount < maxRetries)
                    {
                        try
                        {
                            LogInfo($"[CodeRunner] 尝试启动编译 ({retryCount + 1}/{maxRetries})...");
                            started = assemblyBuilder.Build();

                            if (started)
                            {
                                LogInfo($"[CodeRunner] 编译启动成功");
                            }
                            else
                            {
                                retryCount++;
                                LogError($"[CodeRunner] 编译启动失败, 重试 {retryCount}/{maxRetries}");

                                if (retryCount < maxRetries)
                                {
                                    System.Threading.Thread.Sleep(100 * retryCount); // 递增延迟

                                    // 创建新的AssemblyBuilder
                                    var newAssemblyPath = Path.ChangeExtension(tempFilePath, $"_retry{retryCount}.dll");
                                    LogInfo($"[CodeRunner] 创建新的AssemblyBuilder: {newAssemblyPath}");
                                    assemblyBuilder = new AssemblyBuilder(newAssemblyPath, new[] { tempFilePath });
                                    assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                                    assemblyBuilder.additionalReferences = uniqueReferences; // 使用正确的引用
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                LogInfo($"[CodeRunner] Compilation exception, retry {retryCount}/{maxRetries}: {ex.Message}");
                                System.Threading.Thread.Sleep(200 * retryCount);

                                // 创建新的AssemblyBuilder
                                var newAssemblyPath = Path.ChangeExtension(tempFilePath, $"_retry{retryCount}.dll");
                                assemblyBuilder = new AssemblyBuilder(newAssemblyPath, new[] { tempFilePath });
                                assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                                assemblyBuilder.additionalReferences = uniqueReferences;
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }

                    if (!started)
                    {
                        return (false, null, new[] { $"Failed to start compilation after {maxRetries} retries" });
                    }

                    // 等待编译完成
                    LogInfo($"[CodeRunner] 开始等待编译完成, 初始状态: {assemblyBuilder.status}");
                    var timeout = DateTime.Now.AddSeconds(30); // 30秒超时
                    int waitCount = 0;

                    while (assemblyBuilder.status == AssemblyBuilderStatus.IsCompiling)
                    {
                        if (DateTime.Now > timeout)
                        {
                            LogError($"[CodeRunner] 编译超时, 最终状态: {assemblyBuilder.status}");
                            return (false, null, new[] { "Compilation timeout" });
                        }
                        System.Threading.Thread.Sleep(50);
                        waitCount++;

                        // 每秒输出一次状态
                        if (waitCount % 20 == 0)
                        {
                            LogInfo($"[CodeRunner] 编译中... 当前状态: {assemblyBuilder.status}, 已等待 {waitCount * 50}ms");
                        }
                    }

                    LogInfo($"[CodeRunner] 编译完成, 最终状态: {assemblyBuilder.status}");

                    if (assemblyBuilder.status == AssemblyBuilderStatus.Finished)
                    {
                        // 加载编译后的程序集
                        var assemblyPath = assemblyBuilder.assemblyPath;

                        // 等待文件存在，最多等待1秒
                        waitCount = 0;
                        const int maxWaitCount = 20; // 20 * 50ms = 1000ms

                        while (!File.Exists(assemblyPath) && waitCount < maxWaitCount)
                        {
                            System.Threading.Thread.Sleep(50);
                            waitCount++;
                        }

                        if (File.Exists(assemblyPath))
                        {
                            try
                            {
                                // 等待文件写入完成
                                System.Threading.Thread.Sleep(100);

                                var assemblyBytes = File.ReadAllBytes(assemblyPath);
                                if (assemblyBytes.Length > 0)
                                {
                                    var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                                    LogInfo($"[CodeRunner] Assembly loaded successfully: {assemblyPath} ({assemblyBytes.Length} bytes)");
                                    return (true, loadedAssembly, null);
                                }
                                else
                                {
                                    return (false, null, new[] { "Assembly file is empty" });
                                }
                            }
                            catch (Exception ex)
                            {
                                return (false, null, new[] { $"Failed to load assembly: {ex.Message}" });
                            }
                        }
                        else
                        {
                            // 详细的错误信息
                            var errorDetails = new List<string>
                            {
                                "Compiled assembly file not found",
                                $"Expected path: {assemblyPath}",
                                $"Directory exists: {Directory.Exists(Path.GetDirectoryName(assemblyPath))}",
                                $"Temp directory: {Path.GetDirectoryName(tempFilePath)}"
                            };

                            // 检查目录中的文件
                            try
                            {
                                tempDir = Path.GetDirectoryName(tempFilePath);
                                if (Directory.Exists(tempDir))
                                {
                                    var files = Directory.GetFiles(tempDir, "*.dll");
                                    errorDetails.Add($"DLL files in temp directory: {string.Join(", ", files.Select(Path.GetFileName))}");
                                }
                            }
                            catch { }

                            return (false, null, errorDetails.ToArray());
                        }
                    }
                    else
                    {
                        var errors = new List<string> { $"Compilation failed with status: {assemblyBuilder.status}" };
                        LogError($"[CodeRunner] 编译失败, 状态: {assemblyBuilder.status}");

                        // 检查程序集文件是否存在（有时状态不准确）
                        if (File.Exists(assemblyBuilder.assemblyPath))
                        {
                            LogInfo($"[CodeRunner] 程序集文件存在，尝试加载: {assemblyBuilder.assemblyPath}");
                            try
                            {
                                var assemblyBytes = File.ReadAllBytes(assemblyBuilder.assemblyPath);
                                if (assemblyBytes.Length > 0)
                                {
                                    var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                                    LogInfo($"[CodeRunner] 程序集加载成功，尽管状态显示失败");
                                    return (true, loadedAssembly, null);
                                }
                            }
                            catch (Exception loadEx)
                            {
                                LogError($"[CodeRunner] 无法加载程序集: {loadEx.Message}");
                            }
                        }

                        // 尝试获取编译错误信息
                        try
                        {
                            // 检查多种可能的日志文件
                            var logPaths = new string[]
                            {
                                Path.ChangeExtension(assemblyBuilder.assemblyPath, ".log"),
                                assemblyBuilder.assemblyPath + ".log",
                                Path.Combine(Path.GetDirectoryName(assemblyBuilder.assemblyPath), "CompilationLog.txt")
                            };

                            foreach (var logPath in logPaths)
                            {
                                if (File.Exists(logPath))
                                {
                                    var logContent = File.ReadAllText(logPath);
                                    errors.Add($"Compilation log ({Path.GetFileName(logPath)}): {logContent}");
                                    LogError($"[CodeRunner] 编译日志: {logContent}");
                                    break;
                                }
                            }
                        }
                        catch (Exception logEx)
                        {
                            LogError($"[CodeRunner] 无法读取编译日志: {logEx.Message}");
                        }

                        // 检查临时目录中的所有文件
                        try
                        {
                            tempDir = Path.GetDirectoryName(tempFilePath);
                            if (Directory.Exists(tempDir))
                            {
                                var allFiles = Directory.GetFiles(tempDir);
                                LogInfo($"[CodeRunner] 临时目录中的所有文件: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                                errors.Add($"Temp directory files: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                            }
                        }
                        catch (Exception dirEx)
                        {
                            LogError($"[CodeRunner] 无法检查临时目录: {dirEx.Message}");
                        }

                        return (false, null, errors.ToArray());
                    }
                }
                finally
                {
                    // 清理由调用方负责
                }
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] Exception occurred during compilation: {e.Message}");
                return (false, null, new[] { $"Compilation exception: {e.Message}" });
            }
        }

        /// <summary>
        /// 执行完整代码（自动查找可执行方法）
        /// </summary>
        private List<ExecutionResult> ExecuteCompleteCode(ReflectionAssembly assembly, object[] parameters, bool returnOutput)
        {
            var results = new List<ExecutionResult>();

            try
            {
                // 获取程序集中的所有类型
                var types = assembly.GetTypes();
                LogInfo($"[CodeRunner] 在完整代码中找到 {types.Length} 个类型");

                foreach (var type in types)
                {
                    // 查找所有公共静态方法
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

                    foreach (var method in methods)
                    {
                        // 跳过特殊方法和属性访问器
                        if (method.IsSpecialName || method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
                            continue;

                        LogInfo($"[CodeRunner] 尝试执行方法: {type.FullName}.{method.Name}");

                        var executionResult = new ExecutionResult
                        {
                            MethodName = $"{type.Name}.{method.Name}"
                        };

                        var startTime = DateTime.Now;

                        // 准备控制台输出捕获
                        StringWriter outputWriter = null;
                        TextWriter originalOutput = null;

                        try
                        {
                            if (returnOutput)
                            {
                                outputWriter = new StringWriter();
                                originalOutput = Console.Out;
                                Console.SetOut(outputWriter);
                            }

                            // 准备方法参数
                            var methodParameters = method.GetParameters();
                            object[] actualParameters = null;

                            if (methodParameters.Length > 0)
                            {
                                actualParameters = new object[methodParameters.Length];
                                for (int i = 0; i < methodParameters.Length && i < parameters.Length; i++)
                                {
                                    try
                                    {
                                        actualParameters[i] = Convert.ChangeType(parameters[i], methodParameters[i].ParameterType);
                                    }
                                    catch
                                    {
                                        actualParameters[i] = parameters[i];
                                    }
                                }
                            }

                            // 执行方法
                            var returnValue = method.Invoke(null, actualParameters);

                            executionResult.Success = true;
                            executionResult.Message = "Method executed successfully";
                            executionResult.ReturnValue = returnValue;

                            LogInfo($"[CodeRunner] 方法 {method.Name} 执行成功");

                            if (returnValue != null)
                            {
                                LogInfo($"[CodeRunner] 方法返回值: {returnValue}");
                            }
                        }
                        catch (TargetInvocationException tie)
                        {
                            var innerException = tie.InnerException ?? tie;
                            executionResult.Success = false;
                            executionResult.Message = innerException.Message;
                            executionResult.StackTrace = innerException.StackTrace;
                            LogError($"[CodeRunner] 方法 {method.Name} 执行失败: {innerException.Message}");
                        }
                        catch (Exception e)
                        {
                            executionResult.Success = false;
                            executionResult.Message = e.Message;
                            executionResult.StackTrace = e.StackTrace;
                            LogError($"[CodeRunner] 方法 {method.Name} 执行异常: {e.Message}");
                        }
                        finally
                        {
                            // 恢复控制台输出
                            if (returnOutput && originalOutput != null)
                            {
                                Console.SetOut(originalOutput);
                                executionResult.Output = outputWriter?.ToString() ?? "";
                                outputWriter?.Dispose();
                            }

                            executionResult.Duration = (DateTime.Now - startTime).TotalMilliseconds;
                        }

                        results.Add(executionResult);
                        LogInfo($"[CodeRunner] 方法 {method.Name}: {(executionResult.Success ? "SUCCESS" : "FAILED")} ({executionResult.Duration:F2}ms)");

                        // 如果方法执行成功，通常只执行第一个找到的方法
                        if (executionResult.Success)
                        {
                            return results;
                        }
                    }
                }

                // 如果没有找到任何可执行的方法
                if (results.Count == 0)
                {
                    results.Add(new ExecutionResult
                    {
                        MethodName = "Unknown",
                        Success = false,
                        Message = "No executable public static methods found in the assembly",
                        Output = "",
                        Duration = 0
                    });
                }
            }
            catch (Exception e)
            {
                LogError($"[CodeRunner] 执行完整代码时发生异常: {e.Message}");
                results.Add(new ExecutionResult
                {
                    MethodName = "Unknown",
                    Success = false,
                    Message = $"Failed to execute complete code: {e.Message}",
                    Output = "",
                    Duration = 0,
                    StackTrace = e.StackTrace
                });
            }

            return results;
        }

        /// <summary>
        /// 执行编译后的代码
        /// </summary>
        private List<ExecutionResult> ExecuteCompiledCode(ReflectionAssembly assembly, string namespaceName, string className, string methodName, object[] parameters, bool returnOutput)
        {
            var results = new List<ExecutionResult>();
            var fullClassName = $"{namespaceName}.{className}";
            var codeType = assembly.GetType(fullClassName);

            if (codeType == null)
            {
                throw new Exception($"Code class not found: {fullClassName}");
            }

            // 查找指定的方法
            var targetMethod = codeType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            if (targetMethod == null)
            {
                // 如果找不到指定方法，尝试查找任何public方法
                var allMethods = codeType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                targetMethod = allMethods.FirstOrDefault(m => !m.IsSpecialName && m.DeclaringType == codeType);

                if (targetMethod != null)
                {
                    LogWarning($"[CodeRunner] Method '{methodName}' not found, using '{targetMethod.Name}' instead");
                    methodName = targetMethod.Name;
                }
            }

            if (targetMethod == null)
            {
                throw new Exception($"No suitable method found in class {fullClassName}");
            }

            var executionResult = new ExecutionResult
            {
                MethodName = methodName
            };

            var startTime = DateTime.Now;

            // 准备控制台输出捕获
            StringWriter outputWriter = null;
            TextWriter originalOutput = null;

            try
            {
                if (returnOutput)
                {
                    outputWriter = new StringWriter();
                    originalOutput = Console.Out;
                    Console.SetOut(outputWriter);
                }

                // 创建实例（如果需要）
                object instance = null;
                if (!targetMethod.IsStatic)
                {
                    instance = Activator.CreateInstance(codeType);
                }

                // 准备方法参数
                var methodParameters = targetMethod.GetParameters();
                object[] actualParameters = null;

                if (methodParameters.Length > 0)
                {
                    actualParameters = new object[methodParameters.Length];
                    for (int i = 0; i < methodParameters.Length && i < parameters.Length; i++)
                    {
                        try
                        {
                            // 尝试转换参数类型
                            actualParameters[i] = Convert.ChangeType(parameters[i], methodParameters[i].ParameterType);
                        }
                        catch
                        {
                            actualParameters[i] = parameters[i];
                        }
                    }
                }

                // 执行方法
                var returnValue = targetMethod.Invoke(instance, actualParameters);

                executionResult.Success = true;
                executionResult.Message = "Code executed successfully";
                executionResult.ReturnValue = returnValue;

                LogInfo($"[CodeRunner] Method {methodName} executed successfully");

                // 如果方法执行了Unity相关操作，确保它们被正确记录
                if (returnValue != null)
                {
                    LogInfo($"[CodeRunner] Method returned: {returnValue}");
                }
            }
            catch (TargetInvocationException tie)
            {
                var innerException = tie.InnerException ?? tie;
                executionResult.Success = false;
                executionResult.Message = innerException.Message;
                executionResult.StackTrace = innerException.StackTrace;
                LogError($"[CodeRunner] Method {methodName} failed: {innerException.Message}");
                Debug.LogException(innerException);
            }
            catch (Exception e)
            {
                executionResult.Success = false;
                executionResult.Message = e.Message;
                executionResult.StackTrace = e.StackTrace;
                LogError($"[CodeRunner] Method {methodName} failed: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                // 恢复控制台输出
                if (returnOutput && originalOutput != null)
                {
                    Console.SetOut(originalOutput);
                    executionResult.Output = outputWriter?.ToString() ?? "";
                    outputWriter?.Dispose();
                }

                executionResult.Duration = (DateTime.Now - startTime).TotalMilliseconds;
            }

            results.Add(executionResult);
            LogInfo($"[CodeRunner] Method {methodName}: {(executionResult.Success ? "SUCCESS" : "FAILED")} ({executionResult.Duration:F2}ms)");

            return results;
        }


        /// <summary>
        /// 获取默认的using语句
        /// </summary>
        private string[] GetDefaultIncludes()
        {
            return new[]
            {
                "System",
                "System.Collections",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "System.IO",
                "UnityEngine",
                "UnityEditor",
                "System.Reflection"
            };
        }

        /// <summary>
        /// 根据代码内容生成哈希值作为临时目录名
        /// </summary>
        private string GetCodeHash(string code)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(code);
                var hash = sha256.ComputeHash(bytes);
                // 取前8个字节转换为16进制字符串
                var hashString = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLower();
                LogInfo($"[CodeRunner] 代码哈希值: {hashString}");
                return hashString;
            }
        }

        /// <summary>
        /// 清理临时目录
        /// </summary>
        private void CleanupTempDirectory(string tempDir)
        {
            if (string.IsNullOrEmpty(tempDir) || !Directory.Exists(tempDir))
                return;
            // 判断临时目录下是否存在dll文件，如果没有则不进行清理
            try
            {
                if (!Directory.Exists(tempDir))
                    return;

                var dllFiles = Directory.GetFiles(tempDir, "*.dll", SearchOption.AllDirectories);
                if (dllFiles == null || dllFiles.Length == 0)
                {
                    LogInfo($"[CodeRunner] 临时目录中未找到dll文件，无需清理: {tempDir}");
                    return;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[CodeRunner] 检查临时目录dll文件时发生异常: {tempDir}, 错误: {ex.Message}");
                return;
            }

            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    LogInfo($"[CodeRunner] 临时目录清理成功: {tempDir}");
                    return; // 成功删除，退出
                }
                catch (IOException ex)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        LogInfo($"[CodeRunner] 清理临时目录失败，重试 {retryCount}/{maxRetries}: {tempDir}");
                        System.Threading.Thread.Sleep(100 * retryCount);
                    }
                    else
                    {
                        LogWarning($"[CodeRunner] 无法清理临时目录: {tempDir}, 错误: {ex.Message}");
                        // 尝试逐个删除文件
                        CleanupDirectoryFiles(tempDir);
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[CodeRunner] 清理临时目录时发生意外错误: {tempDir}, 错误: {ex.Message}");
                    break; // 非IO错误，不重试
                }
            }
        }

        /// <summary>
        /// 尝试逐个删除目录中的文件
        /// </summary>
        private void CleanupDirectoryFiles(string tempDir)
        {
            try
            {
                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    CleanupSingleFile(file);
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[CodeRunner] 无法枚举临时目录文件: {tempDir}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理临时文件（保留原方法以兼容）
        /// </summary>
        private void CleanupTempFiles(string tempFilePath, string tempAssemblyPath)
        {
            // 获取临时目录并清理整个目录
            if (!string.IsNullOrEmpty(tempFilePath))
            {
                var tempDir = Path.GetDirectoryName(tempFilePath);
                CleanupTempDirectory(tempDir);
            }
        }

        /// <summary>
        /// 清理单个文件，带重试机制
        /// </summary>
        private void CleanupSingleFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    File.Delete(filePath);
                    return; // 成功删除，退出
                }
                catch (IOException ex)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        LogInfo($"[CodeRunner] Failed to clean file, retry {retryCount}/{maxRetries}: {filePath}");
                        System.Threading.Thread.Sleep(100 * retryCount);
                    }
                    else
                    {
                        LogWarning($"[CodeRunner] Unable to clean temporary file: {filePath}, error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[CodeRunner] Unexpected error occurred while cleaning file: {filePath}, error: {ex.Message}");
                    break; // 非IO错误，不重试
                }
            }
        }
    }
}