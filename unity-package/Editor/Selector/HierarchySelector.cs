using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 场景层级对象选择器
    /// 专门处理场景层级中的对象查找
    /// 支持GameObject和Component的查找
    /// </summary>
    /// <typeparam name="T">要查找的Unity对象类型</typeparam>
    public class HierarchySelector<T> : BaseObjectSelector<T> where T : UnityEngine.Object
    {

        public override MethodKey[] CreateKeys()
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


        /// <summary>
        /// 构建场景层级对象查找状态树
        /// </summary>
        public override StateTree BuildStateTree()
        {
            var builder = StateTreeBuilder.Create()
                .Key("search_method")
                .Leaf("by_name", (Func<StateTreeContext, object>)HandleByNameSearch)
                .Leaf("by_type", (Func<StateTreeContext, object>)HandleByTypeSearch)
                .Leaf("by_id", (Func<StateTreeContext, object>)HandleByIdSearch)
                .Leaf("by_path", (Func<StateTreeContext, object>)HandleByPathSearch)
                .Leaf("find_all", (Func<StateTreeContext, object>)HandleFindAllSearch);

            // 为GameObject添加特殊的查找方法
            if (typeof(T) == typeof(GameObject) || typeof(GameObject).IsAssignableFrom(typeof(T)))
            {
                builder
                    .Leaf("by_tag", (Func<StateTreeContext, object>)HandleByTagSearch)
                    .Leaf("by_layer", (Func<StateTreeContext, object>)HandleByLayerSearch)
                    .Leaf("by_component", (Func<StateTreeContext, object>)HandleByComponentSearch);
            }

            return builder.DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultSearch).Build();
        }

        /// <summary>
        /// 按名称查找指定类型的对象数组
        /// </summary>
        public override T[] FindObjectsByName(string name)
        {
            List<T> foundObjects = new List<T>();

            // 在当前加载的场景中查找
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindObjectsByNameRecursive(rootObj.transform, name, foundObjects);
                    }
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

            // 在当前加载的场景中查找
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindAllObjectsRecursive(rootObj.transform, foundObjects);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按ID查找指定类型的对象
        /// </summary>
        public override T[] FindObjectsById(int instanceId)
        {
            List<T> foundObjects = new List<T>();

            // 在当前加载的场景中查找
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindObjectsByIdRecursive(rootObj.transform, instanceId, foundObjects);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按GUID查找对象（场景中的对象通常没有GUID）
        /// </summary>
        public override T[] FindObjectsByGuid(string guid)
        {
            // 场景对象通常没有GUID，返回空数组
            return new T[0];
        }

        #region 场景特有的查找方法

        /// <summary>
        /// 按标签搜索处理（仅适用于GameObject）
        /// </summary>
        private object HandleByTagSearch(StateTreeContext args)
        {
            if (typeof(T) != typeof(GameObject) && !typeof(GameObject).IsAssignableFrom(typeof(T)))
            {
                return Response.Error($"Tag search is only supported for GameObject, not for {typeof(T).Name}.");
            }

            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_tag search method.");
            }

            string targetTag = target.ToString();
            T[] foundObjects = FindGameObjectsByTag(targetTag);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No GameObjects with tag '{targetTag}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按层级搜索处理（仅适用于GameObject）
        /// </summary>
        private object HandleByLayerSearch(StateTreeContext args)
        {
            if (typeof(T) != typeof(GameObject) && !typeof(GameObject).IsAssignableFrom(typeof(T)))
            {
                return Response.Error($"Layer search is only supported for GameObject, not for {typeof(T).Name}.");
            }

            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_layer search method.");
            }

            string layerName = target.ToString();
            T[] foundObjects = FindGameObjectsByLayer(layerName);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No GameObjects in layer '{layerName}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按组件搜索处理（仅适用于GameObject）
        /// </summary>
        private object HandleByComponentSearch(StateTreeContext args)
        {
            if (typeof(T) != typeof(GameObject) && !typeof(GameObject).IsAssignableFrom(typeof(T)))
            {
                return Response.Error($"Component search is only supported for GameObject, not for {typeof(T).Name}.");
            }

            if (!args.TryGetValue("target", out object target) || target == null)
            {
                return Response.Error("Target parameter is required for by_component search method.");
            }

            string componentName = target.ToString();
            T[] foundObjects = FindGameObjectsByComponent(componentName);

            if (foundObjects.Length == 0)
            {
                return Response.Error($"No GameObjects with component '{componentName}' found.");
            }

            return foundObjects;
        }

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
                return Response.Error($"No objects at path '{targetPath}' found.");
            }

            return foundObjects;
        }

        /// <summary>
        /// 按标签查找GameObject数组
        /// </summary>
        private T[] FindGameObjectsByTag(string tag)
        {
            List<T> foundObjects = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindObjectsByTagRecursive(rootObj.transform, tag, foundObjects);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按层级查找GameObject数组
        /// </summary>
        private T[] FindGameObjectsByLayer(string layerName)
        {
            int layerId = LayerMask.NameToLayer(layerName);
            if (layerId == -1)
            {
                return new T[0]; // 无效层级名称
            }

            List<T> foundObjects = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindObjectsByLayerRecursive(rootObj.transform, layerId, foundObjects);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按组件查找GameObject数组
        /// </summary>
        private T[] FindGameObjectsByComponent(string componentName)
        {
            Type componentType = FindUnityObjectType(componentName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                return new T[0];
            }

            List<T> foundObjects = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject[] rootObjects = scene.GetRootGameObjects();
                    foreach (GameObject rootObj in rootObjects)
                    {
                        FindObjectsByComponentRecursive(rootObj.transform, componentType, foundObjects);
                    }
                }
            }

            return foundObjects.ToArray();
        }

        /// <summary>
        /// 按路径查找对象数组
        /// </summary>
        private T[] FindObjectsByPath(string path)
        {
            List<T> foundObjects = new List<T>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    GameObject foundObj = FindObjectByPath(scene, path);
                    if (foundObj != null)
                    {
                        if (foundObj is T directMatch)
                        {
                            foundObjects.Add(directMatch);
                        }

                        // 如果T是Component类型，查找GameObject上的组件
                        if (typeof(Component).IsAssignableFrom(typeof(T)))
                        {
                            T component = foundObj.GetComponent<T>();
                            if (component != null)
                            {
                                foundObjects.Add(component);
                            }
                        }
                    }
                }
            }

            return foundObjects.ToArray();
        }

        #endregion

        #region 递归查找辅助方法

        /// <summary>
        /// 递归查找对象按名称
        /// </summary>
        private void FindObjectsByNameRecursive(Transform parent, string name, List<T> results)
        {
            GameObject obj = parent.gameObject;

            // 检查GameObject本身
            if (obj.name == name)
            {
                if (obj is T gameObjectMatch)
                {
                    results.Add(gameObjectMatch);
                }

                // 如果T是Component类型，查找所有匹配的组件
                if (typeof(Component).IsAssignableFrom(typeof(T)))
                {
                    T[] components = obj.GetComponents<T>();
                    results.AddRange(components);
                }
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindObjectsByNameRecursive(parent.GetChild(i), name, results);
            }
        }

        /// <summary>
        /// 递归查找所有对象
        /// </summary>
        private void FindAllObjectsRecursive(Transform parent, List<T> results)
        {
            GameObject obj = parent.gameObject;

            // 检查GameObject本身
            if (obj is T gameObjectMatch)
            {
                results.Add(gameObjectMatch);
            }

            // 如果T是Component类型，查找所有匹配的组件
            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                T[] components = obj.GetComponents<T>();
                results.AddRange(components);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindAllObjectsRecursive(parent.GetChild(i), results);
            }
        }

        /// <summary>
        /// 递归查找对象按ID
        /// </summary>
        private void FindObjectsByIdRecursive(Transform parent, int instanceId, List<T> results)
        {
            GameObject obj = parent.gameObject;

            // 检查GameObject本身
            if (obj.GetInstanceID() == instanceId && obj is T gameObjectMatch)
            {
                results.Add(gameObjectMatch);
            }

            // 检查所有组件
            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component.GetInstanceID() == instanceId && component is T componentMatch)
                {
                    results.Add(componentMatch);
                }
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindObjectsByIdRecursive(parent.GetChild(i), instanceId, results);
            }
        }

        /// <summary>
        /// 递归查找对象按标签
        /// </summary>
        private void FindObjectsByTagRecursive(Transform parent, string tag, List<T> results)
        {
            GameObject obj = parent.gameObject;

            if (obj.CompareTag(tag) && obj is T gameObjectMatch)
            {
                results.Add(gameObjectMatch);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindObjectsByTagRecursive(parent.GetChild(i), tag, results);
            }
        }

        /// <summary>
        /// 递归查找对象按层级
        /// </summary>
        private void FindObjectsByLayerRecursive(Transform parent, int layerId, List<T> results)
        {
            GameObject obj = parent.gameObject;

            if (obj.layer == layerId && obj is T gameObjectMatch)
            {
                results.Add(gameObjectMatch);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindObjectsByLayerRecursive(parent.GetChild(i), layerId, results);
            }
        }

        /// <summary>
        /// 递归查找对象按组件
        /// </summary>
        private void FindObjectsByComponentRecursive(Transform parent, Type componentType, List<T> results)
        {
            GameObject obj = parent.gameObject;

            if (obj.GetComponent(componentType) != null && obj is T gameObjectMatch)
            {
                results.Add(gameObjectMatch);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                FindObjectsByComponentRecursive(parent.GetChild(i), componentType, results);
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 在场景中按路径查找对象
        /// </summary>
        private GameObject FindObjectByPath(Scene scene, string path)
        {
            string[] pathParts = path.Split('/');
            if (pathParts.Length == 0) return null;

            // 在根对象中查找第一部分
            GameObject[] rootObjects = scene.GetRootGameObjects();
            GameObject current = null;

            foreach (GameObject rootObj in rootObjects)
            {
                if (rootObj.name == pathParts[0])
                {
                    current = rootObj;
                    break;
                }
            }

            if (current == null) return null;

            // 遍历路径的其余部分
            for (int i = 1; i < pathParts.Length; i++)
            {
                Transform found = current.transform.Find(pathParts[i]);
                if (found == null) return null;
                current = found.gameObject;
            }

            return current;
        }

        /// <summary>
        /// 获取对象的详细信息（重写）
        /// </summary>
        public override string GetObjectInfo(T obj)
        {
            if (obj == null) return "null";

            string baseInfo = base.GetObjectInfo(obj);

            if (obj is GameObject gameObject)
            {
                string path = GetGameObjectPath(gameObject);
                string tag = gameObject.tag;
                string layer = LayerMask.LayerToName(gameObject.layer);
                bool active = gameObject.activeInHierarchy;

                return $"{baseInfo}, Path: {path}, Tag: {tag}, Layer: {layer}, Active: {active}";
            }
            else if (obj is Component component)
            {
                string path = GetGameObjectPath(component.gameObject);
                return $"{baseInfo}, GameObject: {component.gameObject.name}, Path: {path}";
            }

            return baseInfo;
        }

        #endregion
    }
}