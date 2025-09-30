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
    [ToolName("hierarchy_search", "层级管理")]
    public class HierarchySearch : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("search_type", "Search method: by_name, by_id, by_tag, by_layer, by_component, by_query, etc.", false),
                new MethodKey("query", "Search criteria can be ID, name or path (supports wildcard *)", false),
                new MethodKey("select_many", "Whether to find all matching items", true),
                new MethodKey("include_hierarchy", "Whether to include complete hierarchy data for all children", true),
                new MethodKey("include_inactive", "Whether to search inactive objects", true),
                new MethodKey("use_regex", "Whether to use regular expressions", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("search_type")
                    .Leaf("by_name", HandleSearchByName)
                    .Leaf("by_id", HandleSearchById)
                    .Leaf("by_tag", HandleSearchByTag)
                    .Leaf("by_layer", HandleSearchByLayer)
                    .Leaf("by_component", HandleSearchByComponent)
                    .Leaf("by_query", HandleSearchByquery)
                .DefaultLeaf(HandleDefaultSearch) // 添加默认处理函数
                .Build();
        }

        /// <summary>
        /// 默认搜索处理函数 - 当没有指定search_type时使用按名称搜索
        /// </summary>
        private object HandleDefaultSearch(JObject args)
        {
            string query = args["query"]?.ToString();
            
            // 如果有query参数，默认使用按名称搜索
            if (!string.IsNullOrEmpty(query))
            {
                return HandleSearchByName(args);
            }
            
            return Response.Error("Either 'search_type' must be specified or 'query' must be provided for default name search.");
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 按名称搜索GameObject
        /// </summary>
        private object HandleSearchByName(JObject args)
        {
            string query = args["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
            {
                return Response.Error("query is required for by_name search.");
            }

            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool includeHierarchy = args["include_hierarchy"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            List<GameObject> foundObjects = new List<GameObject>();

            // 精确名称搜索 - 使用Unity内置API
            GameObject exactMatch = GameObject.Find(query);
            if (exactMatch != null && (searchInInactive || exactMatch.activeInHierarchy))
            {
                foundObjects.Add(exactMatch);
            }

            if (findAll || foundObjects.Count == 0)
            {
                // 从当前场景搜索所有GameObject
                GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

                // 检查是否包含通配符
                bool hasWildcards = query.Contains('*');
                Regex regex = null;
                
                if (hasWildcards)
                {
                    // 将通配符转换为正则表达式
                    string regexPattern = ConvertWildcardToRegex(query);
                    try
                    {
                        regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    }
                    catch (ArgumentException)
                    {
                        // 如果正则表达式无效，回退到普通搜索
                        hasWildcards = false;
                    }
                }

                foreach (GameObject go in allObjects)
                {
                    bool nameMatches;
                    
                    if (hasWildcards && regex != null)
                    {
                        nameMatches = regex.IsMatch(go.name);
                    }
                    else
                    {
                        nameMatches = go.name.Contains(query, StringComparison.OrdinalIgnoreCase);
                    }

                    if (nameMatches)
                    {
                        if (foundObjects.Contains(go))
                            continue;
                        foundObjects.Add(go);
                    }
                }
            }

            return CreateHierarchySearchResult(foundObjects, "name", includeHierarchy);
        }

        /// <summary>
        /// 按ID搜索GameObject
        /// </summary>
        private object HandleSearchById(JObject args)
        {
            string query = args["query"]?.ToString();
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(query))
            {
                return Response.Error("query is required for by_id search.");
            }

            List<GameObject> foundObjects = new List<GameObject>();

            // 尝试解析ID并查找
            if (int.TryParse(query, out int instanceId))
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
            string searchTerm = args["query"]?.ToString();
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
                // 搜索非激活对象 - 从当前场景获取
                GameObject[] allObjects = GetAllGameObjectsInActiveScene(true);
                foreach (GameObject go in allObjects)
                {
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
            string searchTerm = args["query"]?.ToString();
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

            // 从当前场景搜索GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
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
            string searchTerm = args["query"]?.ToString();
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

            // 从当前场景搜索包含指定组件的GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
                if (go.GetComponent(componentType) != null)
                {
                    foundObjects.Add(go);
                }
            }

            return CreateSearchResult(foundObjects, "component");
        }

        /// <summary>
        /// 按通用术语搜索GameObject
        /// </summary>
        private object HandleSearchByquery(JObject args)
        {
            string searchTerm = args["query"]?.ToString();
            bool findAll = args["select_many"]?.ToObject<bool>() ?? false;
            bool includeHierarchy = args["include_hierarchy"]?.ToObject<bool>() ?? false;
            bool searchInInactive = args["include_inactive"]?.ToObject<bool>() ?? false;
            bool useRegex = args["use_regex"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchTerm))
            {
                return Response.Error("Search term is required for by_query search.");
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
                Type queryType = GetComponentType(typeName);
                if (queryType == null)
                {
                    return Response.Error($"Component type '{typeName}' not found.");
                }

                GameObject[] sceneObjects = GetAllGameObjectsInActiveScene(searchInInactive);

                foreach (GameObject go in sceneObjects)
                {
                    if (go.GetComponent(queryType) != null)
                    {
                        if (uniqueObjects.Add(go))
                        {
                            foundObjects.Add(go);
                        }
                    }
                }

                return CreateSearchResult(foundObjects, "type");
            }

            // 从当前场景搜索所有GameObject
            GameObject[] allObjects = GetAllGameObjectsInActiveScene(searchInInactive);

            foreach (GameObject go in allObjects)
            {
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

                // 5. 检查子对象名称匹配（默认启用）
                if (!matches)
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

            return CreateHierarchySearchResult(foundObjects, "term", includeHierarchy);
        }

        // --- Helper Methods ---

        /// <summary>
        /// 获取当前激活场景中的所有GameObject
        /// </summary>
        private GameObject[] GetAllGameObjectsInActiveScene(bool includeInactive)
        {
            List<GameObject> allObjects = new List<GameObject>();

            // 获取当前激活场景的根对象
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return allObjects.ToArray();
            }

            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                if (includeInactive)
                {
                    // 包含非激活对象，获取所有子对象（包括非激活的）
                    Transform[] allTransforms = rootObj.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allTransforms)
                    {
                        allObjects.Add(t.gameObject);
                    }
                }
                else
                {
                    // 只包含激活对象
                    if (rootObj.activeInHierarchy)
                    {
                        Transform[] activeTransforms = rootObj.GetComponentsInChildren<Transform>(false);
                        foreach (Transform t in activeTransforms)
                        {
                            allObjects.Add(t.gameObject);
                        }
                    }
                }
            }

            return allObjects.ToArray();
        }

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
                message = $"No GameObjects found using search method: {searchType}.";
            }
            else
            {
                message = $"Found {results.Count} GameObjects using {searchType}.";
            }

            // 构建响应对象，包含执行时间、成功标志、消息和数据
            var response = new JObject
            {
                ["success"] = true,
                ["message"] = message,
                ["data"] = JToken.FromObject(results),
                ["exec_time_ms"] = 1.00,
                ["mode"] = "Async mode"
            };

            return response;
        }

        /// <summary>
        /// 创建包含完整层级信息的搜索结果
        /// </summary>
        private object CreateHierarchySearchResult(List<GameObject> foundObjects, string searchType, bool includeHierarchy)
        {
            // 构建结果数据 - 使用JObject确保正确序列化
            var results = new List<JObject>();
            
            if (includeHierarchy)
            {
                // 获取完整的层级数据
                foreach (var go in foundObjects)
                {
                    var hierarchyData = GetCompleteHierarchyData(go);
                    results.Add(JObject.FromObject(hierarchyData));
                }
            }
            else
            {
                // 使用标准的GameObject数据
                foreach (var go in foundObjects)
                {
                    results.Add(JObject.FromObject(GameObjectUtils.GetGameObjectData(go)));
                }
            }

            // 构建消息
            string message;
            if (results.Count == 0)
            {
                message = $"No GameObjects found using search method: {searchType}.";
            }
            else
            {
                string hierarchyInfo = includeHierarchy ? " with complete hierarchy" : "";
                message = $"Found {results.Count} GameObjects using {searchType}{hierarchyInfo}.";
            }

            // 构建响应对象，确保正确序列化
            var response = new JObject
            {
                ["success"] = true,
                ["message"] = message,
                ["data"] = JToken.FromObject(results),
                ["exec_time_ms"] = 1.00,
                ["mode"] = "Async mode"
            };

            return response;
        }

        /// <summary>
        /// 获取GameObject的完整层级数据（包含所有子对象的完整信息）
        /// </summary>
        private object GetCompleteHierarchyData(GameObject go)
        {
            if (go == null)
                return null;

            // 获取当前对象的基本YAML数据
            var baseYaml = GameObjectUtils.GetGameObjectDataYaml(go);
            
            // 递归获取所有子对象的完整数据
            var childrenData = new List<object>();
            foreach (Transform child in go.transform)
            {
                if (child != null && child.gameObject != null)
                {
                    childrenData.Add(GetCompleteHierarchyData(child.gameObject));
                }
            }

            return new
            {
                yaml = baseYaml,
                children = childrenData.Count > 0 ? childrenData : null
            };
        }

        /// <summary>
        /// 获取组件类型
        /// </summary>
        private Type GetComponentType(string typeName)
        {
            // 尝试从常见的Unity命名空间获取
            string[] commonNamespaces = {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.EventSystems",
                "UnityEditor"
            };

            foreach (string ns in commonNamespaces)
            {
                Type type = Type.GetType($"{ns}.{typeName}");
                if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                    return type;
            }

            // 尝试直接获取类型
            Type directType = Type.GetType(typeName);
            if (directType != null && typeof(UnityEngine.Object).IsAssignableFrom(directType))
                return directType;

            // 尝试从所有已加载的程序集中查找
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // 先尝试全名匹配
                    Type type = assembly.GetType(typeName);
                    if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                        return type;

                    // 再遍历所有类型，尝试短名匹配
                    foreach (var t in assembly.GetTypes())
                    {
                        if ((t.Name == typeName || t.FullName == typeName) &&
                            typeof(UnityEngine.Object).IsAssignableFrom(t))
                            return t;
                    }
                }
                catch
                {
                    // 忽略无法访问的程序集
                    continue;
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
