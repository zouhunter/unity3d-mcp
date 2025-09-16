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
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.Compilation;
using CompilationAssembly = UnityEditor.Compilation.Assembly;
using ReflectionAssembly = System.Reflection.Assembly;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles C# test script execution including compilation and running test methods.
    /// 对应方法名: test_runner
    /// </summary>
    [ToolName("test_runner", "开发工具")]
    public class TestRunner : StateMethodBase
    {
        // Test execution tracking
        private class TestOperation
        {
            public TaskCompletionSource<object> CompletionSource { get; set; }
            public string TestCode { get; set; }
            public string ClassName { get; set; }
            public List<TestResult> Results { get; set; } = new List<TestResult>();
        }

        private class TestResult
        {
            public string TestName { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public double Duration { get; set; }
        }

        // Queue of active test operations
        private readonly List<TestOperation> _activeOperations = new List<TestOperation>();

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
                new MethodKey("test_code", "C# test code content", true),
                new MethodKey("class_name", "Test class name, default is TestClass", true),
                new MethodKey("namespace", "Namespace, default is TestNamespace", true),
                new MethodKey("includes", "Referenced using statements list, JSON array format", true),
                new MethodKey("timeout", "Execution timeout (seconds), default 30 seconds", true),
                new MethodKey("cleanup", "Whether to clean up temporary files after execution, default true", true)
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
                    .Leaf("execute", HandleExecuteTest)
                    .Leaf("validate", HandleValidateTest)
                    .DefaultLeaf(HandleExecuteTest)
                .Build();
        }

        // --- 测试执行操作处理方法 ---

        /// <summary>
        /// 处理执行测试操作
        /// </summary>
        private object HandleExecuteTest(StateTreeContext ctx)
        {
            LogInfo("[TestRunner] Executing test code");
            return ctx.AsyncReturn(ExecuteTestCoroutine(ctx.JsonData));
        }

        /// <summary>
        /// 处理验证测试代码操作
        /// </summary>
        private object HandleValidateTest(StateTreeContext ctx)
        {
            LogInfo("[TestRunner] Validating test code");
            return ctx.AsyncReturn(ValidateTestCoroutine(ctx.JsonData));
        }

        // --- 异步执行方法 ---

        /// <summary>
        /// 异步执行测试代码协程
        /// </summary>
        private IEnumerator ExecuteTestCoroutine(JObject args)
        {
            string tempFilePath = null;
            string tempAssemblyPath = null;

            try
            {
                string testCode = args["test_code"]?.ToString();
                if (string.IsNullOrEmpty(testCode))
                {
                    yield return Response.Error("test_code parameter is required");
                    yield break;
                }

                string className = args["class_name"]?.ToString() ?? "TestClass";
                string namespaceName = args["namespace"]?.ToString() ?? "TestNamespace";
                var includes = args["includes"]?.ToObject<string[]>() ?? GetDefaultIncludes();
                int timeout = args["timeout"]?.ToObject<int>() ?? 30;
                bool cleanup = args["cleanup"]?.ToObject<bool>() ?? true;

                LogInfo($"[TestRunner] Executing test class: {namespaceName}.{className}");

                // 使用协程执行测试
                yield return ExecuteTestCoroutineInternal(testCode, className, namespaceName, includes, timeout, cleanup,
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
        /// 验证测试代码协程
        /// </summary>
        private IEnumerator ValidateTestCoroutine(JObject args)
        {
            string tempFilePath = null;
            string tempAssemblyPath = null;

            try
            {
                string testCode = args["test_code"]?.ToString();
                if (string.IsNullOrEmpty(testCode))
                {
                    yield return Response.Error("test_code parameter is required");
                    yield break;
                }

                string className = args["class_name"]?.ToString() ?? "TestClass";
                string namespaceName = args["namespace"]?.ToString() ?? "TestNamespace";
                var includes = args["includes"]?.ToObject<string[]>() ?? GetDefaultIncludes();

                LogInfo($"[TestRunner] Validating test class: {namespaceName}.{className}");

                // 在协程外部处理异常
                validationResult = null;
                string fullCode = testCode;

                bool failed = false;
                try
                {
                    fullCode = GenerateFullTestCode(testCode, className, namespaceName, includes);
                    LogInfo($"[TestRunner] Generated code for validation");
                }
                catch (Exception e)
                {
                    LogError($"[TestRunner] Failed to generate validation code: {e.Message}");
                    validationResult = Response.Error($"Failed to generate test code: {e.Message}");
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
                                "Test code syntax is valid", new
                                {
                                    operation = "validate",
                                    class_name = className,
                                    namespace_name = namespaceName,
                                    generated_code = fullCode
                                });
                        }
                        else
                        {
                            validationResult = Response.Error("Test code syntax validation failed", new
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
        /// 执行测试代码的内部协程
        /// </summary>
        private IEnumerator ExecuteTestCoroutineInternal(string testCode, string className, string namespaceName, string[] includes, int timeout, bool cleanup, System.Action<string, string> onTempFilesCreated = null)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            executionResult = null;

            // 生成完整的测试代码
            string fullCode = testCode;
            bool failed = false;
            try
            {
                fullCode = GenerateFullTestCode(testCode, className, namespaceName, includes);
                LogInfo($"[TestRunner] Generated complete code");
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] Failed to generate code: {e.Message}");
                executionResult = Response.Error($"Failed to generate test code: {e.Message}");
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
                            // 执行测试
                            var testResults = ExecuteCompiledTests(assembly, namespaceName, className);
                            executionResult = Response.Success(
                                $"Test execution completed, {testResults.Count} tests executed",
                                new
                                {
                                    operation = "execute",
                                    class_name = className,
                                    namespace_name = namespaceName,
                                    test_count = testResults.Count,
                                    passed_count = testResults.Count(r => r.Success),
                                    failed_count = testResults.Count(r => !r.Success),
                                    results = testResults.Select(r => new
                                    {
                                        test_name = r.TestName,
                                        success = r.Success,
                                        message = r.Message,
                                        stack_trace = r.StackTrace,
                                        duration = r.Duration
                                    }).ToArray()
                                }
                            );
                        }
                        catch (Exception e)
                        {
                            LogError($"[TestRunner] Test execution failed: {e.Message}");
                            executionResult = Response.Error($"Failed to execute compiled tests: {e.Message}");
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
        /// 验证测试代码语法
        /// </summary>
        private object ValidateTestCode(JObject args)
        {
            try
            {
                string testCode = args["test_code"]?.ToString();
                if (string.IsNullOrEmpty(testCode))
                {
                    return Response.Error("test_code parameter is required");
                }

                string className = args["class_name"]?.ToString() ?? "TestClass";
                string namespaceName = args["namespace"]?.ToString() ?? "TestNamespace";
                var includes = args["includes"]?.ToObject<string[]>() ?? GetDefaultIncludes();

                LogInfo("[TestRunner] Validating test code syntax");

                string fullCode = GenerateFullTestCode(testCode, className, namespaceName, includes);
                var compilationResult = CompileCode(fullCode);

                if (compilationResult.success)
                {
                    return Response.Success("Test code syntax validation passed", new
                    {
                        operation = "validate",
                        class_name = className,
                        namespace_name = namespaceName,
                        generated_code = fullCode
                    });
                }
                else
                {
                    return Response.Error("Test code syntax validation failed", new
                    {
                        operation = "validate",
                        errors = string.Join("\n", compilationResult.errors)
                    });
                }
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] Test code validation failed: {e.Message}");
                return Response.Error($"Failed to validate test code: {e.Message}");
            }
        }

        // --- 核心执行方法 ---

        /// <summary>
        /// 执行测试代码
        /// </summary>
        private object ExecuteTest(string testCode, string className, string namespaceName, string[] includes, int timeout, bool cleanup)
        {
            try
            {
                // 生成完整的测试代码
                string fullCode = GenerateFullTestCode(testCode, className, namespaceName, includes);
                LogInfo($"[TestRunner] Generated complete code:\n{fullCode}");

                // 编译代码
                var compilationResult = CompileCode(fullCode);
                if (!compilationResult.success)
                {
                    return Response.Error("Code compilation failed", new
                    {
                        operation = "execute",
                        errors = string.Join("\n", compilationResult.errors ?? new string[] { "Unknown compilation error" })
                    });
                }

                // 执行测试
                var testResults = ExecuteCompiledTests(compilationResult.assembly, namespaceName, className);

                return Response.Success(
                    $"Test execution completed, {testResults.Count} tests executed",
                    new
                    {
                        operation = "execute",
                        class_name = className,
                        namespace_name = namespaceName,
                        test_count = testResults.Count,
                        passed_count = testResults.Count(r => r.Success),
                        failed_count = testResults.Count(r => !r.Success),
                        results = testResults.Select(r => new
                        {
                            test_name = r.TestName,
                            success = r.Success,
                            message = r.Message,
                            stack_trace = r.StackTrace,
                            duration = r.Duration
                        }).ToArray()
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] Error occurred while executing test: {e.Message}");
                return Response.Error($"Failed to execute test: {e.Message}");
            }
        }

        /// <summary>
        /// 生成完整的测试代码
        /// </summary>
        private string GenerateFullTestCode(string testCode, string className, string namespaceName, string[] includes)
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
            sb.AppendLine($"    [TestFixture]");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            // 缩进用户代码
            var lines = testCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                sb.AppendLine($"        {line}");
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
        /// 执行编译后的测试
        /// </summary>
        private List<TestResult> ExecuteCompiledTests(ReflectionAssembly assembly, string namespaceName, string className)
        {
            var results = new List<TestResult>();
            var fullClassName = $"{namespaceName}.{className}";
            var testType = assembly.GetType(fullClassName);

            if (testType == null)
            {
                throw new Exception($"Test class not found: {fullClassName}");
            }

            // 获取所有标记了[Test]特性的方法
            var testMethods = testType.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
                .ToArray();

            if (testMethods.Length == 0)
            {
                LogWarning($"[TestRunner] No methods marked with [Test] attribute found in class {fullClassName}");
            }

            // 创建测试类实例
            var testInstance = Activator.CreateInstance(testType);

            // 执行SetUp方法（如果存在）
            var setupMethod = testType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttributes(typeof(SetUpAttribute), false).Length > 0);

            // 执行TearDown方法（如果存在）
            var teardownMethod = testType.GetMethods()
                .FirstOrDefault(m => m.GetCustomAttributes(typeof(TearDownAttribute), false).Length > 0);

            foreach (var testMethod in testMethods)
            {
                var testResult = new TestResult
                {
                    TestName = testMethod.Name
                };

                var startTime = DateTime.Now;

                try
                {
                    // 执行SetUp
                    setupMethod?.Invoke(testInstance, null);

                    // 执行测试方法
                    testMethod.Invoke(testInstance, null);

                    testResult.Success = true;
                    testResult.Message = "Test passed";
                }
                catch (TargetInvocationException tie)
                {
                    var innerException = tie.InnerException ?? tie;

                    // 检查是否是Assert.Pass抛出的成功异常
                    if (IsTestPassException(innerException))
                    {
                        testResult.Success = true;
                        testResult.Message = innerException.Message;
                        LogInfo($"[TestRunner] Test {testMethod.Name} passed via Assert.Pass: {innerException.Message}");
                    }
                    else
                    {
                        testResult.Success = false;
                        testResult.Message = innerException.Message;
                        testResult.StackTrace = innerException.StackTrace;
                        LogError($"[TestRunner] Test {testMethod.Name} failed: {innerException.Message}");
                        Debug.LogException(innerException);
                    }
                }
                catch (Exception e)
                {
                    testResult.Success = false;
                    testResult.Message = e.Message;
                    testResult.StackTrace = e.StackTrace;
                    Debug.LogException(new Exception($"[TestRunner] Test {testMethod.Name} failed: {e.Message}", e));
                }
                finally
                {
                    try
                    {
                        // 执行TearDown
                        teardownMethod?.Invoke(testInstance, null);
                    }
                    catch (Exception e)
                    {
                        LogWarning($"[TestRunner] TearDown failed for {testMethod.Name}: {e.Message}");
                    }

                    testResult.Duration = (DateTime.Now - startTime).TotalMilliseconds;
                }

                results.Add(testResult);
                LogInfo($"[TestRunner] Test {testMethod.Name}: {(testResult.Success ? "PASSED" : "FAILED")} ({testResult.Duration:F2}ms)");
            }

            return results;
        }

        /// <summary>
        /// 检查异常是否是Assert.Pass抛出的成功异常
        /// </summary>
        private bool IsTestPassException(Exception exception)
        {
            if (exception == null) return false;

            var exceptionTypeName = exception.GetType().Name;
            var exceptionFullTypeName = exception.GetType().FullName;

            // 检查常见的测试通过异常类型
            var passExceptionTypes = new[]
            {
                "SuccessException",
                "PassedException",
                "IgnoreException",
                "InconclusiveException"
            };

            var passFullTypeNames = new[]
            {
                "NUnit.Framework.SuccessException",
                "NUnit.Framework.PassedException",
                "NUnit.Framework.IgnoreException",
                "NUnit.Framework.InconclusiveException"
            };

            // 检查类型名称
            foreach (var passType in passExceptionTypes)
            {
                if (exceptionTypeName.Equals(passType, StringComparison.OrdinalIgnoreCase))
                {
                    LogInfo($"[TestRunner] 检测到测试通过异常类型: {exceptionTypeName}");
                    return true;
                }
            }

            // 检查完整类型名称
            foreach (var passFullType in passFullTypeNames)
            {
                if (exceptionFullTypeName.Equals(passFullType, StringComparison.OrdinalIgnoreCase))
                {
                    LogInfo($"[TestRunner] 检测到测试通过异常完整类型: {exceptionFullTypeName}");
                    return true;
                }
            }

            // 检查异常消息中是否包含"pass"相关关键词（作为后备方案）
            var message = exception.Message?.ToLowerInvariant() ?? "";
            if (message.Contains("passed") || message.Contains("success"))
            {
                LogInfo($"[TestRunner] 根据异常消息判断为测试通过: {exception.Message}");
                return true;
            }

            LogInfo($"[TestRunner] 异常类型不是测试通过异常: {exceptionFullTypeName}");
            return false;
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
                "UnityEngine",
                "UnityEditor",
                "NUnit.Framework"
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