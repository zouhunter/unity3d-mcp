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
    [ToolName("test_runner")]
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
        private object HandleExecuteTest(JObject args)
        {
            LogInfo("[TestRunner] Executing test code");
            return ExecuteTestAsync(args);
        }

        /// <summary>
        /// 处理验证测试代码操作
        /// </summary>
        private object HandleValidateTest(JObject args)
        {
            LogInfo("[TestRunner] Validating test code");
            return ValidateTestCode(args);
        }

        // --- 异步执行方法 ---

        /// <summary>
        /// 异步执行测试代码
        /// </summary>
        private object ExecuteTestAsync(JObject args)
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
                int timeout = args["timeout"]?.ToObject<int>() ?? 30;
                bool cleanup = args["cleanup"]?.ToObject<bool>() ?? true;

                LogInfo($"[TestRunner] Executing test class: {namespaceName}.{className}");

                return ExecuteTest(testCode, className, namespaceName, includes, timeout, cleanup);
            }
            catch (Exception e)
            {
                LogError($"[TestRunner] Test execution failed: {e.Message}");
                return Response.Error($"Failed to execute test: {e.Message}");
            }
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
                        errors = compilationResult.errors
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
                        errors = compilationResult.errors
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
        /// 编译代码
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

                    // 使用Unity编译流水线编译
                    var assemblyBuilder = new AssemblyBuilder(tempAssemblyPath, new[] { tempFilePath });

                    // 添加必要的引用
                    var references = new List<string>();

                    // 获取Unity引擎和编辑器程序集
                    foreach (var assembly in CompilationPipeline.GetAssemblies())
                    {
                        if (assembly.name.Contains("UnityEngine") ||
                            assembly.name.Contains("UnityEditor") ||
                            assembly.name.Contains("nunit.framework"))
                        {
                            references.AddRange(assembly.compiledAssemblyReferences);
                        }
                    }

                    // 添加基础引用
                    references.Add(typeof(object).Assembly.Location); // mscorlib
                    references.Add(typeof(System.Linq.Enumerable).Assembly.Location); // System.Core
                    references.Add(typeof(UnityEngine.Debug).Assembly.Location); // UnityEngine
                    references.Add(typeof(UnityEditor.EditorApplication).Assembly.Location); // UnityEditor

                    // 查找NUnit程序集
                    var nunitAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("nunit.framework"));
                    if (nunitAssembly != null)
                    {
                        references.Add(nunitAssembly.Location);
                    }

                    assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                    assemblyBuilder.additionalReferences = references.Distinct().ToArray();

                    // 开始编译，添加重试机制
                    bool started = false;
                    int retryCount = 0;
                    const int maxRetries = 3;

                    while (!started && retryCount < maxRetries)
                    {
                        try
                        {
                            started = assemblyBuilder.Build();
                            if (!started)
                            {
                                retryCount++;
                                if (retryCount < maxRetries)
                                {
                                    LogInfo($"[TestRunner] Compilation startup failed, retry {retryCount}/{maxRetries}");
                                    System.Threading.Thread.Sleep(100 * retryCount); // 递增延迟

                                    // 创建新的AssemblyBuilder
                                    var newAssemblyPath = Path.ChangeExtension(tempFilePath, $"_retry{retryCount}.dll");
                                    assemblyBuilder = new AssemblyBuilder(newAssemblyPath, new[] { tempFilePath });
                                    assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
                                    assemblyBuilder.additionalReferences = references.Distinct().ToArray();
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
                                assemblyBuilder.additionalReferences = references.Distinct().ToArray();
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
                    var timeout = DateTime.Now.AddSeconds(30); // 30秒超时
                    while (assemblyBuilder.status == AssemblyBuilderStatus.IsCompiling)
                    {
                        if (DateTime.Now > timeout)
                        {
                            return (false, null, new[] { "Compilation timeout" });
                        }
                        System.Threading.Thread.Sleep(50);
                    }

                    if (assemblyBuilder.status == AssemblyBuilderStatus.Finished)
                    {
                        // 加载编译后的程序集
                        var assemblyPath = assemblyBuilder.assemblyPath;
                        if (File.Exists(assemblyPath))
                        {
                            var assemblyBytes = File.ReadAllBytes(assemblyPath);
                            var loadedAssembly = ReflectionAssembly.Load(assemblyBytes);
                            return (true, loadedAssembly, null);
                        }
                        else
                        {
                            return (false, null, new[] { "Compiled assembly file not found" });
                        }
                    }
                    else
                    {
                        var errors = new List<string> { $"Compilation failed with status: {assemblyBuilder.status}" };

                        // 尝试获取编译错误信息
                        try
                        {
                            var logPath = Path.ChangeExtension(assemblyBuilder.assemblyPath, ".log");
                            if (File.Exists(logPath))
                            {
                                var logContent = File.ReadAllText(logPath);
                                errors.Add($"Compilation log: {logContent}");
                            }
                        }
                        catch
                        {
                            // 忽略获取日志错误
                        }

                        return (false, null, errors.ToArray());
                    }
                }
                finally
                {
                    // 清理临时文件
                    CleanupTempFiles(tempFilePath, tempAssemblyPath);
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
                    testResult.Success = false;
                    testResult.Message = innerException.Message;
                    testResult.StackTrace = innerException.StackTrace;
                }
                catch (Exception e)
                {
                    testResult.Success = false;
                    testResult.Message = e.Message;
                    testResult.StackTrace = e.StackTrace;
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