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
                new MethodKey("action", "操作类型：play, pause, stop, get_state, set_active_tool, add_tag, add_layer", false),
                new MethodKey("wait_for_completion", "是否等待操作完成", true),
                new MethodKey("tool_name", "工具名称（设置活动工具时使用）", true),
                new MethodKey("tag_name", "标签名称（添加标签时使用）", true),
                new MethodKey("layer_name", "层名称（添加层时使用）", true)
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
                        if (UnityMcp.EnableLog) Debug.LogWarning(
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
                    if (UnityMcp.EnableLog) Debug.LogError("[ManageEditor] TagManager.asset not found in ProjectSettings.");
                    return null;
                }
                // The first object in the asset file should be the TagManager
                return new SerializedObject(tagManagerAssets[0]);
            }
            catch (Exception e)
            {
                if (UnityMcp.EnableLog) Debug.LogError($"[ManageEditor] Error accessing TagManager.asset: {e.Message}");
                return null;
            }
        }


        // --- Example Implementations for Settings ---
        /*
        private object SetGameViewResolution(int width, int height) { ... }
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

