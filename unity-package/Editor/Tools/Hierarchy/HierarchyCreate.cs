using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    /// Handles GameObject creation operations in the scene hierarchy.
    /// 对应方法名: hierarchy_create
    /// </summary>
    [ToolName("hierarchy_create", "层级管理")]
    public class HierarchyCreate : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("name", "GameObject名称", false),
                new MethodKey("from", "操作类型：menu, primitive, prefab", false),
                new MethodKey("tag", "GameObject标签", true),
                new MethodKey("layer", "GameObject所在层", true),
                new MethodKey("parent_id", "父对象标识符", true),
                new MethodKey("position", "位置坐标 [x, y, z]", true),
                new MethodKey("rotation", "旋转角度 [x, y, z]", true),
                new MethodKey("scale", "缩放比例 [x, y, z]", true),
                new MethodKey("primitive_type", "基元类型：Cube, Sphere, Cylinder, Capsule, Plane, Quad", true),
                new MethodKey("prefab_path", "预制体路径", true),
                new MethodKey("menu_path", "菜单路径", true),
                new MethodKey("save_as_prefab", "是否保存为预制体", true),
                new MethodKey("set_active", "设置激活状态", true),
            };
        }

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("from")
                    .Leaf("menu", HandleCreateFromMenu)
                    .Branch("primitive")
                        .OptionalKey("primitive_type")
                            .Leaf("Cube", HandleCreateCube)
                            .Leaf("Sphere", HandleCreateSphere)
                            .Leaf("Cylinder", HandleCreateCylinder)
                            .Leaf("Capsule", HandleCreateCapsule)
                            .Leaf("Plane", HandleCreatePlane)
                            .Leaf("Quad", HandleCreateQuad)
                            .DefaultLeaf(HandleCreateFromPrimitive)
                        .Up()
                        .DefaultLeaf(HandleCreateFromPrimitive)
                    .Up()
                    .Leaf("prefab", HandleCreateFromPrefab)
                .Build();
        }

        /// <summary>
        /// 异步下载 
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private IEnumerator HandleCreateFromMenuAsync(StateTreeContext ctx)
        {
            string menuPath = ctx["menu_path"]?.ToString();
            if (string.IsNullOrEmpty(menuPath))
            {
                yield return Response.Error("'menu_path' parameter is required for menu creation.");
                yield break;
            }

            LogInfo($"[HierarchyCreate] Creating GameObject from menu: '{menuPath}'");

            // 记录创建前的选中对象
            GameObject previousSelection = Selection.activeGameObject;
            int previousSelectionID = previousSelection != null ? previousSelection.GetInstanceID() : 0;

            // 执行菜单项
            var menuResult = MenuUtils.TryExecuteMenuItem(menuPath);

            // 检查菜单执行结果
            string menuResultStr = menuResult?.ToString() ?? "";
            if (menuResultStr.Contains("Failed") || menuResultStr.Contains("Error"))
            {
                LogInfo($"[HierarchyCreate] Menu execution failed: {menuResultStr}");
                yield return menuResult;
                yield break;
            }

            LogInfo($"[HierarchyCreate] Menu executed successfully: {menuResultStr}");

            // 多次尝试检测新创建的对象，因为菜单创建可能需要时间
            GameObject newObject = null;
            int maxRetries = 10;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                newObject = Selection.activeGameObject;

                // 检查是否找到了新对象
                if (newObject != null &&
                    (previousSelection == null || newObject.GetInstanceID() != previousSelectionID))
                {
                    LogInfo($"[HierarchyCreate] Found newly created object: '{newObject.name}' (ID: {newObject.GetInstanceID()}) after {retryCount} retries");
                    break;
                }

                retryCount++;
                if (retryCount < maxRetries)
                {
                    // 优化携程调用：每次重试之间yield return null，等待一帧
                    yield return null;
                }
            }

            // 如果找到了新对象，进行设置
            if (newObject != null &&
                (previousSelection == null || newObject.GetInstanceID() != previousSelectionID))
            {
                // 再次等待一帧，确保对象完全初始化
                yield return null;

                LogInfo($"[HierarchyCreate] Finalizing newly created object: '{newObject.name}' (ID: {newObject.GetInstanceID()})");

                // 应用其他设置（名称、位置等）
                var finalizeResult = FinalizeGameObjectCreation(ctx.JsonData, newObject, false);
                LogInfo($"[HierarchyCreate] Finalization result: {finalizeResult}");
                yield return finalizeResult;
                yield break;
            }
            else
            {
                // 如果没有找到新对象，但菜单执行成功
                LogInfo($"[HierarchyCreate] Menu executed but no new object was detected after {maxRetries} retries. Previous: {previousSelection?.name}, Current: {newObject?.name}");
                yield return Response.Success($"Menu item '{menuPath}' executed successfully, but no new GameObject was detected.");
                yield break;
            }
        }

        /// <summary>
        /// 处理从菜单创建GameObject的操作
        /// </summary>
        private object HandleCreateFromMenu(StateTreeContext ctx)
        {
            return ctx.AsyncReturn(HandleCreateFromMenuAsync(ctx));
        }

        /// <summary>
        /// 处理从预制体创建GameObject的操作
        /// </summary>
        private object HandleCreateFromPrefab(JObject args)
        {
            string prefabPath = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(prefabPath))
            {
                return Response.Error("'prefab_path' parameter is required for prefab instantiation.");
            }

            LogInfo($"[HierarchyCreate] Creating GameObject from prefab: '{prefabPath}'");
            return CreateGameObjectFromPrefab(args, prefabPath);
        }

        /// <summary>
        /// 处理从基元类型创建GameObject的操作
        /// </summary>
        private object HandleCreateFromPrimitive(JObject args)
        {
            string primitiveType = args["primitive_type"]?.ToString();
            if (string.IsNullOrEmpty(primitiveType))
            {
                // 默认使用Cube作为基元类型
                primitiveType = "Cube";
                LogInfo("[HierarchyCreate] No primitive_type specified, using default: Cube");
            }

            LogInfo($"[HierarchyCreate] Creating GameObject from primitive: '{primitiveType}'");
            return CreateGameObjectFromPrimitive(args, primitiveType);
        }

        /// <summary>
        /// 处理创建Cube的操作
        /// </summary>
        private object HandleCreateCube(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Cube primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cube");
        }

        /// <summary>
        /// 处理创建Sphere的操作
        /// </summary>
        private object HandleCreateSphere(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Sphere primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Sphere");
        }

        /// <summary>
        /// 处理创建Cylinder的操作
        /// </summary>
        private object HandleCreateCylinder(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Cylinder primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Cylinder");
        }

        /// <summary>
        /// 处理创建Capsule的操作
        /// </summary>
        private object HandleCreateCapsule(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Capsule primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Capsule");
        }

        /// <summary>
        /// 处理创建Plane的操作
        /// </summary>
        private object HandleCreatePlane(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Plane primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Plane");
        }

        /// <summary>
        /// 处理创建Quad的操作
        /// </summary>
        private object HandleCreateQuad(JObject args)
        {
            LogInfo("[HierarchyCreate] Creating Quad primitive using specialized handler");
            return CreateGameObjectFromPrimitive(args, "Quad");
        }

        // --- Core Creation Methods ---

        /// <summary>
        /// 从预制体创建GameObject
        /// </summary>
        private object CreateGameObjectFromPrefab(JObject args, string prefabPath)
        {
            try
            {
                // 处理预制体路径查找逻辑
                string resolvedPath = ResolvePrefabPath(prefabPath);
                if (string.IsNullOrEmpty(resolvedPath))
                {
                    LogInfo($"[HierarchyCreate] Prefab not found at path: '{prefabPath}'");
                    return Response.Error($"Prefab not found at path: '{prefabPath}'");
                }

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(resolvedPath);
                if (prefabAsset == null)
                {
                    LogInfo($"[HierarchyCreate] Failed to load prefab asset at: '{resolvedPath}'");
                    return Response.Error($"Failed to load prefab asset at: '{resolvedPath}'");
                }

                // 实例化预制体
                GameObject newGo = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (newGo == null)
                {
                    LogInfo($"[HierarchyCreate] Failed to instantiate prefab: '{resolvedPath}'");
                    return Response.Error($"Failed to instantiate prefab: '{resolvedPath}'");
                }

                // 等待Unity完成对象初始化
                //Thread.Sleep(10);

                // 设置名称
                string name = args["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }

                // 注册撤销操作
                Undo.RegisterCreatedObjectUndo(newGo, $"Instantiate Prefab '{prefabAsset.name}' as '{newGo.name}'");
                LogInfo($"[HierarchyCreate] Instantiated prefab '{prefabAsset.name}' from path '{resolvedPath}' as '{newGo.name}'");

                return FinalizeGameObjectCreation(args, newGo, false);
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Error instantiating prefab '{prefabPath}': {e.Message}");
                return Response.Error($"Error instantiating prefab '{prefabPath}': {e.Message}");
            }
        }

        /// <summary>
        /// 从基元类型创建GameObject
        /// </summary>
        private object CreateGameObjectFromPrimitive(JObject args, string primitiveType)
        {
            try
            {
                PrimitiveType type = (PrimitiveType)Enum.Parse(typeof(PrimitiveType), primitiveType, true);
                GameObject newGo = GameObject.CreatePrimitive(type);

                // 等待Unity完成对象初始化
                //Thread.Sleep(10);

                // 设置名称
                string name = args["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    newGo.name = name;
                }
                else
                {
                    LogInfo("[HierarchyCreate] 'name' parameter is recommended when creating a primitive.");
                }

                // 注册撤销操作
                Undo.RegisterCreatedObjectUndo(newGo, $"Create GameObject '{newGo.name}'");
                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (ArgumentException)
            {
                LogInfo($"[HierarchyCreate] Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
                return Response.Error($"Invalid primitive type: '{primitiveType}'. Valid types: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}");
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Failed to create primitive '{primitiveType}': {e.Message}");
                return Response.Error($"Failed to create primitive '{primitiveType}': {e.Message}");
            }
        }

        /// <summary>
        /// 创建空GameObject
        /// </summary>
        private object CreateEmptyGameObject(JObject args, string name)
        {
            try
            {
                GameObject newGo = new GameObject(name);

                // 等待Unity完成对象初始化
                //Thread.Sleep(10);

                Undo.RegisterCreatedObjectUndo(newGo, $"Create GameObject '{newGo.name}'");

                return FinalizeGameObjectCreation(args, newGo, true);
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Failed to create empty GameObject '{name}': {e.Message}");
                return Response.Error($"Failed to create empty GameObject '{name}': {e.Message}");
            }
        }

        /// <summary>
        /// 解析预制体路径
        /// </summary>
        private string ResolvePrefabPath(string prefabPath)
        {
            // 如果没有路径分隔符且没有.prefab扩展名，搜索预制体
            if (!prefabPath.Contains("/") && !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                string prefabNameOnly = prefabPath;
                LogInfo($"[HierarchyCreate] Searching for prefab named: '{prefabNameOnly}'");

                string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabNameOnly}");
                if (guids.Length == 0)
                {
                    return null; // 未找到
                }
                else if (guids.Length > 1)
                {
                    string foundPaths = string.Join(", ", guids.Select(g => AssetDatabase.GUIDToAssetPath(g)));
                    LogInfo($"[HierarchyCreate] Multiple prefabs found matching name '{prefabNameOnly}': {foundPaths}. Using first one.");
                }

                string resolvedPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                LogInfo($"[HierarchyCreate] Found prefab at path: '{resolvedPath}'");
                return resolvedPath;
            }
            else if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // 自动添加.prefab扩展名
                LogInfo($"[HierarchyCreate] Adding .prefab extension to path: '{prefabPath}'");
                return prefabPath + ".prefab";
            }

            return prefabPath;
        }

        /// <summary>
        /// 完成GameObject创建的通用设置
        /// </summary>
        private object FinalizeGameObjectCreation(JObject args, GameObject newGo, bool createdNewObject)
        {
            if (newGo == null)
            {
                return Response.Error("GameObject creation failed.");
            }

            try
            {
                LogInfo($"[HierarchyCreate] Starting finalization for '{newGo.name}' (ID: {newGo.GetInstanceID()})");

                // 记录变换和属性的变更
                Undo.RecordObject(newGo.transform, "Set GameObject Transform");
                Undo.RecordObject(newGo, "Set GameObject Properties");

                // 应用通用设置（包括名称设置）
                GameObjectUtils.ApplyCommonGameObjectSettings(args, newGo, LogInfo);

                LogInfo($"[HierarchyCreate] Applied settings to '{newGo.name}' (ID: {newGo.GetInstanceID()})");

                // 处理预制体保存
                GameObject finalInstance = newGo;
                bool saveAsPrefab = args["save_as_prefab"]?.ToObject<bool>() ?? false;

                if (createdNewObject && saveAsPrefab)
                {
                    finalInstance = HandlePrefabSaving(args, newGo);
                    if (finalInstance == null)
                    {
                        return Response.Error("Failed to save GameObject as prefab.");
                    }
                }

                // 等待一下再选择对象
                //Thread.Sleep(5);

                // 选择对象
                Selection.activeGameObject = finalInstance;

                LogInfo($"[HierarchyCreate] Finalized '{finalInstance.name}' (ID: {finalInstance.GetInstanceID()})");

                // 生成成功消息
                string successMessage = GenerateCreationSuccessMessage(args, finalInstance, createdNewObject, saveAsPrefab);
                return Response.Success(successMessage, GameObjectUtils.GetGameObjectData(finalInstance));
            }
            catch (Exception e)
            {
                LogError($"[HierarchyCreate] Error finalizing GameObject creation: {e.Message}");
                // 清理失败的对象
                if (newGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                }
                return Response.Error($"Error finalizing GameObject creation: {e.Message}");
            }
        }



        /// <summary>
        /// 处理预制体保存
        /// </summary>
        private GameObject HandlePrefabSaving(JObject args, GameObject newGo)
        {
            string prefabPath = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(prefabPath))
            {
                LogInfo("[HierarchyCreate] 'prefab_path' is required when 'save_as_prefab' is true.");
                return null;
            }

            string finalPrefabPath = prefabPath;
            if (!finalPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                LogInfo($"[HierarchyCreate] Adding .prefab extension to save path: '{finalPrefabPath}'");
                finalPrefabPath += ".prefab";
            }

            try
            {
                // 确保目录存在
                string directoryPath = System.IO.Path.GetDirectoryName(finalPrefabPath);
                if (!string.IsNullOrEmpty(directoryPath) && !System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                    AssetDatabase.Refresh();
                    LogInfo($"[HierarchyCreate] Created directory for prefab: {directoryPath}");

                    // 等待Unity完成目录创建
                    //Thread.Sleep(50);
                }

                // 保存为预制体
                GameObject finalInstance = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    newGo,
                    finalPrefabPath,
                    InteractionMode.UserAction
                );

                if (finalInstance == null)
                {
                    UnityEngine.Object.DestroyImmediate(newGo);
                    return null;
                }

                // 等待预制体保存完成
                //Thread.Sleep(10);

                LogInfo($"[HierarchyCreate] GameObject '{newGo.name}' saved as prefab to '{finalPrefabPath}' and instance connected.");
                return finalInstance;
            }
            catch (Exception e)
            {
                LogInfo($"[HierarchyCreate] Error saving prefab '{finalPrefabPath}': {e.Message}");
                UnityEngine.Object.DestroyImmediate(newGo);
                return null;
            }
        }

        /// <summary>
        /// 生成创建成功消息
        /// </summary>
        private string GenerateCreationSuccessMessage(JObject args, GameObject finalInstance, bool createdNewObject, bool saveAsPrefab)
        {
            string messagePrefabPath = AssetDatabase.GetAssetPath(
                PrefabUtility.GetCorrespondingObjectFromSource(finalInstance) ?? (UnityEngine.Object)finalInstance
            );

            if (!createdNewObject && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"Prefab '{messagePrefabPath}' instantiated successfully as '{finalInstance.name}'.";
            }
            else if (createdNewObject && saveAsPrefab && !string.IsNullOrEmpty(messagePrefabPath))
            {
                return $"GameObject '{finalInstance.name}' created and saved as prefab to '{messagePrefabPath}'.";
            }
            else
            {
                return $"GameObject '{finalInstance.name}' created successfully in scene.";
            }
        }
    }
}
