using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Component manipulation operations (get, add, remove, set properties).
    /// 对应方法名: manage_component
    /// </summary>
    [ToolName("manage_component")]
    public class ManageComponent : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：get_components, add_component, remove_component, set_component_property", false),
                new MethodKey("target", "目标GameObject标识符（名称、ID或路径）", false),
                new MethodKey("search_method", "搜索方法：by_name, by_id, by_tag, by_layer等", true),
                new MethodKey("component_name", "组件名称", true),
                new MethodKey("components_to_add", "要添加的组件列表", true),
                new MethodKey("components_to_remove", "要移除的组件列表", true),
                new MethodKey("component_properties", "组件属性字典", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("get_components", HandleGetComponentsAction)
                    .Leaf("add_component", HandleAddComponentAction)
                    .Leaf("remove_component", HandleRemoveComponentAction)
                    .Leaf("set_component_property", HandleSetComponentPropertyAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理获取组件的操作
        /// </summary>
        private object HandleGetComponentsAction(JObject args)
        {
            JToken targetToken = args["target"];
            string searchMethod = args["search_method"]?.ToString()?.ToLower();
            string getCompTarget = targetToken?.ToString();

            if (getCompTarget == null)
                return Response.Error("'target' parameter required for get_components.");

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args, "get_components");
            if (redirectResult != null)
                return redirectResult;

            return GetComponentsFromTarget(getCompTarget, searchMethod);
        }

        /// <summary>
        /// 处理添加组件的操作
        /// </summary>
        private object HandleAddComponentAction(JObject args)
        {
            JToken targetToken = args["target"];
            string searchMethod = args["search_method"]?.ToString()?.ToLower();

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args, "add_component");
            if (redirectResult != null)
                return redirectResult;

            return AddComponentToTarget(args, targetToken, searchMethod);
        }

        /// <summary>
        /// 处理移除组件的操作
        /// </summary>
        private object HandleRemoveComponentAction(JObject args)
        {
            JToken targetToken = args["target"];
            string searchMethod = args["search_method"]?.ToString()?.ToLower();

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args, "remove_component");
            if (redirectResult != null)
                return redirectResult;

            return RemoveComponentFromTarget(args, targetToken, searchMethod);
        }

        /// <summary>
        /// 处理设置组件属性的操作
        /// </summary>
        private object HandleSetComponentPropertyAction(JObject args)
        {
            JToken targetToken = args["target"];
            string searchMethod = args["search_method"]?.ToString()?.ToLower();

            // 检查预制体重定向
            object redirectResult = CheckPrefabRedirection(args, "set_component_property");
            if (redirectResult != null)
                return redirectResult;

            return SetComponentPropertyOnTarget(args, targetToken, searchMethod);
        }

        /// <summary>
        /// 检查预制体重定向逻辑
        /// </summary>
        private object CheckPrefabRedirection(JObject args, string action)
        {
            JToken targetToken = args["target"];
            string targetPath = targetToken?.Type == JTokenType.String ? targetToken.ToString() : null;

            if (string.IsNullOrEmpty(targetPath) || !targetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return null; // 不是预制体，继续正常处理

            // 允许某些操作，禁止其他操作
            if (action == "set_component_property")
            {
                LogInfo($"[ManageComponent->ManageAsset] Redirecting action '{action}' for prefab '{targetPath}' to ManageAsset.");

                // 准备 ManageAsset 参数
                JObject assetParams = new JObject();
                assetParams["action"] = "modify"; // ManageAsset 使用 "modify"
                assetParams["path"] = targetPath;

                // 提取属性
                string compName = args["component_name"]?.ToString();
                JObject compProps = args["component_properties"]?[compName] as JObject;
                if (string.IsNullOrEmpty(compName))
                    return Response.Error("Missing 'component_name' for 'set_component_property' on prefab.");
                if (compProps == null)
                    return Response.Error($"Missing or invalid 'component_properties' for component '{compName}' for 'set_component_property' on prefab.");

                JObject properties = new JObject();
                properties[compName] = compProps;
                assetParams["properties"] = properties;

                // 调用 ManageAsset 处理器
                return new ManageAsset().ExecuteMethod(assetParams);
            }
            else if (action == "add_component" || action == "remove_component" || action == "get_components")
            {
                return Response.Error($"Action '{action}' on a prefab asset ('{targetPath}') should be performed using the 'manage_asset' command.");
            }

            return null; // 其他操作可以继续
        }

        // --- Core Methods ---

        private object GetComponentsFromTarget(string target, string searchMethod)
        {
            GameObject targetGo = GameObjectUtils.FindObjectInternal(target, searchMethod);
            if (targetGo == null)
            {
                return Response.Error(
                    $"Target GameObject ('{target}') not found using method '{searchMethod ?? "default"}'."
                );
            }

            try
            {
                Component[] components = targetGo.GetComponents<Component>();
                var componentData = components.Select(c => GetComponentData(c)).ToList();
                return Response.Success(
                    $"Retrieved {componentData.Count} components from '{targetGo.name}'.",
                    componentData
                );
            }
            catch (Exception e)
            {
                return Response.Error(
                    $"Error getting components from '{targetGo.name}': {e.Message}"
                );
            }
        }

        private object AddComponentToTarget(
            JObject cmd,
            JToken targetToken,
            string searchMethod
        )
        {
            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, searchMethod);
            if (targetGo == null)
            {
                return Response.Error(
                    $"Target GameObject ('{targetToken}') not found using method '{searchMethod ?? "default"}'."
                );
            }

            string typeName = null;
            JObject properties = null;

            // Allow adding component specified directly or via components_to_add array (take first)
            if (cmd["component_name"] != null)
            {
                typeName = cmd["component_name"]?.ToString();
                properties = cmd["component_properties"]?[typeName] as JObject; // Check if props are nested under name
            }
            else if (
               cmd["components_to_add"] is JArray componentsToAddArray
                && componentsToAddArray.Count > 0
            )
            {
                var compToken = componentsToAddArray.First;
                if (compToken.Type == JTokenType.String)
                    typeName = compToken.ToString();
                else if (compToken is JObject compObj)
                {
                    typeName = compObj["typeName"]?.ToString();
                    properties = compObj["properties"] as JObject;
                }
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return Response.Error(
                    "Component type name ('component_name' or first element in 'components_to_add') is required."
                );
            }

            var addResult = GameObjectUtils.AddComponentInternal(targetGo, typeName, properties);
            if (addResult != null)
                return addResult; // Return error

            // Set properties if provided (after successful component addition)
            if (properties != null)
            {
                var setResult = SetComponentPropertiesInternal(
                    targetGo,
                    typeName,
                    properties
                );
                if (setResult != null)
                {
                    // If setting properties failed, consider removing the component or log warning
                    Debug.LogWarning($"[ManageComponent] Failed to set properties for component '{typeName}': {setResult}");
                }
            }

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Component '{typeName}' added to '{targetGo.name}'.",
                GameObjectUtils.GetGameObjectData(targetGo)
            ); // Return updated GO data
        }

        private object RemoveComponentFromTarget(
            JObject cmd,
            JToken targetToken,
            string searchMethod
        )
        {
            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, searchMethod);
            if (targetGo == null)
            {
                return Response.Error(
                    $"Target GameObject ('{targetToken}') not found using method '{searchMethod ?? "default"}'."
                );
            }

            string typeName = null;
            // Allow removing component specified directly or via components_to_remove array (take first)
            if (cmd["component_name"] != null)
            {
                typeName = cmd["component_name"]?.ToString();
            }
            else if (
               cmd["components_to_remove"] is JArray componentsToRemoveArray
                && componentsToRemoveArray.Count > 0
            )
            {
                typeName = componentsToRemoveArray.First?.ToString();
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return Response.Error(
                    "Component type name ('component_name' or first element in 'components_to_remove') is required."
                );
            }

            var removeResult = RemoveComponentInternal(targetGo, typeName);
            if (removeResult != null)
                return removeResult; // Return error

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Component '{typeName}' removed from '{targetGo.name}'.",
                GameObjectUtils.GetGameObjectData(targetGo)
            );
        }

        private object SetComponentPropertyOnTarget(
            JObject cmd,
            JToken targetToken,
            string searchMethod
        )
        {
            GameObject targetGo = GameObjectUtils.FindObjectInternal(targetToken, searchMethod);
            if (targetGo == null)
            {
                return Response.Error(
                    $"Target GameObject ('{targetToken}') not found using method '{searchMethod ?? "default"}'."
                );
            }

            string compName = cmd["component_name"]?.ToString();
            JObject propertiesToSet = null;

            if (!string.IsNullOrEmpty(compName))
            {
                // Properties might be directly under component_properties or nested under the component name
                if (cmd["component_properties"] is JObject compProps)
                {
                    propertiesToSet = compProps[compName] as JObject ?? compProps; // Allow flat or nested structure
                }
            }
            else
            {
                return Response.Error("'component_name' parameter is required.");
            }

            if (propertiesToSet == null || !propertiesToSet.HasValues)
            {
                return Response.Error(
                    "'component_properties' dictionary for the specified component is required and cannot be empty."
                );
            }

            var setResult = SetComponentPropertiesInternal(targetGo, compName, propertiesToSet);
            if (setResult != null)
                return setResult; // Return error

            EditorUtility.SetDirty(targetGo);
            return Response.Success(
                $"Properties set for component '{compName}' on '{targetGo.name}'.",
                GameObjectUtils.GetGameObjectData(targetGo)
            );
        }

        // --- Component Helper Methods ---

        /// <summary>
        /// Removes a component by type name.
        /// Returns null on success, or an error response object on failure.
        /// </summary>
        private object RemoveComponentInternal(GameObject targetGo, string typeName)
        {
            Type componentType = GameObjectUtils.FindType(typeName);
            if (componentType == null)
            {
                return Response.Error($"Component type '{typeName}' not found for removal.");
            }

            // Prevent removing essential components
            if (componentType == typeof(Transform))
            {
                return Response.Error("Cannot remove the Transform component.");
            }

            Component componentToRemove = targetGo.GetComponent(componentType);
            if (componentToRemove == null)
            {
                return Response.Error(
                    $"Component '{typeName}' not found on '{targetGo.name}' to remove."
                );
            }

            try
            {
                // Use Undo.DestroyObjectImmediate for undo support
                Undo.DestroyObjectImmediate(componentToRemove);
                return null; // Success
            }
            catch (Exception e)
            {
                return Response.Error(
                    $"Error removing component '{typeName}' from '{targetGo.name}': {e.Message}"
                );
            }
        }

        /// <summary>
        /// Sets properties on a component.
        /// Returns null on success, or an error response object on failure.
        /// </summary>
        private object SetComponentPropertiesInternal(
            GameObject targetGo,
            string compName,
            JObject propertiesToSet,
            Component targetComponentInstance = null
        )
        {
            Component targetComponent = targetComponentInstance;

            // If no specific component instance is provided, find it by type name
            if (targetComponent == null)
            {
                // Use FindType helper to locate the correct component type
                Type componentType = GameObjectUtils.FindType(compName);
                if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                {
                    targetComponent = targetGo.GetComponent(componentType);
                }
                else
                {
                    // Fallback: try common Unity component namespaces
                    string[] commonNamespaces = { "UnityEngine", "UnityEngine.UI" };
                    foreach (string ns in commonNamespaces)
                    {
                        string fullTypeName = $"{ns}.{compName}";
                        componentType = GameObjectUtils.FindType(fullTypeName);
                        if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                        {
                            targetComponent = targetGo.GetComponent(componentType);
                            break;
                        }
                    }
                }
            }

            if (targetComponent == null)
            {
                return Response.Error(
                    $"Component '{compName}' not found on '{targetGo.name}' to set properties."
                );
            }

            Undo.RecordObject(targetComponent, "Set Component Properties");

            foreach (var prop in propertiesToSet.Properties())
            {
                string propName = prop.Name;
                JToken propValue = prop.Value;

                try
                {
                    if (!SetProperty(targetComponent, propName, propValue))
                    {
                        // Log warning if property could not be set
                        Debug.LogWarning(
                            $"[ManageComponent] Could not set property '{propName}' on component '{compName}' ('{targetComponent.GetType().Name}'). Property might not exist, be read-only, or type mismatch."
                        );
                        // Optionally return an error here instead of just logging
                        // return Response.Error($"Could not set property '{propName}' on component '{compName}'.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[ManageComponent] Error setting property '{propName}' on '{compName}': {e.Message}"
                    );
                    // Optionally return an error here
                    // return Response.Error($"Error setting property '{propName}' on '{compName}': {e.Message}");
                }
            }
            EditorUtility.SetDirty(targetComponent);
            return null; // Success (or partial success if warnings were logged)
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
