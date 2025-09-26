using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Models; // For Response class

namespace UnityMcp.Tools
{
    /// <summary>
    /// Handles RectTransform modification operations using dual state tree architecture.
    /// First tree: Target location (using GameObjectSelector)
    /// Second tree: Layout operations based on action type
    /// 
    /// 使用方式：
    /// - do_layout: 执行综合布局修改 (可同时设置多个属性)
    /// - get_layout: 获取RectTransform属性 (获取所有属性信息)
    /// 
    /// 特殊参数：
    /// - anchor_self: 当为true时，锚点预设将基于元素当前位置而不是父容器的预设位置
    ///   * stretch_all + anchor_self = tattoo功能（等同于UGUIUtil.AnchorsToCorners）
    ///   * top_center + anchor_self = 将锚点设置到元素自己的顶部中心位置
    ///   * 其他预设 + anchor_self = 将锚点设置到元素自身对应的位置
    /// 
    /// 例如：
    /// action="do_layout", anchor_min=[0,1], anchor_max=[0,1], anchored_pos=[100, -50], size_delta=[200, 100]
    /// action="do_layout", anchor_preset="stretch_all", anchor_self=true  // tattoo效果
    /// action="do_layout", anchor_preset="top_center", anchor_self=true   // 钉在元素顶部中心
    /// action="get_layout"
    /// 
    /// 对应方法名: edit_recttransform
    /// </summary>
    [ToolName("ugui_layout", "UI管理")]
    public class UGUILayout : DualStateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                // 目标查找参数
                new MethodKey("instance_id", "Object InstanceID", true),
                new MethodKey("path", "Object Hierarchy path", false),
                
                // 操作参数
                new MethodKey("action", "Operation type: do_layout(综合布局), get_layout(获取属性)", true),
                
                // RectTransform基本属性
                new MethodKey("anchored_pos", "Anchor position [x, y]", true),
                new MethodKey("size_delta", "Size delta [width, height]", true),
                new MethodKey("anchor_min", "Minimum anchor [x, y]", true),
                new MethodKey("anchor_max", "Maximum anchor [x, y]", true),
                      // 预设锚点类型
                new MethodKey("anchor_preset", "Anchor preset: top_left, top_center, top_right, middle_left, middle_center, middle_right, bottom_left, bottom_center, bottom_right, stretch_horizontal, stretch_vertical, stretch_all", true),
                new MethodKey("anchor_self", "When true, anchor preset will be based on element's current position rather than parent's preset position (default: false)", true),
                new MethodKey("preserve_visual_position", "Whether to preserve visual position when changing anchor preset (default: true)", true),
                new MethodKey("pivot", "Pivot point [x, y]", true),
                
                // Transform继承属性
                new MethodKey("local_position", "Local position [x, y, z]", true),
                new MethodKey("local_rotation", "Local rotation [x, y, z]", true),
                new MethodKey("local_scale", "Local scale [x, y, z]", true),
                
                // 层级控制
                new MethodKey("sibling_index", "Sibling index in parent hierarchy", true),
            };
        }

        /// <summary>
        /// 创建目标定位状态树（使用GameObjectSelector）
        /// </summary>
        protected override StateTree CreateTargetTree()
        {
            return new ObjectSelector<GameObject>().BuildStateTree();
        }

        /// <summary>
        /// 创建操作执行状态树
        /// </summary>
        protected override StateTree CreateActionTree()
        {
            return StateTreeBuilder
                .Create()
                .Key("action")
                    .Leaf("do_layout", (Func<StateTreeContext, object>)HandleDoLayoutAction)
                    .Leaf("get_layout", (Func<StateTreeContext, object>)HandleGetLayoutAction)
                    .DefaultLeaf((Func<StateTreeContext, object>)HandleDefaultAction)
                .Build();
        }

        /// <summary>
        /// 处理布局操作（执行RectTransform修改）
        /// </summary>
        private object HandleDoLayoutAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }

            // 否则根据传入的各种属性参数执行 RectTransform 修改
            if (targets.Length == 1)
            {
                return ApplyRectTransformModifications(targets[0], args);
            }
            else
            {
                return ApplyRectTransformModificationsToMultiple(targets, args);
            }
        }

        /// <summary>
        /// 处理获取布局信息操作
        /// </summary>
        private object HandleGetLayoutAction(StateTreeContext args)
        {
            GameObject[] targets = GetTargetsBasedOnSelectMany(args);
            if (targets.Length == 0)
            {
                return Response.Error("No target GameObjects found in execution context.");
            }
            // 否则获取所有 RectTransform 属性信息
            if (targets.Length == 1)
            {
                return GetAllRectTransformProperties(targets[0]);
            }
            else
            {
                return GetAllRectTransformPropertiesFromMultiple(targets);
            }
        }



        /// <summary>
        /// 默认操作处理（不指定 action 时默认为 do_layout）
        /// </summary>
        private object HandleDefaultAction(StateTreeContext args)
        {
            LogInfo("[UGUILayout] No action specified, using default do_layout action");
            return HandleDoLayoutAction(args);
        }



        #region 核心修改方法

        /// <summary>
        /// 应用RectTransform修改到单个GameObject
        /// </summary>
        private object ApplyRectTransformModifications(GameObject targetGo, StateTreeContext args)
        {
            RectTransform rectTransform = targetGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return Response.Error($"GameObject '{targetGo.name}' does not have a RectTransform component.");
            }

            Undo.RecordObject(rectTransform, "Modify RectTransform");

            bool modified = false;

            // 处理锚点预设
            modified |= ApplyAnchorPreset(rectTransform, args);

            // 应用RectTransform特有属性
            modified |= ApplyAnchoredPosition(rectTransform, args);
            modified |= ApplySizeDelta(rectTransform, args);
            modified |= ApplyAnchorMin(rectTransform, args);
            modified |= ApplyAnchorMax(rectTransform, args);
            modified |= ApplyPivot(rectTransform, args);

            // 已移除便捷设置参数，只保留核心属性

            // 应用Transform继承属性
            modified |= ApplyLocalPosition(rectTransform, args);
            modified |= ApplyLocalRotation(rectTransform, args);
            modified |= ApplyLocalScale(rectTransform, args);

            // 应用层级控制
            modified |= ApplySetSiblingIndex(rectTransform, args);
            if (!modified)
            {
                return Response.Success(
                    $"No modifications applied to RectTransform on '{targetGo.name}'.",
                    GetRectTransformData(rectTransform)
                );
            }

            EditorUtility.SetDirty(rectTransform);
            return Response.Success(
                $"RectTransform on '{targetGo.name}' modified successfully.",
                GetRectTransformData(rectTransform)
            );
        }

        /// <summary>
        /// 应用RectTransform修改到多个GameObject
        /// </summary>
        private object ApplyRectTransformModificationsToMultiple(GameObject[] targets, StateTreeContext args)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject targetGo in targets)
            {
                if (targetGo == null) continue;

                try
                {
                    var result = ApplyRectTransformModifications(targetGo, args);

                    if (IsSuccessResponse(result, out object data, out string responseMessage))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                        else
                        {
                            var rectTransform = targetGo.GetComponent<RectTransform>();
                            if (rectTransform != null)
                            {
                                results.Add(GetRectTransformData(rectTransform));
                            }
                        }
                    }
                    else
                    {
                        errors.Add($"[{targetGo.name}] {responseMessage ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{targetGo.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("modify RectTransform", successCount, targets.Length, results, errors);
        }

        #endregion

        #region RectTransform属性应用方法

        /// <summary>
        /// 应用锚点预设（保持视觉位置不变）
        /// </summary>
        private bool ApplyAnchorPreset(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("anchor_preset", out object presetObj) && presetObj != null)
            {
                string preset = presetObj.ToString().ToLower();
                Vector2 targetAnchorMin, targetAnchorMax, targetPivot;

                switch (preset)
                {
                    case "top_left":
                        targetAnchorMin = new Vector2(0, 1);
                        targetAnchorMax = new Vector2(0, 1);
                        targetPivot = new Vector2(0, 1);
                        break;
                    case "top_center":
                        targetAnchorMin = new Vector2(0.5f, 1);
                        targetAnchorMax = new Vector2(0.5f, 1);
                        targetPivot = new Vector2(0.5f, 1);
                        break;
                    case "top_right":
                        targetAnchorMin = new Vector2(1, 1);
                        targetAnchorMax = new Vector2(1, 1);
                        targetPivot = new Vector2(1, 1);
                        break;
                    case "middle_left":
                        targetAnchorMin = new Vector2(0, 0.5f);
                        targetAnchorMax = new Vector2(0, 0.5f);
                        targetPivot = new Vector2(0, 0.5f);
                        break;
                    case "middle_center":
                        targetAnchorMin = new Vector2(0.5f, 0.5f);
                        targetAnchorMax = new Vector2(0.5f, 0.5f);
                        targetPivot = new Vector2(0.5f, 0.5f);
                        break;
                    case "middle_right":
                        targetAnchorMin = new Vector2(1, 0.5f);
                        targetAnchorMax = new Vector2(1, 0.5f);
                        targetPivot = new Vector2(1, 0.5f);
                        break;
                    case "bottom_left":
                        targetAnchorMin = new Vector2(0, 0);
                        targetAnchorMax = new Vector2(0, 0);
                        targetPivot = new Vector2(0, 0);
                        break;
                    case "bottom_center":
                        targetAnchorMin = new Vector2(0.5f, 0);
                        targetAnchorMax = new Vector2(0.5f, 0);
                        targetPivot = new Vector2(0.5f, 0);
                        break;
                    case "bottom_right":
                        targetAnchorMin = new Vector2(1, 0);
                        targetAnchorMax = new Vector2(1, 0);
                        targetPivot = new Vector2(1, 0);
                        break;
                    case "stretch_horizontal":
                        targetAnchorMin = new Vector2(0, 0.5f);
                        targetAnchorMax = new Vector2(1, 0.5f);
                        targetPivot = new Vector2(0.5f, 0.5f);
                        break;
                    case "stretch_vertical":
                        targetAnchorMin = new Vector2(0.5f, 0);
                        targetAnchorMax = new Vector2(0.5f, 1);
                        targetPivot = new Vector2(0.5f, 0.5f);
                        break;
                    case "stretch_all":
                        targetAnchorMin = new Vector2(0, 0);
                        targetAnchorMax = new Vector2(1, 1);
                        targetPivot = new Vector2(0.5f, 0.5f);
                        break;
                    default:
                        return false;
                }

                // 检查是否使用anchor_self模式
                bool anchorSelf = false;
                if (args.TryGetValue("anchor_self", out object anchorSelfObj))
                {
                    if (anchorSelfObj is bool anchorSelfBool)
                        anchorSelf = anchorSelfBool;
                    else if (bool.TryParse(anchorSelfObj?.ToString(), out bool parsedAnchorSelf))
                        anchorSelf = parsedAnchorSelf;
                }

                // 如果使用anchor_self模式，基于元素当前位置重新计算锚点
                if (anchorSelf)
                {
                    return ApplyAnchorSelfPreset(rectTransform, preset, args);
                }

                // 检查是否需要修改
                if (rectTransform.anchorMin == targetAnchorMin &&
                    rectTransform.anchorMax == targetAnchorMax &&
                    rectTransform.pivot == targetPivot)
                {
                    return false; // 已经是目标锚点，无需修改
                }

                // 检查是否需要保持视觉位置
                bool preserveVisualPosition = true; // 默认保持视觉位置
                if (args.TryGetValue("preserve_visual_position", out object preserveObj))
                {
                    if (preserveObj is bool preserveBool)
                        preserveVisualPosition = preserveBool;
                    else if (bool.TryParse(preserveObj?.ToString(), out bool parsedPreserve))
                        preserveVisualPosition = parsedPreserve;
                }

                // 应用锚点预设
                if (preserveVisualPosition)
                {
                    return ApplyAnchorPresetWithVisualPositionPreserved(rectTransform, targetAnchorMin, targetAnchorMax, targetPivot);
                }
                else
                {
                    // 直接设置锚点，不保持视觉位置
                    rectTransform.anchorMin = targetAnchorMin;
                    rectTransform.anchorMax = targetAnchorMax;
                    rectTransform.pivot = targetPivot;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用锚点预设并保持视觉位置不变（参考UGUIUtil.AnchorsToCorners实现）
        /// </summary>
        private bool ApplyAnchorPresetWithVisualPositionPreserved(RectTransform rectTransform, Vector2 targetAnchorMin, Vector2 targetAnchorMax, Vector2 targetPivot)
        {
            // 获取父容器的RectTransform
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                // 如果没有父RectTransform，直接设置锚点
                rectTransform.anchorMin = targetAnchorMin;
                rectTransform.anchorMax = targetAnchorMax;
                rectTransform.pivot = targetPivot;
                return true;
            }

            // 保存当前的世界位置和尺寸
            Vector3[] worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);
            Vector2 worldSize = new Vector2(
                Vector3.Distance(worldCorners[0], worldCorners[3]),
                Vector3.Distance(worldCorners[0], worldCorners[1])
            );

            // 计算当前在父容器中的相对位置（参考UGUIUtil.AnchorsToCorners的计算方式）
            Vector2 currentOffsetMin = rectTransform.offsetMin;
            Vector2 currentOffsetMax = rectTransform.offsetMax;
            Vector2 currentAnchorMin = rectTransform.anchorMin;
            Vector2 currentAnchorMax = rectTransform.anchorMax;

            // 计算当前的实际锚点位置（包含offset的影响）
            Vector2 actualAnchorMin = new Vector2(
                currentAnchorMin.x + currentOffsetMin.x / parentRect.rect.width,
                currentAnchorMin.y + currentOffsetMin.y / parentRect.rect.height
            );
            Vector2 actualAnchorMax = new Vector2(
                currentAnchorMax.x + currentOffsetMax.x / parentRect.rect.width,
                currentAnchorMax.y + currentOffsetMax.y / parentRect.rect.height
            );

            // 设置新的锚点和轴心点
            rectTransform.anchorMin = targetAnchorMin;
            rectTransform.anchorMax = targetAnchorMax;
            rectTransform.pivot = targetPivot;

            // 计算新锚点下需要的offset来保持相同的视觉位置
            Vector2 newOffsetMin = new Vector2(
                (actualAnchorMin.x - targetAnchorMin.x) * parentRect.rect.width,
                (actualAnchorMin.y - targetAnchorMin.y) * parentRect.rect.height
            );
            Vector2 newOffsetMax = new Vector2(
                (actualAnchorMax.x - targetAnchorMax.x) * parentRect.rect.width,
                (actualAnchorMax.y - targetAnchorMax.y) * parentRect.rect.height
            );

            // 应用新的offset
            rectTransform.offsetMin = newOffsetMin;
            rectTransform.offsetMax = newOffsetMax;

            return true;
        }

        /// <summary>
        /// 应用基于自身位置的锚点预设（anchor_self=true时调用）
        /// </summary>
        private bool ApplyAnchorSelfPreset(RectTransform rectTransform, string preset, StateTreeContext args)
        {
            // 获取父容器的RectTransform
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                Debug.LogWarning("[UGUILayout] Anchor self preset requires a parent RectTransform, skipping.");
                return false;
            }

            // 获取元素当前在父容器中的世界位置
            Vector3[] worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);

            Vector3[] parentWorldCorners = new Vector3[4];
            parentRect.GetWorldCorners(parentWorldCorners);

            // 计算元素在父容器中的相对位置（0-1范围）
            Vector3 elementBottomLeft = worldCorners[0];
            Vector3 elementTopRight = worldCorners[2];
            Vector3 elementCenter = (elementBottomLeft + elementTopRight) * 0.5f;

            Vector3 parentBottomLeft = parentWorldCorners[0];
            Vector3 parentTopRight = parentWorldCorners[2];

            Vector2 elementCenterRel = new Vector2(
                (elementCenter.x - parentBottomLeft.x) / (parentTopRight.x - parentBottomLeft.x),
                (elementCenter.y - parentBottomLeft.y) / (parentTopRight.y - parentBottomLeft.y)
            );

            Vector2 elementBottomLeftRel = new Vector2(
                (elementBottomLeft.x - parentBottomLeft.x) / (parentTopRight.x - parentBottomLeft.x),
                (elementBottomLeft.y - parentBottomLeft.y) / (parentTopRight.y - parentBottomLeft.y)
            );

            Vector2 elementTopRightRel = new Vector2(
                (elementTopRight.x - parentBottomLeft.x) / (parentTopRight.x - parentBottomLeft.x),
                (elementTopRight.y - parentBottomLeft.y) / (parentTopRight.y - parentBottomLeft.y)
            );

            // 限制在0-1范围内
            elementCenterRel.x = Mathf.Clamp01(elementCenterRel.x);
            elementCenterRel.y = Mathf.Clamp01(elementCenterRel.y);
            elementBottomLeftRel.x = Mathf.Clamp01(elementBottomLeftRel.x);
            elementBottomLeftRel.y = Mathf.Clamp01(elementBottomLeftRel.y);
            elementTopRightRel.x = Mathf.Clamp01(elementTopRightRel.x);
            elementTopRightRel.y = Mathf.Clamp01(elementTopRightRel.y);

            Vector2 newAnchorMin, newAnchorMax, newPivot;

            // 根据预设类型基于元素自身位置计算锚点
            switch (preset)
            {
                case "top_left":
                    newAnchorMin = new Vector2(elementBottomLeftRel.x, elementTopRightRel.y);
                    newAnchorMax = new Vector2(elementBottomLeftRel.x, elementTopRightRel.y);
                    newPivot = new Vector2(0, 1);
                    break;
                case "top_center":
                    newAnchorMin = new Vector2(elementCenterRel.x, elementTopRightRel.y);
                    newAnchorMax = new Vector2(elementCenterRel.x, elementTopRightRel.y);
                    newPivot = new Vector2(0.5f, 1);
                    break;
                case "top_right":
                    newAnchorMin = new Vector2(elementTopRightRel.x, elementTopRightRel.y);
                    newAnchorMax = new Vector2(elementTopRightRel.x, elementTopRightRel.y);
                    newPivot = new Vector2(1, 1);
                    break;
                case "middle_left":
                    newAnchorMin = new Vector2(elementBottomLeftRel.x, elementCenterRel.y);
                    newAnchorMax = new Vector2(elementBottomLeftRel.x, elementCenterRel.y);
                    newPivot = new Vector2(0, 0.5f);
                    break;
                case "middle_center":
                    newAnchorMin = elementCenterRel;
                    newAnchorMax = elementCenterRel;
                    newPivot = new Vector2(0.5f, 0.5f);
                    break;
                case "middle_right":
                    newAnchorMin = new Vector2(elementTopRightRel.x, elementCenterRel.y);
                    newAnchorMax = new Vector2(elementTopRightRel.x, elementCenterRel.y);
                    newPivot = new Vector2(1, 0.5f);
                    break;
                case "bottom_left":
                    newAnchorMin = elementBottomLeftRel;
                    newAnchorMax = elementBottomLeftRel;
                    newPivot = new Vector2(0, 0);
                    break;
                case "bottom_center":
                    newAnchorMin = new Vector2(elementCenterRel.x, elementBottomLeftRel.y);
                    newAnchorMax = new Vector2(elementCenterRel.x, elementBottomLeftRel.y);
                    newPivot = new Vector2(0.5f, 0);
                    break;
                case "bottom_right":
                    newAnchorMin = new Vector2(elementTopRightRel.x, elementBottomLeftRel.y);
                    newAnchorMax = new Vector2(elementTopRightRel.x, elementBottomLeftRel.y);
                    newPivot = new Vector2(1, 0);
                    break;
                case "stretch_horizontal":
                    newAnchorMin = new Vector2(elementBottomLeftRel.x, elementCenterRel.y);
                    newAnchorMax = new Vector2(elementTopRightRel.x, elementCenterRel.y);
                    newPivot = new Vector2(0.5f, 0.5f);
                    break;
                case "stretch_vertical":
                    newAnchorMin = new Vector2(elementCenterRel.x, elementBottomLeftRel.y);
                    newAnchorMax = new Vector2(elementCenterRel.x, elementTopRightRel.y);
                    newPivot = new Vector2(0.5f, 0.5f);
                    break;
                case "stretch_all":
                    // stretch_all + anchor_self = tattoo功能（AnchorsToCorners）
                    newAnchorMin = new Vector2(
                        rectTransform.anchorMin.x + rectTransform.offsetMin.x / parentRect.rect.width,
                        rectTransform.anchorMin.y + rectTransform.offsetMin.y / parentRect.rect.height
                    );
                    newAnchorMax = new Vector2(
                        rectTransform.anchorMax.x + rectTransform.offsetMax.x / parentRect.rect.width,
                        rectTransform.anchorMax.y + rectTransform.offsetMax.y / parentRect.rect.height
                    );
                    newPivot = new Vector2(0.5f, 0.5f);
                    break;
                default:
                    return false;
            }

            // 检查是否需要修改（避免不必要的更新）
            if (Vector2.Distance(rectTransform.anchorMin, newAnchorMin) < 0.001f &&
                Vector2.Distance(rectTransform.anchorMax, newAnchorMax) < 0.001f &&
                Vector2.Distance(rectTransform.pivot, newPivot) < 0.001f &&
                Vector2.Distance(rectTransform.offsetMin, Vector2.zero) < 0.001f &&
                Vector2.Distance(rectTransform.offsetMax, Vector2.zero) < 0.001f)
            {
                return false; // 已经是目标状态，无需修改
            }

            // 应用新的锚点
            rectTransform.anchorMin = newAnchorMin;
            rectTransform.anchorMax = newAnchorMax;
            rectTransform.pivot = newPivot;

            // 重置偏移量为零（anchor_self的核心特征）
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Debug.Log($"[UGUILayout] Applied anchor_self preset '{preset}' to '{rectTransform.name}': anchors [{newAnchorMin.x:F3},{newAnchorMin.y:F3}] to [{newAnchorMax.x:F3},{newAnchorMax.y:F3}]");
            return true;
        }

        /// <summary>
        /// 应用锚点位置修改
        /// </summary>
        private bool ApplyAnchoredPosition(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("anchored_pos", out object positionObj) || args.TryGetValue("anchored_position", out positionObj))
            {
                Vector2? position = ParseVector2(positionObj);
                if (position.HasValue && rectTransform.anchoredPosition != position.Value)
                {
                    rectTransform.anchoredPosition = position.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用尺寸增量修改
        /// </summary>
        private bool ApplySizeDelta(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("size_delta", out object sizeObj))
            {
                Vector2? size = ParseVector2(sizeObj);
                if (size.HasValue && rectTransform.sizeDelta != size.Value)
                {
                    rectTransform.sizeDelta = size.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用最小锚点修改
        /// </summary>
        private bool ApplyAnchorMin(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("anchor_min", out object anchorObj))
            {
                Vector2? anchor = ParseVector2(anchorObj);
                if (anchor.HasValue && rectTransform.anchorMin != anchor.Value)
                {
                    rectTransform.anchorMin = anchor.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用最大锚点修改
        /// </summary>
        private bool ApplyAnchorMax(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("anchor_max", out object anchorObj))
            {
                Vector2? anchor = ParseVector2(anchorObj);
                if (anchor.HasValue && rectTransform.anchorMax != anchor.Value)
                {
                    rectTransform.anchorMax = anchor.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用轴心点修改
        /// </summary>
        private bool ApplyPivot(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("pivot", out object pivotObj))
            {
                Vector2? pivot = ParseVector2(pivotObj);
                if (pivot.HasValue && rectTransform.pivot != pivot.Value)
                {
                    rectTransform.pivot = pivot.Value;
                    return true;
                }
            }
            return false;
        }







        /// <summary>
        /// 应用本地位置修改
        /// </summary>
        private bool ApplyLocalPosition(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("local_position", out object positionObj))
            {
                Vector3? position = ParseVector3(positionObj);
                if (position.HasValue && rectTransform.localPosition != position.Value)
                {
                    rectTransform.localPosition = position.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用本地旋转修改
        /// </summary>
        private bool ApplyLocalRotation(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("local_rotation", out object rotationObj))
            {
                Vector3? rotation = ParseVector3(rotationObj);
                if (rotation.HasValue && rectTransform.localEulerAngles != rotation.Value)
                {
                    rectTransform.localEulerAngles = rotation.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用本地缩放修改
        /// </summary>
        private bool ApplyLocalScale(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("local_scale", out object scaleObj))
            {
                Vector3? scale = ParseVector3(scaleObj);
                if (scale.HasValue && rectTransform.localScale != scale.Value)
                {
                    rectTransform.localScale = scale.Value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 应用SiblingIndex修改
        /// </summary>
        private bool ApplySetSiblingIndex(RectTransform rectTransform, StateTreeContext args)
        {
            if (args.TryGetValue("sibling_index", out object indexObj))
            {
                if (int.TryParse(indexObj?.ToString(), out int siblingIndex))
                {
                    int currentIndex = rectTransform.GetSiblingIndex();
                    if (currentIndex != siblingIndex)
                    {
                        rectTransform.SetSiblingIndex(siblingIndex);
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion




        #region 属性操作方法

        /// <summary>
        /// 在单个目标上设置属性
        /// </summary>
        private object SetPropertyOnSingleTarget(GameObject targetGo, string propertyName, object valueObj)
        {
            RectTransform rectTransform = targetGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return Response.Error($"GameObject '{targetGo.name}' does not have a RectTransform component.");
            }

            try
            {
                Undo.RecordObject(rectTransform, $"Set RectTransform Property {propertyName}");

                if (valueObj is JToken valueToken)
                {
                    SetPropertyValue(rectTransform, propertyName, valueToken);
                }
                else
                {
                    JToken convertedToken = JToken.FromObject(valueObj);
                    SetPropertyValue(rectTransform, propertyName, convertedToken);
                }

                EditorUtility.SetDirty(rectTransform);

                LogInfo($"[EditRectTransform] Set property '{propertyName}' on {targetGo.name}");

                return Response.Success(
                    $"RectTransform property '{propertyName}' set successfully on {targetGo.name}.",
                    new Dictionary<string, object>
                    {
                        { "target", targetGo.name },
                        { "property", propertyName },
                        { "value", valueObj?.ToString() }
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to set RectTransform property '{propertyName}': {e.Message}");
            }
        }

        /// <summary>
        /// 从单个目标获取属性
        /// </summary>
        private object GetPropertyFromSingleTarget(GameObject targetGo, string propertyName)
        {
            RectTransform rectTransform = targetGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return Response.Error($"GameObject '{targetGo.name}' does not have a RectTransform component.");
            }

            try
            {
                var value = GetPropertyValue(rectTransform, propertyName);
                LogInfo($"[EditRectTransform] Got property '{propertyName}' from {targetGo.name}: {value}");

                return Response.Success(
                    $"RectTransform property '{propertyName}' retrieved successfully from {targetGo.name}.",
                    new Dictionary<string, object>
                    {
                        { "target", targetGo.name },
                        { "property", propertyName },
                        { "value", value }
                    }
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get RectTransform property '{propertyName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取单个目标的所有RectTransform属性
        /// </summary>
        private object GetAllRectTransformProperties(GameObject targetGo)
        {
            RectTransform rectTransform = targetGo.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return Response.Error($"GameObject '{targetGo.name}' does not have a RectTransform component.");
            }

            return Response.Success(
                $"RectTransform properties retrieved successfully from '{targetGo.name}'.",
                GetRectTransformData(rectTransform)
            );
        }

        /// <summary>
        /// 获取多个目标的所有RectTransform属性
        /// </summary>
        private object GetAllRectTransformPropertiesFromMultiple(GameObject[] targets)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                try
                {
                    var result = GetAllRectTransformProperties(target);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                    }
                    else
                    {
                        errors.Add($"[{target.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{target.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse("get all properties", successCount, targets.Length, results, errors);
        }

        #endregion

        #region 批量操作方法

        /// <summary>
        /// 在多个目标上设置属性
        /// </summary>
        private object SetPropertyOnMultipleTargets(GameObject[] targets, string propertyName, object valueObj)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                try
                {
                    var result = SetPropertyOnSingleTarget(target, propertyName, valueObj);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                    }
                    else
                    {
                        errors.Add($"[{target.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{target.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse($"set property '{propertyName}'", successCount, targets.Length, results, errors);
        }

        /// <summary>
        /// 从多个目标获取属性
        /// </summary>
        private object GetPropertyFromMultipleTargets(GameObject[] targets, string propertyName)
        {
            var results = new List<Dictionary<string, object>>();
            var errors = new List<string>();
            int successCount = 0;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                try
                {
                    var result = GetPropertyFromSingleTarget(target, propertyName);

                    if (IsSuccessResponse(result, out object data, out string message))
                    {
                        successCount++;
                        if (data is Dictionary<string, object> dictData)
                        {
                            results.Add(dictData);
                        }
                    }
                    else
                    {
                        errors.Add($"[{target.name}] {message ?? "Unknown error"}");
                    }
                }
                catch (Exception e)
                {
                    errors.Add($"[{target.name}] Error: {e.Message}");
                }
            }

            return CreateBatchOperationResponse($"get property '{propertyName}'", successCount, targets.Length, results, errors);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从执行上下文中提取目标GameObject数组
        /// </summary>
        private GameObject[] ExtractTargetsFromContext(StateTreeContext context)
        {
            // 先尝试从ObjectReferences获取（避免序列化问题）
            if (context.TryGetObjectReference("_resolved_targets", out object targetsObj))
            {
                if (targetsObj is GameObject[] gameObjectArray)
                {
                    return gameObjectArray;
                }
                else if (targetsObj is GameObject singleGameObject)
                {
                    return new GameObject[] { singleGameObject };
                }
                else if (targetsObj is System.Collections.IList list)
                {
                    var gameObjects = new List<GameObject>();
                    foreach (var item in list)
                    {
                        if (item is GameObject go)
                            gameObjects.Add(go);
                    }
                    return gameObjects.ToArray();
                }
            }

            // 如果ObjectReferences中没有，尝试从JsonData获取（向后兼容）
            if (context.TryGetJsonValue("_resolved_targets", out JToken targetToken))
            {
                if (targetToken is JArray targetArray)
                {
                    return targetArray.ToObject<GameObject[]>();
                }
                else
                {
                    // 单个对象的情况
                    GameObject single = targetToken.ToObject<GameObject>();
                    return single != null ? new GameObject[] { single } : new GameObject[0];
                }
            }

            return new GameObject[0];
        }

        /// <summary>
        /// 检查是否应该进行批量操作
        /// </summary>
        private bool ShouldSelectMany(StateTreeContext context)
        {
            if (context.TryGetValue("select_many", out object selectManyObj))
            {
                if (selectManyObj is bool selectMany)
                    return selectMany;
                if (bool.TryParse(selectManyObj?.ToString(), out bool parsedSelectMany))
                    return parsedSelectMany;
            }
            return false; // 默认为false
        }

        /// <summary>
        /// 根据select_many参数获取目标对象（单个或多个）
        /// </summary>
        private GameObject[] GetTargetsBasedOnSelectMany(StateTreeContext context)
        {
            GameObject[] targets = ExtractTargetsFromContext(context);

            if (ShouldSelectMany(context))
            {
                return targets; // 返回所有匹配的对象
            }
            else
            {
                // 只返回第一个对象（如果存在）
                return targets.Length > 0 ? new GameObject[] { targets[0] } : new GameObject[0];
            }
        }

        /// <summary>
        /// 解析Vector2
        /// </summary>
        private Vector2? ParseVector2(object obj)
        {
            if (obj == null) return null;

            if (obj is JArray jArray && jArray.Count >= 2)
            {
                try
                {
                    return new Vector2(
                        jArray[0].ToObject<float>(),
                        jArray[1].ToObject<float>()
                    );
                }
                catch
                {
                    return null;
                }
            }

            if (obj is Vector2 vector2)
            {
                return vector2;
            }

            return null;
        }

        /// <summary>
        /// 解析Vector3
        /// </summary>
        private Vector3? ParseVector3(object obj)
        {
            if (obj == null) return null;

            if (obj is JArray jArray && jArray.Count >= 3)
            {
                try
                {
                    return new Vector3(
                        jArray[0].ToObject<float>(),
                        jArray[1].ToObject<float>(),
                        jArray[2].ToObject<float>()
                    );
                }
                catch
                {
                    return null;
                }
            }

            if (obj is Vector3 vector3)
            {
                return vector3;
            }

            return null;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        private object GetPropertyValue(object target, string propertyName)
        {
            Type type = target.GetType();
            PropertyInfo propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propInfo != null && propInfo.CanRead)
            {
                return propInfo.GetValue(target);
            }

            FieldInfo fieldInfo = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(target);
            }

            throw new ArgumentException($"Property or field '{propertyName}' not found on type '{type.Name}'");
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        private void SetPropertyValue(object target, string propertyName, JToken value)
        {
            Type type = target.GetType();
            PropertyInfo propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propInfo != null && propInfo.CanWrite)
            {
                object convertedValue = ConvertValue(value, propInfo.PropertyType);
                propInfo.SetValue(target, convertedValue);
                return;
            }

            FieldInfo fieldInfo = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                object convertedValue = ConvertValue(value, fieldInfo.FieldType);
                fieldInfo.SetValue(target, convertedValue);
                return;
            }

            throw new ArgumentException($"Property or field '{propertyName}' not found or is read-only on type '{type.Name}'");
        }

        /// <summary>
        /// 转换JToken值到指定类型
        /// </summary>
        private object ConvertValue(JToken token, Type targetType)
        {
            if (targetType == typeof(string))
                return token.ToObject<string>();
            if (targetType == typeof(int))
                return token.ToObject<int>();
            if (targetType == typeof(float))
                return token.ToObject<float>();
            if (targetType == typeof(bool))
                return token.ToObject<bool>();
            if (targetType == typeof(Vector2) && token is JArray arr2 && arr2.Count == 2)
                return new Vector2(arr2[0].ToObject<float>(), arr2[1].ToObject<float>());
            if (targetType == typeof(Vector3) && token is JArray arr3 && arr3.Count == 3)
                return new Vector3(arr3[0].ToObject<float>(), arr3[1].ToObject<float>(), arr3[2].ToObject<float>());

            // 尝试直接转换
            return token.ToObject(targetType);
        }

        /// <summary>
        /// 检查Response对象是否表示成功
        /// </summary>
        private bool IsSuccessResponse(object response, out object data, out string message)
        {
            data = null;
            message = null;

            var resultType = response.GetType();
            var successProperty = resultType.GetProperty("success");
            var dataProperty = resultType.GetProperty("data");
            var messageProperty = resultType.GetProperty("message");
            var errorProperty = resultType.GetProperty("error");

            bool isSuccess = successProperty != null && (bool)successProperty.GetValue(response);
            data = dataProperty?.GetValue(response);
            message = isSuccess ?
                messageProperty?.GetValue(response)?.ToString() :
                (errorProperty?.GetValue(response)?.ToString() ?? messageProperty?.GetValue(response)?.ToString());

            return isSuccess;
        }

        /// <summary>
        /// 创建批量操作响应
        /// </summary>
        private object CreateBatchOperationResponse(string operation, int successCount, int totalCount,
            List<Dictionary<string, object>> results, List<string> errors)
        {
            string message;
            if (successCount == totalCount)
            {
                message = $"Successfully completed {operation} on {successCount} RectTransform(s).";
            }
            else if (successCount > 0)
            {
                message = $"Completed {operation} on {successCount} of {totalCount} RectTransform(s). {errors.Count} failed.";
            }
            else
            {
                message = $"Failed to complete {operation} on any of the {totalCount} RectTransform(s).";
            }

            var responseData = new Dictionary<string, object>
            {
                { "operation", operation },
                { "success_count", successCount },
                { "total_count", totalCount },
                { "success_rate", (double)successCount / totalCount },
                { "affected_objects", results }
            };

            if (errors.Count > 0)
            {
                responseData["errors"] = errors;
            }

            if (successCount > 0)
            {
                return Response.Success(message, responseData);
            }
            else
            {
                return Response.Error(message, responseData);
            }
        }

        /// <summary>
        /// 获取RectTransform的数据表示
        /// </summary>
        private Dictionary<string, object> GetRectTransformData(RectTransform rectTransform)
        {
            if (rectTransform == null) return null;

            var data = new Dictionary<string, object>
            {
                { "name", rectTransform.name },
                { "instanceID", rectTransform.GetInstanceID() },
                { "anchoredPosition", new { x = rectTransform.anchoredPosition.x, y = rectTransform.anchoredPosition.y } },
                { "sizeDelta", new { x = rectTransform.sizeDelta.x, y = rectTransform.sizeDelta.y } },
                { "anchorMin", new { x = rectTransform.anchorMin.x, y = rectTransform.anchorMin.y } },
                { "anchorMax", new { x = rectTransform.anchorMax.x, y = rectTransform.anchorMax.y } },
                { "pivot", new { x = rectTransform.pivot.x, y = rectTransform.pivot.y } },
                { "offsetMin", new { x = rectTransform.offsetMin.x, y = rectTransform.offsetMin.y } },
                { "offsetMax", new { x = rectTransform.offsetMax.x, y = rectTransform.offsetMax.y } },
                { "localPosition", new { x = rectTransform.localPosition.x, y = rectTransform.localPosition.y, z = rectTransform.localPosition.z } },
                { "localRotation", new { x = rectTransform.localRotation.x, y = rectTransform.localRotation.y, z = rectTransform.localRotation.z, w = rectTransform.localRotation.w } },
                { "localScale", new { x = rectTransform.localScale.x, y = rectTransform.localScale.y, z = rectTransform.localScale.z } },
                { "rect", new { x = rectTransform.rect.x, y = rectTransform.rect.y, width = rectTransform.rect.width, height = rectTransform.rect.height } }
            };

            return data;
        }

        #endregion
    }
}

