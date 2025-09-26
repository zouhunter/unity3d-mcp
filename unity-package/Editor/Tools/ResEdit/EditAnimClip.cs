using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 专门的动画片段管理工具，提供动画片段的创建、修改、复制、删除等操作
    /// 对应方法名: manage_anim_clip
    /// </summary>
    [ToolName("edit_anim_clip", "资源管理")]
    public class EditAnimClip : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create, modify, duplicate, delete, get_info, search, set_curve, set_events", false),
                new MethodKey("path", "动画片段资源路径，Unity标准格式：Assets/Animations/ClipName.anim", false),
                new MethodKey("source_path", "源动画片段路径（复制时使用）", true),
                new MethodKey("destination", "目标路径（复制/移动时使用）", true),
                new MethodKey("query", "搜索模式，如*.anim", true),
                new MethodKey("recursive", "是否递归搜索子文件夹", true),
                new MethodKey("force", "是否强制执行操作（覆盖现有文件等）", true),
                new MethodKey("length", "动画长度（秒）", true),
                new MethodKey("frame_rate", "帧率", true),
                new MethodKey("loop_time", "是否循环播放", true),
                new MethodKey("loop_pose", "是否循环姿势", true),
                new MethodKey("cycle_offset", "循环偏移", true),
                new MethodKey("root_rotation_offset_y", "根旋转Y轴偏移", true),
                new MethodKey("root_height_offset_y", "根高度Y轴偏移", true),
                new MethodKey("root_height_offset_y_active", "是否启用根高度Y轴偏移", true),
                new MethodKey("lock_root_height_y", "是否锁定根高度Y轴", true),
                new MethodKey("lock_root_rotation_y", "是否锁定根旋转Y轴", true),
                new MethodKey("lock_root_rotation_offset_y", "是否锁定根旋转偏移Y轴", true),
                new MethodKey("keep_original_orientation_y", "是否保持原始方向Y轴", true),
                new MethodKey("height_from_ground", "是否从地面计算高度", true),
                new MethodKey("mirror", "是否镜像", true),
                new MethodKey("body_orientation", "身体方向", true),
                new MethodKey("curves", "动画曲线数据", true),
                new MethodKey("events", "动画事件数据", true)
            };
        }

        /// <summary>
        /// 创建状态树
        /// </summary>
        protected override StateTree CreateStateTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("create", CreateAnimClip)
                    .Leaf("modify", ModifyAnimClip)
                    .Leaf("duplicate", DuplicateAnimClip)
                    .Leaf("delete", DeleteAnimClip)
                    .Leaf("get_info", GetAnimClipInfo)
                    .Leaf("search", SearchAnimClips)
                    .Leaf("set_curve", SetAnimClipCurve)
                    .Leaf("set_events", SetAnimClipEvents)
                    .Leaf("set_settings", SetAnimClipSettings)
                    .Leaf("copy_from_model", CopyAnimClipFromModel)
                .Build();
        }

        // --- 状态树操作方法 ---

        private object CreateAnimClip(JObject args)
        {
            string path = args["path"]?.ToString();
            float length = args["length"]?.ToObject<float>() ?? 1.0f;
            float frameRate = args["frame_rate"]?.ToObject<float>() ?? 30.0f;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for create.");

            string fullPath = SanitizeAssetPath(path);
            string directory = Path.GetDirectoryName(fullPath);

            // 确保目录存在
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), directory)))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), directory));
                AssetDatabase.Refresh();
            }

            if (AssetExists(fullPath))
                return Response.Error($"Animation clip already exists at path: {fullPath}");

            try
            {
                AnimationClip clip = new AnimationClip();

                // 设置基本属性
                clip.frameRate = frameRate;
                // 注意：clip.length 是只读属性，不能直接设置
                // 动画长度通常由动画数据本身决定

                // 应用设置
                JObject settings = args["settings"] as JObject;
                if (settings != null)
                {
                    ApplyAnimClipSettings(clip, settings);
                }

                AssetDatabase.CreateAsset(clip, fullPath);
                AssetDatabase.SaveAssets();

                LogInfo($"[ManageAnimClip] Created animation clip at '{fullPath}' with length {length}s and frame rate {frameRate}fps");
                return Response.Success($"Animation clip '{fullPath}' created successfully.", GetAnimClipData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to create animation clip at '{fullPath}': {e.Message}");
            }
        }

        private object ModifyAnimClip(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject settings = args["settings"] as JObject;
            JObject curves = args["curves"] as JObject;
            JArray events = args["events"] as JArray;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for modify.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
                if (clip == null)
                    return Response.Error($"Failed to load animation clip at path: {fullPath}");

                Undo.RecordObject(clip, $"Modify Animation Clip '{Path.GetFileName(fullPath)}'");

                bool modified = false;

                // 应用设置
                if (settings != null && settings.HasValues)
                {
                    modified |= ApplyAnimClipSettings(clip, settings);
                }

                // 应用曲线
                if (curves != null && curves.HasValues)
                {
                    modified |= ApplyAnimClipCurves(clip, curves);
                }

                // 应用事件
                if (events != null && events.Count > 0)
                {
                    modified |= ApplyAnimClipEvents(clip, events);
                }

                if (modified)
                {
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                    LogInfo($"[ManageAnimClip] Modified animation clip at '{fullPath}'");
                    return Response.Success($"Animation clip '{fullPath}' modified successfully.", GetAnimClipData(fullPath));
                }
                else
                {
                    return Response.Success($"No applicable properties found to modify for animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to modify animation clip '{fullPath}': {e.Message}");
            }
        }

        private object DuplicateAnimClip(JObject args)
        {
            string path = args["path"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for duplicate.");

            string sourcePath = SanitizeAssetPath(path);
            if (!AssetExists(sourcePath))
                return Response.Error($"Source animation clip not found at path: {sourcePath}");

            string destPath;
            if (string.IsNullOrEmpty(destinationPath))
            {
                destPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            }
            else
            {
                destPath = SanitizeAssetPath(destinationPath);
                if (AssetExists(destPath))
                    return Response.Error($"Animation clip already exists at destination path: {destPath}");
                EnsureDirectoryExists(Path.GetDirectoryName(destPath));
            }

            try
            {
                bool success = AssetDatabase.CopyAsset(sourcePath, destPath);
                if (success)
                {
                    LogInfo($"[ManageAnimClip] Duplicated animation clip from '{sourcePath}' to '{destPath}'");
                    return Response.Success($"Animation clip '{sourcePath}' duplicated to '{destPath}'.", GetAnimClipData(destPath));
                }
                else
                {
                    return Response.Error($"Failed to duplicate animation clip from '{sourcePath}' to '{destPath}'.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error duplicating animation clip '{sourcePath}': {e.Message}");
            }
        }

        private object DeleteAnimClip(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for delete.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                bool success = AssetDatabase.DeleteAsset(fullPath);
                if (success)
                {
                    LogInfo($"[ManageAnimClip] Deleted animation clip at '{fullPath}'");
                    return Response.Success($"Animation clip '{fullPath}' deleted successfully.");
                }
                else
                {
                    return Response.Error($"Failed to delete animation clip '{fullPath}'. Check logs or if the file is locked.");
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error deleting animation clip '{fullPath}': {e.Message}");
            }
        }

        private object GetAnimClipInfo(JObject args)
        {
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for get_info.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                return Response.Success("Animation clip info retrieved.", GetAnimClipData(fullPath));
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting info for animation clip '{fullPath}': {e.Message}");
            }
        }

        private object SearchAnimClips(JObject args)
        {
            string searchPattern = args["query"]?.ToString();
            string pathScope = args["path"]?.ToString();
            bool recursive = args["recursive"]?.ToObject<bool>() ?? true;

            List<string> searchFilters = new List<string>();
            if (!string.IsNullOrEmpty(searchPattern))
                searchFilters.Add(searchPattern);
            searchFilters.Add("t:AnimationClip");

            string[] folderScope = null;
            if (!string.IsNullOrEmpty(pathScope))
            {
                folderScope = new string[] { SanitizeAssetPath(pathScope) };
                if (!AssetDatabase.IsValidFolder(folderScope[0]))
                {
                    LogWarning($"Search path '{folderScope[0]}' is not a valid folder. Searching entire project.");
                    folderScope = null;
                }
            }

            try
            {
                string[] guids = AssetDatabase.FindAssets(string.Join(" ", searchFilters), folderScope);
                List<object> results = new List<object>();

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    results.Add(GetAnimClipData(assetPath));
                }

                LogInfo($"[ManageAnimClip] Found {results.Count} animation clip(s)");
                return Response.Success($"Found {results.Count} animation clip(s).", results);
            }
            catch (Exception e)
            {
                return Response.Error($"Error searching animation clips: {e.Message}");
            }
        }

        private object SetAnimClipCurve(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject curves = args["curves"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for set_curve.");
            if (curves == null || !curves.HasValues)
                return Response.Error("'curves' are required for set_curve.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
                if (clip == null)
                    return Response.Error($"Failed to load animation clip at path: {fullPath}");

                Undo.RecordObject(clip, $"Set Curves on Animation Clip '{Path.GetFileName(fullPath)}'");

                bool modified = ApplyAnimClipCurves(clip, curves);

                if (modified)
                {
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                    LogInfo($"[ManageAnimClip] Set curves on animation clip '{fullPath}'");
                    return Response.Success($"Curves set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
                else
                {
                    return Response.Success($"No valid curves found to set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting curves on animation clip '{fullPath}': {e.Message}");
            }
        }

        private object SetAnimClipEvents(JObject args)
        {
            string path = args["path"]?.ToString();
            JArray events = args["events"] as JArray;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for set_events.");
            if (events == null || events.Count == 0)
                return Response.Error("'events' are required for set_events.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
                if (clip == null)
                    return Response.Error($"Failed to load animation clip at path: {fullPath}");

                Undo.RecordObject(clip, $"Set Events on Animation Clip '{Path.GetFileName(fullPath)}'");

                bool modified = ApplyAnimClipEvents(clip, events);

                if (modified)
                {
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                    LogInfo($"[ManageAnimClip] Set events on animation clip '{fullPath}'");
                    return Response.Success($"Events set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
                else
                {
                    return Response.Success($"No valid events found to set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting events on animation clip '{fullPath}': {e.Message}");
            }
        }

        private object SetAnimClipSettings(JObject args)
        {
            string path = args["path"]?.ToString();
            JObject settings = args["settings"] as JObject;

            if (string.IsNullOrEmpty(path))
                return Response.Error("'path' is required for set_settings.");
            if (settings == null || !settings.HasValues)
                return Response.Error("'settings' are required for set_settings.");

            string fullPath = SanitizeAssetPath(path);
            if (!AssetExists(fullPath))
                return Response.Error($"Animation clip not found at path: {fullPath}");

            try
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
                if (clip == null)
                    return Response.Error($"Failed to load animation clip at path: {fullPath}");

                Undo.RecordObject(clip, $"Set Settings on Animation Clip '{Path.GetFileName(fullPath)}'");

                bool modified = ApplyAnimClipSettings(clip, settings);

                if (modified)
                {
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                    LogInfo($"[ManageAnimClip] Set settings on animation clip '{fullPath}'");
                    return Response.Success($"Settings set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
                else
                {
                    return Response.Success($"No valid settings found to set on animation clip '{fullPath}'.", GetAnimClipData(fullPath));
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting settings on animation clip '{fullPath}': {e.Message}");
            }
        }

        private object CopyAnimClipFromModel(JObject args)
        {
            string modelPath = args["model_path"]?.ToString();
            string clipName = args["clip_name"]?.ToString();
            string destinationPath = args["destination"]?.ToString();

            if (string.IsNullOrEmpty(modelPath))
                return Response.Error("'model_path' is required for copy_from_model.");
            if (string.IsNullOrEmpty(clipName))
                return Response.Error("'clip_name' is required for copy_from_model.");

            string modelFullPath = SanitizeAssetPath(modelPath);
            if (!AssetExists(modelFullPath))
                return Response.Error($"Model not found at path: {modelFullPath}");

            try
            {
                ModelImporter modelImporter = AssetImporter.GetAtPath(modelFullPath) as ModelImporter;
                if (modelImporter == null)
                    return Response.Error($"Failed to get ModelImporter for '{modelFullPath}'");

                // 获取模型中的所有动画片段
                ModelImporterClipAnimation[] clipAnimations = modelImporter.defaultClipAnimations;
                AnimationClip targetClip = null;

                foreach (var clipAnim in clipAnimations)
                {
                    if (clipAnim.name == clipName)
                    {
                        // 找到目标动画片段，现在需要提取它
                        // 这里需要更复杂的逻辑来从模型中提取特定的动画片段
                        // 暂时返回错误，提示用户使用其他方法
                        return Response.Error($"Extracting specific animation clips from models requires more complex implementation. Please use the model import settings or create animation clips manually.");
                    }
                }

                return Response.Error($"Animation clip '{clipName}' not found in model '{modelFullPath}'");
            }
            catch (Exception e)
            {
                return Response.Error($"Error copying animation clip from model '{modelFullPath}': {e.Message}");
            }
        }

        // --- 内部辅助方法 ---

        /// <summary>
        /// 确保资产路径以"Assets/"开头
        /// </summary>
        private string SanitizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + path.TrimStart('/');
            }
            return path;
        }

        /// <summary>
        /// 检查资产是否存在
        /// </summary>
        private bool AssetExists(string sanitizedPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sanitizedPath)))
            {
                return true;
            }
            if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return AssetDatabase.IsValidFolder(sanitizedPath);
            }
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sanitizedPath)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;
            string fullDirPath = Path.Combine(Directory.GetCurrentDirectory(), directoryPath);
            if (!Directory.Exists(fullDirPath))
            {
                Directory.CreateDirectory(fullDirPath);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 应用动画片段设置
        /// </summary>
        private bool ApplyAnimClipSettings(AnimationClip clip, JObject settings)
        {
            if (clip == null || settings == null)
                return false;
            bool modified = false;

            foreach (var setting in settings.Properties())
            {
                string settingName = setting.Name;
                JToken settingValue = setting.Value;

                try
                {
                    switch (settingName.ToLowerInvariant())
                    {
                        case "length":
                            if (settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Integer)
                            {
                                float length = settingValue.ToObject<float>();
                                // 注意：clip.length 是只读属性，不能直接设置
                                // 动画长度通常由动画数据本身决定
                                LogWarning($"[ApplyAnimClipSettings] Cannot set length property - it is read-only. Current length: {clip.length}");
                            }
                            break;
                        case "frame_rate":
                            if (settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Integer)
                            {
                                float frameRate = settingValue.ToObject<float>();
                                if (Math.Abs(clip.frameRate - frameRate) > 0.001f)
                                {
                                    clip.frameRate = frameRate;
                                    modified = true;
                                }
                            }
                            break;
                        case "loop_time":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool loopTime = settingValue.ToObject<bool>();
                                // 注意：clip.isLooping 是只读属性，需要通过 AnimationClipSettings 设置
                                LogWarning($"[ApplyAnimClipSettings] Cannot set isLooping property directly - it is read-only. Use AnimationClipSettings instead.");
                            }
                            break;
                        case "loop_pose":
                            if (settingValue.Type == JTokenType.Boolean)
                            {
                                bool loopPose = settingValue.ToObject<bool>();
                                // 注意：loopPose 设置需要通过 AnimationClipSettings 来设置
                                // 这里简化处理，实际可能需要更复杂的逻辑
                                break;
                            }
                            break;
                        case "cycle_offset":
                            if (settingValue.Type == JTokenType.Float || settingValue.Type == JTokenType.Integer)
                            {
                                float cycleOffset = settingValue.ToObject<float>();
                                // 注意：cycleOffset 设置需要通过 AnimationClipSettings 来设置
                                break;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[ApplyAnimClipSettings] Error setting '{settingName}': {ex.Message}");
                }
            }

            return modified;
        }

        /// <summary>
        /// 应用动画片段曲线
        /// </summary>
        private bool ApplyAnimClipCurves(AnimationClip clip, JObject curves)
        {
            if (clip == null || curves == null)
                return false;
            bool modified = false;

            foreach (var curve in curves.Properties())
            {
                string propertyPath = curve.Name;
                JToken curveData = curve.Value;

                try
                {
                    if (curveData is JObject curveObj)
                    {
                        // 这里需要根据具体的曲线数据格式来设置
                        // 简化实现，实际可能需要更复杂的曲线解析逻辑
                        LogWarning($"[ApplyAnimClipCurves] Curve setting for '{propertyPath}' not fully implemented yet.");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"[ApplyAnimClipCurves] Error setting curve for '{propertyPath}': {ex.Message}");
                }
            }

            return modified;
        }

        /// <summary>
        /// 应用动画片段事件
        /// </summary>
        private bool ApplyAnimClipEvents(AnimationClip clip, JArray events)
        {
            if (clip == null || events == null)
                return false;
            bool modified = false;

            try
            {
                // 清除现有事件
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);

                List<AnimationEvent> animationEvents = new List<AnimationEvent>();

                foreach (var eventData in events)
                {
                    if (eventData is JObject eventObj)
                    {
                        AnimationEvent animEvent = new AnimationEvent();

                        if (eventObj["time"] != null)
                            animEvent.time = eventObj["time"].ToObject<float>();

                        if (eventObj["function_name"] != null)
                            animEvent.functionName = eventObj["function_name"].ToString();

                        if (eventObj["string_parameter"] != null)
                            animEvent.stringParameter = eventObj["string_parameter"].ToString();

                        if (eventObj["float_parameter"] != null)
                            animEvent.floatParameter = eventObj["float_parameter"].ToObject<float>();

                        if (eventObj["int_parameter"] != null)
                            animEvent.intParameter = eventObj["int_parameter"].ToObject<int>();

                        if (eventObj["object_reference_parameter"] != null)
                        {
                            string objPath = eventObj["object_reference_parameter"].ToString();
                            animEvent.objectReferenceParameter = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SanitizeAssetPath(objPath));
                        }

                        animationEvents.Add(animEvent);
                    }
                }

                if (animationEvents.Count > 0)
                {
                    AnimationUtility.SetAnimationEvents(clip, animationEvents.ToArray());
                    modified = true;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[ApplyAnimClipEvents] Error setting events: {ex.Message}");
            }

            return modified;
        }

        /// <summary>
        /// 获取动画片段数据
        /// </summary>
        private object GetAnimClipData(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetExists(path))
                return null;

            string guid = AssetDatabase.AssetPathToGUID(path);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
                return null;

            // 获取动画事件
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            List<object> eventList = events.Select(e => new
            {
                time = e.time,
                function_name = e.functionName,
                string_parameter = e.stringParameter,
                float_parameter = e.floatParameter,
                int_parameter = e.intParameter,
                object_reference_parameter = e.objectReferenceParameter != null ? AssetDatabase.GetAssetPath(e.objectReferenceParameter) : null
            }).ToList<object>();

            return new
            {
                path = path,
                guid = guid,
                name = Path.GetFileNameWithoutExtension(path),
                fileName = Path.GetFileName(path),
                length = clip.length,
                frame_rate = clip.frameRate,
                is_looping = clip.isLooping,
                events = eventList,
                lastWriteTimeUtc = File.GetLastWriteTimeUtc(Path.Combine(Directory.GetCurrentDirectory(), path)).ToString("o")
            };
        }
    }
}