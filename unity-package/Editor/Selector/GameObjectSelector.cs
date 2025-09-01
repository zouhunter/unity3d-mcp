using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// GameObject动态选择器
    /// 根据search_in_scene参数动态选择使用HierarchySelector或ProjectSelector
    /// 提供向后兼容性，替代原有的GameObjectSelector
    /// 
    /// 性能优化：
    /// - 在构造函数中预构建所有状态树，避免运行时重复构建
    /// - 所有查找方法直接使用预构建的状态树，提高查找效率
    /// - 减少了状态树构建的开销，特别是在频繁查找时
    /// </summary>
    public class GameObjectSelector : IObjectSelector
    {
        private readonly HierarchySelector<GameObject> hierarchySelector;
        private readonly ProjectSelector<GameObject> projectSelector;
        private readonly StateTree hierarchyStateTree;
        private readonly StateTree projectStateTree;

        /// <summary>
        /// 构造函数 - 预构建所有状态树以避免运行时性能开销
        /// </summary>
        public GameObjectSelector()
        {
            hierarchySelector = new HierarchySelector<GameObject>();
            projectSelector = new ProjectSelector<GameObject>();

            // 预构建状态树，避免运行时重复构建
            hierarchyStateTree = hierarchySelector.BuildStateTree();
            projectStateTree = projectSelector.BuildStateTree();
        }

        public MethodKey[] CreateKeys()
        {
            var keys = new List<MethodKey> { new MethodKey("search_in_scene", "是否在场景中搜索", true) };
            // 合并并按key去重
            var allKeys = hierarchySelector.CreateKeys().Concat(projectSelector.CreateKeys());
            keys.AddRange(allKeys.GroupBy(k => k.Key).Select(g => g.First()));
            return keys.ToArray();
        }

        /// <summary>
        /// 构建动态选择状态树
        /// </summary>
        public StateTree BuildStateTree()
        {
            return StateTreeBuilder.Create()
                .Key("search_method")
                .Leaf("by_name", (Func<StateTreeContext, object>)HandleByNameSearch)
                .Leaf("by_id", (Func<StateTreeContext, object>)HandleByIdSearch)
                .Leaf("by_tag", (Func<StateTreeContext, object>)HandleByTagSearch)
                .Leaf("by_layer", (Func<StateTreeContext, object>)HandleByLayerSearch)
                .Leaf("by_component", (Func<StateTreeContext, object>)HandleByComponentSearch)
                .Leaf("by_path", (Func<StateTreeContext, object>)HandleByPathSearch)
                .Leaf("by_guid", (Func<StateTreeContext, object>)HandleByGuidSearch)
                .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch)
                .Build();
        }

        /// <summary>
        /// 按名称搜索GameObject
        /// </summary>
        private object HandleByNameSearch(StateTreeContext args)
        {
            var stateTree = GetStateTree(args);
            return stateTree.Run(args);
        }

        /// <summary>
        /// 按ID搜索GameObject
        /// </summary>
        private object HandleByIdSearch(StateTreeContext args)
        {
            var stateTree = GetStateTree(args);
            return stateTree.Run(args);
        }

        /// <summary>
        /// 按标签搜索GameObject（仅场景）
        /// </summary>
        private object HandleByTagSearch(StateTreeContext args)
        {
            // 标签查找只在场景中有意义，强制使用预构建的HierarchyStateTree
            return hierarchyStateTree.Run(args);
        }

        /// <summary>
        /// 按层级搜索GameObject（仅场景）
        /// </summary>
        private object HandleByLayerSearch(StateTreeContext args)
        {
            // 层级查找只在场景中有意义，强制使用预构建的HierarchyStateTree
            return hierarchyStateTree.Run(args);
        }

        /// <summary>
        /// 按组件搜索GameObject（仅场景）
        /// </summary>
        private object HandleByComponentSearch(StateTreeContext args)
        {
            // 组件查找只在场景中有意义，强制使用预构建的HierarchyStateTree
            return hierarchyStateTree.Run(args);
        }

        /// <summary>
        /// 按路径搜索GameObject
        /// </summary>
        private object HandleByPathSearch(StateTreeContext args)
        {
            var stateTree = GetStateTree(args);
            return stateTree.Run(args);
        }

        /// <summary>
        /// 按GUID搜索GameObject（仅项目资产）
        /// </summary>
        private object HandleByGuidSearch(StateTreeContext args)
        {
            // GUID查找只在项目资产中有意义，强制使用预构建的ProjectStateTree
            return projectStateTree.Run(args);
        }

        /// <summary>
        /// 默认搜索方法
        /// </summary>
        private object HandleDefaultSearch(StateTreeContext args)
        {
            bool searchInScene = args.TryGetValue<bool>("search_in_scene", out bool value) ? value : true;

            if (searchInScene)
            {
                // 优先在场景中查找，使用预构建的状态树
                var hierarchyResult = hierarchyStateTree.Run(args);

                // 如果场景中找到了结果，直接返回
                if (hierarchyResult is GameObject[] hierarchyGameObjects && hierarchyGameObjects.Length > 0)
                {
                    return hierarchyResult;
                }

                // 如果场景中没找到，也在项目中查找，使用预构建的状态树
                var projectResult = projectStateTree.Run(args);
                if (projectResult is GameObject[] projectGameObjects && projectGameObjects.Length > 0)
                {
                    return projectResult;
                }

                return Response.Error($"No GameObjects found in scene or project matching the search criteria.");
            }
            else
            {
                // 在项目资产和场景中都查找，使用预构建的状态树
                List<GameObject> allFound = new List<GameObject>();

                var hierarchyResult = hierarchyStateTree.Run(args);
                if (hierarchyResult is GameObject[] hierarchyGameObjects)
                {
                    allFound.AddRange(hierarchyGameObjects);
                }

                var projectResult = projectStateTree.Run(args);
                if (projectResult is GameObject[] projectGameObjects)
                {
                    allFound.AddRange(projectGameObjects);
                }

                if (allFound.Count == 0)
                {
                    return Response.Error($"No GameObjects found matching the search criteria.");
                }

                return allFound.Distinct().ToArray();
            }
        }

        /// <summary>
        /// 根据search_in_scene参数选择合适的预构建状态树
        /// </summary>
        private StateTree GetStateTree(StateTreeContext args)
        {
            bool searchInScene = args.TryGetValue<bool>("search_in_scene", out bool value) ? value : true;
            return searchInScene ? hierarchyStateTree : projectStateTree;
        }

        /// <summary>
        /// 从GameObject数组中提取第一个对象（兼容性方法）
        /// </summary>
        public GameObject GetFirstGameObject(object result)
        {
            if (result is GameObject[] gameObjects && gameObjects.Length > 0)
            {
                return gameObjects[0];
            }
            return null;
        }

        /// <summary>
        /// 从结果中提取GameObject数组（兼容性方法）
        /// </summary>
        public GameObject[] GetGameObjects(object result)
        {
            if (result is GameObject[] gameObjects)
            {
                return gameObjects;
            }
            return new GameObject[0];
        }

        /// <summary>
        /// 获取GameObject的详细信息
        /// </summary>
        public string GetGameObjectInfo(GameObject gameObject)
        {
            if (gameObject == null) return "null";

            // 判断是场景对象还是项目资产
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(gameObject);
            bool isSceneObject = string.IsNullOrEmpty(assetPath);

            if (isSceneObject)
            {
                return hierarchySelector.GetObjectInfo(gameObject);
            }
            else
            {
                return projectSelector.GetObjectInfo(gameObject);
            }
        }
    }
}
