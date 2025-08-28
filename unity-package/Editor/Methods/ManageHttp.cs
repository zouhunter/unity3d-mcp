using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
    /// 对应方法名: manage_network
    /// </summary>
    [ToolName("manage_http")]
    public class ManageHttp : StateMethodBase
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
        private object HandleGetRequest(JObject args)
        {
            LogInfo("[ManageNetwork] Executing GET request");
            return ExecuteHttpRequest(args, "GET");
        }

        /// <summary>
        /// 处理POST请求
        /// </summary>
        private object HandlePostRequest(JObject args)
        {
            LogInfo("[ManageNetwork] Executing POST request");
            return ExecuteHttpRequest(args, "POST");
        }

        /// <summary>
        /// 处理PUT请求
        /// </summary>
        private object HandlePutRequest(JObject args)
        {
            LogInfo("[ManageNetwork] Executing PUT request");
            return ExecuteHttpRequest(args, "PUT");
        }

        /// <summary>
        /// 处理DELETE请求
        /// </summary>
        private object HandleDeleteRequest(JObject args)
        {
            LogInfo("[ManageNetwork] Executing DELETE request");
            return ExecuteHttpRequest(args, "DELETE");
        }

        /// <summary>
        /// 处理文件下载
        /// </summary>
        private object HandleDownloadFile(JObject args)
        {
            LogInfo("[ManageNetwork] Starting file download");
            return DownloadFile(args);
        }

        /// <summary>
        /// 处理文件上传
        /// </summary>
        private object HandleUploadFile(JObject args)
        {
            LogInfo("[ManageNetwork] Starting file upload");
            return UploadFile(args);
        }

        /// <summary>
        /// 处理PING请求
        /// </summary>
        private object HandlePingRequest(JObject args)
        {
            LogInfo("[ManageNetwork] Executing PING request");
            return PingHost(args);
        }

        /// <summary>
        /// 处理批量下载
        /// </summary>
        private object HandleBatchDownload(JObject args)
        {
            LogInfo("[ManageHttp] Starting batch download");
            return BatchDownload(args);
        }

        // --- 核心实现方法 ---

        /// <summary>
        /// 执行HTTP请求的通用方法
        /// </summary>
        private object ExecuteHttpRequest(JObject args, string defaultMethod)
        {
            try
            {
                string url = args["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                // 解析参数
                string method = args["method"]?.ToString() ?? defaultMethod;
                int timeout = args["timeout"]?.ToObject<int>() ?? 30;
                string contentType = args["content_type"]?.ToString() ?? "application/json";
                string userAgent = args["user_agent"]?.ToString() ?? "Unity-MCP-Network-Manager/1.0";
                bool acceptCertificates = args["accept_certificates"]?.ToObject<bool>() ?? true;
                bool followRedirects = args["follow_redirects"]?.ToObject<bool>() ?? true;
                int retryCount = args["retry_count"]?.ToObject<int>() ?? 0;
                float retryDelay = args["retry_delay"]?.ToObject<float>() ?? 1f;

                // 构建完整URL（包含查询参数）
                string fullUrl = BuildUrlWithQueryParams(url, args["query_params"] as JObject);

                // 执行请求（带重试机制）
                return ExecuteWithRetry(() => PerformHttpRequest(fullUrl, method, args, timeout, contentType, userAgent, acceptCertificates, followRedirects), retryCount, retryDelay);
            }
            catch (Exception e)
            {
                LogError($"[ManageNetwork] HTTP请求执行失败: {e.Message}");
                return Response.Error($"HTTP请求执行失败: {e.Message}");
            }
        }

        /// <summary>
        /// 执行具体的HTTP请求
        /// </summary>
        private object PerformHttpRequest(string url, string method, JObject args, int timeout, string contentType, string userAgent, bool acceptCertificates, bool followRedirects)
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
                        request = CreatePostRequest(url, args, contentType);
                        break;
                    case "PUT":
                        request = CreatePutRequest(url, args, contentType);
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
                ConfigureRequest(request, args, timeout, userAgent, acceptCertificates, followRedirects);

                // 同步执行请求
                var asyncOp = request.SendWebRequest();

                // 等待请求完成
                var startTime = EditorApplication.timeSinceStartup;
                while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                {
                    System.Threading.Thread.Sleep(10); // 避免忙等待
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
                LogError($"[ManageNetwork] 请求执行错误: {e.Message}");
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
        private UnityWebRequest CreatePostRequest(string url, JObject args, string contentType)
        {
            UnityWebRequest request;

            // 检查是否使用表单数据
            if (args["form_data"] is JObject formData)
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
                string jsonData = GetRequestBodyData(args);
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
        private UnityWebRequest CreatePutRequest(string url, JObject args, string contentType)
        {
            string jsonData = GetRequestBodyData(args);
            byte[] bodyRaw = string.IsNullOrEmpty(jsonData) ? null : Encoding.UTF8.GetBytes(jsonData);
            var request = UnityWebRequest.Put(url, bodyRaw);
            request.SetRequestHeader("Content-Type", contentType);
            return request;
        }

        /// <summary>
        /// 配置请求参数
        /// </summary>
        private void ConfigureRequest(UnityWebRequest request, JObject args, int timeout, string userAgent, bool acceptCertificates, bool followRedirects)
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
            if (args["headers"] is JObject headers)
            {
                foreach (var header in headers.Properties())
                {
                    request.SetRequestHeader(header.Name, header.Value.ToString());
                }
            }

            // 认证
            SetAuthentication(request, args);
        }

        /// <summary>
        /// 设置认证信息
        /// </summary>
        private void SetAuthentication(UnityWebRequest request, JObject args)
        {
            // Bearer Token认证
            string authToken = args["auth_token"]?.ToString();
            if (!string.IsNullOrEmpty(authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            }

            // Basic认证
            string basicAuth = args["basic_auth"]?.ToString();
            if (!string.IsNullOrEmpty(basicAuth))
            {
                string encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(basicAuth));
                request.SetRequestHeader("Authorization", $"Basic {encodedAuth}");
            }
        }

        /// <summary>
        /// 获取请求体数据
        /// </summary>
        private string GetRequestBodyData(JObject args)
        {
            var data = args["data"];
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
        /// 下载文件
        /// </summary>
        private object DownloadFile(JObject args)
        {
            try
            {
                string url = args["url"]?.ToString();
                string savePath = args["save_path"]?.ToString();

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

                int timeout = args["timeout"]?.ToObject<int>() ?? 60;

                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = timeout;
                    ConfigureRequest(request, args, timeout, "Unity-MCP-Downloader/1.0", true, true);

                    var asyncOp = request.SendWebRequest();

                    // 等待下载完成
                    var startTime = EditorApplication.timeSinceStartup;
                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                    {
                        System.Threading.Thread.Sleep(10);
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
                LogError($"[ManageNetwork] 文件下载失败: {e.Message}");
                return Response.Error($"文件下载失败: {e.Message}");
            }
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        private object UploadFile(JObject args)
        {
            try
            {
                string url = args["url"]?.ToString();
                string filePath = args["file_path"]?.ToString();

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
                if (args["form_data"] is JObject formData)
                {
                    foreach (var pair in formData.Properties())
                    {
                        form.AddField(pair.Name, pair.Value.ToString());
                    }
                }

                int timeout = args["timeout"]?.ToObject<int>() ?? 60;

                using (var request = UnityWebRequest.Post(url, form))
                {
                    request.timeout = timeout;
                    ConfigureRequest(request, args, timeout, "Unity-MCP-Uploader/1.0", true, true);

                    var asyncOp = request.SendWebRequest();

                    // 等待上传完成
                    var startTime = EditorApplication.timeSinceStartup;
                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < timeout)
                    {
                        System.Threading.Thread.Sleep(10);
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
                LogError($"[ManageNetwork] 文件上传失败: {e.Message}");
                return Response.Error($"文件上传失败: {e.Message}");
            }
        }

        /// <summary>
        /// PING主机
        /// </summary>
        private object PingHost(JObject args)
        {
            try
            {
                string url = args["url"]?.ToString();
                if (string.IsNullOrEmpty(url))
                {
                    return Response.Error("URL参数是必需的");
                }

                // 简单的连通性测试，使用HEAD请求
                using (var request = UnityWebRequest.Head(url))
                {
                    request.timeout = args["timeout"]?.ToObject<int>() ?? 10;

                    var startTime = EditorApplication.timeSinceStartup;
                    var asyncOp = request.SendWebRequest();

                    while (!asyncOp.isDone && (EditorApplication.timeSinceStartup - startTime) < request.timeout)
                    {
                        System.Threading.Thread.Sleep(10);
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
                LogError($"[ManageNetwork] Ping失败: {e.Message}");
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

                    // 等待重试
                    if (retryDelay > 0)
                    {
                        System.Threading.Thread.Sleep((int)(retryDelay * 1000));
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
                        System.Threading.Thread.Sleep((int)(retryDelay * 1000));
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
        /// 批量下载文件
        /// </summary>
        private object BatchDownload(JObject args)
        {
            try
            {
                var urlsToken = args["urls"];
                string saveDirectory = args["save_directory"]?.ToString() ?? args["save_path"]?.ToString();

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

                int timeout = args["timeout"]?.ToObject<int>() ?? 60;
                var downloadResults = new List<object>();
                var errors = new List<string>();

                LogInfo($"[ManageHttp] 开始批量下载 {urls.Length} 个文件到 {fullSaveDirectory}");

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
                        if (args["headers"] != null) downloadArgs["headers"] = args["headers"];
                        if (args["auth_token"] != null) downloadArgs["auth_token"] = args["auth_token"];
                        if (args["basic_auth"] != null) downloadArgs["basic_auth"] = args["basic_auth"];
                        if (args["user_agent"] != null) downloadArgs["user_agent"] = args["user_agent"];

                        LogInfo($"[ManageHttp] 下载文件 {i + 1}/{urls.Length}: {url}");

                        // 调用单个文件下载方法
                        var result = DownloadFile(downloadArgs);

                        downloadResults.Add(new
                        {
                            url = url,
                            file_path = filePath,
                            file_name = fileName,
                            success = IsSuccessResponse(result),
                            result = result
                        });
                    }
                    catch (Exception e)
                    {
                        string error = $"下载失败 {url}: {e.Message}";
                        errors.Add(error);
                        LogError($"[ManageHttp] {error}");

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
                LogError($"[ManageHttp] 批量下载失败: {e.Message}");
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
