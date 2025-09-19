using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Python script execution including validation and running Python code.
    /// 对应方法名: python_runner
    /// </summary>
    [ToolName("python_runner", "开发工具")]
    public class PythonRunner : StateMethodBase
    {
        // Python execution tracking
        private class PythonOperation
        {
            public string PythonCode { get; set; }
            public string ScriptName { get; set; }
            public List<PythonResult> Results { get; set; } = new List<PythonResult>();
        }

        private class PythonResult
        {
            public string Operation { get; set; }
            public bool Success { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
            public double Duration { get; set; }
            public int ExitCode { get; set; }
        }

        private object validationResult;
        private object executionResult;

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: execute, validate, install_package", false),
                new MethodKey("python_code", "Python script code content", true),
                new MethodKey("script_name", "Python script name, default is script.py", true),
                new MethodKey("python_path", "Path to Python interpreter, default is 'python'", true),
                new MethodKey("working_directory", "Working directory for script execution", true),
                new MethodKey("timeout", "Execution timeout (seconds), default 300 seconds", true),
                new MethodKey("cleanup", "Whether to clean up temporary files after execution, default true", true),
                new MethodKey("packages", "Python packages to install (comma-separated or JSON array)", true),
                new MethodKey("requirements_file", "Path to requirements.txt file", true),
                new MethodKey("virtual_env", "Path to virtual environment to use", true)
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
                    .Leaf("execute", HandleExecutePython)
                    .Leaf("validate", HandleValidatePython)
                    .Leaf("install_package", HandleInstallPackage)
                    .DefaultLeaf(HandleExecutePython)
                .Build();
        }

        // --- Python执行操作处理方法 ---

        /// <summary>
        /// 处理执行Python操作
        /// </summary>
        private object HandleExecutePython(StateTreeContext ctx)
        {
            LogInfo("[PythonRunner] Executing Python code");
            return ctx.AsyncReturn(ExecutePythonCoroutine(ctx.JsonData));
        }

        /// <summary>
        /// 处理验证Python代码操作
        /// </summary>
        private object HandleValidatePython(StateTreeContext ctx)
        {
            LogInfo("[PythonRunner] Validating Python code");
            return ctx.AsyncReturn(ValidatePythonCoroutine(ctx.JsonData));
        }

        /// <summary>
        /// 处理安装Python包操作
        /// </summary>
        private object HandleInstallPackage(StateTreeContext ctx)
        {
            LogInfo("[PythonRunner] Installing Python packages");
            return ctx.AsyncReturn(InstallPackageCoroutine(ctx.JsonData));
        }

        // --- 异步执行方法 ---

        /// <summary>
        /// 异步执行Python代码协程
        /// </summary>
        private IEnumerator ExecutePythonCoroutine(JObject args)
        {
            string tempFilePath = null;

            try
            {
                string pythonCode = args["python_code"]?.ToString();
                if (string.IsNullOrEmpty(pythonCode))
                {
                    yield return Response.Error("python_code parameter is required");
                    yield break;
                }

                string scriptName = args["script_name"]?.ToString() ?? "script.py";
                string pythonPath = args["python_path"]?.ToString() ?? "python";
                string workingDirectory = args["working_directory"]?.ToString() ?? System.Environment.CurrentDirectory;
                int timeout = args["timeout"]?.ToObject<int>() ?? 300;
                bool cleanup = args["cleanup"]?.ToObject<bool>() ?? true;
                string virtualEnv = args["virtual_env"]?.ToString();

                LogInfo($"[PythonRunner] Executing Python script: {scriptName}");

                // 使用协程执行Python
                yield return ExecutePythonCoroutineInternal(pythonCode, scriptName, pythonPath, workingDirectory, timeout, virtualEnv,
                    (tFilePath) => { tempFilePath = tFilePath; });
                yield return executionResult;
            }
            finally
            {
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempFilePath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath);
                }
            }
        }

        /// <summary>
        /// 验证Python代码协程
        /// </summary>
        private IEnumerator ValidatePythonCoroutine(JObject args)
        {
            string tempFilePath = null;

            try
            {
                string pythonCode = args["python_code"]?.ToString();
                if (string.IsNullOrEmpty(pythonCode))
                {
                    yield return Response.Error("python_code parameter is required");
                    yield break;
                }

                string scriptName = args["script_name"]?.ToString() ?? "script.py";
                string pythonPath = args["python_path"]?.ToString() ?? "python";
                string virtualEnv = args["virtual_env"]?.ToString();

                LogInfo($"[PythonRunner] Validating Python script: {scriptName}");

                // 创建临时文件
                var tempDir = Path.Combine(Application.temporaryCachePath, "PythonRunner");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var timestamp = DateTime.Now.Ticks;
                var randomId = UnityEngine.Random.Range(1000, 9999);
                var tempFileName = $"{Path.GetFileNameWithoutExtension(scriptName)}_{timestamp}_{randomId}.py";
                tempFilePath = Path.Combine(tempDir, tempFileName);

                File.WriteAllText(tempFilePath, pythonCode, Encoding.UTF8);

                // 验证Python语法
                yield return ValidatePythonSyntax(tempFilePath, pythonPath, virtualEnv, (success, output, error) =>
                {
                    if (success)
                    {
                        validationResult = Response.Success("Python code syntax is valid", new
                        {
                            operation = "validate",
                            script_name = scriptName,
                            python_path = pythonPath,
                            temp_file = tempFilePath
                        });
                    }
                    else
                    {
                        validationResult = Response.Error("Python code syntax validation failed", new
                        {
                            operation = "validate",
                            error = error,
                            output = output
                        });
                    }
                });

                yield return validationResult;
            }
            finally
            {
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempFilePath))
                {
                    EditorApplication.delayCall += () => CleanupTempFiles(tempFilePath);
                }
            }
        }

        /// <summary>
        /// 安装Python包协程
        /// </summary>
        private IEnumerator InstallPackageCoroutine(JObject args)
        {
            var packages = args["packages"];
            string requirementsFile = args["requirements_file"]?.ToString();
            string pythonPath = args["python_path"]?.ToString() ?? "python";
            string virtualEnv = args["virtual_env"]?.ToString();
            int timeout = args["timeout"]?.ToObject<int>() ?? 60;

            if (packages == null && string.IsNullOrEmpty(requirementsFile))
            {
                yield return Response.Error("Either 'packages' or 'requirements_file' parameter is required");
                yield break;
            }

            LogInfo("[PythonRunner] Installing Python packages");

            object installResult = null;

            if (!string.IsNullOrEmpty(requirementsFile))
            {
                // 安装requirements.txt中的包
                yield return InstallFromRequirements(requirementsFile, pythonPath, virtualEnv, timeout, (result) =>
                {
                    installResult = result;
                });
            }
            else
            {
                // 安装指定的包
                var packageList = new List<string>();
                if (packages.Type == JTokenType.Array)
                {
                    packageList.AddRange(packages.ToObject<string[]>());
                }
                else
                {
                    packageList.AddRange(packages.ToString().Split(',').Select(p => p.Trim()));
                }

                yield return InstallPackages(packageList.ToArray(), pythonPath, virtualEnv, timeout, (result) =>
                {
                    installResult = result;
                });
            }

            yield return installResult;
        }

        /// <summary>
        /// 执行Python代码的内部协程
        /// </summary>
        private IEnumerator ExecutePythonCoroutineInternal(string pythonCode, string scriptName, string pythonPath,
            string workingDirectory, int timeout, string virtualEnv, System.Action<string> onTempFileCreated = null)
        {
            executionResult = null;

            // 创建临时Python文件
            var tempDir = Path.Combine(Application.temporaryCachePath, "PythonRunner");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            var timestamp = DateTime.Now.Ticks;
            var randomId = UnityEngine.Random.Range(1000, 9999);
            var tempFileName = $"{Path.GetFileNameWithoutExtension(scriptName)}_{timestamp}_{randomId}.py";
            var tempFilePath = Path.Combine(tempDir, tempFileName);

            // 在协程外部处理文件写入异常
            bool fileCreated = false;
            try
            {
                // 添加Base64编码解决方案来避免编码问题，并强制刷新输出
                string encodingSolutionCode = @"#!/usr/bin/env python
# -*- coding: utf-8 -*-
import sys
import io
import base64
import json
import os
import builtins

# 设置环境变量确保无缓冲输出
os.environ['PYTHONUNBUFFERED'] = '1'

# Unity MCP Python Runner 编码解决方案
class UnicodeOutput:
    def __init__(self, original_stream):
        self.original_stream = original_stream
        
    def write(self, text):
        if text and isinstance(text, str):
            try:
                # 如果文本包含非ASCII字符，使用Base64编码
                if any(ord(c) > 127 for c in text):
                    encoded_text = base64.b64encode(text.encode('utf-8')).decode('ascii')
                    self.original_stream.write(f'[UNITY_MCP_B64:{encoded_text}]')
                else:
                    self.original_stream.write(text)
                self.original_stream.flush()  # 立即刷新
            except:
                try:
                    self.original_stream.write(text.encode('ascii', errors='replace').decode('ascii'))
                    self.original_stream.flush()  # 立即刷新
                except:
                    pass
    
    def flush(self):
        try:
            self.original_stream.flush()
        except:
            pass
    
    def __getattr__(self, name):
        return getattr(self.original_stream, name)

# 替换标准输出
try:
    sys.stdout = UnicodeOutput(sys.stdout)
    sys.stderr = UnicodeOutput(sys.stderr)
except:
    pass

# 重写print函数确保立即刷新
original_print = builtins.print
def unity_print(*args, **kwargs):
    kwargs.setdefault('flush', True)  # 默认强制刷新
    return original_print(*args, **kwargs)

builtins.print = unity_print

";
                string finalCode = encodingSolutionCode + pythonCode;
                File.WriteAllText(tempFilePath, finalCode, Encoding.UTF8);
                LogInfo($"[PythonRunner] 临时文件路径: {tempFilePath}");

                onTempFileCreated?.Invoke(tempFilePath);
                fileCreated = true;
            }
            catch (Exception e)
            {
                LogError($"[PythonRunner] Failed to create temporary file: {e.Message}");
                executionResult = Response.Error($"Failed to create temporary file: {e.Message}");
            }

            if (!fileCreated)
            {
                yield return executionResult;
                yield break;
            }

            // 执行Python脚本
            yield return ExecutePythonScript(tempFilePath, pythonPath, workingDirectory, timeout, virtualEnv, (result) =>
            {
                if (result != null)
                {
                    executionResult = Response.Success("Python script execution completed", new
                    {
                        operation = "execute",
                        script_name = scriptName,
                        success = result.Success,
                        output = result.Output,
                        error = result.Error,
                        exit_code = result.ExitCode,
                        duration = result.Duration,
                        temp_file = tempFilePath
                    });
                }
                else
                {
                    executionResult = Response.Error("Python script execution failed - no result returned");
                }
            });

            yield return executionResult;
        }

        /// <summary>
        /// 执行Python脚本
        /// </summary>
        private IEnumerator ExecutePythonScript(string scriptPath, string pythonPath, string workingDirectory,
            int timeout, string virtualEnv, System.Action<PythonResult> callback)
        {
            var result = new PythonResult { Operation = "execute" };
            var startTime = DateTime.Now;
            bool processStarted = false;
            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;

            // 构建Python命令
            string pythonExecutable = pythonPath;
            if (!string.IsNullOrEmpty(virtualEnv))
            {
                // 如果指定了虚拟环境，使用虚拟环境中的Python
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python"); // Linux/Mac
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u \"{scriptPath}\"", // 添加-u参数禁用Python输出缓冲
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // 设置Python环境变量以确保UTF-8输出和禁用缓冲
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // 禁用Python缓冲
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            LogInfo($"[PythonRunner] 执行命令: {pythonExecutable} \"{scriptPath}\"");
            LogInfo($"[PythonRunner] 工作目录: {workingDirectory}");

            // 在协程外部处理进程启动异常
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);
                            
                            // 实时输出到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);
                            
                            // 实时输出错误到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时错误输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
            }
            catch (Exception ex)
            {
                LogError($"[PythonRunner] Failed to start Python process: {ex.Message}");
                result.Success = false;
                result.Error = $"Failed to start Python process: {ex.Message}";
                result.ExitCode = -1;
                result.Duration = (DateTime.Now - startTime).TotalMilliseconds;
                callback(result);
                yield break;
            }

            if (processStarted && process != null)
            {
                // 改进的等待和输出处理机制
                float elapsedTime = 0f;
                const float checkInterval = 0.05f; // 更频繁地检查（50ms）
                
                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(checkInterval);
                    elapsedTime += checkInterval;
                    
                    // 实时记录当前已获得的输出（用于调试）
                    if (elapsedTime % 1.0f < checkInterval) // 每秒记录一次
                    {
                        var currentOutput = outputBuilder?.ToString() ?? "";
                        var currentError = errorBuilder?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(currentOutput) || !string.IsNullOrEmpty(currentError))
                        {
                            LogInfo($"[PythonRunner] 进程运行中 ({elapsedTime:F1}s), 已获得输出: {currentOutput.Length} 字符");
                        }
                    }
                }

                if (!process.HasExited)
                {
                    // 在杀死进程前，先尝试获取已有的输出
                    var partialOutput = outputBuilder?.ToString() ?? "";
                    var partialError = errorBuilder?.ToString() ?? "";
                    
                    LogWarning($"[PythonRunner] Python script execution timeout after {timeout} seconds");
                    LogWarning($"[PythonRunner] 超时前已获得输出: {partialOutput.Length} 字符, 错误: {partialError.Length} 字符");
                    
                    try
                    {
                        process.Kill();
                        // 给进程一点时间来完成输出读取
                    }
                    catch { }
                    yield return new WaitForSeconds(0.2f);
                    result.Success = false;
                    result.Error = $"Script execution timeout after {timeout} seconds. Partial output captured: {partialOutput.Length} chars.";
                    result.ExitCode = -1;
                    result.Output = partialOutput; // 保存超时前的部分输出
                }
                else
                {
                    try
                    {
                        process.WaitForExit(1000); // 最多等待1秒让输出完全读取
                        result.Success = process.ExitCode == 0;
                        result.Output = outputBuilder?.ToString() ?? "";
                        result.Error = errorBuilder?.ToString() ?? "";
                        result.ExitCode = process.ExitCode;

                        LogInfo($"[PythonRunner] Python script completed with exit code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(result.Output))
                            LogInfo($"[PythonRunner] Output ({result.Output.Length} chars):\n{result.Output}");
                        if (!string.IsNullOrEmpty(result.Error))
                            LogWarning($"[PythonRunner] Error ({result.Error.Length} chars):\n{result.Error}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"[PythonRunner] Error reading process result: {ex.Message}");
                        result.Success = false;
                        result.Error = $"Error reading process result: {ex.Message}";
                        result.ExitCode = -1;
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }

            result.Duration = (DateTime.Now - startTime).TotalMilliseconds;
            callback(result);
        }

        /// <summary>
        /// 验证Python语法
        /// </summary>
        private IEnumerator ValidatePythonSyntax(string scriptPath, string pythonPath, string virtualEnv,
            System.Action<bool, string, string> callback)
        {
            string pythonExecutable = pythonPath;
            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m py_compile \"{scriptPath}\"", // 添加-u参数
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // 设置Python环境变量以确保UTF-8输出和禁用缓冲
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // 禁用Python缓冲
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            LogInfo($"[PythonRunner] 验证语法: {pythonExecutable} -m py_compile \"{scriptPath}\"");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // 在协程外部处理进程启动异常
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);
                            
                            // 实时输出到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);
                            
                            // 实时输出错误到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时错误输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(false, "", "Failed to start Python process");
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(false, "", $"Failed to validate syntax: {ex.Message}");
                yield break;
            }

            if (processStarted && process != null)
            {
                // 等待验证完成
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < 10f)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsedTime += 0.1f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(false, "", "Syntax validation timeout");
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;
                        callback(success, outputBuilder?.ToString() ?? "", errorBuilder?.ToString() ?? "");
                    }
                    catch (Exception ex)
                    {
                        callback(false, "", $"Error reading validation result: {ex.Message}");
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// 安装Python包
        /// </summary>
        private IEnumerator InstallPackages(string[] packages, string pythonPath, string virtualEnv, int timeout,
            System.Action<object> callback)
        {
            string pythonExecutable = pythonPath;
            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var packageList = string.Join(" ", packages);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m pip install {packageList}", // 添加-u参数
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // 设置Python环境变量以确保UTF-8输出和禁用缓冲
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // 禁用Python缓冲
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            LogInfo($"[PythonRunner] 安装包: {pythonExecutable} -m pip install {packageList}");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // 在协程外部处理进程启动异常
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);
                            
                            // 实时输出到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);
                            
                            // 实时输出错误到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时错误输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(Response.Error("Failed to start pip process"));
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(Response.Error($"Failed to install packages: {ex.Message}"));
                yield break;
            }

            if (processStarted && process != null)
            {
                // 等待安装完成
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsedTime += 0.5f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(Response.Error("Package installation timeout", new
                    {
                        operation = "install_package",
                        packages = packages,
                        timeout = timeout
                    }));
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;

                        if (success)
                        {
                            callback(Response.Success("Python packages installed successfully", new
                            {
                                operation = "install_package",
                                packages = packages,
                                output = outputBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                        else
                        {
                            callback(Response.Error("Package installation failed", new
                            {
                                operation = "install_package",
                                packages = packages,
                                error = errorBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        callback(Response.Error($"Error reading installation result: {ex.Message}"));
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// 从requirements文件安装包
        /// </summary>
        private IEnumerator InstallFromRequirements(string requirementsFile, string pythonPath, string virtualEnv,
            int timeout, System.Action<object> callback)
        {
            if (!File.Exists(requirementsFile))
            {
                callback(Response.Error($"Requirements file not found: {requirementsFile}"));
                yield break;
            }

            string pythonExecutable = pythonPath;
            if (!string.IsNullOrEmpty(virtualEnv))
            {
                pythonExecutable = Path.Combine(virtualEnv, "Scripts", "python.exe");
                if (!File.Exists(pythonExecutable))
                {
                    pythonExecutable = Path.Combine(virtualEnv, "bin", "python");
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-u -m pip install -r \"{requirementsFile}\"", // 添加-u参数
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // 设置Python环境变量以确保UTF-8输出和禁用缓冲
            processStartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            processStartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            processStartInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // 禁用Python缓冲
            processStartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processStartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            LogInfo($"[PythonRunner] 从requirements安装: {pythonExecutable} -m pip install -r \"{requirementsFile}\"");

            Process process = null;
            StringBuilder outputBuilder = null;
            StringBuilder errorBuilder = null;
            bool processStarted = false;

            // 在协程外部处理进程启动异常
            try
            {
                process = Process.Start(processStartInfo);
                if (process != null)
                {
                    outputBuilder = new StringBuilder();
                    errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            outputBuilder.AppendLine(fixedData);
                            
                            // 实时输出到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.Log($"[Python] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // 尝试修复编码问题
                            string fixedData = FixEncodingIssues(e.Data);
                            errorBuilder.AppendLine(fixedData);
                            
                            // 实时输出错误到Unity控制台 - 需要在主线程中执行
                            UnityEditor.EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    UnityEngine.Debug.LogError($"[Python Error] {fixedData}");
                                }
                                catch (System.Exception ex)
                                {
                                    UnityEngine.Debug.LogWarning($"[PythonRunner] 实时错误输出失败: {ex.Message}");
                                }
                            };
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    processStarted = true;
                }
                else
                {
                    callback(Response.Error("Failed to start pip process"));
                    yield break;
                }
            }
            catch (Exception ex)
            {
                callback(Response.Error($"Failed to install from requirements: {ex.Message}"));
                yield break;
            }

            if (processStarted && process != null)
            {
                // 等待安装完成
                float elapsedTime = 0f;
                while (!process.HasExited && elapsedTime < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsedTime += 0.5f;
                }

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    callback(Response.Error("Requirements installation timeout", new
                    {
                        operation = "install_package",
                        requirements_file = requirementsFile,
                        timeout = timeout
                    }));
                }
                else
                {
                    try
                    {
                        process.WaitForExit();
                        bool success = process.ExitCode == 0;

                        if (success)
                        {
                            callback(Response.Success("Requirements installed successfully", new
                            {
                                operation = "install_package",
                                requirements_file = requirementsFile,
                                output = outputBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                        else
                        {
                            callback(Response.Error("Requirements installation failed", new
                            {
                                operation = "install_package",
                                requirements_file = requirementsFile,
                                error = errorBuilder?.ToString() ?? "",
                                exit_code = process.ExitCode
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        callback(Response.Error($"Error reading requirements installation result: {ex.Message}"));
                    }
                }

                try
                {
                    process.Dispose();
                }
                catch { }
            }
        }

        /// <summary>
        /// 尝试修复编码问题
        /// </summary>
        private string FixEncodingIssues(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                // 首先检查是否包含Base64编码的内容
                string decodedInput = DecodeBase64Content(input);
                if (decodedInput != input)
                {
                    return decodedInput; // 如果成功解码了Base64内容，直接返回
                }

                // 检查字符串是否包含乱码字符
                bool hasBadChars = input.Any(c => c == '�' || (c >= 0xFFFD && c <= 0xFFFE));

                if (hasBadChars || HasSuspiciousCharacterPattern(input))
                {
                    // 方法1: 尝试假设原始数据是UTF-8，但被错误地按照系统默认编码解释
                    try
                    {
                        // 获取系统默认编码（通常是GBK/GB2312）
                        var systemEncoding = Encoding.Default;
                        // 将字符串按系统编码转回字节
                        byte[] systemBytes = systemEncoding.GetBytes(input);
                        // 按UTF-8重新解释
                        string utf8Result = Encoding.UTF8.GetString(systemBytes);

                        if (IsValidChineseText(utf8Result))
                        {
                            return utf8Result;
                        }
                    }
                    catch { }

                    // 方法2: 尝试ISO-8859-1到UTF-8的转换
                    try
                    {
                        byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(input);
                        string utf8Result = Encoding.UTF8.GetString(bytes);

                        if (IsValidChineseText(utf8Result))
                        {
                            return utf8Result;
                        }
                    }
                    catch { }
                }

                return input;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// 解码Base64编码的内容
        /// </summary>
        private string DecodeBase64Content(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            try
            {
                // 查找Base64编码标记
                const string prefix = "[UNITY_MCP_B64:";
                const string suffix = "]";

                string result = input;
                int startIndex = 0;

                while (true)
                {
                    int prefixIndex = result.IndexOf(prefix, startIndex);
                    if (prefixIndex == -1)
                        break;

                    int suffixIndex = result.IndexOf(suffix, prefixIndex + prefix.Length);
                    if (suffixIndex == -1)
                        break;

                    // 提取Base64编码内容
                    string base64Content = result.Substring(prefixIndex + prefix.Length, suffixIndex - prefixIndex - prefix.Length);

                    try
                    {
                        // 解码Base64
                        byte[] bytes = Convert.FromBase64String(base64Content);
                        string decodedText = Encoding.UTF8.GetString(bytes);

                        // 替换原始标记为解码后的文本
                        string originalTag = prefix + base64Content + suffix;
                        result = result.Replace(originalTag, decodedText);
                    }
                    catch
                    {
                        // 解码失败，跳过这个标记
                        startIndex = suffixIndex + suffix.Length;
                    }
                }

                return result;
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        /// 检查是否有可疑的字符模式（可能是编码错误）
        /// </summary>
        private bool HasSuspiciousCharacterPattern(string input)
        {
            // 检查是否包含高位ASCII字符，这通常表示编码问题
            return input.Any(c => c >= 128 && c <= 255);
        }

        /// <summary>
        /// 验证文本是否包含有效的中文字符
        /// </summary>
        private bool IsValidChineseText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 检查是否包含中文字符
            bool hasChinese = text.Any(c => c >= 0x4E00 && c <= 0x9FFF);

            // 检查是否没有控制字符或无效字符
            bool hasValidChars = !text.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');

            return hasChinese && hasValidChars;
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
        private void CleanupTempFiles(string tempFilePath)
        {
            CleanupSingleFile(tempFilePath);

            // 清理可能的.pyc文件
            var pycPath = tempFilePath + "c";
            CleanupSingleFile(pycPath);

            // 清理__pycache__目录
            var directory = Path.GetDirectoryName(tempFilePath);
            var pycacheDir = Path.Combine(directory, "__pycache__");
            if (Directory.Exists(pycacheDir))
            {
                try
                {
                    Directory.Delete(pycacheDir, true);
                }
                catch (Exception ex)
                {
                    LogWarning($"[PythonRunner] Failed to clean __pycache__ directory: {ex.Message}");
                }
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
                    LogInfo($"[PythonRunner] Cleaned temporary file: {filePath}");
                    return;
                }
                catch (IOException ex)
                {
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        LogInfo($"[PythonRunner] Failed to clean file, retry {retryCount}/{maxRetries}: {filePath}");
                        System.Threading.Thread.Sleep(100 * retryCount);
                    }
                    else
                    {
                        LogWarning($"[PythonRunner] Unable to clean temporary file: {filePath}, error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[PythonRunner] Unexpected error occurred while cleaning file: {filePath}, error: {ex.Message}");
                    break;
                }
            }
        }
    }
}
