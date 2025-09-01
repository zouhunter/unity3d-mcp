using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class
using UnityMcp.Tools; // 添加这个引用

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Unity ScriptableObject asset management operations including create, modify, duplicate, etc.
    /// 对应方法名: manage_scriptableobject
    /// </summary>
    [ToolName("edit_scriptableobject")]
    public class EditScriptableObject : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create, modify, duplicate, get_info, search等", false),
                new MethodKey("path", "ScriptableObject资产路径，Unity标准格式：Assets/Folder/AssetName.asset", false),
                new MethodKey("script_class", "ScriptableObject脚本类名（创建时需要）", true),
                new MethodKey("properties", "资产属性字典，用于设置ScriptableObject的各种属性", true),
                new MethodKey("destination", "目标路径（复制时使用）", true),
                new MethodKey("search_pattern", "搜索模式，如*.asset等（搜索时使用）", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        /// <returns>状态树</returns>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("create", CreateScriptableObject)
                    .Leaf("modify", ModifyScriptableObject)
                    .Leaf("duplicate", DuplicateScriptableObject)
                    .Leaf("search", SearchScriptableObjects)
                    .Leaf("get_info", GetScriptableObjectInfo)
                .Build();
        }

        private object CreateScriptableObject(JObject args)
        {
            string path = args["path"]?.ToString();
            string scriptClassName = args["script_class"]?.ToString();
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create.");
            if (string.IsNullOrEmpty(scriptClassName))
                return Response.Error("'script_class' is required for create.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // Ensure directory exists
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh(); // Make sure Unity knows about the new folder
            }

            if (AssetExists(fullPath))
                return Response.Error($"ScriptableObject already exists at path: {fullPath}");

            try
            {
                Type scriptType = FindType(scriptClassName);
                if (scriptType == null || !typeof(ScriptableObject).IsAssignableFrom(scriptType))
                {
                    return Response.Error(
                        $"Script class '{scriptClassName}' not found or does not inherit from ScriptableObject."
                    );
                }

                ScriptableObject so = ScriptableObject.CreateInstance(scriptType);

                // Apply properties from JObject to the ScriptableObject instance
                if (properties != null && properties.HasValues)
                {
                    bool soModified = ApplyObjectProperties(so, properties);
                    if (soModified)
                    {
                        EditorUtility.SetDirty(so);
                    }
                }

                AssetDatabase.CreateAsset(so, fullPath);
                AssetDatabase.SaveAssets();

                return Response.Success(
                    $"ScriptableObject '{fullPath}' created successfully.",
                    GetScriptableObjectData(fullPath)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create ScriptableObject at '{fullPath}': {e.Message}");
            }
        }

        private object ModifyScriptableObject(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject properties = args["properties"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");
            if (properties == null || !properties.HasValues)
                return Response.Error("'properties' are required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"ScriptableObject not found at path: {fullPath}");

            try
            {
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(fullPath);
                if (so == null)
                    return Response.Error($"Failed to load ScriptableObject at path: {fullPath}");

                // Record the asset state for Undo before making any modifications
                Undo.RecordObject(so, $"Modify ScriptableObject '{Path.GetFileName(fullPath)}'");

                bool modified = ApplyObjectProperties(so, properties);

                if (modified)
                {
                    // Mark the asset as dirty so Unity knows to save it
                    EditorUtility.SetDirty(so);
                    // Save all modified assets to disk
                    AssetDatabase.SaveAssets();

                    return Response.Success(
                        $"ScriptableObject '{fullPath}' modified successfully.",
                        GetScriptableObjectData(fullPath)
                    );
                }
                else
                {
                    return Response.Success(
                        $"No applicable properties found to modify for ScriptableObject '{fullPath}'.",
                        GetScriptableObjectData(fullPath)
                    );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageScriptableObject] Action 'modify' failed for path '{path}': {e}");
                return Response.Error($"Failed to modify ScriptableObject '{fullPath}': {e.Message}");
            }
        }

        private object DuplicateScriptableObject(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source ScriptableObject not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                // Generate a unique path if destination is not provided
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"ScriptableObject already exists at destination path: {destPath}");
                // Ensure destination directory exists
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    return Response.Success(
                        $"ScriptableObject '{sourcePath}' duplicated to '{destPath}'.",
                        GetScriptableObjectData(destPath)
                    );
                }
                else
                {
                    return Response.Error(
                        $"Failed to duplicate ScriptableObject from '{sourcePath}' to '{destPath}'."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating ScriptableObject '{sourcePath}': {e.Message}");
            }
        }

        private object SearchScriptableObjects(JObject args)
        {
            string searchPattern = args["search_pattern"]?.ToString();
            string pathScope = args["path"]?.ToString(); // Use path as folder scope
            int pageSize = args["page_size"]?.ToObject<int?>() ?? 50; // Default page size
            int pageNumber = args["page_number"]?.ToObject<int?>() ?? 1; // Default page number (1-based)
            bool generatePreview = args["generate_preview"]?.ToObject<bool>() ?? false;

            List<string> searchFilters = new List<string>();
            if (!string.IsNullOrEmpty(searchPattern))
                searchFilters.Add(searchPattern);

            // Always filter for ScriptableObject type
            searchFilters.Add("t:ScriptableObject");

            string[] folderScope = null;
            if (!string.IsNullOrEmpty(pathScope))
            {
                folderScope = new string[] { SanitizeAssetPath(pathScope) };
                if (!AssetDatabase.IsValidFolder(folderScope[0]))
                {
                    Debug.LogWarning(
                        $"Search path '{folderScope[0]}' is not a valid folder. Searching entire project."
                    );
                    folderScope = null; // Search everywhere if path isn't a folder
                }
            }

            try
            {
                string[] guids = AssetDatabase.FindAssets(
                    string.Join(" ", searchFilters),
                    folderScope
                );
                List<object> results = new List<object>();
                int totalFound = 0;

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    // Verify it's actually a ScriptableObject
                    ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (so == null)
                        continue;

                    totalFound++; // Count matching assets before pagination
                    results.Add(GetScriptableObjectData(assetPath, generatePreview));
                }

                // Apply pagination
                int startIndex = (pageNumber - 1) * pageSize;
                var pagedResults = results.Skip(startIndex).Take(pageSize).ToList();

                return Response.Success(
                    $"Found {totalFound} ScriptableObject(s). Returning page {pageNumber} ({pagedResults.Count} assets).",
                    new
                    {
                        totalAssets = totalFound,
                        pageSize = pageSize,
                        pageNumber = pageNumber,
                        assets = pagedResults,
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching ScriptableObjects: {e.Message}");
            }
        }

        private object GetScriptableObjectInfo(JObject args)
        {
            string path = args["path"]?.ToString();
            bool generatePreview = args["generate_preview"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");
            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"ScriptableObject not found at path: {fullPath}");

            try
            {
                return Response.Success(
                    "ScriptableObject info retrieved.",
                    GetScriptableObjectData(fullPath, generatePreview)
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for ScriptableObject '{fullPath}': {e.Message}");
            }
        }

        // --- Internal Helpers ---

        /// <summary>
        /// Ensures the asset path starts with "Assets/".
        /// </summary>
        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/'); // Normalize separators
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// Checks if an asset exists at the given path.
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            {
                return true;
            }
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ensures the directory for a given asset path exists, creating it if necessary.
        /// </summary>
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
            string fullDirPath = Path.Combine(Directory.GetCurrentDirectory(), directoryPath);
            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
                AssetDatabase.Refresh(); // Let Unity know about the new folder
            }
        }

        /// <summary>
        /// Generic helper to set properties on any UnityEngine.Object using reflection.
        /// </summary>
        private bool ApplyObjectProperties(UnityEngine.Object target, JObject properties)
        {
            if (target == null || properties == null)
                return false;
            bool modified = false;
            Type type = target.GetType();

            foreach (var prop in properties.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;
                if (SetPropertyOrField(target, propName, propValue, type))
                {
                    modified = true;
                }
            }
            return modified;
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types and Unity objects.
        /// </summary>
        private bool SetPropertyOrField(
            object target,
            string memberName,
            JToken value,
            Type type = null
        )
        {
            type = type ?? target.GetType();
            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.IgnoreCase;

            try
            {
                System.Reflection.PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType);
                    if (
                        convertedValue != null
                        && !object.Equals(propInfo.GetValue(target), convertedValue)
                    )
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    System.Reflection.FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(value, fieldInfo.FieldType);
                        if (
                            convertedValue != null
                            && !object.Equals(fieldInfo.GetValue(target), convertedValue)
                        )
                        {
                            fieldInfo.SetValue(target, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SetPropertyOrField] Failed to set '{memberName}' on {type.Name}: {ex.Message}"
                );
            }
            return false;
        }

        /// <summary>
        /// Simple JToken to Type conversion for common Unity types and primitives.
        /// </summary>
        private object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                if (token == null || token.Type == JTokenType.Null)
                    return null;

                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();
                if (targetType == typeof(Vector2) && token is JArray arrV2 && arrV2.Count == 2)
                    return new Vector2(arrV2[0].ToObject<float>(), arrV2[1].ToObject<float>());
                if (targetType == typeof(Vector3) && token is JArray arrV3 && arrV3.Count == 3)
                    return new Vector3(
                        arrV3[0].ToObject<float>(),
                        arrV3[1].ToObject<float>(),
                        arrV3[2].ToObject<float>()
                    );
                if (targetType == typeof(Vector4) && token is JArray arrV4 && arrV4.Count == 4)
                    return new Vector4(
                        arrV4[0].ToObject<float>(),
                        arrV4[1].ToObject<float>(),
                        arrV4[2].ToObject<float>(),
                        arrV4[3].ToObject<float>()
                    );
                if (targetType == typeof(Quaternion) && token is JArray arrQ && arrQ.Count == 4)
                    return new Quaternion(
                        arrQ[0].ToObject<float>(),
                        arrQ[1].ToObject<float>(),
                        arrQ[2].ToObject<float>(),
                        arrQ[3].ToObject<float>()
                    );
                if (targetType == typeof(Color) && token is JArray arrC && arrC.Count >= 3) // Allow RGB or RGBA
                    return new Color(
                        arrC[0].ToObject<float>(),
                        arrC[1].ToObject<float>(),
                        arrC[2].ToObject<float>(),
                        arrC.Count > 3 ? arrC[3].ToObject<float>() : 1.0f
                    );
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true); // Case-insensitive enum parsing

                // Handle loading Unity Objects (Materials, Textures, etc.) by path
                if (
                    typeof(UnityEngine.Object).IsAssignableFrom(targetType)
                    && token.Type == JTokenType.String
                )
                {
                    string assetPath = SanitizeAssetPath(token.ToString());
                    UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(
                        assetPath,
                        targetType
                    );
                    if (loadedAsset == null)
                    {
                        Debug.LogWarning(
                            $"[ConvertJTokenToType] Could not load asset of type {targetType.Name} from path: {assetPath}"
                        );
                    }
                    return loadedAsset;
                }

                // Fallback: Try direct conversion (might work for other simple value types)
                return token.ToObject(targetType);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ConvertJTokenToType] Could not convert JToken '{token}' (type {token.Type}) to type '{targetType.Name}': {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Helper to find a Type by name, searching relevant assemblies.
        /// Needed for creating ScriptableObjects or finding component types by name.
        /// </summary>
        private Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // Try direct lookup first (common Unity types often don't need assembly qualified name)
            var type =
                Type.GetType(typeName)
                ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule")
                ?? Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI")
                ?? Type.GetType($"UnityEditor.{typeName}, UnityEditor.CoreModule");

            if (type != null)
                return type;

            // If not found, search loaded assemblies (slower but more robust for user scripts)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Look for non-namespaced first
                type = assembly.GetType(typeName, false, true); // throwOnError=false, ignoreCase=true
                if (type != null)
                    return type;

                // Check common namespaces if simple name given
                type = assembly.GetType("UnityEngine." + typeName, false, true);
                if (type != null)
                    return type;
                type = assembly.GetType("UnityEditor." + typeName, false, true);
                if (type != null)
                    return type;
                // Add other likely namespaces if needed (e.g., specific plugins)
            }

            Debug.LogWarning($"[FindType] Type '{typeName}' not found in any loaded assembly.");
            return null; // Not found
        }

        // --- Data Serialization ---

        /// <summary>
        /// Creates a serializable representation of a ScriptableObject asset.
        /// </summary>
        private object GetScriptableObjectData(string path, bool generatePreview = false)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            string previewBase64 = null;
            int previewWidth = 0;
            int previewHeight = 0;

            if (generatePreview && so != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(so);

                if (preview != null)
                {
                    try
                    {
                        // Ensure texture is readable for EncodeToPNG
                        // Creating a temporary readable copy is safer
                        RenderTexture rt = RenderTexture.GetTemporary(
                            preview.width,
                            preview.height
                        );
                        Graphics.Blit(preview, rt);
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        Texture2D readablePreview = new Texture2D(preview.width, preview.height);
                        readablePreview.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        readablePreview.Apply();
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(rt);

                        byte[] pngData = readablePreview.EncodeToPNG();
                        previewBase64 = Convert.ToBase64String(pngData);
                        previewWidth = readablePreview.width;
                        previewHeight = readablePreview.height;
                        UnityEngine.Object.DestroyImmediate(readablePreview); // Clean up temp texture
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Failed to generate readable preview for '{path}': {ex.Message}. Preview might not be readable."
                        );
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"Could not get asset preview for {path} (Type: {assetType?.Name}). Is it supported?"
                    );
                }
            }

            return new
            {
                path = path,
                guid = guid,
                assetType = assetType?.FullName ?? "Unknown",
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                isScriptableObject = so != null,
                instanceID = so?.GetInstanceID() ?? 0,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                        Path.Combine(Directory.GetCurrentDirectory(), path)
                    )
                    .ToString("o"), // ISO 8601
                // --- Preview Data ---
                previewBase64 = previewBase64, // PNG data as Base64 string
                previewWidth = previewWidth,
                previewHeight = previewHeight,
            };
        }
    }
}