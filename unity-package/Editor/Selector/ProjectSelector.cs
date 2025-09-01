using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 项目资产对象选择器
    /// 专门处理项目资产中的对象查找
    /// 支持各种Unity资产类型的查找
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型</typeparam>
    public class ProjectSelector<T> : BaseObjectSelector<T> where T : UnityEngine.Object
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        public override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("target", "搜索目标（可以是名称、GUID、路径、扩展名或标签）", false),
                new MethodKey("search_method", "搜索方法：by_name, by_type, by_guid, by_path, by_extension, by_label", false),
                new MethodKey("select_many", "是否查找所有匹配项", true),
                new MethodKey("search_term", "搜索条件（支持通配符*）", true),
                new MethodKey("use_regex", "是否使用正则表达式", true)
            };
        }

        /// <summary>
        /// 构建项目资产对象查找状态树
        /// </summary>
        public override StateTree BuildStateTree()
        {
            return StateTreeBuilder.Create()
                .Key("search_method")
                .Leaf("by_name", (Func<StateTreeContext, object>)HandleByNameSearch)
                .Leaf("by_type", (Func<StateTreeContext, object>)HandleByTypeSearch)
                .Leaf("by_guid", (Func<StateTreeContext, object>)HandleByGuidSearch)
                .Leaf("by_path", (Func<StateTreeContext, object>)HandleByPathSearch)
                .Leaf("by_extension", (Func<StateTreeContext, object>)HandleByExtensionSearch)
                .Leaf("by_label", (Func<StateTreeContext, object>)HandleByLabelSearch)
                .Leaf("find_all", (Func<StateTreeContext, object>)HandleFindAllSearch)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch)
                .Build();
        }

        /// <summary>
        /// 按名称查找指定类型的对象数组
        /// </summary>
        public override T[] FindObjectsByName(string name)
        {
            List<T> foundObjects = new List<T>();

            // 使用AssetDatabase查找指定类型的资产
            string typeName = GetTypeNameForAssetDatabase();
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeName} {name}");

            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset != null && asset.name == name)
                {
                    foundObjects.Add(asset);
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 查找所有指定类型的对象
        /// </summary>
        public override T[] FindAllObjects()
        {
            List<T> foundObjects = new List<T>();

            // 获取类型名称
            string typeName = GetTypeNameForAssetDatabase();

            // 查找指定类型的所有资产
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeName}");
            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset != null)
                {
                    foundObjects.Add(asset);
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按ID查找指定类型的对象（项目资产中通常使用GUID而不是InstanceID）
        /// </summary>
        public override T[] FindObjectsById(int instanceId)
        {
            List<T> foundObjects = new List<T>();

            // 查找所有指定类型的资产
            string typeName = GetTypeNameForAssetDatabase();
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeName}");

            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset != null && asset.GetInstanceID() == instanceId)
                {
                    foundObjects.Add(asset);
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按GUID查找指定类型的对象
        /// </summary>
        public override T[] FindObjectsByGuid(string guid)
        {
            List<T> foundObjects = new List<T>();

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    foundObjects.Add(asset);
                }
            }

            return foundObjects.ToArray();
        }

        #region 项目特有的查找方法

        /// <summary>
        /// 按路径搜索处理
        /// </summary>
        private object HandleByPathSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_path search method.");
            }

            string targetPath = target.ToString();
            T[] foundObjects = FindObjectsByPath(targetPath);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' at path '{targetPath}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按扩展名搜索处理
        /// </summary>
        private object HandleByExtensionSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_extension search method.");
            }

            string extension = target.ToString();
            T[] foundObjects = FindObjectsByExtension(extension);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' with extension '{extension}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按标签搜索处理
        /// </summary>
        private object HandleByLabelSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_label search method.");
            }

            string label = target.ToString();
            T[] foundObjects = FindObjectsByLabel(label);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' with label '{label}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按路径查找对象
        /// </summary>
        private T[] FindObjectsByPath(string path)
        {
            List<T> foundObjects = new List<T>();

            // 直接加载指定路径的资产
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                foundObjects.Add(asset);
            }

            // 也查找路径中包含指定字符串的资产
            string typeName = GetTypeNameForAssetDatabase();
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeName}");

            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.Contains(path))
                {
                    T pathAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                    if (pathAsset != null && !foundObjects.Contains(pathAsset))
                    {
                        foundObjects.Add(pathAsset);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按扩展名查找对象
        /// </summary>
        private T[] FindObjectsByExtension(string extension)
        {
            List<T> foundObjects = new List<T>();

            // 确保扩展名以点开头
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            string typeName = GetTypeNameForAssetDatabase();
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:{typeName}");

            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                    if (asset != null)
                    {
                        foundObjects.Add(asset);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按标签查找对象
        /// </summary>
        private T[] FindObjectsByLabel(string label)
        {
            List<T> foundObjects = new List<T>();

            // 使用AssetDatabase查找带指定标签的资产
            string[] assetGUIDs = AssetDatabase.FindAssets($"l:{label}");
            foreach (string guid in assetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    foundObjects.Add(asset);
                }
            }

            return foundObjects.ToArray();
        }

        #endregion

        #region 项目特有的默认搜索

        /// <summary>
        /// 项目资产默认搜索方法
        /// </summary>
        protected override object HandleDefaultSearch(StateTreeContext args)
        {
            if (!args.TryGetValue("target", out object targetObj) || targetObj == null)
            {
                return Response.Error("Target parameter is required.");
            }

            string target = targetObj.ToString();
            List<T> allFound = new List<T>();

            // 尝试按名称查找
            allFound.AddRange(FindObjectsByName(target));

            // 尝试按GUID查找
            allFound.AddRange(FindObjectsByGuid(target));

            // 尝试按路径查找
            allFound.AddRange(FindObjectsByPath(target));

            // 尝试按扩展名查找（如果包含点）
            if (target.Contains("."))
            {
                allFound.AddRange(FindObjectsByExtension(target));
            }

            // 尝试按标签查找
            allFound.AddRange(FindObjectsByLabel(target));

            // 去重
            T[] uniqueObjects = allFound.Distinct().ToArray();

            if (uniqueObjects.Length == 0)
            {
                return Response.Error($"No objects of type '{typeof(T).Name}' matching '{target}' found using default search method.");
            }

            return uniqueObjects;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取用于AssetDatabase查询的类型名称
        /// </summary>
        private string GetTypeNameForAssetDatabase()
        {
            Type type = typeof(T);

            // 对于常见的Unity类型，使用简短名称
            if (type == typeof(UnityEngine.Object))
                return "Object";
            else if (type == typeof(GameObject))
                return "GameObject";
            else if (type == typeof(Texture2D))
                return "Texture2D";
            else if (type == typeof(Texture))
                return "Texture";
            else if (type == typeof(AudioClip))
                return "AudioClip";
            else if (type == typeof(Material))
                return "Material";
            else if (type == typeof(Mesh))
                return "Mesh";
            else if (type == typeof(AnimationClip))
                return "AnimationClip";
            else if (type == typeof(ScriptableObject))
                return "ScriptableObject";
            else if (type == typeof(MonoScript))
                return "MonoScript";
            else if (type == typeof(Shader))
                return "Shader";
            else if (type == typeof(Font))
                return "Font";
            else
                return type.Name;
        }

        /// <summary>
        /// 获取资产的详细信息（重写）
        /// </summary>
        public override string GetObjectInfo(T asset)
        {
            if (asset == null) return "null";

            string baseInfo = base.GetObjectInfo(asset);
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            return $"{baseInfo}, Path: {assetPath}, GUID: {guid}";
        }

        /// <summary>
        /// 获取资产的依赖关系
        /// </summary>
        public T[] GetAssetDependencies(T asset)
        {
            List<T> dependencies = new List<T>();

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string[] dependencyPaths = AssetDatabase.GetDependencies(assetPath, false);
                foreach (string depPath in dependencyPaths)
                {
                    T depAsset = AssetDatabase.LoadAssetAtPath<T>(depPath);
                    if (depAsset != null && !depAsset.Equals(asset))
                    {
                        dependencies.Add(depAsset);
                    }
                }
            }

            return dependencies.ToArray();
        }

        /// <summary>
        /// 获取引用指定资产的所有资产
        /// </summary>
        public T[] GetAssetReferences(T asset)
        {
            List<T> references = new List<T>();

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
                foreach (string checkPath in allAssetPaths)
                {
                    string[] dependencies = AssetDatabase.GetDependencies(checkPath, false);
                    if (dependencies.Contains(assetPath))
                    {
                        T refAsset = AssetDatabase.LoadAssetAtPath<T>(checkPath);
                        if (refAsset != null && !refAsset.Equals(asset))
                        {
                            references.Add(refAsset);
                        }
                    }
                }
            }

            return references.ToArray();
        }

        /// <summary>
        /// 获取资产的标签
        /// </summary>
        public string[] GetAssetLabels(T asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return AssetDatabase.GetLabels(asset);
            }
            return new string[0];
        }

        /// <summary>
        /// 设置资产的标签
        /// </summary>
        public void SetAssetLabels(T asset, string[] labels)
        {
            AssetDatabase.SetLabels(asset, labels);
        }

        #endregion
    }
}