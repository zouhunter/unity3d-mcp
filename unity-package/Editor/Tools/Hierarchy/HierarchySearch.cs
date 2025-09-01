using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    /// Handles GameObject search and find operations in the scene hierarchy.
    /// 对应方法名: hierarchy_search
    /// </summary>
    [ToolName("hierarchy_search")]
    public class HierarchySearch : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("target", "搜索目标（可以是ID、名称或路径）", false),
                new MethodKey("search_method", "搜索方法：by_name, by_id, by_tag, by_layer, by_component, by_term等", false),
                new MethodKey("select_many", "是否查找所有匹配项", true),
                new MethodKey("search_term", "搜索条件（支持通配符*）", true),
                new MethodKey("root_only", "是否仅搜索根对象（不包括子物体）", true),
                new MethodKey("include_inactive", "是否搜索非激活对象", true),
                new MethodKey("use_regex", "是否使用正则表达式", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("search_method")
                    .Leaf("by_name", HandleSearchByName)
                    .Leaf("by_id", HandleSearchById)
                    .Leaf("by_tag", HandleSearchByTag)
                    .Leaf("by_layer", HandleSearchByLayer)
                    .Leaf("by_component", HandleSearchByComponent)
                    .Leaf("by_term", HandleSearchByTerm)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 按名称搜索GameObject
        /// </summary>
        private object HandleSearchByName(JObject args)
        {
            string target = args["target"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool rootOnly = args["root_only"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            List<GameObject> foundObjects = new List<GameObject>();

            if (!string.IsNullOrEmpty(target))
            {
                // 精确名称搜索 - 使用Unity内置API
                GameObject exactMatch = GameObject.Find(target);
                if (exactMatch != null && (searchInInactive || exactMatch.activeInHierarchy))
                {
                    foundObjects.Add(exactMatch);
                }
            }

            if (findAll || foundObjects.Count == 0)
            {
                // 使用Unity内置API搜索所有GameObject
                GameObject[] allObjects;
                if (searchInInactive)
                {
                    // 包括非激活对象
                    allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                }
                else
                {
                    // 只搜索激活对象
                    allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                }

                foreach (GameObject go in allObjects)
                {
                    if (go.scene.name == null) continue; // 跳过预制体资源

                    bool nameMatches = string.IsNullOrEmpty(target) ||
                                     go.name.Contains(target, StringComparison.OrdinalIgnoreCase);

                    if (nameMatches)
                    {
                        if (rootOnly && go.transform.parent != null) continue;
                        foundObjects.Add(go);
                    }
                }
            }

            return CreateSearchResult(foundObjects, "name");
        }

        /// <summary>
        /// 按ID搜索GameObject
        /// </summary>
        private object HandleSearchById(JObject args)
        {
            string target = args["target"]?.ToString();
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(target))
            {
                return Response.Error("Target ID is required for by_id search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // 尝试解析ID并查找
            if (int.TryParse(target, out int instanceId))
            {
                GameObject found = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (found != null && (searchInInactive || found.activeInHierarchy))
                {
                    foundObjects.Add(found);
                }
            }

            return CreateSearchResult(foundObjects, "ID");
        }

        /// <summary>
        /// 按标签搜索GameObject
        /// </summary>
        private object HandleSearchByTag(JObject args)
        {
            string searchTerm = args["search_term"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_tag search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // 使用Unity内置的FindGameObjectsWithTag方法
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(searchTerm);
            foundObjects.AddRange(taggedObjects);

            if (searchInInactive)
            {
                // 搜索非激活对象 - 使用Unity内置API
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject go in allObjects)
                {
                    if (go.scene.name == null) continue;
                    if (!go.activeInHierarchy && go.CompareTag(searchTerm))
                    {
                        foundObjects.Add(go);
                    }
                }
            }

            return CreateSearchResult(foundObjects, "tag");
        }

        /// <summary>
        /// 按层级搜索GameObject
        /// </summary>
        private object HandleSearchByLayer(JObject args)
        {
            string searchTerm = args["search_term"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_layer search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // 获取层级索引
            int layerIndex = LayerMask.NameToLayer(searchTerm);
            if (layerIndex == -1)
            {
                return Response.Error($"Layer '{searchTerm}' not found.");
            }

            // 搜索所有GameObject
            GameObject[] allObjects = searchInInactive ?
                Resources.FindObjectsOfTypeAll<GameObject>() :
                UnityEngine.Object.FindObjectsOfType<GameObject>();

            foreach (GameObject go in allObjects)
            {
                if (go.scene.name == null) continue;
                if (go.layer == layerIndex)
                {
                    foundObjects.Add(go);
                }
            }

            return CreateSearchResult(foundObjects, "layer");
        }

        /// <summary>
        /// 按组件搜索GameObject
        /// </summary>
        private object HandleSearchByComponent(JObject args)
        {
            string searchTerm = args["search_term"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_component search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // 尝试获取组件类型
            Type componentType = GetComponentType(searchTerm);
            if (componentType == null)
            {
                return Response.Error($"Component type '{searchTerm}' not found.");
            }

            // 使用Unity内置API搜索包含指定组件的GameObject
            Component[] components;
            if (searchInInactive)
            {
                // 包括非激活对象
                components = Resources.FindObjectsOfTypeAll(componentType).Cast<Component>().ToArray();
            }
            else
            {
                // 只搜索激活对象
                components = UnityEngine.Object.FindObjectsOfType(componentType).Cast<Component>().ToArray();
            }

            foreach (Component component in components)
            {
                if (component.gameObject.scene.name == null) continue;
                foundObjects.Add(component.gameObject);
            }

            return CreateSearchResult(foundObjects, "component");
        }

        /// <summary>
        /// 按通用术语搜索GameObject
        /// </summary>
        private object HandleSearchByTerm(JObject args)
        {
            string searchTerm = args["search_term"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool rootOnly = args["root_only"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;
            bool useRegex = args["use_regex"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_term search.");
            }

            // 检查是否是类型搜索（t:TypeName 格式）
            bool isTypeSearch = false;
            string typeName = null;
            if (searchTerm.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
            {
                isTypeSearch = true;
                typeName = searchTerm.Substring(2).Trim();
                if (string.IsNullOrEmpty(typeName))
                {
                    return Response.Error("Type name is required after 't:' prefix.");
                }
            }

            // 处理搜索模式：通配符、正则表达式或普通文本
            Regex regex = null;
            bool isPatternMatch = false;

            if (!isTypeSearch)
            {
                // 检查是否包含通配符
                bool hasWildcards = searchTerm.Contains('*');

                if (useRegex)
                {
                    // 直接使用正则表达式
                    try
                    {
                        regex = new Regex(searchTerm, RegexOptions.IgnoreCase);
                        isPatternMatch = true;
                    }
                    catch (ArgumentException ex)
                    {
                        return Response.Error($"Invalid regular expression: {ex.Message}");
                    }
                }
                else if (hasWildcards)
                {
                    // 将通配符转换为正则表达式
                    string regexPattern = ConvertWildcardToRegex(searchTerm);
                    try
                    {
                        regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                        isPatternMatch = true;
                    }
                    catch (ArgumentException ex)
                    {
                        return Response.Error($"Invalid wildcard pattern: {ex.Message}");
                    }
                }
            }

            List<GameObject> foundObjects = new List<GameObject>();
            HashSet<GameObject> uniqueObjects = new HashSet<GameObject>(); // 避免重复

            // 如果是类型搜索，直接使用FindObjectsOfType
            if (isTypeSearch)
            {
                Type targetType = GetComponentType(typeName);
                if (targetType == null)
                {
                    return Response.Error($"Component type '{typeName}' not found.");
                }

                Component[] components = searchInInactive ?
                    Resources.FindObjectsOfTypeAll(targetType).Cast<Component>().ToArray() :
                    UnityEngine.Object.FindObjectsOfType(targetType).Cast<Component>().ToArray();

                foreach (Component component in components)
                {
                    if (component.gameObject.scene.name == null) continue; // 跳过预制体资源

                    // 检查是否仅搜索根对象
                    if (rootOnly && component.gameObject.transform.parent != null) continue;

                    if (uniqueObjects.Add(component.gameObject))
                    {
                        foundObjects.Add(component.gameObject);
                    }
                }

                return CreateSearchResult(foundObjects, "type");
            }

            // 搜索所有GameObject
            GameObject[] allObjects = searchInInactive ?
                Resources.FindObjectsOfTypeAll<GameObject>() :
                UnityEngine.Object.FindObjectsOfType<GameObject>();

            foreach (GameObject go in allObjects)
            {
                if (go.scene.name == null) continue; // 跳过预制体资源

                // 检查是否仅搜索根对象
                if (rootOnly && go.transform.parent != null) continue;

                bool matches = false;

                // 1. 检查名称匹配
                if (isPatternMatch && regex != null)
                {
                    if (regex.IsMatch(go.name))
                    {
                        matches = true;
                    }
                }
                else
                {
                    if (go.name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }
                }

                // 2. 检查标签匹配
                if (!matches)
                {
                    if (isPatternMatch && regex != null)
                    {
                        if (regex.IsMatch(go.tag))
                        {
                            matches = true;
                        }
                    }
                    else
                    {
                        // 安全地检查标签，避免未定义标签的错误
                        try
                        {
                            if (go.CompareTag(searchTerm) || go.tag.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                matches = true;
                            }
                        }
                        catch (UnityException)
                        {
                            // 标签未定义，跳过标签匹配
                        }
                    }
                }

                // 3. 检查层级匹配
                if (!matches)
                {
                    string layerName = LayerMask.LayerToName(go.layer);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        if (isPatternMatch && regex != null)
                        {
                            if (regex.IsMatch(layerName))
                            {
                                matches = true;
                            }
                        }
                        else
                        {
                            if (layerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            {
                                matches = true;
                            }
                        }
                    }
                }

                // 4. 检查组件匹配
                if (!matches)
                {
                    Component[] components = go.GetComponents<Component>();
                    foreach (Component component in components)
                    {
                        if (component != null)
                        {
                            string componentTypeName = component.GetType().Name;
                            if (isPatternMatch && regex != null)
                            {
                                if (regex.IsMatch(componentTypeName))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (componentTypeName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 5. 检查子对象名称匹配（默认启用，除非设置了root_only）
                if (!matches && !rootOnly)
                {
                    Transform[] children = go.GetComponentsInChildren<Transform>();
                    foreach (Transform child in children)
                    {
                        if (child != go.transform)
                        {
                            if (isPatternMatch && regex != null)
                            {
                                if (regex.IsMatch(child.name))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                            else
                            {
                                if (child.name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (matches && uniqueObjects.Add(go))
                {
                    foundObjects.Add(go);
                }
            }

            return CreateSearchResult(foundObjects, "term");
        }

        // --- Helper Methods ---

        /// <summary>
        /// 创建搜索结果
        /// </summary>
        private object CreateSearchResult(List<GameObject> foundObjects, string searchType)
        {
            // 构建结果数据
            var results = foundObjects.Select(go => JObject.FromObject(GameObjectUtils.GetGameObjectData(go))).ToList();

            // 构建中文消息
            string message;
            if (results.Count == 0)
            {
                message = $"未找到任何GameObject，搜索方式：{searchType}。";
            }
            else
            {
                message = $"已通过{searchType}找到{results.Count}个GameObject。";
            }

            // 构建响应对象，包含执行时间、成功标志、消息和数据
            var response = new JObject
            {
                ["success"] = true,
                ["message"] = message,
                ["data"] = JToken.FromObject(results),
                ["exec_time_ms"] = 1.00,
                ["mode"] = "异步模式"
            };

            return response;
        }

        /// <summary>
        /// 获取组件类型
        /// </summary>
        private Type GetComponentType(string typeName)
        {
            // 尝试直接获取类型
            Type type = Type.GetType(typeName);
            if (type != null) return type;
            // 尝试从Unity命名空间获取
            type = Type.GetType($"UnityEngine.{typeName}");
            if (type != null) return type;

            // 尝试从所有已加载的程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 先尝试全名匹配
                type = assembly.GetType(typeName);
                if (type != null) return type;
                // 再遍历所有类型，尝试短名匹配
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == typeName || t.FullName == typeName)
                        return t;
                }
            }

            return null;
        }

        /// <summary>
        /// 将通配符模式转换为正则表达式
        /// </summary>
        /// <param name="wildcardPattern">包含通配符*的模式</param>
        /// <returns>正则表达式字符串</returns>
        private string ConvertWildcardToRegex(string wildcardPattern)
        {
            if (string.IsNullOrEmpty(wildcardPattern))
                return string.Empty;

            // 转义正则表达式中的特殊字符，但保留通配符*
            string escaped = Regex.Escape(wildcardPattern);

            // 将转义后的\*替换为.*（匹配任意字符）
            string regexPattern = escaped.Replace("\\*", ".*");

            // 添加锚点以确保完整匹配
            return $"^{regexPattern}$";
        }
    }
}
