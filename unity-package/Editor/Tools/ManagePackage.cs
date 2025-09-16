using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
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
        // Class to track each package operation
        private class PackageOperation
        {
            public Request Request { get; set; }
            public TaskCompletionSource<object> CompletionSource { get; set; }
            public string OperationType { get; set; }
        }

        // Queue of active package operations
        private readonly List<PackageOperation> _activeOperations = new List<PackageOperation>();

        // Flag to track if the update callback is registered
        private bool _updateCallbackRegistered = false;

        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: add, remove, list, search, refresh, resolve", false),
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
                new MethodKey("timeout", "Operation timeout (seconds), default 30 seconds", true)
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
                    .Leaf("add", HandleAddPackage)
                    .Leaf("remove", HandleRemovePackage)
                    .Leaf("list", HandleListPackages)
                    .Leaf("search", HandleSearchPackages)
                    .Leaf("refresh", HandleRefreshPackages)
                    .Leaf("resolve", HandleResolvePackages)
                .Build();
        }

        // --- 包管理操作处理方法 ---

        /// <summary>
        /// 处理添加包操作
        /// </summary>
        private object HandleAddPackage(JObject args)
        {
            LogInfo("[ManagePackage] Executing add package operation");
            return ExecuteAddPackageAsync(args);
        }

        /// <summary>
        /// 处理移除包操作
        /// </summary>
        private object HandleRemovePackage(JObject args)
        {
            LogInfo("[ManagePackage] Executing remove package operation");
            return ExecuteRemovePackageAsync(args);
        }

        /// <summary>
        /// 处理列出包操作
        /// </summary>
        private object HandleListPackages(JObject args)
        {
            LogInfo("[ManagePackage] Executing list packages operation");
            return ExecuteListPackagesAsync(args);
        }

        /// <summary>
        /// 处理搜索包操作
        /// </summary>
        private object HandleSearchPackages(JObject args)
        {
            LogInfo("[ManagePackage] Executing search packages operation");
            return ExecuteSearchPackagesAsync(args);
        }

        /// <summary>
        /// 处理刷新包操作
        /// </summary>
        private object HandleRefreshPackages(JObject args)
        {
            LogInfo("[ManagePackage] Executing refresh packages operation");
            return ExecuteRefreshPackagesAsync(args);
        }

        /// <summary>
        /// 处理解析包依赖操作
        /// </summary>
        private object HandleResolvePackages(JObject args)
        {
            LogInfo("[ManagePackage] Executing resolve packages operation");
            return ExecuteResolvePackagesAsync(args);
        }

        // --- 异步执行方法 ---

        /// <summary>
        /// 异步执行添加包操作
        /// </summary>
        private object ExecuteAddPackageAsync(JObject args)
        {
            try
            {
                string source = args["source"]?.ToString()?.ToLowerInvariant() ?? "registry";
                AddRequest request = null;

                switch (source)
                {
                    case "registry":
                        request = AddFromRegistry(args);
                        break;
                    case "github":
                        request = AddFromGitHub(args);
                        break;
                    case "disk":
                        request = AddFromDisk(args);
                        break;
                    default:
                        return Response.Error($"Unknown package source type '{source}'. Supported types: registry, github, disk");
                }

                if (request == null)
                {
                    return Response.Error("Failed to create package add request");
                }

                return MonitorAsyncOperation(request, "add", args);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] Failed to add package: {e.Message}");
                return Response.Error($"Failed to add package: {e.Message}");
            }
        }

        /// <summary>
        /// 异步执行移除包操作
        /// </summary>
        private object ExecuteRemovePackageAsync(JObject args)
        {
            try
            {
                string packageName = args["package_name"]?.ToString() ?? args["package_identifier"]?.ToString();

                if (string.IsNullOrEmpty(packageName))
                {
                    return Response.Error("package_name or package_identifier parameter is required");
                }

                LogInfo($"[ManagePackage] 移除包: {packageName}");
                var request = Client.Remove(packageName);

                return MonitorAsyncOperation(request, "remove", args);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 移除包失败: {e.Message}");
                return Response.Error($"Failed to remove package: {e.Message}");
            }
        }

        /// <summary>
        /// 异步执行列出包操作
        /// </summary>
        private object ExecuteListPackagesAsync(JObject args)
        {
            try
            {
                bool includeIndirect = args["include_dependencies"]?.ToObject<bool>() ?? false;
                LogInfo($"[ManagePackage] 列出包 (包含间接依赖: {includeIndirect})");

                var request = Client.List(includeIndirect);

                return MonitorAsyncOperation(request, "list", args);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 列出包失败: {e.Message}");
                return Response.Error($"Failed to list packages: {e.Message}");
            }
        }

        /// <summary>
        /// 异步执行搜索包操作
        /// </summary>
        private object ExecuteSearchPackagesAsync(JObject args)
        {
            try
            {
                string keywords = args["search_keywords"]?.ToString();

                SearchRequest request;

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

                return MonitorAsyncOperation(request, "search", args);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 搜索包失败: {e.Message}");
                return Response.Error($"Failed to search packages: {e.Message}");
            }
        }

        /// <summary>
        /// 异步执行刷新包操作
        /// </summary>
        private object ExecuteRefreshPackagesAsync(JObject args)
        {
            try
            {
                LogInfo("[ManagePackage] 刷新包列表");
                Client.Resolve();

                return Response.Success("Package list refresh operation started", new { operation = "refresh" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 刷新包失败: {e.Message}");
                return Response.Error($"Failed to refresh packages: {e.Message}");
            }
        }

        /// <summary>
        /// 异步执行解析包依赖操作
        /// </summary>
        private object ExecuteResolvePackagesAsync(JObject args)
        {
            try
            {
                LogInfo("[ManagePackage] 解析包依赖");
                Client.Resolve();

                return Response.Success("Package dependency resolution operation started", new { operation = "resolve" });
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 解析包依赖失败: {e.Message}");
                return Response.Error($"Failed to resolve package dependencies: {e.Message}");
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

        // --- 异步操作监控 ---

        /// <summary>
        /// 监控异步操作并返回结果
        /// </summary>
        private object MonitorAsyncOperation(Request request, string operationType, JObject args)
        {
            var tcs = new TaskCompletionSource<object>();
            var operation = new PackageOperation
            {
                Request = request,
                CompletionSource = tcs,
                OperationType = operationType
            };

            lock (_activeOperations)
            {
                _activeOperations.Add(operation);

                if (!_updateCallbackRegistered)
                {
                    EditorApplication.update += CheckOperationsCompletion;
                    _updateCallbackRegistered = true;
                }
            }

            int timeout = args["timeout"]?.ToObject<int>() ?? 30;

            // 启动超时处理
            _ = Task.Run(async () =>
            {
                await Task.Delay(timeout * 1000);
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(Response.Error($"Operation timeout ({timeout} seconds)"));
                }
            });

            // 等待操作完成（同步等待）
            try
            {
                var result = tcs.Task.Result;
                return result;
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to wait for operation completion: {e.Message}");
            }
        }

        /// <summary>
        /// 检查所有活动操作的完成状态
        /// </summary>
        private void CheckOperationsCompletion()
        {
            lock (_activeOperations)
            {
                for (int i = _activeOperations.Count - 1; i >= 0; i--)
                {
                    var operation = _activeOperations[i];

                    if (operation.Request != null && operation.Request.IsCompleted)
                    {
                        ProcessCompletedOperation(operation);
                        _activeOperations.RemoveAt(i);
                    }
                }

                if (_activeOperations.Count == 0 && _updateCallbackRegistered)
                {
                    EditorApplication.update -= CheckOperationsCompletion;
                    _updateCallbackRegistered = false;
                }
            }
        }

        /// <summary>
        /// 处理已完成的包操作
        /// </summary>
        private void ProcessCompletedOperation(PackageOperation operation)
        {
            if (operation.CompletionSource == null)
            {
                LogError("[ManagePackage] TaskCompletionSource为空");
                return;
            }

            try
            {
                object result = null;

                if (operation.Request.Status == StatusCode.Success)
                {
                    result = ProcessSuccessfulOperation(operation);
                }
                else if (operation.Request.Status == StatusCode.Failure)
                {
                    result = Response.Error($"Operation failed: {operation.Request.Error?.message ?? "Unknown error"}");
                }
                else
                {
                    result = Response.Error($"Unknown operation status: {operation.Request.Status}");
                }

                operation.CompletionSource.TrySetResult(result);
            }
            catch (Exception e)
            {
                LogError($"[ManagePackage] 处理完成操作时发生错误: {e.Message}");
                operation.CompletionSource.TrySetResult(Response.Error($"Failed to process operation result: {e.Message}"));
            }
        }

        /// <summary>
        /// 处理成功的操作结果
        /// </summary>
        private object ProcessSuccessfulOperation(PackageOperation operation)
        {
            switch (operation.OperationType)
            {
                case "add":
                    return ProcessAddResult(operation.Request as AddRequest);
                case "remove":
                    return ProcessRemoveResult(operation.Request as RemoveRequest);
                case "list":
                    return ProcessListResult(operation.Request as ListRequest);
                case "search":
                    return ProcessSearchResult(operation.Request as SearchRequest);
                default:
                    return Response.Success($"{operation.OperationType} operation completed");
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
                            status = result.status.ToString(),
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
                    status = pkg.status.ToString(),
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