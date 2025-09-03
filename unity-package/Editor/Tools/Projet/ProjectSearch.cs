using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Project window search operations for finding assets and objects.
    /// 对应方法名: project_search
    /// </summary>
    [ToolName("project_search")]
    public class ProjectSearch : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("search_target", "搜索类型：asset, folder, script, texture, material, prefab, scene等", false),
                new MethodKey("query", "搜索关键词", false),
                new MethodKey("directory", "搜索路径（相对于Assets）", true),
                new MethodKey("file_extension", "文件扩展名过滤", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("case_sensitive", "是否区分大小写", true),
                new MethodKey("max_results", "最大返回结果数", true),
                new MethodKey("include_meta", "是否包含.meta文件", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("search_target")
                     .Leaf("asset", HandleAssetSearch)
                     .Leaf("folder", HandleFolderSearch)
                     .Leaf("script", HandleScriptSearch)
                     .Leaf("texture", HandleTextureSearch)
                     .Leaf("material", HandleMaterialSearch)
                     .Leaf("prefab", HandlePrefabSearch)
                     .Leaf("scene", HandleSceneSearch)
                     .Leaf("audio", HandleAudioSearch)
                     .Leaf("model", HandleModelSearch)
                     .Leaf("shader", HandleShaderSearch)
                     .Leaf("animation", HandleAnimationSearch)
                     .Leaf("general", HandleGeneralSearch)
                .Build();
        }

        /// <summary>
        /// 搜索所有类型的资产
        /// </summary>
        private object HandleAssetSearch(JObject args)
        {
            return PerformSearch(args, null);
        }

        /// <summary>
        /// 搜索文件夹
        /// </summary>
        private object HandleFolderSearch(JObject args)
        {
            return PerformSearch(args, "folder");
        }

        /// <summary>
        /// 搜索脚本文件
        /// </summary>
        private object HandleScriptSearch(JObject args)
        {
            return PerformSearch(args, "script", new[] { ".cs", ".js", ".boo" });
        }

        /// <summary>
        /// 搜索纹理文件
        /// </summary>
        private object HandleTextureSearch(JObject args)
        {
            return PerformSearch(args, "texture", new[] { ".png", ".jpg", ".jpeg", ".tga", ".tiff", ".bmp", ".psd", ".exr" });
        }

        /// <summary>
        /// 搜索材质文件
        /// </summary>
        private object HandleMaterialSearch(JObject args)
        {
            return PerformSearch(args, "material", new[] { ".mat" });
        }

        /// <summary>
        /// 搜索预制体文件
        /// </summary>
        private object HandlePrefabSearch(JObject args)
        {
            return PerformSearch(args, "prefab", new[] { ".prefab" });
        }

        /// <summary>
        /// 搜索场景文件
        /// </summary>
        private object HandleSceneSearch(JObject args)
        {
            return PerformSearch(args, "scene", new[] { ".unity" });
        }

        /// <summary>
        /// 搜索音频文件
        /// </summary>
        private object HandleAudioSearch(JObject args)
        {
            return PerformSearch(args, "audio", new[] { ".mp3", ".wav", ".ogg", ".aiff", ".aif" });
        }

        /// <summary>
        /// 搜索3D模型文件
        /// </summary>
        private object HandleModelSearch(JObject args)
        {
            return PerformSearch(args, "model", new[] { ".fbx", ".obj", ".dae", ".3ds", ".dxf", ".skp", ".blend", ".max", ".c4d", ".ma", ".mb" });
        }

        /// <summary>
        /// 搜索Shader文件
        /// </summary>
        private object HandleShaderSearch(JObject args)
        {
            return PerformSearch(args, "shader", new[] { ".shader", ".cginc", ".hlsl" });
        }

        /// <summary>
        /// 搜索动画文件
        /// </summary>
        private object HandleAnimationSearch(JObject args)
        {
            return PerformSearch(args, "animation", new[] { ".anim", ".controller", ".playable" });
        }

        /// <summary>
        /// 通用搜索
        /// </summary>
        private object HandleGeneralSearch(JObject args)
        {
            return PerformSearch(args, null);
        }

        /// <summary>
        /// 执行搜索的主要实现
        /// </summary>
        private object PerformSearch(JObject args, string searchType, string[] extensions = null)
        {
            string searchTerm = args["query"]?.ToString();
            string searchPath = args["directory"]?.ToString() ?? "Assets";
            bool recursive = args["recursive"]?.ToObject<bool>() ?? true;
            bool caseSensitive = args["case_sensitive"]?.ToObject<bool>() ?? false;
            int maxResults = args["max_results"]?.ToObject<int>() ?? 100;
            bool includeMeta = args["include_meta"]?.ToObject<bool>() ?? false;

            // 验证搜索路径
            if (!searchPath.StartsWith("Assets/") && searchPath != "Assets")
            {
                searchPath = "Assets/" + searchPath.TrimStart('/');
            }

            try
            {
                // 用JArray来序列化结果，确保兼容JSON序列化
                List<JObject> results = new List<JObject>();

                // 获取所有资产GUID
                string[] guids = AssetDatabase.FindAssets(searchTerm, new[] { searchPath });

                foreach (string guid in guids)
                {
                    if (results.Count >= maxResults)
                        break;

                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    // 检查是否在指定路径范围内
                    if (!IsInSearchPath(assetPath, searchPath, recursive))
                        continue;

                    // 检查文件扩展名
                    if (extensions != null && !extensions.Any(ext => assetPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // 跳过.meta文件（除非明确要求包含）
                    if (!includeMeta && assetPath.EndsWith(".meta"))
                        continue;

                    // 获取资产对象
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset != null)
                    {
                        var assetInfo = GetAssetInfo(asset, assetPath, guid);
                        // assetInfo本身是JObject，直接加入JArray
                        results.Add(assetInfo);
                    }
                }

                string message = $"Found {results.Count} asset(s) matching '{searchTerm}' in '{searchPath}'";
                if (searchType != null)
                {
                    message += $" (type: {searchType})";
                }

                // 用JObject包装返回，保证序列化友好
                var resultObj = new JObject
                {
                    ["query"] = searchTerm,
                    ["directory"] = searchPath,
                    ["search_target"] = searchType,
                    ["total_results"] = results.Count,
                    ["max_results"] = maxResults,
                    ["results"] = JToken.FromObject(results),
                };
                return Response.Success(message, resultObj);
            }
            catch (Exception ex)
            {
                return Response.Error($"Search failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查资产路径是否在搜索路径范围内
        /// </summary>
        private bool IsInSearchPath(string assetPath, string searchPath, bool recursive)
        {
            if (assetPath.StartsWith(searchPath))
            {
                if (recursive)
                    return true;

                // 如果不递归，检查是否在直接子目录中
                string relativePath = assetPath.Substring(searchPath.Length).TrimStart('/');
                return !relativePath.Contains('/');
            }
            return false;
        }

        /// <summary>
        /// 获取资产的详细信息
        /// </summary>
        private JObject GetAssetInfo(UnityEngine.Object asset, string assetPath, string guid)
        {
            var info = new JObject
            {
                ["name"] = asset.name,
                ["path"] = assetPath,
                ["guid"] = guid,
                ["type"] = asset.GetType().Name,
                ["instanceID"] = asset.GetInstanceID()
            };

            // 根据资产类型添加特定信息
            if (asset is Texture2D texture)
            {
                info["width"] = texture.width;
                info["height"] = texture.height;
                info["format"] = texture.format.ToString();
            }
            else if (asset is Material material)
            {
                info["shader"] = material.shader?.name ?? "None";
            }
            else if (asset is GameObject prefab)
            {
                info["prefabType"] = PrefabUtility.GetPrefabAssetType(prefab).ToString();
            }
            else if (asset is SceneAsset scene)
            {
                info["sceneName"] = scene.name;
            }
            else if (asset is AudioClip audio)
            {
                info["length"] = audio.length;
                info["frequency"] = audio.frequency;
                info["channels"] = audio.channels;
            }
            else if (asset is Mesh mesh)
            {
                info["vertexCount"] = mesh.vertexCount;
                info["triangleCount"] = mesh.triangles.Length / 3;
            }
            else if (asset is ScriptableObject scriptableObject)
            {
                info["scriptableObjectType"] = scriptableObject.GetType().FullName;
            }

            // 添加文件信息
            try
            {
                var fileInfo = new System.IO.FileInfo(assetPath);
                info["fileSize"] = fileInfo.Length;
                info["lastModified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                // 忽略文件信息获取错误
            }

            return info;
        }

        /// <summary>
        /// 搜索特定类型的资产
        /// </summary>
        private object SearchByType<T>(string searchTerm, string searchPath, bool recursive, int maxResults) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name} {searchTerm}", new[] { searchPath });
            List<object> results = new List<object>();

            foreach (string guid in guids)
            {
                if (results.Count >= maxResults)
                    break;

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsInSearchPath(assetPath, searchPath, recursive))
                    continue;

                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    var assetInfo = GetAssetInfo(asset, assetPath, guid);
                    results.Add(assetInfo);
                }
            }

            return results;
        }


    }
}