using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal; // Required for tag management
using UnityEngine;
using UnityMcp.Models; // For Response class
using UnityMcp;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles Unity Editor state management and controls.
    /// 对应方法名: manage_editor
    /// </summary>
    [ToolName("manage_editor")]
    public class ManageEditor : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: play, pause, stop, get_state, set_active_tool, add_tag, add_layer, execute_menu, set_resolution", false),
                new MethodKey("wait_for_completion", "Whether to wait for operation completion", true),
                new MethodKey("tool_name", "Tool name (used when setting active tool)", true),
                new MethodKey("tag_name", "Tag name (used when adding tag)", true),
                new MethodKey("layer_name", "Layer name (used when adding layer)", true),
                new MethodKey("menu_path", "Menu path (used when executing menu)", true),
                new MethodKey("width", "Game window width (used when setting resolution)", true),
                new MethodKey("height", "Game window height (used when setting resolution)", true)
            };
        }

        // Constant for starting user layer index
        private const int FirstUserLayerIndex = 8;

        // Constant for total layer count
        private const int TotalLayerCount = 32;

        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    // Play Mode Control
                    .Leaf("play", HandlePlayAction)
                    .Leaf("pause", HandlePauseAction)
                    .Leaf("stop", HandleStopAction)

                    // Editor State/Info
                    .Leaf("get_state", HandleGetStateAction)
                    .Leaf("get_windows", HandleGetWindowsAction)
                    .Leaf("get_active_tool", HandleGetActiveToolAction)
                    .Leaf("get_selection", HandleGetSelectionAction)
                    .Leaf("set_active_tool", HandleSetActiveToolAction)

                    // Tag Management
                    .Leaf("add_tag", HandleAddTagAction)
                    .Leaf("remove_tag", HandleRemoveTagAction)
                    .Leaf("get_tags", HandleGetTagsAction)

                    // Layer Management
                    .Leaf("add_layer", HandleAddLayerAction)
                    .Leaf("remove_layer", HandleRemoveLayerAction)
                    .Leaf("get_layers", HandleGetLayersAction)

                    // Menu Management
                    .Leaf("execute_menu", MenuUtils.HandleExecuteMenu)

                    // Resolution Management
                    .Leaf("set_resolution", HandleSetResolutionAction)
                .Build();
        }

        // --- State Tree Action Handlers ---

        /// <summary>
        /// 处理进入播放模式的操作
        /// </summary>
        private object HandlePlayAction(JObject args)
        {
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    LogInfo("[ManageEditor] Entering play mode");
                    EditorApplication.isPlaying = true;
                    return Response.Success("Entered play mode.");
                }
                return Response.Success("Already in play mode.");
            }
            catch (Exception e)
            {
                LogInfo($"[ManageEditor] Error entering play mode: {e.Message}");
                return Response.Error($"Error entering play mode: {e.Message}");
            }
        }

        /// <summary>
        /// 处理暂停/恢复播放模式的操作
        /// </summary>
        private object HandlePauseAction(JObject args)
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    string statusMessage = EditorApplication.isPaused ? "Game paused." : "Game resumed.";
                    LogInfo($"[ManageEditor] {statusMessage}");
                    return Response.Success(statusMessage);
                }
                return Response.Error("Cannot pause/resume: Not in play mode.");
            }
            catch (Exception e)
            {
                LogInfo($"[ManageEditor] Error pausing/resuming game: {e.Message}");
                return Response.Error($"Error pausing/resuming game: {e.Message}");
            }
        }

        /// <summary>
        /// 处理停止播放模式的操作
        /// </summary>
        private object HandleStopAction(JObject args)
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    LogInfo("[ManageEditor] Exiting play mode");
                    EditorApplication.isPlaying = false;
                    return Response.Success("Exited play mode.");
                }
                return Response.Success("Already stopped (not in play mode).");
            }
            catch (Exception e)
            {
                LogInfo($"[ManageEditor] Error stopping play mode: {e.Message}");
                return Response.Error($"Error stopping play mode: {e.Message}");
            }
        }

        /// <summary>
        /// 处理获取编辑器状态的操作
        /// </summary>
        private object HandleGetStateAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting editor state");
            return GetEditorState();
        }

        /// <summary>
        /// 处理获取编辑器窗口的操作
        /// </summary>
        private object HandleGetWindowsAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting editor windows");
            return GetEditorWindows();
        }

        /// <summary>
        /// 处理获取当前工具的操作
        /// </summary>
        private object HandleGetActiveToolAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting active tool");
            return GetActiveTool();
        }

        /// <summary>
        /// 处理获取选择对象的操作
        /// </summary>
        private object HandleGetSelectionAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting selection");
            return GetSelection();
        }

        /// <summary>
        /// 处理设置激活工具的操作
        /// </summary>
        private object HandleSetActiveToolAction(JObject args)
        {
            string toolName = args["toolName"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                return Response.Error("'toolName' parameter required for set_active_tool.");
            }

            LogInfo($"[ManageEditor] Setting active tool to: {toolName}");
            return SetActiveTool(toolName);
        }

        /// <summary>
        /// 处理添加标签的操作
        /// </summary>
        private object HandleAddTagAction(JObject args)
        {
            string tagName = args["tagName"]?.ToString();
            if (string.IsNullOrEmpty(tagName))
            {
                return Response.Error("'tagName' parameter required for add_tag.");
            }

            LogInfo($"[ManageEditor] Adding tag: {tagName}");
            return AddTag(tagName);
        }

        /// <summary>
        /// 处理移除标签的操作
        /// </summary>
        private object HandleRemoveTagAction(JObject args)
        {
            string tagName = args["tagName"]?.ToString();
            if (string.IsNullOrEmpty(tagName))
            {
                return Response.Error("'tagName' parameter required for remove_tag.");
            }

            LogInfo($"[ManageEditor] Removing tag: {tagName}");
            return RemoveTag(tagName);
        }

        /// <summary>
        /// 处理获取标签列表的操作
        /// </summary>
        private object HandleGetTagsAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting tags");
            return GetTags();
        }

        /// <summary>
        /// 处理添加层的操作
        /// </summary>
        private object HandleAddLayerAction(JObject args)
        {
            string layerName = args["layerName"]?.ToString();
            if (string.IsNullOrEmpty(layerName))
            {
                return Response.Error("'layerName' parameter required for add_layer.");
            }

            LogInfo($"[ManageEditor] Adding layer: {layerName}");
            return AddLayer(layerName);
        }

        /// <summary>
        /// 处理移除层的操作
        /// </summary>
        private object HandleRemoveLayerAction(JObject args)
        {
            string layerName = args["layerName"]?.ToString();
            if (string.IsNullOrEmpty(layerName))
            {
                return Response.Error("'layerName' parameter required for remove_layer.");
            }

            LogInfo($"[ManageEditor] Removing layer: {layerName}");
            return RemoveLayer(layerName);
        }

        /// <summary>
        /// 处理获取层列表的操作
        /// </summary>
        private object HandleGetLayersAction(JObject args)
        {
            LogInfo("[ManageEditor] Getting layers");
            return GetLayers();
        }

        /// <summary>
        /// 处理设置游戏窗口分辨率的操作
        /// </summary>
        private object HandleSetResolutionAction(JObject args)
        {
            var widthToken = args["width"];
            var heightToken = args["height"];

            if (widthToken == null || heightToken == null)
            {
                return Response.Error("Both 'width' and 'height' parameters are required for set_resolution.");
            }

            if (!int.TryParse(widthToken.ToString(), out int width) || width <= 0)
            {
                return Response.Error("Width must be a positive integer.");
            }

            if (!int.TryParse(heightToken.ToString(), out int height) || height <= 0)
            {
                return Response.Error("Height must be a positive integer.");
            }

            LogInfo($"[ManageEditor] Setting game view resolution to {width}x{height}");
            return SetGameViewResolution(width, height);
        }

        // --- Editor State/Info Methods ---
        private object GetEditorState()
        {
            try
            {
                var state = new
                {
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    applicationPath = EditorApplication.applicationPath,
                    applicationContentsPath = EditorApplication.applicationContentsPath,
                    timeSinceStartup = EditorApplication.timeSinceStartup,
                };
                return Response.Success("Retrieved editor state.", state);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor state: {e.Message}");
            }
        }

        private object GetEditorWindows()
        {
            try
            {
                // Get all types deriving from EditorWindow
                var windowTypes = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.IsSubclassOf(typeof(EditorWindow)))
                    .ToList();

                var openWindows = new List<object>();

                // Find currently open instances
                // Resources.FindObjectsOfTypeAll seems more reliable than GetWindow for finding *all* open windows
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();

                foreach (EditorWindow window in allWindows)
                {
                    if (window == null)
                        continue; // Skip potentially destroyed windows

                    try
                    {
                        openWindows.Add(
                            new
                            {
                                title = window.titleContent.text,
                                typeName = window.GetType().FullName,
                                isFocused = EditorWindow.focusedWindow == window,
                                position = new
                                {
                                    x = window.position.x,
                                    y = window.position.y,
                                    width = window.position.width,
                                    height = window.position.height,
                                },
                                instanceID = window.GetInstanceID(),
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        if (McpConnect.EnableLog) Debug.LogWarning(
                            $"Could not get info for window {window.GetType().Name}: {ex.Message}"
                        );
                    }
                }

                return Response.Success("Retrieved list of open editor windows.", openWindows);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor windows: {e.Message}");
            }
        }

        private object GetActiveTool()
        {
            try
            {
                Tool currentTool = UnityEditor.Tools.current;
                string toolName = currentTool.ToString(); // Enum to string
                bool customToolActive = UnityEditor.Tools.current == Tool.Custom; // Check if a custom tool is active
                string activeToolName = customToolActive
                    ? EditorTools.GetActiveToolName()
                    : toolName; // Get custom name if needed

                var toolInfo = new
                {
                    activeTool = activeToolName,
                    isCustom = customToolActive,
                    pivotMode = UnityEditor.Tools.pivotMode.ToString(),
                    pivotRotation = UnityEditor.Tools.pivotRotation.ToString(),
                    handleRotation = UnityEditor.Tools.handleRotation.eulerAngles, // Euler for simplicity
                    handlePosition = UnityEditor.Tools.handlePosition,
                };

                return Response.Success("Retrieved active tool information.", toolInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting active tool: {e.Message}");
            }
        }

        private object SetActiveTool(string toolName)
        {
            try
            {
                Tool targetTool;
                if (Enum.TryParse<Tool>(toolName, true, out targetTool)) // Case-insensitive parse
                {
                    // Check if it's a valid built-in tool
                    if (targetTool != Tool.None && targetTool <= Tool.Custom) // Tool.Custom is the last standard tool
                    {
                        UnityEditor.Tools.current = targetTool;
                        return Response.Success($"Set active tool to '{targetTool}'.");
                    }
                    else
                    {
                        return Response.Error(
                            $"Cannot directly set tool to '{toolName}'. It might be None, Custom, or invalid."
                        );
                    }
                }
                else
                {
                    // Potentially try activating a custom tool by name here if needed
                    // This often requires specific editor scripting knowledge for that tool.
                    return Response.Error(
                        $"Could not parse '{toolName}' as a standard Unity Tool (View, Move, Rotate, Scale, Rect, Transform, Custom)."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting active tool: {e.Message}");
            }
        }

        private object GetSelection()
        {
            try
            {
                var selectionInfo = new
                {
                    activeObject = Selection.activeObject?.name,
                    activeGameObject = Selection.activeGameObject?.name,
                    activeTransform = Selection.activeTransform?.name,
                    activeInstanceID = Selection.activeInstanceID,
                    count = Selection.count,
                    objects = Selection
                        .objects.Select(obj => new
                        {
                            name = obj?.name,
                            type = obj?.GetType().FullName,
                            instanceID = obj?.GetInstanceID(),
                        })
                        .ToList(),
                    gameObjects = Selection
                        .gameObjects.Select(go => new
                        {
                            name = go?.name,
                            instanceID = go?.GetInstanceID(),
                        })
                        .ToList(),
                    assetGUIDs = Selection.assetGUIDs, // GUIDs for selected assets in Project view
                };

                return Response.Success("Retrieved current selection details.", selectionInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting selection: {e.Message}");
            }
        }

        // --- Tag Management Methods ---

        private object AddTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");

            // Check if tag already exists
            if (InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' already exists.");
            }

            try
            {
                // Add the tag using the internal utility
                InternalEditorUtility.AddTag(tagName);
                // Force save assets to ensure the change persists in the TagManager asset
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' added successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add tag '{tagName}': {e.Message}");
            }
        }

        private object RemoveTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");
            if (tagName.Equals("Untagged", StringComparison.OrdinalIgnoreCase))
                return Response.Error("Cannot remove the built-in 'Untagged' tag.");

            // Check if tag exists before attempting removal
            if (!InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' does not exist.");
            }

            try
            {
                // Remove the tag using the internal utility
                InternalEditorUtility.RemoveTag(tagName);
                // Force save assets
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' removed successfully.");
            }
            catch (Exception e)
            {
                // Catch potential issues if the tag is somehow in use or removal fails
                return Response.Error($"Failed to remove tag '{tagName}': {e.Message}");
            }
        }

        private object GetTags()
        {
            try
            {
                string[] tags = InternalEditorUtility.tags;
                return Response.Success("Retrieved current tags.", tags);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve tags: {e.Message}");
            }
        }

        // --- Layer Management Methods ---

        private object AddLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Check if layer name already exists (case-insensitive check recommended)
            for (int i = 0; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return Response.Error($"Layer '{layerName}' already exists at index {i}.");
                }
            }

            // Find the first empty user layer slot (indices 8 to 31)
            int firstEmptyUserLayer = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && string.IsNullOrEmpty(layerSP.stringValue))
                {
                    firstEmptyUserLayer = i;
                    break;
                }
            }

            if (firstEmptyUserLayer == -1)
            {
                return Response.Error("No empty User Layer slots available (8-31 are full).");
            }

            // Assign the name to the found slot
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    firstEmptyUserLayer
                );
                targetLayerSP.stringValue = layerName;
                // Apply the changes to the TagManager asset
                tagManager.ApplyModifiedProperties();
                // Save assets to make sure it's written to disk
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' added successfully to slot {firstEmptyUserLayer}."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add layer '{layerName}': {e.Message}");
            }
        }

        private object RemoveLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Find the layer by name (must be user layer)
            int layerIndexToRemove = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++) // Start from user layers
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                // Case-insensitive comparison is safer
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    layerIndexToRemove = i;
                    break;
                }
            }

            if (layerIndexToRemove == -1)
            {
                return Response.Error($"User layer '{layerName}' not found.");
            }

            // Clear the name for that index
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    layerIndexToRemove
                );
                targetLayerSP.stringValue = string.Empty; // Set to empty string to remove
                // Apply the changes
                tagManager.ApplyModifiedProperties();
                // Save assets
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' (slot {layerIndexToRemove}) removed successfully."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove layer '{layerName}': {e.Message}");
            }
        }

        private object GetLayers()
        {
            try
            {
                var layers = new Dictionary<int, string>();
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName)) // Only include layers that have names
                    {
                        layers.Add(i, layerName);
                    }
                }
                return Response.Success("Retrieved current named layers.", layers);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve layers: {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// Gets the SerializedObject for the TagManager asset.
        /// </summary>
        private SerializedObject GetTagManager()
        {
            try
            {
                // Load the TagManager asset from the ProjectSettings folder
                UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset"
                );
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    if (McpConnect.EnableLog) Debug.LogError("[ManageEditor] TagManager.asset not found in ProjectSettings.");
                    return null;
                }
                // The first object in the asset file should be the TagManager
                return new SerializedObject(tagManagerAssets[0]);
            }
            catch (Exception e)
            {
                if (McpConnect.EnableLog) Debug.LogError($"[ManageEditor] Error accessing TagManager.asset: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置游戏窗口分辨率
        /// </summary>
        private object SetGameViewResolution(int width, int height)
        {
            try
            {
                // 使用反射访问GameView类，因为它不是公开的API
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null)
                {
                    return Response.Error("Could not find GameView type.");
                }

                // 获取当前的GameView窗口
                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView == null)
                {
                    return Response.Error("Could not get GameView window.");
                }

                // 获取GameViewSizes类
                var gameViewSizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
                var gameViewSizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
                var gameViewSizeTypeEnum = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType");

                if (gameViewSizesType == null || gameViewSizeType == null || gameViewSizeTypeEnum == null)
                {
                    return Response.Error("Could not find GameView size types.");
                }

                // 获取单例实例
                var singletonMethod = gameViewSizesType.GetMethod("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var gameViewSizes = singletonMethod.Invoke(null, null);

                // 获取当前平台的游戏视图尺寸组
                var currentGroupMethod = gameViewSizes.GetType().GetMethod("GetCurrentGroupType");
                var currentGroup = (int)currentGroupMethod.Invoke(gameViewSizes, null);

                var getGroupMethod = gameViewSizes.GetType().GetMethod("GetGroup");
                var group = getGroupMethod.Invoke(gameViewSizes, new object[] { currentGroup });

                // 创建自定义分辨率
                var freeAspectValue = System.Enum.GetValues(gameViewSizeTypeEnum).GetValue(1); // GameViewSizeType.FreeAspectRatio
                var customSize = System.Activator.CreateInstance(gameViewSizeType, freeAspectValue, width, height, $"Custom {width}x{height}");

                // 检查是否已存在相同的自定义尺寸
                var getSizeCountMethod = group.GetType().GetMethod("GetBuiltinCount");
                var getCustomSizeCountMethod = group.GetType().GetMethod("GetCustomCount");
                var getTotalCountMethod = group.GetType().GetMethod("GetTotalCount");

                int totalCount = (int)getTotalCountMethod.Invoke(group, null);
                bool foundExistingSize = false;
                int targetIndex = -1;

                // 查找是否有匹配的分辨率
                var getSizeAtMethod = group.GetType().GetMethod("GetGameViewSize");
                for (int i = 0; i < totalCount; i++)
                {
                    var size = getSizeAtMethod.Invoke(group, new object[] { i });
                    var sizeWidth = (int)size.GetType().GetProperty("width").GetValue(size);
                    var sizeHeight = (int)size.GetType().GetProperty("height").GetValue(size);

                    if (sizeWidth == width && sizeHeight == height)
                    {
                        foundExistingSize = true;
                        targetIndex = i;
                        break;
                    }
                }

                // 如果没找到，添加新的自定义尺寸
                if (!foundExistingSize)
                {
                    var addCustomSizeMethod = group.GetType().GetMethod("AddCustomSize");
                    addCustomSizeMethod.Invoke(group, new object[] { customSize });
                    targetIndex = totalCount; // 新添加的索引
                }

                // 设置GameView使用该分辨率
                var selectedSizeIndexProperty = gameViewType.GetProperty("selectedSizeIndex",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (selectedSizeIndexProperty != null)
                {
                    selectedSizeIndexProperty.SetValue(gameView, targetIndex, null);

                    // 刷新GameView
                    gameView.Repaint();

                    return Response.Success($"Game view resolution set to {width}x{height}");
                }
                else
                {
                    return Response.Error("Could not set game view resolution: selectedSizeIndex property not found.");
                }
            }
            catch (System.Exception e)
            {
                return Response.Error($"Error setting game view resolution: {e.Message}");
            }
        }

        // --- Example Implementations for Settings ---
        /*
        private object SetQualityLevel(JToken qualityLevelToken) { ... }
        */
    }

    // Helper class to get custom tool names (remains the same)
    internal static class EditorTools
    {
        public static string GetActiveToolName()
        {
            // This is a placeholder. Real implementation depends on how custom tools
            // are registered and tracked in the specific Unity project setup.
            // It might involve checking static variables, calling methods on specific tool managers, etc.
            if (UnityEditor.Tools.current == Tool.Custom)
            {
                // Example: Check a known custom tool manager
                // if (MyCustomToolManager.IsActive) return MyCustomToolManager.ActiveToolName;
                return "Unknown Custom Tool";
            }
            return UnityEditor.Tools.current.ToString();
        }

    }
}

