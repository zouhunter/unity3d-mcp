using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles GameObject modification operations.
    /// Scene hierarchy and creation operations are handled by ManageHierarchy.
    /// Component operations are handled by ManageComponent.
    /// 对应方法名: gameobject_modify
    /// </summary>
    [ToolName("gameobject_modify")]
    public class GameObjectModify : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("search_method", "搜索方法：by_name, by_id, by_tag, by_layer等", false),
                new MethodKey("target", "目标GameObject标识符（名称、ID或路径）", false),
                new MethodKey("name", "GameObject名称", true),
                new MethodKey("tag", "GameObject标签", true),
                new MethodKey("layer", "GameObject所在层", true),
                new MethodKey("parent", "父对象标识符", true),
                new MethodKey("position", "位置坐标 [x, y, z]", true),
                new MethodKey("rotation", "旋转角度 [x, y, z]", true),
                new MethodKey("scale", "缩放比例 [x, y, z]", true),
                new MethodKey("set_active", "设置激活状态", true),
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Leaf("by_name", HandleByNameSearch)
                .Leaf("by_id", HandleByIdSearch)
                .Leaf("by_tag", HandleByTagSearch)
                .Leaf("by_layer", HandleByLayerSearch)
                .Leaf("by_component", HandleByComponentSearch)
                .Leaf("by_path", HandleByPathSearch)
                .Leaf("default", HandleDefaultSearch)
                .Build();
        }

        /// <summary>
        /// 按名称搜索并修改GameObject
        /// </summary>
        private object HandleByNameSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_name search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_name");
            if (targetGo == null)
            {
                return Response.Error($"GameObject with name '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 按ID搜索并修改GameObject
        /// </summary>
        private object HandleByIdSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_id search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_id");
            if (targetGo == null)
            {
                return Response.Error($"GameObject with ID '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 按标签搜索并修改GameObject
        /// </summary>
        private object HandleByTagSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_tag search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_tag");
            if (targetGo == null)
            {
                return Response.Error($"GameObject with tag '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 按层级搜索并修改GameObject
        /// </summary>
        private object HandleByLayerSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_layer search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_layer");
            if (targetGo == null)
            {
                return Response.Error($"GameObject in layer '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 按组件搜索并修改GameObject
        /// </summary>
        private object HandleByComponentSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_component search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_component");
            if (targetGo == null)
            {
                return Response.Error($"GameObject with component '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 按路径搜索并修改GameObject
        /// </summary>
        private object HandleByPathSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required for by_path search method.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_path");
            if (targetGo == null)
            {
                return Response.Error($"GameObject at path '{targetToken}' not found.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 默认搜索方法（兼容性）
        /// </summary>
        private object HandleDefaultSearch(JObject args)
        {
            JToken targetToken = args["target"];
            if (targetToken == null)
            {
                return Response.Error("Target parameter is required.");
            }

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args);
            if (redirectResult != null)
                return redirectResult;

            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, "by_id_or_name_or_path");
            if (targetGo == null)
            {
                return Response.Error($"GameObject '{targetToken}' not found using default search method.");
            }

            return ApplyModifications(targetGo, args);
        }

        /// <summary>
        /// 检查预制体重定向逻辑
        /// </summary>
        private object CheckPrefabRedirection(JObject args)
        {
            JToken targetToken = args["target"];
            string targetPath = targetToken?.Type == JTokenType.String ? targetToken.ToString() : null;

            if (string.IsNullOrEmpty(targetPath) || !targetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return null; // 不是预制体，继续正常处理

            return Response.Error($"Prefab modification should be performed using the 'manage_asset' command.");
        }

        /// <summary>
        /// 应用修改到GameObject
        /// </summary>
        private object ApplyModifications(GameObject targetGo, JObject args)
        {
            // Record state for Undo *before* modifications
            Undo.RecordObject(targetGo.transform, "Modify GameObject Transform");
            Undo.RecordObject(targetGo, "Modify GameObject Properties");

            bool modified = false;

            // 应用名称修改
            modified |= ApplyNameModification(targetGo, args);

            // 应用父对象修改
            object parentResult = ApplyParentModification(targetGo, args);
            if (parentResult != null)
                return parentResult;
            modified |= parentResult != null;

            // 应用激活状态修改
            modified |= ApplyActiveStateModification(targetGo, args);

            // 应用标签修改
            object tagResult = ApplyTagModification(targetGo, args);
            if (tagResult != null)
                return tagResult;
            modified |= tagResult != null;

            // 应用层级修改
            modified |= ApplyLayerModification(targetGo, args);

            // 应用变换修改
            modified |= ApplyTransformModifications(targetGo, args);

            if (!modified)
            {
                return Response.Success(
                    $"No modifications applied to GameObject '{targetGo.name}'.",
                    GameObjectUtils.GetGameObjectData(targetGo)
                );
            }

            EditorUtility.SetDirty(targetGo); // Mark scene as dirty
            return Response.Success(
                $"GameObject '{targetGo.name}' modified successfully.",
                GameObjectUtils.GetGameObjectData(targetGo)
            );
        }

        /// <summary>
        /// 应用名称修改
        /// </summary>
        private bool ApplyNameModification(GameObject targetGo, JObject args)
        {
            string name = args["name"]?.ToString();
            if (!string.IsNullOrEmpty(name) && targetGo.name != name)
            {
                targetGo.name = name;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 应用父对象修改
        /// </summary>
        private object ApplyParentModification(GameObject targetGo, JObject args)
        {
            JToken parentToken = args["parent"];
            if (parentToken != null)
            {
                GameObject newParentGo = GameObjectUtils.FindObjectInternal(parentToken, "by_id_or_name_or_path");
                if (
                    newParentGo == null
                    && !(
                        parentToken.Type == JTokenType.Null
                        || (
                            parentToken.Type == JTokenType.String
                            && string.IsNullOrEmpty(parentToken.ToString())
                        )
                    )
                )
                {
                    return Response.Error($"New parent ('{parentToken}') not found.");
                }
                // Check for hierarchy loops
                if (newParentGo != null && newParentGo.transform.IsChildOf(targetGo.transform))
                {
                    return Response.Error(
                        $"Cannot parent '{targetGo.name}' to '{newParentGo.name}', as it would create a hierarchy loop."
                    );
                }
                if (targetGo.transform.parent != (newParentGo?.transform))
                {
                    targetGo.transform.SetParent(newParentGo?.transform, true); // worldPositionStays = true
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用激活状态修改
        /// </summary>
        private bool ApplyActiveStateModification(GameObject targetGo, JObject args)
        {
            bool? setActive = args["setActive"]?.ToObject<bool?>();
            if (setActive.HasValue && targetGo.activeSelf != setActive.Value)
            {
                targetGo.SetActive(setActive.Value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 应用标签修改
        /// </summary>
        private object ApplyTagModification(GameObject targetGo, JObject args)
        {
            string tag = args["tag"]?.ToString();
            // Only attempt to change tag if a non-null tag is provided and it's different from the current one.
            // Allow setting an empty string to remove the tag (Unity uses "Untagged").
            if (tag != null && targetGo.tag != tag)
            {
                // Ensure the tag is not empty, if empty, it means "Untagged" implicitly
                string tagToSet = string.IsNullOrEmpty(tag) ? "Untagged" : tag;

                try
                {
                    // First attempt to set the tag
                    targetGo.tag = tagToSet;
                    return true;
                }
                catch (UnityException ex)
                {
                    // Check if the error is specifically because the tag doesn't exist
                    if (ex.Message.Contains("is not defined"))
                    {
                        LogInfo($"[GameObjectModify] Tag '{tagToSet}' not found. Attempting to create it.");
                        try
                        {
                            // Attempt to create the tag using internal utility
                            InternalEditorUtility.AddTag(tagToSet);
                            // Wait a frame maybe? Not strictly necessary but sometimes helps editor updates.
                            // yield return null; // Cannot yield here, editor script limitation

                            // Retry setting the tag immediately after creation
                            targetGo.tag = tagToSet;
                            LogInfo($"[GameObjectModify] Tag '{tagToSet}' created and assigned successfully.");
                            return true;
                        }
                        catch (Exception innerEx)
                        {
                            // Handle failure during tag creation or the second assignment attempt
                            Debug.LogError(
                                $"[GameObjectModify] Failed to create or assign tag '{tagToSet}' after attempting creation: {innerEx.Message}"
                            );
                            return Response.Error(
                                $"Failed to create or assign tag '{tagToSet}': {innerEx.Message}. Check Tag Manager and permissions."
                            );
                        }
                    }
                    else
                    {
                        // If the exception was for a different reason, return the original error
                        return Response.Error($"Failed to set tag to '{tagToSet}': {ex.Message}.");
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 应用层级修改
        /// </summary>
        private bool ApplyLayerModification(GameObject targetGo, JObject args)
        {
            string layerName = args["layer"]?.ToString();
            if (!string.IsNullOrEmpty(layerName))
            {
                int layerId = LayerMask.NameToLayer(layerName);
                if (layerId == -1 && layerName != "Default")
                {
                    Debug.LogWarning($"Invalid layer specified: '{layerName}'. Use a valid layer name.");
                    return false;
                }
                if (layerId != -1 && targetGo.layer != layerId)
                {
                    targetGo.layer = layerId;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用变换修改
        /// </summary>
        private bool ApplyTransformModifications(GameObject targetGo, JObject args)
        {
            bool modified = false;

            Vector3? position = GameObjectUtils.ParseVector3(args["position"] as JArray);
            Vector3? rotation = GameObjectUtils.ParseVector3(args["rotation"] as JArray);
            Vector3? scale = GameObjectUtils.ParseVector3(args["scale"] as JArray);

            if (position.HasValue && targetGo.transform.localPosition != position.Value)
            {
                targetGo.transform.localPosition = position.Value;
                modified = true;
            }
            if (rotation.HasValue && targetGo.transform.localEulerAngles != rotation.Value)
            {
                targetGo.transform.localEulerAngles = rotation.Value;
                modified = true;
            }
            if (scale.HasValue && targetGo.transform.localScale != scale.Value)
            {
                targetGo.transform.localScale = scale.Value;
                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Helper to set a property or field via reflection, handling basic types.
        /// </summary>
        private bool SetProperty(object target, string memberName, JToken value)
        {
            Type type = target.GetType();
            BindingFlags flags =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                // Handle special case for materials with dot notation (material.property)
                // Examples: material.color, sharedMaterial.color, materials[0].color
                if (memberName.Contains('.') || memberName.Contains('['))
                {
                    return SetNestedProperty(target, memberName, value);
                }

                PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(value, fieldInfo.FieldType);
                        if (convertedValue != null)
                        {
                            fieldInfo.SetValue(target, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SetProperty] Failed to set '{memberName}' on {type.Name}: {ex.Message}"
                );
            }
            return false;
        }

        /// <summary>
        /// Sets a nested property using dot notation (e.g., "material.color") or array access (e.g., "materials[0]")
        /// </summary>
        private bool SetNestedProperty(object target, string path, JToken value)
        {
            try
            {
                // Split the path into parts (handling both dot notation and array indexing)
                string[] pathParts = SplitPropertyPath(path);
                if (pathParts.Length == 0)
                    return false;

                object currentObject = target;
                Type currentType = currentObject.GetType();
                BindingFlags flags =
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

                // Traverse the path until we reach the final property
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    string part = pathParts[i];
                    bool isArray = false;
                    int arrayIndex = -1;

                    // Check if this part contains array indexing
                    if (part.Contains("["))
                    {
                        int startBracket = part.IndexOf('[');
                        int endBracket = part.IndexOf(']');
                        if (startBracket > 0 && endBracket > startBracket)
                        {
                            string indexStr = part.Substring(
                                startBracket + 1,
                                endBracket - startBracket - 1
                            );
                            if (int.TryParse(indexStr, out arrayIndex))
                            {
                                isArray = true;
                                part = part.Substring(0, startBracket);
                            }
                        }
                    }

                    // Get the property/field
                    PropertyInfo propInfo = currentType.GetProperty(part, flags);
                    FieldInfo fieldInfo = null;
                    if (propInfo == null)
                    {
                        fieldInfo = currentType.GetField(part, flags);
                        if (fieldInfo == null)
                        {
                            Debug.LogWarning(
                                $"[SetNestedProperty] Could not find property or field '{part}' on type '{currentType.Name}'"
                            );
                            return false;
                        }
                    }

                    // Get the value
                    currentObject =
                        propInfo != null
                            ? propInfo.GetValue(currentObject)
                            : fieldInfo.GetValue(currentObject);

                    // If the current property is null, we need to stop
                    if (currentObject == null)
                    {
                        Debug.LogWarning(
                            $"[SetNestedProperty] Property '{part}' is null, cannot access nested properties."
                        );
                        return false;
                    }

                    // If this is an array/list access, get the element at the index
                    if (isArray)
                    {
                        if (currentObject is Material[])
                        {
                            var materials = currentObject as Material[];
                            if (arrayIndex < 0 || arrayIndex >= materials.Length)
                            {
                                Debug.LogWarning(
                                    $"[SetNestedProperty] Material index {arrayIndex} out of range (0-{materials.Length - 1})"
                                );
                                return false;
                            }
                            currentObject = materials[arrayIndex];
                        }
                        else if (currentObject is System.Collections.IList)
                        {
                            var list = currentObject as System.Collections.IList;
                            if (arrayIndex < 0 || arrayIndex >= list.Count)
                            {
                                Debug.LogWarning(
                                    $"[SetNestedProperty] Index {arrayIndex} out of range (0-{list.Count - 1})"
                                );
                                return false;
                            }
                            currentObject = list[arrayIndex];
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[SetNestedProperty] Property '{part}' is not an array or list, cannot access by index."
                            );
                            return false;
                        }
                    }

                    // Update type for next iteration
                    currentType = currentObject.GetType();
                }

                // Set the final property
                string finalPart = pathParts[pathParts.Length - 1];

                // Special handling for Material properties (shader properties)
                if (currentObject is Material material && finalPart.StartsWith("_"))
                {
                    // Handle various material property types
                    if (value is JArray jArray)
                    {
                        if (jArray.Count == 4) // Color with alpha
                        {
                            Color color = new Color(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetColor(finalPart, color);
                            return true;
                        }
                        else if (jArray.Count == 3) // Color without alpha
                        {
                            Color color = new Color(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                1.0f
                            );
                            material.SetColor(finalPart, color);
                            return true;
                        }
                        else if (jArray.Count == 2) // Vector2
                        {
                            Vector2 vec = new Vector2(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>()
                            );
                            material.SetVector(finalPart, vec);
                            return true;
                        }
                        else if (jArray.Count == 4) // Vector4
                        {
                            Vector4 vec = new Vector4(
                                jArray[0].ToObject<float>(),
                                jArray[1].ToObject<float>(),
                                jArray[2].ToObject<float>(),
                                jArray[3].ToObject<float>()
                            );
                            material.SetVector(finalPart, vec);
                            return true;
                        }
                    }
                    else if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                    {
                        material.SetFloat(finalPart, value.ToObject<float>());
                        return true;
                    }
                    else if (value.Type == JTokenType.Boolean)
                    {
                        material.SetFloat(finalPart, value.ToObject<bool>() ? 1f : 0f);
                        return true;
                    }
                    else if (value.Type == JTokenType.String)
                    {
                        // Might be a texture path
                        string texturePath = value.ToString();
                        if (
                            texturePath.EndsWith(".png")
                            || texturePath.EndsWith(".jpg")
                            || texturePath.EndsWith(".tga")
                        )
                        {
                            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                                texturePath
                            );
                            if (texture != null)
                            {
                                material.SetTexture(finalPart, texture);
                                return true;
                            }
                        }
                        else
                        {
                            // Materials don't have SetString, use SetTextureOffset as workaround or skip
                            // material.SetString(finalPart, texturePath);
                            Debug.LogWarning(
                                $"[SetNestedProperty] String values not directly supported for material property {finalPart}"
                            );
                            return false;
                        }
                    }

                    Debug.LogWarning(
                        $"[SetNestedProperty] Unsupported material property value type: {value.Type} for {finalPart}"
                    );
                    return false;
                }

                // For standard properties (not shader specific)
                PropertyInfo finalPropInfo = currentType.GetProperty(finalPart, flags);
                if (finalPropInfo != null && finalPropInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, finalPropInfo.PropertyType);
                    if (convertedValue != null)
                    {
                        finalPropInfo.SetValue(currentObject, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo finalFieldInfo = currentType.GetField(finalPart, flags);
                    if (finalFieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(
                            value,
                            finalFieldInfo.FieldType
                        );
                        if (convertedValue != null)
                        {
                            finalFieldInfo.SetValue(currentObject, convertedValue);
                            return true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[SetNestedProperty] Could not find final property or field '{finalPart}' on type '{currentType.Name}'"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SetNestedProperty] Error setting nested property '{path}': {ex.Message}"
                );
            }

            return false;
        }

        /// <summary>
        /// Split a property path into parts, handling both dot notation and array indexers
        /// </summary>
        private string[] SplitPropertyPath(string path)
        {
            // Handle complex paths with both dots and array indexers
            List<string> parts = new List<string>();
            int startIndex = 0;
            bool inBrackets = false;

            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];

                if (c == '[')
                {
                    inBrackets = true;
                }
                else if (c == ']')
                {
                    inBrackets = false;
                }
                else if (c == '.' && !inBrackets)
                {
                    // Found a dot separator outside of brackets
                    parts.Add(path.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            // Add the final part
            if (startIndex < path.Length)
            {
                parts.Add(path.Substring(startIndex));
            }

            return parts.ToArray();
        }

        /// <summary>
        /// Simple JToken to Type conversion for common Unity types.
        /// </summary>
        private object ConvertJTokenToType(JToken token, Type targetType)
        {
            try
            {
                // Unwrap nested material properties if we're assigning to a Material
                if (typeof(Material).IsAssignableFrom(targetType) && token is JObject materialProps)
                {
                    // Handle case where we're passing shader properties directly in a nested object
                    string materialPath = token["path"]?.ToString();
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        // Load the material by path
                        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material != null)
                        {
                            // If there are additional properties, set them
                            foreach (var prop in materialProps.Properties())
                            {
                                if (prop.Name != "path")
                                {
                                    SetProperty(material, prop.Name, prop.Value);
                                }
                            }
                            return material;
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Could not load material at path: '{materialPath}'"
                            );
                            return null;
                        }
                    }

                    // If no path is specified, could be a dynamic material or instance set by reference
                    return null;
                }

                // Basic types first
                if (targetType == typeof(string))
                    return token.ToObject<string>();
                if (targetType == typeof(int))
                    return token.ToObject<int>();
                if (targetType == typeof(float))
                    return token.ToObject<float>();
                if (targetType == typeof(bool))
                    return token.ToObject<bool>();

                // Vector/Quaternion/Color types
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

                // Enum types
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, token.ToString(), true); // Case-insensitive enum parsing

                // Handle assigning Unity Objects (Assets, Scene Objects, Components)
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    // CASE 1: Reference is a JSON Object specifying a scene object/component find criteria
                    if (token is JObject refObject)
                    {
                        JToken findToken = refObject["find"];
                        string findMethod =
                            refObject["method"]?.ToString() ?? "by_id_or_name_or_path"; // Default search
                        string componentTypeName = refObject["component"]?.ToString();

                        if (findToken == null)
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Reference object missing 'find' property: {token}"
                            );
                            return null;
                        }

                        // Find the target GameObject
                        // Pass 'searchInactive: true' for internal lookups to be more robust
                        JObject findParams = new JObject();
                        findParams["searchInactive"] = true;
                        GameObject foundGo = GameObjectUtils.FindObjectInternal(findToken, findMethod, findParams);

                        if (foundGo == null)
                        {
                            Debug.LogWarning(
                                $"[ConvertJTokenToType] Could not find GameObject specified by reference object: {token}"
                            );
                            return null;
                        }

                        // If a component type is specified, try to get it
                        if (!string.IsNullOrEmpty(componentTypeName))
                        {
                            Type compType = GameObjectUtils.FindType(componentTypeName);
                            if (compType == null)
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Could not find component type '{componentTypeName}' specified in reference object: {token}"
                                );
                                return null;
                            }

                            // Ensure the targetType is assignable from the found component type
                            if (!targetType.IsAssignableFrom(compType))
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found component '{componentTypeName}' but it is not assignable to the target property type '{targetType.Name}'. Reference: {token}"
                                );
                                return null;
                            }

                            Component foundComp = foundGo.GetComponent(compType);
                            if (foundComp == null)
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found GameObject '{foundGo.name}' but could not find component '{componentTypeName}' on it. Reference: {token}"
                                );
                                return null;
                            }
                            return foundComp; // Return the found component
                        }
                        else
                        {
                            // Otherwise, return the GameObject itself, ensuring it's assignable
                            if (!targetType.IsAssignableFrom(typeof(GameObject)))
                            {
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Found GameObject '{foundGo.name}' but it is not assignable to the target property type '{targetType.Name}' (component name was not specified). Reference: {token}"
                                );
                                return null;
                            }
                            return foundGo; // Return the found GameObject
                        }
                    }
                    // CASE 2: Reference is a string, assume it's an asset path
                    else if (token.Type == JTokenType.String)
                    {
                        string assetPath = token.ToString();
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            // Attempt to load the asset from the provided path using the target type
                            UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath(
                                assetPath,
                                targetType
                            );
                            if (loadedAsset != null)
                            {
                                return loadedAsset; // Return the loaded asset if successful
                            }
                            else
                            {
                                // Log a warning if the asset could not be found at the path
                                Debug.LogWarning(
                                    $"[ConvertJTokenToType] Could not load asset of type '{targetType.Name}' from path: '{assetPath}'. Make sure the path is correct and the asset exists."
                                );
                                return null;
                            }
                        }
                        else
                        {
                            // Handle cases where an empty string might be intended to clear the reference
                            return null; // Assign null if the path is empty
                        }
                    }
                    // CASE 3: Reference is null or empty JToken, assign null
                    else if (
                        token.Type == JTokenType.Null
                        || string.IsNullOrEmpty(token.ToString())
                    )
                    {
                        return null;
                    }
                    // CASE 4: Invalid format for Unity Object reference
                    else
                    {
                        Debug.LogWarning(
                            $"[ConvertJTokenToType] Expected a string asset path or a reference object to assign Unity Object of type '{targetType.Name}', but received token type '{token.Type}'. Value: {token}"
                        );
                        return null;
                    }
                }

                // Fallback: Try direct conversion (might work for other simple value types)
                // Be cautious here, this might throw errors for complex types not handled above
                try
                {
                    return token.ToObject(targetType);
                }
                catch (Exception directConversionEx)
                {
                    Debug.LogWarning(
                        $"[ConvertJTokenToType] Direct conversion failed for JToken '{token}' to type '{targetType.Name}': {directConversionEx.Message}. Specific handling might be needed."
                    );
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ConvertJTokenToType] Could not convert JToken '{token}' to type '{targetType.Name}': {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Creates a serializable representation of a Component.
        /// TODO: Add property serialization.
        /// </summary>
        private object GetComponentData(Component c)
        {
            if (c == null)
                return null;
            var data = new Dictionary<string, object>
            {
                { "typeName", c.GetType().FullName },
                { "instanceID", c.GetInstanceID() },
            };

            // Attempt to serialize public properties/fields (can be noisy/complex)
            /*
            try {
                var properties = new Dictionary<string, object>();
                var type = c.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                
                foreach (var prop in type.GetProperties(flags).Where(p => p.CanRead && p.GetIndexargs().Length == 0)) {
                    try { properties[prop.Name] = prop.GetValue(c); } catch { }
                }
                foreach (var field in type.GetFields(flags)) {
                     try { properties[field.Name] = field.GetValue(c); } catch { }
                }
                data["properties"] = properties;
            } catch (Exception ex) {
                data["propertiesError"] = ex.Message;
            }
            */
            return data;
        }
    }
} 