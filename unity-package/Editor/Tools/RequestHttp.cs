using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityMcp.Models;
using System.Linq;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles network operations including HTTP requests, file downloads, and API calls.
    /// 对应方法名: request_http
    /// </summary>
    [ToolName("request_http")]
    public class RequestHttp : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：get, post, put, delete, download, upload, ping, batch_download", false),
                new MethodKey("url", "请求URL地址", false),
                new MethodKey("data", "请求数据（POST/PUT时使用，JSON格式）", true),
                new MethodKey("headers", "请求头字典", true),
                new MethodKey("save_path", "保存路径（下载时使用，相对于Assets或绝对路径）", true),
                new MethodKey("file_path", "文件路径（上传时使用）", true),
                new MethodKey("timeout", "超时时间（秒），默认30秒", true),
                new MethodKey("method", "HTTP方法（GET, POST, PUT, DELETE等）", true),
                new MethodKey("content_type", "内容类型，默认application/json", true),
                new MethodKey("user_agent", "用户代理字符串", true),
                new MethodKey("accept_certificates", "是否接受所有证书（用于测试）", true),
                new MethodKey("follow_redirects", "是否跟随重定向", true),
                new MethodKey("encoding", "文本编码，默认UTF-8", true),
                new MethodKey("form_data", "表单数据（键值对）", true),
                new MethodKey("query_params", "查询参数（键值对）", true),
                new MethodKey("auth_token", "认证令牌（Bearer Token）", true),
                new MethodKey("basic_auth", "基础认证（username:password）", true),
                new MethodKey("retry_count", "重试次数，默认0", true),
                new MethodKey("retry_delay", "重试延迟（秒），默认1秒", true),
                new MethodKey("urls", "URL数组（批量下载时使用）", true)
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
                    .Leaf("get", HandleGetRequest)
                    .Leaf("post", HandlePostRequest)
                    .Leaf("put", HandlePutRequest)
                    .Leaf("delete", HandleDeleteRequest)
                    .Leaf("download", HandleDownloadFile)
                    .Leaf("upload", HandleUploadFile)
                    .Leaf("ping", HandlePingRequest)
                    .Leaf("batch_download", HandleBatchDownload)
                .Build();
        }
        // --- 请求处理方法 ---

        /// <summary>
        /// 处理GET请求
        /// </summary>
        private object HandleGetRequest(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Executing GET request");
            return ExecuteHttpRequest(ctx, "GET");
        }

        /// <summary>
        /// 处理POST请求
        /// </summary>
        private object HandlePostRequest(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Executing POST request");
            return ExecuteHttpRequest(ctx, "POST");
        }

        /// <summary>
        /// 处理PUT请求
        /// </summary>
        private object HandlePutRequest(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Executing PUT request");
            return ExecuteHttpRequest(ctx, "PUT");
        }

        /// <summary>
        /// 处理DELETE请求
        /// </summary>
        private object HandleDeleteRequest(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Executing DELETE request");
            return ExecuteHttpRequest(ctx, "DELETE");
        }

        /// <summary>
        /// 处理文件下载（默认使用协程）
        /// </summary>
        private object HandleDownloadFile(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Starting coroutine file download");
            return DownloadFileCoroutine(ctx);
        }

        /// <summary>
        /// 使用协程下载文件（异步）
        /// </summary>
        private object DownloadFileCoroutine(StateTreeContext ctx)
        {
            string url = ctx["url"]?.ToString();
            string savePath = ctx["save_path"]?.ToString();
            float timeout = ctx.TryGetValue("timeout", out int timeoutValue) ? timeoutValue : 60f;

            // 参数验证
            if (string.IsNullOrEmpty(url))
            {
                ctx.Complete(Response.Error("URL参数是必需的"));
                return null;
            }

            if (string.IsNullOrEmpty(savePath))
            {
                ctx.Complete(Response.Error("save_path参数是必需的"));
                return null;
            }

            LogInfo($"[RequestHttp] 启动协程下载: {url}");

            // 启动协程下载
            CoroutineRunner.StartCoroutine(DownloadFileAsync(url, savePath, timeout, (result) =>
            {
                ctx.Complete(result);
            }, null, ctx));

            // 返回null，表示异步执行
            return null;
        }

        /// <summary>
        /// 处理文件上传
        /// </summary>
        private object HandleUploadFile(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Starting file upload");
            return UploadFile(ctx);
        }

        /// <summary>
        /// 处理PING请求
        /// </summary>
        private object HandlePingRequest(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Executing PING request");
            return PingHost(ctx);
        }

        /// <summary>
        /// 处理批量下载（默认使用协程）
        /// </summary>
        private object HandleBatchDownload(StateTreeContext ctx)
        {
            LogInfo("[RequestHttp] Starting coroutine batch download");
            return BatchDownloadCoroutine(ctx);
        }

        /// <summary>
        /// 使用协程进行批量下载（异步）
        /// </summary>
        private object BatchDownloadCoroutine(StateTreeContext ctx)
        {
            var urlsToken = ctx["urls"];
            string saveDirectory = ctx["save_directory"]?.ToString() ?? ctx["save_path"]?.ToString();

            // 参数验证
            if (urlsToken == null)
            {
                ctx.Complete(Response.Error("urls参数是必需的"));
                return null;
            }

            if (string.IsNullOrEmpty(saveDirectory))
            {
                ctx.Complete(Response.Error("save_directory或save_path参数是必需的"));
                return null;
            }

            LogInfo($"[RequestHttp] 启动协程批量下载");

            // 启动协程批量下载
            CoroutineRunner.StartCoroutine(BatchDownloadAsync(ctx, (result) =>
            {
                ctx.Complete(result);
            }));

            // 返回null，表示异步执行
            return null;
        }

        // --- 核心实现方法 ---

        /// <summary>
        /// 执行HTTP请求的通用方法
        /// </summary>
        private object ExecuteHttpRequest(StateTreeContext ctx, string defaultMethod)
        {
            try
            {
                string url = ctx["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                // 解析参数
                string method = ctx["method"]?.ToString() ?? defaultMethod;
                int timeout = ctx.TryGetValue<int>("timeout", out var timeoutValue) ? timeoutValue : 30;
                string contentType = ctx["content_type"]?.ToString() ?? "application/json";
                string userAgent = ctx["user_agent"]?.ToString() ?? "Unity-MCP-Network-Manager/1.0";
                bool acceptCertificates = ctx.TryGetValue<bool>("accept_certificates", out var acceptCerts) ? acceptCerts : true;
                bool followRedirects = ctx.TryGetValue<bool>("follow_redirects", out var followRedirectsValue) ? followRedirectsValue : true;
                int retryCount = ctx.TryGetValue<int>("retry_count", out var retryCountValue) ? retryCountValue : 0;
                float retryDelay = ctx.TryGetValue<float>("retry_delay", out var retryDelayValue) ? retryDelayValue : 1f;

                // 构建完整URL（包含查询参数）
                string fullUrl = BuildUrlWithQueryParams(url, ctx["query_params"] as JObject);

                // 执行请求（带重试机制）
                return ExecuteWithRetry(() => PerformHttpRequest(fullUrl, method, ctx, timeout, contentType, userAgent, acceptCertificates, followRedirects), retryCount, retryDelay);
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] HTTP请求执行失败: {e.Message}");
                return Response.Error($"HTTP请求执行失败: {e.Message}");
            }
        }

        /// <summary>
        /// 执行具体的HTTP请求
        /// </summary>
        private object PerformHttpRequest(string url, string method, StateTreeContext ctx, int timeout, string contentType, string userAgent, bool acceptCertificates, bool followRedirects)
        {
            UnityWebRequest request = null;

            try
            {
                // 根据方法类型创建请求
                switch (method.ToUpper())
                {
                    case "GET":
                        request = UnityWebRequest.Get(url);
                        break;
                    case "POST":
                        request = CreatePostRequest(url, ctx, contentType);
                        break;
                    case "PUT":
                        request = CreatePutRequest(url, ctx, contentType);
                        break;
                    case "DELETE":
                        request = UnityWebRequest.Delete(url);
                        break;
                    default:
                        request = new UnityWebRequest(url, method);
                        request.downloadHandler = new DownloadHandlerBuffer();
                        break;
                }

                // 配置请求
                ConfigureRequest(request, ctx, timeout, userAgent, acceptCertificates, followRedirects);

                // 同步执行请求
                var asyncOp = request.SendWebRequest();

                // 等待请求完成（同步等待，但不阻塞主线程）
                var startTime = EditorApplication.timeSinceStartup;
                while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                {
                    // 让Unity处理其他事件，不阻塞主线程
                    System.Threading.Thread.Yield();
                }

                // 检查超时
                if (!asyncOp.isDone)
                {
                    request.Abort();
                    return Response.Error($"请求超时 ({timeout}秒)");
                }

                // 处理响应
                return ProcessHttpResponse(request);
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] 请求执行错误: {e.Message}");
                return Response.Error($"请求执行错误: {e.Message}");
            }
            finally
            {
                request?.Dispose();
            }
        }

        /// <summary>
        /// 创建POST请求
        /// </summary>
        private UnityWebRequest CreatePostRequest(string url, StateTreeContext ctx, string contentType)
        {
            UnityWebRequest request;

            // 检查是否使用表单数据
            var formData = ctx["form_data"] as JObject;
            if (formData != null)
            {
                var form = new WWWForm();
                foreach (var pair in formData.Properties())
                {
                    form.AddField(pair.Name, pair.Value.ToString());
                }
                request = UnityWebRequest.Post(url, form);
            }
            else
            {
                // 使用JSON数据
                string jsonData = GetRequestBodyData(ctx);
                byte[] bodyRaw = string.IsNullOrEmpty(jsonData) ? null : Encoding.UTF8.GetBytes(jsonData);
                request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
            }

            return request;
        }

        /// <summary>
        /// 创建PUT请求
        /// </summary>
        private UnityWebRequest CreatePutRequest(string url, StateTreeContext ctx, string contentType)
        {
            string jsonData = GetRequestBodyData(ctx);
            byte[] bodyRaw = string.IsNullOrEmpty(jsonData) ? null : Encoding.UTF8.GetBytes(jsonData);
            var request = UnityWebRequest.Put(url, bodyRaw);
            request.SetRequestHeader("Content-Type", contentType);
            return request;
        }

        /// <summary>
        /// 配置请求参数
        /// </summary>
        private void ConfigureRequest(UnityWebRequest request, StateTreeContext ctx, int timeout, string userAgent, bool acceptCertificates, bool followRedirects)
        {
            // 基本配置
            request.timeout = timeout;
            request.SetRequestHeader("User-Agent", userAgent);

            // 证书验证
            if (acceptCertificates)
            {
                request.certificateHandler = new AcceptAllCertificatesHandler();
            }

            // 重定向
            request.redirectLimit = followRedirects ? 10 : 0;

            // 添加自定义请求头
            var headers = ctx["headers"] as JObject;
            if (headers != null)
            {
                foreach (var header in headers.Properties())
                {
                    request.SetRequestHeader(header.Name, header.Value.ToString());
                }
            }

            // 认证
            SetAuthentication(request, ctx);
        }

        /// <summary>
        /// 设置认证信息
        /// </summary>
        private void SetAuthentication(UnityWebRequest request, StateTreeContext ctx)
        {
            // Bearer Token认证
            string authToken = ctx["auth_token"]?.ToString();
            if (!string.IsNullOrEmpty(authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            }

            // Basic认证
            string basicAuth = ctx["basic_auth"]?.ToString();
            if (!string.IsNullOrEmpty(basicAuth))
            {
                string encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicAuth));
                request.SetRequestHeader("Authorization", $"Basic {encodedAuth}");
            }
        }

        /// <summary>
        /// 获取请求体数据
        /// </summary>
        private string GetRequestBodyData(StateTreeContext ctx)
        {
            var data = ctx["data"];
            if (data == null) return null;

            if (data is JObject || data is JArray)
            {
                return data.ToString();
            }
            else
            {
                return data.ToString();
            }
        }

        /// <summary>
        /// 构建包含查询参数的URL
        /// </summary>
        private string BuildUrlWithQueryParams(string baseUrl, JObject queryParams)
        {
            if (queryParams == null || !queryParams.HasValues)
                return baseUrl;

            var sb = new StringBuilder(baseUrl);
            bool hasQuery = baseUrl.Contains("?");

            foreach (var param in queryParams.Properties())
            {
                sb.Append(hasQuery ? "&" : "?");
                sb.Append(Uri.EscapeDataString(param.Name));
                sb.Append("=");
                sb.Append(Uri.EscapeDataString(param.Value.ToString()));
                hasQuery = true;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 处理HTTP响应
        /// </summary>
        private object ProcessHttpResponse(UnityWebRequest request)
        {
            bool isSuccess = request.result == UnityWebRequest.Result.Success;
            long responseCode = request.responseCode;
            string responseText = request.downloadHandler?.text ?? "";

            // 获取响应头
            var responseHeaders = new Dictionary<string, string>();
            if (request.GetResponseHeaders() != null)
            {
                foreach (var header in request.GetResponseHeaders())
                {
                    responseHeaders[header.Key] = header.Value;
                }
            }

            // 尝试解析JSON响应
            object responseData = null;
            try
            {
                if (!string.IsNullOrEmpty(responseText) && responseText.Trim().StartsWith("{") || responseText.Trim().StartsWith("["))
                {
                    responseData = JToken.Parse(responseText);
                }
                else
                {
                    responseData = responseText;
                }
            }
            catch
            {
                responseData = responseText;
            }

            var result = new
            {
                success = isSuccess,
                status_code = responseCode,
                headers = responseHeaders,
                data = responseData,
                raw_text = responseText,
                error = isSuccess ? null : request.error,
                url = request.url,
                method = request.method
            };

            if (isSuccess)
            {
                return Response.Success($"HTTP请求成功 (状态码: {responseCode})", result);
            }
            else
            {
                return Response.Error($"HTTP请求失败 (状态码: {responseCode}): {request.error}", result);
            }
        }

        /// <summary>
        /// 协程版本的文件下载器
        /// </summary>
        /// <param name="url">下载URL</param>
        /// <param name="savePath">保存路径</param>
        /// <param name="timeout">超时时间（秒）</param>
        /// <param name="callback">完成回调</param>
        /// <param name="progressCallback">进度回调，可选</param>
        /// <param name="ctx">上下文，用于获取额外配置</param>
        /// <returns>协程枚举器</returns>
        IEnumerator DownloadFileAsync(string url, string savePath, float timeout, Action<object> callback,
            Action<float> progressCallback = null, StateTreeContext ctx = null)
        {
            LogInfo($"[RequestHttp] 开始协程下载: {url}");

            // 参数验证
            if (string.IsNullOrEmpty(url))
            {
                callback?.Invoke(Response.Error("URL参数是必需的"));
                yield break;
            }

            if (string.IsNullOrEmpty(savePath))
            {
                callback?.Invoke(Response.Error("save_path参数是必需的"));
                yield break;
            }

            // 规范化保存路径
            string fullSavePath = GetFullPath(savePath);
            string directory = Path.GetDirectoryName(fullSavePath);

            // 确保目录存在
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception e)
            {
                callback?.Invoke(Response.Error($"无法创建目录: {e.Message}"));
                yield break;
            }

            // 创建下载请求
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)timeout;

                // 如果有上下文，使用它来配置请求
                if (ctx != null)
                {
                    ConfigureRequest(request, ctx, (int)timeout, "Unity-MCP-Downloader/1.0", true, true);
                }
                else
                {
                    request.SetRequestHeader("User-Agent", "Unity-MCP-Downloader/1.0");
                }

                // 发送请求
                var operation = request.SendWebRequest();
                float startTime = Time.realtimeSinceStartup;

                // 等待下载完成，同时报告进度
                while (!operation.isDone)
                {
                    // 检查超时
                    float elapsedTime = Time.realtimeSinceStartup - startTime;
                    if (elapsedTime > timeout)
                    {
                        request.Abort();
                        callback?.Invoke(Response.Error($"下载超时 ({timeout}秒)"));
                        yield break;
                    }

                    // 报告下载进度
                    if (progressCallback != null && request.downloadHandler != null)
                    {
                        float progress = operation.progress;
                        progressCallback(progress);
                    }

                    yield return null; // 等待一帧
                }

                // 检查下载结果
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // 保存文件
                        File.WriteAllBytes(fullSavePath, request.downloadHandler.data);

                        // 如果是Unity资源，刷新AssetDatabase
                        if (fullSavePath.StartsWith(Application.dataPath))
                        {
                            string relativePath = "Assets" + fullSavePath.Substring(Application.dataPath.Length);
                            AssetDatabase.ImportAsset(relativePath);
                            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        }

                        LogInfo($"[RequestHttp] 协程下载成功: {Path.GetFileName(fullSavePath)}");

                        // 成功回调
                        callback?.Invoke(Response.Success(
                            $"文件下载成功: {Path.GetFileName(fullSavePath)}",
                            new
                            {
                                url = url,
                                save_path = fullSavePath,
                                file_size = request.downloadHandler.data.Length,
                                content_type = request.GetResponseHeader("Content-Type"),
                                elapsed_time = Time.realtimeSinceStartup - startTime
                            }
                        ));
                    }
                    catch (Exception e)
                    {
                        callback?.Invoke(Response.Error($"保存文件失败: {e.Message}"));
                    }
                }
                else
                {
                    // 下载失败
                    string errorMessage = $"下载失败: {request.error}";
                    if (request.responseCode > 0)
                    {
                        errorMessage += $" (HTTP {request.responseCode})";
                    }

                    LogError($"[RequestHttp] {errorMessage}");
                    callback?.Invoke(Response.Error(errorMessage));
                }
            }
        }

        /// <summary>
        /// 批量协程下载文件
        /// </summary>
        /// <param name="ctx">上下文</param>
        /// <param name="callback">完成回调</param>
        /// <returns>协程枚举器</returns>
        IEnumerator BatchDownloadAsync(StateTreeContext ctx, Action<object> callback)
        {
            var urlsToken = ctx["urls"];
            string saveDirectory = ctx["save_directory"]?.ToString() ?? ctx["save_path"]?.ToString();
            float timeout = ctx.TryGetValue("timeout", out int timeoutValue) ? timeoutValue : 60f;

            // 解析URL数组
            string[] urls = null;
            if (urlsToken is JArray urlArray)
            {
                urls = urlArray.ToObject<string[]>();
            }
            else if (urlsToken is string urlString)
            {
                urls = new[] { urlString };
            }

            if (urls == null || urls.Length == 0)
            {
                callback?.Invoke(Response.Error("urls数组不能为空"));
                yield break;
            }

            // 规范化保存目录
            string fullSaveDirectory = GetFullPath(saveDirectory);

            // 确保目录存在
            try
            {
                if (!Directory.Exists(fullSaveDirectory))
                {
                    Directory.CreateDirectory(fullSaveDirectory);
                }
            }
            catch (Exception e)
            {
                callback?.Invoke(Response.Error($"无法创建目录: {e.Message}"));
                yield break;
            }

            LogInfo($"[RequestHttp] 开始批量协程下载 {urls.Length} 个文件到: {fullSaveDirectory}");

            var downloadResults = new List<object>();
            var errors = new List<string>();
            int completed = 0;
            int total = urls.Length;

            // 并发下载计数器
            int activeDownloads = 0;
            const int maxConcurrentDownloads = 3; // 最大并发下载数

            for (int i = 0; i < urls.Length; i++)
            {
                string url = urls[i];

                // 等待空闲槽位
                while (activeDownloads >= maxConcurrentDownloads)
                {
                    yield return null;
                }

                // 从URL生成文件名
                string fileName = GetFileNameFromUrl(url);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"download_{i + 1}";
                }

                string filePath = Path.Combine(fullSaveDirectory, fileName);

                LogInfo($"[RequestHttp] 协程下载文件 {i + 1}/{total}: {url}");

                activeDownloads++;

                // 启动单个文件下载协程
                CoroutineRunner.StartCoroutine(DownloadFileAsync(url, filePath, timeout, (result) =>
                {
                    lock (downloadResults)
                    {
                        bool downloadSuccess = IsSuccessResponse(result);

                        downloadResults.Add(new
                        {
                            url = url,
                            file_path = filePath,
                            success = downloadSuccess,
                            result = result
                        });

                        if (!downloadSuccess)
                        {
                            string errorMsg = $"文件 {Path.GetFileName(filePath)} 下载失败";
                            if (result != null)
                            {
                                var errorProp = result.GetType().GetProperty("error");
                                if (errorProp != null)
                                {
                                    errorMsg += $": {errorProp.GetValue(result)}";
                                }
                            }
                            errors.Add(errorMsg);
                        }

                        completed++;
                        activeDownloads--;

                        LogInfo($"[RequestHttp] 批量下载进度: {completed}/{total} ({(completed * 100f / total):F1}%)");
                    }
                }, null, ctx));
            }

            // 等待所有下载完成
            while (completed < total)
            {
                yield return null;
            }

            // 生成最终结果
            int successCount = downloadResults.Count(r =>
            {
                var successProp = r.GetType().GetProperty("success");
                return successProp != null && (bool)successProp.GetValue(r);
            });

            var finalResult = Response.Success(
                $"批量下载完成: {successCount}/{total} 个文件成功",
                new
                {
                    total_files = total,
                    successful = successCount,
                    failed = total - successCount,
                    save_directory = fullSaveDirectory,
                    results = downloadResults,
                    errors = errors
                }
            );

            LogInfo($"[RequestHttp] 批量协程下载完成: {successCount}/{total} 成功");
            callback?.Invoke(finalResult);
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        private object DownloadFile(StateTreeContext ctx)
        {
            try
            {
                string url = ctx["url"]?.ToString();
                string savePath = ctx["save_path"]?.ToString();

                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                if (string.IsNullOrEmpty(savePath))
                {
                    return Response.Error("save_path参数是必需的");
                }

                // 规范化保存路径
                string fullSavePath = GetFullPath(savePath);
                string directory = Path.GetDirectoryName(fullSavePath);

                // 确保目录存在
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                int timeout = ctx.TryGetValue<int>("timeout", out var timeoutValue) ? timeoutValue : 60;

                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = timeout;
                    ConfigureRequest(request, ctx, timeout, "Unity-MCP-Downloader/1.0", true, true);

                    var asyncOp = request.SendWebRequest();

                    // 等待下载完成（同步）
                    var startTime = EditorApplication.timeSinceStartup;
                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                    {
                        System.Threading.Thread.Yield(); // 让Unity处理其他事件
                    }

                    if (!asyncOp.isDone)
                    {
                        request.Abort();
                        return Response.Error($"下载超时 ({timeout}秒)");
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // 保存文件
                        File.WriteAllBytes(fullSavePath, request.downloadHandler.data);

                        // 如果是Unity资源，刷新AssetDatabase
                        if (fullSavePath.StartsWith(Application.dataPath))
                        {
                            string relativePath = "Assets" + fullSavePath.Substring(Application.dataPath.Length);
                            AssetDatabase.ImportAsset(relativePath);
                        }

                        return Response.Success(
                            $"文件下载成功: {Path.GetFileName(fullSavePath)}",
                            new
                            {
                                url = url,
                                save_path = fullSavePath,
                                file_size = request.downloadHandler.data.Length,
                                content_type = request.GetResponseHeader("Content-Type")
                            }
                        );
                    }
                    else
                    {
                        return Response.Error($"下载失败: {request.error}");
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] 文件下载失败: {e.Message}");
                return Response.Error($"文件下载失败: {e.Message}");
            }
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        private object UploadFile(StateTreeContext ctx)
        {
            try
            {
                string url = ctx["url"]?.ToString();
                string filePath = ctx["file_path"]?.ToString();

                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    return Response.Error("file_path参数是必需的");
                }

                string fullFilePath = GetFullPath(filePath);
                if (!File.Exists(fullFilePath))
                {
                    return Response.Error($"文件不存在: {fullFilePath}");
                }

                byte[] fileData = File.ReadAllBytes(fullFilePath);
                string fileName = Path.GetFileName(fullFilePath);

                var form = new WWWForm();
                form.AddBinaryData("file", fileData, fileName);

                // 添加额外的表单字段
                if (ctx["form_data"] as JObject != null)
                {
                    foreach (var pair in (ctx["form_data"] as JObject).Properties())
                    {
                        form.AddField(pair.Name, pair.Value.ToString());
                    }
                }

                int timeout = ctx.TryGetValue<int>("timeout", out var timeoutValue) ? timeoutValue : 60;

                using (var request = UnityWebRequest.Post(url, form))
                {
                    request.timeout = timeout;
                    ConfigureRequest(request, ctx, timeout, "Unity-MCP-Uploader/1.0", true, true);

                    var asyncOp = request.SendWebRequest();

                    // 等待上传完成（同步）
                    var startTime = EditorApplication.timeSinceStartup;
                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                    {
                        System.Threading.Thread.Yield(); // 让Unity处理其他事件
                    }

                    if (!asyncOp.isDone)
                    {
                        request.Abort();
                        return Response.Error($"上传超时 ({timeout}秒)");
                    }

                    return ProcessHttpResponse(request);
                }
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] 文件上传失败: {e.Message}");
                return Response.Error($"文件上传失败: {e.Message}");
            }
        }

        /// <summary>
        /// PING主机
        /// </summary>
        private object PingHost(StateTreeContext ctx)
        {
            try
            {
                string url = ctx["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                // 简单的连通性测试，使用HEAD请求
                using (var request = UnityWebRequest.Head(url))
                {
                    request.timeout = ctx.TryGetValue<int>("timeout", out var timeoutValue) ? timeoutValue : 10;

                    var startTime = EditorApplication.timeSinceStartup;
                    var asyncOp = request.SendWebRequest();

                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < request.timeout)
                    {
                        System.Threading.Thread.Yield(); // 让Unity处理其他事件
                    }

                    if (!asyncOp.isDone)
                    {
                        request.Abort();
                        return Response.Error("Ping超时");
                    }

                    double responseTime = (EditorApplication.timeSinceStartup - startTime) * 1000; // 转换为毫秒

                    return Response.Success(
                        $"Ping成功: {url}",
                        new
                        {
                            url = url,
                            status_code = request.responseCode,
                            response_time_ms = Math.Round(responseTime, 2),
                            success = request.result == UnityWebRequest.Result.Success
                        }
                    );
                }
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] Ping失败: {e.Message}");
                return Response.Error($"Ping失败: {e.Message}");
            }
        }

        // --- 辅助方法 ---

        /// <summary>
        /// 带重试机制的执行方法
        /// </summary>
        private object ExecuteWithRetry(Func<object> action, int retryCount, float retryDelay)
        {
            object lastResult = null;

            for (int i = 0; i <= retryCount; i++)
            {
                try
                {
                    lastResult = action();

                    // 如果是成功的响应，直接返回
                    if (IsSuccessResponse(lastResult))
                    {
                        return lastResult;
                    }

                    // 如果是最后一次尝试，返回结果
                    if (i == retryCount)
                    {
                        return lastResult;
                    }

                    // 等待重试（同步）
                    if (retryDelay > 0)
                    {
                        var delayStart = EditorApplication.timeSinceStartup;
                        while ((EditorApplication.timeSinceStartup - delayStart) < retryDelay)
                        {
                            System.Threading.Thread.Yield();
                        }
                    }
                }
                catch (Exception e)
                {
                    lastResult = Models.Response.Error($"重试 {i + 1}/{retryCount + 1} 失败: {e.Message}");

                    if (i == retryCount)
                    {
                        return lastResult;
                    }

                    if (retryDelay > 0)
                    {
                        var delayStart = EditorApplication.timeSinceStartup;
                        while ((EditorApplication.timeSinceStartup - delayStart) < retryDelay)
                        {
                            System.Threading.Thread.Yield();
                        }
                    }
                }
            }

            return lastResult;
        }

        /// <summary>
        /// 检查响应对象是否表示成功
        /// </summary>
        private bool IsSuccessResponse(object response)
        {
            if (response == null) return false;

            // 使用反射检查匿名对象的success属性
            var successProperty = response.GetType().GetProperty("success");
            if (successProperty != null && successProperty.PropertyType == typeof(bool))
            {
                return (bool)successProperty.GetValue(response);
            }

            return false;
        }

        /// <summary>
        /// 批量下载文件（支持实时控制台刷新）
        /// </summary>
        private object BatchDownload(StateTreeContext ctx)
        {
            try
            {
                var urlsToken = ctx["urls"];
                string saveDirectory = ctx["save_directory"]?.ToString() ?? ctx["save_path"]?.ToString();

                if (urlsToken == null)
                {
                    return Response.Error("urls参数是必需的");
                }

                // 解析URL数组
                string[] urls;
                if (urlsToken is JArray urlArray)
                {
                    urls = urlArray.Select(token => token.ToString()).ToArray();
                }
                else
                {
                    // 如果是字符串，尝试按逗号分割
                    string urlString = urlsToken.ToString();
                    urls = urlString.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(url => url.Trim())
                                   .Where(url => !string.IsNullOrEmpty(url))
                                   .ToArray();
                }

                if (urls.Length == 0)
                {
                    return Response.Error("没有找到有效的URL");
                }

                if (string.IsNullOrEmpty(saveDirectory))
                {
                    saveDirectory = "Assets/Downloads";
                }

                // 确保保存目录存在
                string fullSaveDirectory = GetFullPath(saveDirectory);
                if (!Directory.Exists(fullSaveDirectory))
                {
                    Directory.CreateDirectory(fullSaveDirectory);
                }

                int timeout = ctx.TryGetValue<int>("timeout", out var timeoutValue) ? timeoutValue : 60;
                var downloadResults = new List<object>();
                var errors = new List<string>();

                LogInfo($"[RequestHttp] 开始批量下载 {urls.Length} 个文件到 {fullSaveDirectory}");

                // 逐个下载文件
                for (int i = 0; i < urls.Length; i++)
                {
                    string url = urls[i];
                    try
                    {
                        // 从URL生成文件名
                        string fileName = GetFileNameFromUrl(url);
                        if (string.IsNullOrEmpty(fileName))
                        {
                            fileName = $"download_{i + 1}";
                        }

                        string filePath = Path.Combine(fullSaveDirectory, fileName);

                        // 创建单个下载参数
                        var downloadArgs = new JObject
                        {
                            ["url"] = url,
                            ["save_path"] = filePath,
                            ["timeout"] = timeout
                        };

                        // 复制认证和请求头参数
                        if (ctx["headers"] as JObject != null) downloadArgs["headers"] = ctx.JsonData["headers"];
                        if (ctx["auth_token"]?.ToString() != null) downloadArgs["auth_token"] = ctx.JsonData["auth_token"];
                        if (ctx["basic_auth"]?.ToString() != null) downloadArgs["basic_auth"] = ctx.JsonData["basic_auth"];
                        if (ctx["user_agent"]?.ToString() != null) downloadArgs["user_agent"] = ctx.JsonData["user_agent"];

                        LogInfo($"[RequestHttp] 下载文件 {i + 1}/{urls.Length}: {url}");

                        // 调用单个文件下载方法
                        var downloadContext = new StateTreeContext(downloadArgs);
                        var result = DownloadFile(downloadContext);

                        bool downloadSuccess = IsSuccessResponse(result);

                        downloadResults.Add(new
                        {
                            url = url,
                            file_path = filePath,
                            file_name = fileName,
                            success = downloadSuccess,
                            result = result
                        });

                        // 每下载完成一个文件，立即输出日志并刷新控制台
                        string statusMessage = downloadSuccess ? "✅ 成功" : "❌ 失败";
                        LogInfo($"[RequestHttp] 文件 {i + 1}/{urls.Length} {statusMessage}: {fileName}");

                        // 强制刷新Unity控制台，让用户实时看到进度
                        System.Threading.Thread.Yield(); // 让Unity有时间处理日志显示
                    }
                    catch (Exception e)
                    {
                        string error = $"下载失败 {url}: {e.Message}";
                        errors.Add(error);
                        LogError($"[RequestHttp] {error}");

                        downloadResults.Add(new
                        {
                            url = url,
                            success = false,
                            error = e.Message
                        });
                    }
                }

                int successCount = downloadResults.Where(r =>
                {
                    var result = r.GetType().GetProperty("success")?.GetValue(r);
                    return result is bool success && success;
                }).Count();

                return Response.Success(
                    $"批量下载完成：成功 {successCount}/{urls.Length} 个文件",
                    new
                    {
                        total_urls = urls.Length,
                        successful_downloads = successCount,
                        failed_downloads = urls.Length - successCount,
                        save_directory = fullSaveDirectory,
                        results = downloadResults,
                        errors = errors
                    }
                );
            }
            catch (Exception e)
            {
                LogError($"[RequestHttp] 批量下载失败: {e.Message}");
                return Response.Error($"批量下载失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从URL提取文件名
        /// </summary>
        private string GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                string fileName = Path.GetFileName(uri.LocalPath);

                // 如果没有扩展名，尝试从查询参数中获取
                if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                {
                    // 尝试从URL中推断文件类型
                    if (url.Contains("image") || url.Contains("img"))
                    {
                        fileName += ".jpg"; // 默认图片格式
                    }
                    else
                    {
                        fileName += ".bin"; // 默认二进制文件
                    }
                }

                // 确保文件名合法
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                return fileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取完整路径
        /// </summary>
        private string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            // 如果路径以Assets开头，使用项目路径
            if (path.StartsWith("Assets"))
            {
                return Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            // 否则相对于Assets文件夹
            return Path.Combine(Application.dataPath, path);
        }
    }

    /// <summary>
    /// 接受所有证书的处理器（仅用于开发测试）
    /// </summary>
    public class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}