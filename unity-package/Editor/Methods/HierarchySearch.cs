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
                new MethodKey("action", "操作类型：find", false),
                new MethodKey("search_method", "搜索方法：by_name, by_id, by_tag, by_layer, by_component等", true),
                new MethodKey("search_term", "搜索条件", true),
                new MethodKey("target", "搜索目标（可以是ID、名称或路径）", true),
                new MethodKey("search_in_children", "是否在子对象中搜索", true),
                new MethodKey("search_in_inactive", "是否搜索非激活对象", true),
                new MethodKey("find_all", "是否查找所有匹配项", true)
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("find", HandleFindAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理查找GameObject的操作
        /// </summary>
        private object HandleFindAction(JObject args)
        {
            JToken targetToken = args["target"];
            string searchMethod = args["search_method"]?.ToString()?.ToLower();
            return FindGameObjects(args, targetToken, searchMethod);
        }

        // --- Search and Find Methods ---

        private object FindGameObjects(
            JObject cmd,
            JToken targetToken,
            string searchMethod
        )
        {
            bool findAll = cmd["find_all"]?.ToObject<bool>() ?? false;
            List<GameObject> foundObjects = GameObjectUtils.FindObjectsInternal(
                targetToken,
                searchMethod,
                findAll,
                cmd
            );

            if (foundObjects.Count == 0)
            {
                return Response.Success("No matching GameObjects found.", new List<object>());
            }

            var results = foundObjects.Select(go => GameObjectUtils.GetGameObjectData(go)).ToList();
            return Response.Success($"Found {results.Count} GameObject(s).", results);
        }








    }
}
