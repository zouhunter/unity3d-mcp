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
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempFilePath) || !string.IsNullOrEmpty(tempAssemblyPath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath, tempAssemblyPath);
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
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempFilePath) || !string.IsNullOrEmpty(tempAssemblyPath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath, tempAssemblyPath);
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
                            // 执行代码
                            var executionResults = ExecuteCompiledCode(assembly, namespaceName, className, methodName, parameters, returnOutput);
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
            // 创建临时目录和文件
            var tempDir = Path.Combine(Application.temporaryCachePath, "TestRunner");
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

                var timestamp = DateTime.Now.Ticks;
                var randomId = UnityEngine.Random.Range(1000, 9999);
                var tempFileName = $"TestClass_{timestamp}_{randomId}.cs";
                tempFilePath = Path.Combine(tempDir, tempFileName);
                tempAssemblyPath = Path.ChangeExtension(tempFilePath, ".dll");

                // 写入代码到临时文件
                File.WriteAllText(tempFilePath, code);
                LogInfo($"[TestRunner] 临时文件路径: {tempFilePath}");
                LogInfo($"[TestRunner] 目标程序集路径: {tempAssemblyPath}");

                // 通知上层函数临时文件路径
                onTempFilesCreated?.Invoke(tempFilePath, tempAssemblyPath);
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] 初始化失败: {e.Message}");
                callback(false, null, new[] { $"Initialization failed: {e.Message}" });
                yield break;
            }

            // 设置编译器
            try
            {
                assemblyBuilder = new AssemblyBuilder(tempAssemblyPath, new[] { tempFilePath });

                // 收集程序集引用
                var references = new List<string>();
                LogInfo("[TestRunner] 开始收集程序集引用...");

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
                LogInfo($"[TestRunner] 收集到 {uniqueReferences.Length} 个有效引用");

                assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                assemblyBuilder.additionalReferences = uniqueReferences;
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] 编译器设置失败: {e.Message}");
                callback(false, null, new[] { $"Compiler setup failed: {e.Message}" });
                yield break;
            }

            // 启动编译
            LogInfo("[TestRunner] 尝试启动编译...");
            bool started = false;

            try
            {
                started = assemblyBuilder.Build();
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] 编译启动异常: {e.Message}");
                callback(false, null, new[] { $"Failed to start compilation: {e.Message}" });
                yield break;
            }

            if (!started)
            {
                LogError("[TestRunner] 无法启动编译");
                callback(false, null, new[] { "Failed to start compilation" });
                yield break;
            }

            LogInfo("[TestRunner] 编译已启动，等待完成...");

            // 使用协程等待编译完成
            float timeout = 30f;
            float elapsedTime = 0f;

            while (assemblyBuilder.status == AssemblyBuilderStatus.IsCompiling && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    LogInfo($"[TestRunner] 编译中... 状态: {assemblyBuilder.status}, 已等待: {elapsedTime:F1}s");
                }
            }

            LogInfo($"[TestRunner] 编译完成, 状态: {assemblyBuilder.status}");

            // 处理编译结果
            if (assemblyBuilder.status == AssemblyBuilderStatus.Finished)
            {
                yield return HandleCompilationSuccess(assemblyBuilder, callback);
            }
            else if (elapsedTime >= timeout)
            {
                LogError("[TestRunner] 编译超时");
                callback(false, null, new[] { "Compilation timeout" });
            }
            else
            {
                LogError($"[TestRunner] 编译失败, 状态: {assemblyBuilder.status}");
                callback(false, null, new[] { $"Compilation failed with status: {assemblyBuilder.status}" });
            }

            // 清理由上层函数负责
        }

        /// <summary>
        /// 处理编译成功后的程序集加载
        /// </summary>
        private IEnumerator HandleCompilationSuccess(AssemblyBuilder assemblyBuilder, System.Action<bool, ReflectionAssembly, string[]> callback)
        {
            var assemblyPath = assemblyBuilder.assemblyPath;
            float waitTime = 0f;
            const float maxWaitTime = 2f;

            // 等待文件存在
            while (!File.Exists(assemblyPath) && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }

            if (File.Exists(assemblyPath))
            {
                // 等待文件写入完成
                yield return new WaitForSeconds(0.1f);

                try
                {
                    var assemblyBytes = File.ReadAllBytes(assemblyPath);
                    if (assemblyBytes.Length > 0)
                    {
                        var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                        LogInfo($"[TestRunner] 程序集加载成功: {assemblyBytes.Length} bytes");
                        callback(true, loadedAssembly, null);
                    }
                    else
                    {
                        callback(false, null, new[] { "Assembly file is empty" });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[TestRunner] 无法加载程序集: {ex.Message}");
                    callback(false, null, new[] { $"Failed to load assembly: {ex.Message}" });
                }
            }
            else
            {
                LogError($"[TestRunner] 程序集文件不存在: {assemblyPath}");
                callback(false, null, new[] { "Assembly file not found after compilation" });
            }
        }

        /// <summary>
        /// 原始的同步编译代码（保留作为后备）
        /// </summary>
        private (bool success, ReflectionAssembly assembly, string[] errors) CompileCode(string code)
        {
            try
            {
                // 创建临时目录和文件
                var tempDir = Path.Combine(Application.temporaryCachePath, "TestRunner");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                // 使用时间戳和随机数创建更唯一的文件名
                var timestamp = DateTime.Now.Ticks;
                var randomId = UnityEngine.Random.Range(1000, 9999);
                var tempFileName = $"TestClass_{timestamp}_{randomId}.cs";
                var tempFilePath = Path.Combine(tempDir, tempFileName);
                var tempAssemblyPath = Path.ChangeExtension(tempFilePath, ".dll");

                try
                {
                    // 写入代码到临时文件
                    File.WriteAllText(tempFilePath, code);

                    LogInfo($"[TestRunner] 临时文件路径: {tempFilePath}");
                    LogInfo($"[TestRunner] 目标程序集路径: {tempAssemblyPath}");
                    LogInfo($"[TestRunner] 代码长度: {code.Length} 字符");

                    // 使用Unity编译流水线编译
                    var assemblyBuilder = new AssemblyBuilder(tempAssemblyPath, new[] { tempFilePath });

                    // 添加必要的引用
                    var references = new List<string>();

                    LogInfo("[TestRunner] 开始收集程序集引用...");

                    // 获取所有程序集引用，包括Assembly-CSharp和所有package程序集
                    foreach (var assembly in CompilationPipeline.GetAssemblies())
                    {
                        LogInfo($"[TestRunner] 处理程序集: {assembly.name}");

                        // 添加程序集的所有引用
                        references.AddRange(assembly.compiledAssemblyReferences);

                        // 如果程序集已经编译，也添加它的路径（包括Assembly-CSharp等）
                        if (!string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath))
                        {
                            references.Add(assembly.outputPath);
                            LogInfo($"[TestRunner] 添加编译后程序集: {assembly.outputPath}");
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
                                LogInfo($"[TestRunner] 添加运行时程序集: {loadedAssembly.GetName().Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略无法访问的程序集
                            LogInfo($"[TestRunner] 无法访问程序集 {loadedAssembly.GetName().Name}: {ex.Message}");
                        }
                    }

                    // 添加基础引用
                    references.Add(typeof(object).Assembly.Location); // mscorlib
                    references.Add(typeof(System.Linq.Enumerable).Assembly.Location); // System.Core
                    references.Add(typeof(UnityEngine.Debug).Assembly.Location); // UnityEngine
                    references.Add(typeof(UnityEditor.EditorApplication).Assembly.Location); // UnityEditor

                    LogInfo($"[TestRunner] 总共收集到 {references.Count} 个程序集引用");

                    // 去除重复引用
                    var uniqueReferences = references.Distinct().Where(r => !string.IsNullOrEmpty(r) && File.Exists(r)).ToArray();
                    LogInfo($"[TestRunner] 去除重复后有 {uniqueReferences.Length} 个有效引用");

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
                            LogInfo($"[TestRunner] 尝试启动编译 ({retryCount + 1}/{maxRetries})...");
                            started = assemblyBuilder.Build();

                            if (started)
                            {
                                LogInfo($"[TestRunner] 编译启动成功");
                            }
                            else
                            {
                                retryCount++;
                                LogError($"[TestRunner] 编译启动失败, 重试 {retryCount}/{maxRetries}");

                                if (retryCount < maxRetries)
                                {
                                    System.Threading.Thread.Sleep(100 * retryCount); // 递增延迟

                                    // 创建新的AssemblyBuilder
                                    var newAssemblyPath = Path.ChangeExtension(tempFilePath, $"_retry{retryCount}.dll");
                                    LogInfo($"[TestRunner] 创建新的AssemblyBuilder: {newAssemblyPath}");
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
                                LogInfo($"[TestRunner] Compilation exception, retry {retryCount}/{maxRetries}: {ex.Message}");
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
                    LogInfo($"[TestRunner] 开始等待编译完成, 初始状态: {assemblyBuilder.status}");
                    var timeout = DateTime.Now.AddSeconds(30); // 30秒超时
                    int waitCount = 0;

                    while (assemblyBuilder.status == AssemblyBuilderStatus.IsCompiling)
                    {
                        if (DateTime.Now > timeout)
                        {
                            LogError($"[TestRunner] 编译超时, 最终状态: {assemblyBuilder.status}");
                            return (false, null, new[] { "Compilation timeout" });
                        }
                        System.Threading.Thread.Sleep(50);
                        waitCount++;

                        // 每秒输出一次状态
                        if (waitCount % 20 == 0)
                        {
                            LogInfo($"[TestRunner] 编译中... 当前状态: {assemblyBuilder.status}, 已等待 {waitCount * 50}ms");
                        }
                    }

                    LogInfo($"[TestRunner] 编译完成, 最终状态: {assemblyBuilder.status}");

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
                                    LogInfo($"[TestRunner] Assembly loaded successfully: {assemblyPath} ({assemblyBytes.Length} bytes)");
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
                        LogError($"[TestRunner] 编译失败, 状态: {assemblyBuilder.status}");

                        // 检查程序集文件是否存在（有时状态不准确）
                        if (File.Exists(assemblyBuilder.assemblyPath))
                        {
                            LogInfo($"[TestRunner] 程序集文件存在，尝试加载: {assemblyBuilder.assemblyPath}");
                            try
                            {
                                var assemblyBytes = File.ReadAllBytes(assemblyBuilder.assemblyPath);
                                if (assemblyBytes.Length > 0)
                                {
                                    var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                                    LogInfo($"[TestRunner] 程序集加载成功，尽管状态显示失败");
                                    return (true, loadedAssembly, null);
                                }
                            }
                            catch (Exception loadEx)
                            {
                                LogError($"[TestRunner] 无法加载程序集: {loadEx.Message}");
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
                                    LogError($"[TestRunner] 编译日志: {logContent}");
                                    break;
                                }
                            }
                        }
                        catch (Exception logEx)
                        {
                            LogError($"[TestRunner] 无法读取编译日志: {logEx.Message}");
                        }

                        // 检查临时目录中的所有文件
                        try
                        {
                            tempDir = Path.GetDirectoryName(tempFilePath);
                            if (Directory.Exists(tempDir))
                            {
                                var allFiles = Directory.GetFiles(tempDir);
                                LogInfo($"[TestRunner] 临时目录中的所有文件: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                                errors.Add($"Temp directory files: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
                            }
                        }
                        catch (Exception dirEx)
                        {
                            LogError($"[TestRunner] 无法检查临时目录: {dirEx.Message}");
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
                LogError($"[TestRunner] Exception occurred during compilation: {e.Message}");
                return (false, null, new[] { $"Compilation exception: {e.Message}" });
            }
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
        /// 清理临时文件
        /// </summary>
        private void CleanupTempFiles(string tempFilePath, string tempAssemblyPath)
        {
            var filesToClean = new List<string> { tempFilePath };

            // 添加可能的程序集相关文件
            var assemblyDir = Path.GetDirectoryName(tempAssemblyPath);
            var assemblyNameWithoutExt = Path.GetFileNameWithoutExtension(tempAssemblyPath);

            if (!string.IsNullOrEmpty(assemblyDir))
            {
                try
                {
                    var potentialFiles = Directory.GetFiles(assemblyDir, $"{assemblyNameWithoutExt}*");
                    filesToClean.AddRange(potentialFiles);
                }
                catch
                {
                    // 忽略获取文件列表的错误
                }
            }

            foreach (var file in filesToClean)
            {
                CleanupSingleFile(file);
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
                        LogInfo($"[TestRunner] Failed to clean file, retry {retryCount}/{maxRetries}: {filePath}");
                        System.Threading.Thread.Sleep(100 * retryCount);
                    }
                    else
                    {
                        LogWarning($"[TestRunner] Unable to clean temporary file: {filePath}, error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[TestRunner] Unexpected error occurred while cleaning file: {filePath}, error: {ex.Message}");
                    break; // 非IO错误，不重试
                }
            }
        }
    }
}