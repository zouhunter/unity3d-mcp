using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.Compilation;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Unity Package Manager operations including adding, removing, listing, and searching packages.
    /// 对应方法名: manage_package
    /// </summary>
    [ToolName("manage_package", "包管理")]
    public class ManagePackage : StateMethodBase
    {
        // Results storage for async operations
        private object operationResult;

        // 静态变量用于处理程序集刷新问题
        private static Dictionary<string, PackageOperationInfo> _pendingOperations = new Dictionary<string, PackageOperationInfo>();
        private static bool _isCompilationListenerRegistered = false;

        // 包操作信息
        private class PackageOperationInfo
        {
            public string OperationType { get; set; }
            public string PackageName { get; set; }
            public DateTime StartTime { get; set; }
            public int TimeoutSeconds { get; set; }
            public string Status { get; set; } = "pending";
        }

        static ManagePackage()
        {
            RegisterCompilationEvents();
        }

        /// <summary>
        /// 注册编译事件监听
        /// </summary>
        private static void RegisterCompilationEvents()
        {
            if (!_isCompilationListenerRegistered)
            {
                CompilationPipeline.compilationFinished += OnCompilationFinished;
                _isCompilationListenerRegistered = true;
            }
        }

        /// <summary>
        /// 编译完成事件处理
        /// </summary>
        private static void OnCompilationFinished(object obj)
        {
            LogInfo("[ManagePackage] 编译完成，检查待处理的包操作...");

            // 处理等待中的包操作
            var completedOperations = new List<string>();
            foreach (var kvp in _pendingOperations)
            {
                var operationInfo = kvp.Value;
                var elapsed = (DateTime.Now - operationInfo.StartTime).TotalSeconds;

                if (elapsed > operationInfo.TimeoutSeconds)
                {
                    LogWarning($"[ManagePackage] 包操作超时: {operationInfo.PackageName} ({operationInfo.OperationType})");
                    completedOperations.Add(kvp.Key);
                }
                else
                {
                    // 检查包操作是否完成
                    CheckPackageOperationStatus(kvp.Key, operationInfo);
                }
            }

            // 清理完成的操作
            foreach (var key in completedOperations)
            {
                _pendingOperations.Remove(key);
            }
        }

        /// <summary>
        /// 检查包操作状态
        /// </summary>
        private static void CheckPackageOperationStatus(string operationId, PackageOperationInfo operationInfo)
        {
            // 这里可以添加更复杂的状态检查逻辑
            // 由于Unity Package Manager操作的异步性质，我们主要依赖编译完成事件
            LogInfo($"[ManagePackage] 检查包操作状态: {operationInfo.PackageName} ({operationInfo.OperationType})");
        }

        /// <summary>
        /// 记录包操作
        /// </summary>
        private static string RegisterPackageOperation(string operationType, string packageName, int timeoutSeconds)
        {
            string operationId = $"{operationType}_{packageName}_{DateTime.Now.Ticks}";
            _pendingOperations[operationId] = new PackageOperationInfo
            {
                OperationType = operationType,
                PackageName = packageName,
                StartTime = DateTime.Now,
                TimeoutSeconds = timeoutSeconds
            };

            LogInfo($"[ManagePackage] 注册包操作: {operationId}");
            return operationId;
        }

        private static new void LogInfo(string message)
        {
            Debug.Log(message);
        }

        private static new void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// 检查待处理操作状态
        /// </summary>
        private object CheckPendingOperationsStatus()
        {
            var operations = _pendingOperations.Values.Select(op => new
            {
                operation_type = op.OperationType,
                package_name = op.PackageName,
                start_time = op.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                elapsed_seconds = (DateTime.Now - op.StartTime).TotalSeconds,
                timeout_seconds = op.TimeoutSeconds,
                status = op.Status
            }).ToArray();

            return Response.Success(
                $"当前有 {operations.Length} 个待处理的包操作",
                new
                {
                    operation = "status",
                    pending_operations_count = operations.Length,
                    operations = operations
                }
            );
        }

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: add, remove, list, search, refresh, resolve, status, restore_auto_refresh", false),
                new MethodKey("source", "Package source type: registry, github, disk (used only for add operation)", true),
                new MethodKey("package_name", "Package name (used for add, remove operations)", true),
                new MethodKey("package_identifier", "Package full identifier (used for remove operation)", true),
                new MethodKey("version", "Package version (used for add operation)", true),
                new MethodKey("repository_url", "GitHub repository URL (used for github source)", true),
                new MethodKey("branch", "GitHub branch name (used for github source)", true),
                new MethodKey("path", "Package path (github source subdirectory or disk source path)", true),
                new MethodKey("search_keywords", "Search keywords (used for search operation, searches all packages when empty)", true),
                new MethodKey("include_dependencies", "Whether to include dependency information (used for list operation)", true),
                new MethodKey("scope", "Package scope filter (used for list operation)", true),
                new MethodKey("timeout", "Operation timeout (seconds), default 60 seconds", true),
                new MethodKey("disable_auto_refresh", "Disable automatic assembly refresh during package operations, default false", true),
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
                    .Node("add", "source")
                            .Leaf("registry", HandleAddFromRegistry)
                            .Leaf("github", HandleAddFromGitHub)
                            .Leaf("disk", HandleAddFromDisk)
                            .DefaultLeaf(HandleAddFromRegistry)
                        .Up()
                    .Leaf("remove", HandleRemovePackage)
                    .Leaf("list", HandleListPackages)
                    .Leaf("search", HandleSearchPackages)
                    .Leaf("refresh", HandleRefreshPackages)
                    .Leaf("resolve", HandleResolvePackages)
                    .Leaf("status", HandleCheckOperationStatus)
                    .Leaf("restore_auto_refresh", HandleRestoreAutoRefresh)
                .Build();
        }

        // --- 包管理操作处理方法 ---

        /// <summary>
        /// 处理从Registry添加包操作
        /// </summary>
        private object HandleAddFromRegistry(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing add package from registry");
            return ctx.AsyncReturn(ExecuteAddFromRegistryAsync(ctx));
        }

        /// <summary>
        /// 处理从GitHub添加包操作
        /// </summary>
        private object HandleAddFromGitHub(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing add package from GitHub");
            return ctx.AsyncReturn(ExecuteAddFromGitHubAsync(ctx));
        }

        /// <summary>
        /// 处理从磁盘添加包操作
        /// </summary>
        private object HandleAddFromDisk(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing add package from disk");
            return ctx.AsyncReturn(ExecuteAddFromDiskAsync(ctx));
        }

        /// <summary>
        /// 处理移除包操作
        /// </summary>
        private object HandleRemovePackage(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing remove package operation");
            return ctx.AsyncReturn(ExecuteRemovePackageAsync(ctx));
        }

        /// <summary>
        /// 处理列出包操作
        /// </summary>
        private object HandleListPackages(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing list packages operation");
            return ctx.AsyncReturn(ExecuteListPackagesAsync(ctx));
        }

        /// <summary>
        /// 处理搜索包操作
        /// </summary>
        private object HandleSearchPackages(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing search packages operation");
            return ctx.AsyncReturn(ExecuteSearchPackagesAsync(ctx));
        }

        /// <summary>
        /// 处理刷新包操作
        /// </summary>
        private object HandleRefreshPackages(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing refresh packages operation");
            return ctx.AsyncReturn(ExecuteRefreshPackagesAsync(ctx));
        }

        /// <summary>
        /// 处理解析包依赖操作
        /// </summary>
        private object HandleResolvePackages(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Executing resolve packages operation");
            return ctx.AsyncReturn(ExecuteResolvePackagesAsync(ctx));
        }

        /// <summary>
        /// 处理检查操作状态
        /// </summary>
        private object HandleCheckOperationStatus(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Checking operation status");
            return CheckPendingOperationsStatus();
        }

        /// <summary>
        /// 处理恢复自动刷新
        /// </summary>
        private object HandleRestoreAutoRefresh(StateTreeContext ctx)
        {
            LogInfo("[ManagePackage] Restoring auto refresh settings");
            try
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();

                LogInfo("[ManagePackage] 自动程序集刷新已恢复");

                return Response.Success(
                    "自动程序集刷新设置已恢复",
                    new
                    {
                        operation = "restore_auto_refresh",
                        auto_refresh_enabled = true,
                        assembly_reload_unlocked = true,
                        message = "Unity现在将在适当时机自动刷新程序集"
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 恢复自动刷新失败: {e.Message}");
                return Response.Error($"Failed to restore auto refresh: {e.Message}");
            }
        }

        // --- 协程异步执行方法 ---

        /// <summary>
        /// 从Registry添加包的异步方法
        /// </summary>
        private IEnumerator ExecuteAddFromRegistryAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            // 默认等待请求完成模式
            string packageName = ctx.JsonData["package_name"]?.ToString();
            int timeout = ctx.JsonData["timeout"]?.ToObject<int>() ?? 60;
            bool disableAutoRefresh = ctx.JsonData["disable_auto_refresh"]?.ToObject<bool>() ?? false;

            // 控制程序集刷新
            bool wasAutoRefreshDisabled = false;
            if (disableAutoRefresh)
            {
                LogInfo($"[ManagePackage] 禁用自动程序集刷新: {packageName}");
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                wasAutoRefreshDisabled = true;
            }

            try
            {
                request = AddFromRegistry(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create registry package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from registry: {e.Message}");
                operationResult = Response.Error($"Failed to add package from registry: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                // 如果操作失败，恢复刷新设置
                if (wasAutoRefreshDisabled)
                {
                    AssetDatabase.AllowAutoRefresh();
                    EditorApplication.UnlockReloadAssemblies();
                    LogInfo($"[ManagePackage] 恢复自动程序集刷新设置");
                }
                yield return operationResult;
                yield break;
            }

            // 等待请求完成模式 - 等待AddRequest完成但不等待程序集刷新
            LogInfo($"[ManagePackage] 等待包添加请求完成: {packageName}");
            yield return WaitForRequestOnlyAsync(request, "add", timeout, packageName, wasAutoRefreshDisabled);
            yield return operationResult;
        }

        /// <summary>
        /// 从GitHub添加包的异步方法
        /// </summary>
        private IEnumerator ExecuteAddFromGitHubAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            try
            {
                request = AddFromGitHub(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create GitHub package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from GitHub: {e.Message}");
                operationResult = Response.Error($"Failed to add package from GitHub: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "add", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// 从磁盘添加包的异步方法
        /// </summary>
        private IEnumerator ExecuteAddFromDiskAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            AddRequest request = null;
            bool failed = false;

            try
            {
                request = AddFromDisk(ctx.JsonData);
                if (request == null)
                {
                    operationResult = Response.Error("Failed to create disk package add request");
                    failed = true;
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package from disk: {e.Message}");
                operationResult = Response.Error($"Failed to add package from disk: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "add", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的移除包操作
        /// </summary>
        private IEnumerator ExecuteRemovePackageAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            RemoveRequest request = null;
            bool failed = false;

            // 默认等待请求完成模式  
            int timeout = ctx.JsonData["timeout"]?.ToObject<int>() ?? 60;
            bool disableAutoRefresh = ctx.JsonData["disable_auto_refresh"]?.ToObject<bool>() ?? false;

            string packageName = ctx.JsonData["package_name"]?.ToString() ?? ctx.JsonData["package_identifier"]?.ToString();

            // 控制程序集刷新
            bool wasAutoRefreshDisabled = false;
            if (disableAutoRefresh)
            {
                LogInfo($"[ManagePackage] 禁用自动程序集刷新: {packageName}");
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                wasAutoRefreshDisabled = true;
            }

            try
            {
                if (string.IsNullOrEmpty(packageName))
                {
                    operationResult = Response.Error("package_name or package_identifier parameter is required");
                    failed = true;
                }
                else
                {
                    LogInfo($"[ManagePackage] 移除包: {packageName}");
                    request = Client.Remove(packageName);
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 移除包失败: {e.Message}");
                operationResult = Response.Error($"Failed to remove package: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                // 如果操作失败，恢复刷新设置
                if (wasAutoRefreshDisabled)
                {
                    AssetDatabase.AllowAutoRefresh();
                    EditorApplication.UnlockReloadAssemblies();
                    LogInfo($"[ManagePackage] 恢复自动程序集刷新设置");
                }
                yield return operationResult;
                yield break;
            }

            // 等待请求完成模式 - 等待RemoveRequest完成但不等待程序集刷新
            LogInfo($"[ManagePackage] 等待包移除请求完成: {packageName}");
            yield return WaitForRequestOnlyAsync(request, "remove", timeout, packageName, wasAutoRefreshDisabled);
            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的列出包操作
        /// </summary>
        private IEnumerator ExecuteListPackagesAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            ListRequest request = null;
            bool failed = false;

            try
            {
                bool includeIndirect = ctx.JsonData["include_dependencies"]?.ToObject<bool>() ?? false;
                LogInfo($"[ManagePackage] 列出包 (包含间接依赖: {includeIndirect})");

                request = Client.List(includeIndirect);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 列出包失败: {e.Message}");
                operationResult = Response.Error($"Failed to list packages: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "list", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的搜索包操作
        /// </summary>
        private IEnumerator ExecuteSearchPackagesAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;
            SearchRequest request = null;
            bool failed = false;

            try
            {
                string keywords = ctx.JsonData["search_keywords"]?.ToString();

                if (string.IsNullOrEmpty(keywords))
                {
                    // 如果没有关键词，搜索所有包
                    LogInfo("[ManagePackage] 搜索所有包");
                    request = Client.SearchAll();
                }
                else
                {
                    // 搜索特定包
                    LogInfo($"[ManagePackage] 搜索包: {keywords}");
                    request = Client.Search(keywords);
                }
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 搜索包失败: {e.Message}");
                operationResult = Response.Error($"Failed to search packages: {e.Message}");
                failed = true;
            }

            if (failed)
            {
                yield return operationResult;
                yield break;
            }

            yield return MonitorOperationAsync(request, "search", ctx.JsonData);
            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的刷新包操作
        /// </summary>
        private IEnumerator ExecuteRefreshPackagesAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;

            try
            {
                LogInfo("[ManagePackage] 刷新包列表");
                Client.Resolve();

                operationResult = Response.Success("Package list refresh operation started", new { operation = "refresh" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 刷新包失败: {e.Message}");
                operationResult = Response.Error($"Failed to refresh packages: {e.Message}");
            }

            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的解析包依赖操作
        /// </summary>
        private IEnumerator ExecuteResolvePackagesAsync(StateTreeContext ctx)
        {
            // 在协程外部处理异常，避免在try-catch中使用yield return
            operationResult = null;

            try
            {
                LogInfo("[ManagePackage] 解析包依赖");
                Client.Resolve();

                operationResult = Response.Success("Package dependency resolution operation started", new { operation = "resolve" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 解析包依赖失败: {e.Message}");
                operationResult = Response.Error($"Failed to resolve package dependencies: {e.Message}");
            }

            yield return operationResult;
        }

        /// <summary>
        /// 异步版本的操作监控
        /// </summary>
        private IEnumerator MonitorOperationAsync(Request request, string operationType, JObject args)
        {
            int timeout = args["timeout"]?.ToObject<int>() ?? 60; // 增加超时时间到60秒
            float elapsedTime = 0f;

            LogInfo($"[ManagePackage] 开始监控 {operationType} 操作，超时时间: {timeout}秒");

            while (!request.IsCompleted && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    LogInfo($"[ManagePackage] {operationType} 操作进行中... 已等待: {elapsedTime:F1}s");
                }
            }

            if (elapsedTime >= timeout)
            {
                operationResult = Response.Error($"Operation timeout ({timeout} seconds)");
            }
            else if (request.Status == StatusCode.Success)
            {
                operationResult = ProcessSuccessfulOperationResult(request, operationType);
            }
            else if (request.Status == StatusCode.Failure)
            {
                operationResult = Response.Error($"Operation failed: {request.Error?.message ?? "Unknown error"}");
            }
            else
            {
                operationResult = Response.Error($"Unknown operation status: {request.Status}");
            }

            LogInfo($"[ManagePackage] {operationType} 操作完成");
        }

        /// <summary>
        /// 只等待请求完成的异步监控（不等待程序集刷新）
        /// </summary>
        private IEnumerator WaitForRequestOnlyAsync(Request request, string operationType, int timeout, string packageName, bool wasAutoRefreshDisabled = false)
        {
            float elapsedTime = 0f;

            LogInfo($"[ManagePackage] 开始等待 {operationType} 请求完成: {packageName}，超时时间: {timeout}秒");

            while (!request.IsCompleted && elapsedTime < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsedTime += 0.1f;

                if (Mathf.FloorToInt(elapsedTime) != Mathf.FloorToInt(elapsedTime - 0.1f))
                {
                    LogInfo($"[ManagePackage] 等待 {operationType} 请求完成中... 已等待: {elapsedTime:F1}s");
                }
            }

            // 处理请求结果
            if (elapsedTime >= timeout)
            {
                operationResult = Response.Error($"Request timeout ({timeout} seconds)");
                LogWarning($"[ManagePackage] {operationType} 请求超时: {packageName}");
            }
            else if (request.Status == StatusCode.Success)
            {
                // 请求成功完成，立即返回结果，不等待程序集刷新
                LogInfo($"[ManagePackage] {operationType} 请求成功完成，立即返回结果: {packageName}");

                // 处理操作结果并添加刷新控制信息
                var result = ProcessSuccessfulOperationResult(request, operationType);
                if (wasAutoRefreshDisabled && result is object resultObj)
                {
                    // 增强返回结果，包含刷新控制信息
                    var enhancedResult = AddRefreshControlInfo(resultObj, wasAutoRefreshDisabled, packageName);
                    operationResult = enhancedResult;
                }
                else
                {
                    operationResult = result;
                }
            }
            else if (request.Status == StatusCode.Failure)
            {
                operationResult = Response.Error($"Request failed: {request.Error?.message ?? "Unknown error"}");
                LogError($"[ManagePackage] {operationType} 请求失败: {packageName}, 错误: {request.Error?.message}");
            }
            else
            {
                operationResult = Response.Error($"Unknown request status: {request.Status}");
                LogWarning($"[ManagePackage] {operationType} 请求状态未知: {packageName}, 状态: {request.Status}");
            }

            // 处理刷新设置
            if (wasAutoRefreshDisabled)
            {
                LogInfo($"[ManagePackage] 请求完成，程序集刷新被禁用。需要手动控制刷新。");
                LogInfo($"[ManagePackage] 使用AssetDatabase.AllowAutoRefresh()和EditorApplication.UnlockReloadAssemblies()来恢复自动刷新");
                LogInfo($"[ManagePackage] 或使用AssetDatabase.Refresh()来手动刷新");
            }

            LogInfo($"[ManagePackage] {operationType} 请求监控完成: {packageName}");
        }

        /// <summary>
        /// 为操作结果添加刷新控制信息
        /// </summary>
        private object AddRefreshControlInfo(object originalResult, bool wasAutoRefreshDisabled, string packageName)
        {
            try
            {
                // 尝试解析原始结果
                var resultType = originalResult.GetType();
                var properties = resultType.GetProperties();
                var messageProperty = properties.FirstOrDefault(p => p.Name.Equals("message", StringComparison.OrdinalIgnoreCase));
                var dataProperty = properties.FirstOrDefault(p => p.Name.Equals("data", StringComparison.OrdinalIgnoreCase));
                var successProperty = properties.FirstOrDefault(p => p.Name.Equals("success", StringComparison.OrdinalIgnoreCase));

                string message = messageProperty?.GetValue(originalResult)?.ToString() ?? "Operation completed";
                object data = dataProperty?.GetValue(originalResult);
                bool success = (bool)(successProperty?.GetValue(originalResult) ?? true);

                // 创建增强的数据对象
                var enhancedData = new Dictionary<string, object>();

                // 复制原始数据
                if (data != null)
                {
                    var dataType = data.GetType();
                    foreach (var prop in dataType.GetProperties())
                    {
                        enhancedData[prop.Name] = prop.GetValue(data);
                    }
                }

                // 添加刷新控制信息
                enhancedData["auto_refresh_disabled"] = wasAutoRefreshDisabled;
                enhancedData["refresh_control"] = new
                {
                    message = "自动程序集刷新已被禁用",
                    instructions = new[]
                    {
                        "要恢复自动刷新，请调用: AssetDatabase.AllowAutoRefresh() 和 EditorApplication.UnlockReloadAssemblies()",
                        "要手动刷新，请调用: AssetDatabase.Refresh()",
                        "推荐在所有包操作完成后再恢复自动刷新"
                    },
                    current_state = "assemblies_locked"
                };

                return Response.Success(
                    $"{message} (自动程序集刷新已禁用)",
                    enhancedData
                );
            }
            catch (Exception ex)
            {
                LogWarning($"[ManagePackage] 无法增强结果信息: {ex.Message}");
                return originalResult;
            }
        }




        // --- 添加包的具体实现方法 ---

        /// <summary>
        /// 从Unity Registry添加包
        /// </summary>
        private AddRequest AddFromRegistry(JObject args)
        {
            string packageName = args["package_name"]?.ToString();
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException("package_name 参数是必需的（registry源）");
            }

            string version = args["version"]?.ToString();
            string packageIdentifier = packageName;

            if (!string.IsNullOrEmpty(version))
            {
                packageIdentifier = $"{packageName}@{version}";
            }

            LogInfo($"[ManagePackage] 从Registry添加包: {packageIdentifier}");
            return Client.Add(packageIdentifier);
        }

        /// <summary>
        /// 从GitHub添加包
        /// </summary>
        private AddRequest AddFromGitHub(JObject args)
        {
            string repositoryUrl = args["repository_url"]?.ToString();
            if (string.IsNullOrEmpty(repositoryUrl))
            {
                throw new ArgumentException("repository_url 参数是必需的（github源）");
            }

            string branch = args["branch"]?.ToString();
            string path = args["path"]?.ToString();

            // 移除.git后缀
            if (repositoryUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repositoryUrl = repositoryUrl.Substring(0, repositoryUrl.Length - 4);
            }

            // 添加分支
            if (!string.IsNullOrEmpty(branch))
            {
                repositoryUrl += "#" + branch;
            }

            // 添加路径
            if (!string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(branch))
                {
                    repositoryUrl += "/" + path;
                }
                else
                {
                    repositoryUrl += "#" + path;
                }
            }

            LogInfo($"[ManagePackage] 从GitHub添加包: {repositoryUrl}");
            return Client.Add(repositoryUrl);
        }

        /// <summary>
        /// 从磁盘添加包
        /// </summary>
        private AddRequest AddFromDisk(JObject args)
        {
            string path = args["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path 参数是必需的（disk源）");
            }

            string packageUrl = $"file:{path}";
            LogInfo($"[ManagePackage] 从磁盘添加包: {packageUrl}");
            return Client.Add(packageUrl);
        }

        // --- 协程异步操作监控 ---

        /// <summary>
        /// 处理成功的操作结果（协程版本）
        /// </summary>
        private object ProcessSuccessfulOperationResult(Request request, string operationType)
        {
            switch (operationType)
            {
                case "add":
                    return ProcessAddResult(request as AddRequest);
                case "remove":
                    return ProcessRemoveResult(request as RemoveRequest);
                case "list":
                    return ProcessListResult(request as ListRequest);
                case "search":
                    return ProcessSearchResult(request as SearchRequest);
                default:
                    return Response.Success($"{operationType} operation completed");
            }
        }

        /// <summary>
        /// 处理添加包的结果
        /// </summary>
        private object ProcessAddResult(AddRequest request)
        {
            var result = request.Result;
            if (result != null)
            {
                return Response.Success(
                    $"成功添加包: {result.displayName} ({result.name}) 版本 {result.version}",
                    new
                    {
                        operation = "add",
                        package_info = new
                        {
                            name = result.name,
                            display_name = result.displayName,
                            version = result.version,
                            description = result.description,
                            // status = result.status.ToString(), // Removed deprecated API
                            source = result.source.ToString()
                        }
                    }
                );
            }

            return Response.Success("Package add operation completed, but no package information returned");
        }

        /// <summary>
        /// 处理移除包的结果
        /// </summary>
        private object ProcessRemoveResult(RemoveRequest request)
        {
            return Response.Success(
                "包移除操作完成",
                new
                {
                    operation = "remove"
                }
            );
        }

        /// <summary>
        /// 处理列出包的结果
        /// </summary>
        private object ProcessListResult(ListRequest request)
        {
            var packages = request.Result;
            if (packages != null)
            {
                var packageList = packages.Select(pkg => new
                {
                    name = pkg.name,
                    display_name = pkg.displayName,
                    version = pkg.version,
                    description = pkg.description,
                    // status = pkg.status.ToString(), // Removed deprecated API
                    source = pkg.source.ToString(),
                    package_id = pkg.packageId,
                    resolved_path = pkg.resolvedPath
                }).ToArray();

                return Response.Success(
                    $"找到 {packageList.Length} 个包",
                    new
                    {
                        operation = "list",
                        package_count = packageList.Length,
                        packages = packageList
                    }
                );
            }

            return Response.Success("Package list operation completed, but no package information returned");
        }

        /// <summary>
        /// 处理搜索包的结果
        /// </summary>
        private object ProcessSearchResult(SearchRequest request)
        {
            var searchResult = request.Result;
            if (searchResult != null)
            {
                var packages = searchResult.Select(pkg => new
                {
                    name = pkg.name,
                    display_name = pkg.displayName,
                    version = pkg.version,
                    description = pkg.description,
                    source = pkg.source.ToString(),
                    package_id = pkg.packageId
                }).ToArray();

                return Response.Success(
                    $"搜索到 {packages.Length} 个包",
                    new
                    {
                        operation = "search",
                        package_count = packages.Length,
                        packages = packages
                    }
                );
            }

            return Response.Success("Package search operation completed, but no search results returned");
        }
    }
}