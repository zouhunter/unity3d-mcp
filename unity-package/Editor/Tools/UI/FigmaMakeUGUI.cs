using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Tools;
using UnityMcp.Models;
using UnityMCP.Model;
using Newtonsoft.Json.Linq;
using System.IO;

namespace UnityMCP.Tools
{
    /// <summary>
    /// Figma UI制作衔接工具，负责管理UI制作方案和修改记录
    /// 对应方法名: figma_make_ugui
    /// </summary>
    [ToolName("figma_make_ugui")]
    public class FigmaMakeUGUI : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "操作类型：create_rule(创建制作方案), get_rule(获取制作方案), add_modify(添加修改记录), get_modifys(获取修改记录), clear_modify(清除修改记录)", false),
                new MethodKey("name", "UI名称，用于查找和记录", false),
                new MethodKey("modify_desc", "修改描述", true),
                new MethodKey("save_path", "保存路径，用于创建新的FigmaUGUIRuleObject", true),
                new MethodKey("properties", "属性数据，JSON格式的字符串", true)
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
                    .Leaf("create_rule", CreateUIRule)
                    .Leaf("get_rule", GetUIRule)
                    .Leaf("add_modify", AddModifyRecord)
                    .Leaf("get_modifys", GetModifyRecords)
                    .Leaf("clear_modify", ClearModifyRecords)
                .Build();
        }

        /// <summary>
        /// 创建UI制作规则
        /// </summary>
        private object CreateUIRule(JObject args)
        {
            string uiName = args["name"]?.ToString();
            string savePath = args["save_path"]?.ToString();
            string propertiesJson = args["properties"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for create_rule.");

            if (string.IsNullOrEmpty(savePath))
            {
                // 如果没有提供保存路径，使用默认路径
                savePath = "Assets/ScriptableObjects";
            }

            try
            {
                // 确保保存目录存在
                if (!System.IO.Directory.Exists(savePath))
                {
                    System.IO.Directory.CreateDirectory(savePath);
                    AssetDatabase.Refresh();
                }

                // 检查是否已经存在同名的资产
                string assetPath = Path.Combine(savePath, $"{uiName}_Rule.asset");
                if (File.Exists(assetPath))
                {
                    return Response.Error($"FigmaUGUIRuleObject already exists at path: {assetPath}");
                }

                // 创建新的 FigmaUGUIRuleObject 实例
                FigmaUGUIRuleObject newRule = ScriptableObject.CreateInstance<FigmaUGUIRuleObject>();

                // 设置基本属性
                newRule.name = uiName;
                newRule.modify_records = new List<string>();

                // 如果提供了properties，尝试解析JSON数据
                if (!string.IsNullOrEmpty(propertiesJson))
                {
                    try
                    {
                        JObject properties = JObject.Parse(propertiesJson);

                        // 设置各种属性
                        if (properties["link_url"] != null)
                            newRule.link_url = properties["link_url"].ToString();

                        if (properties["picture_url"] != null)
                            newRule.picture_url = properties["picture_url"].ToString();

                        if (properties["picture_level"] != null)
                            newRule.picture_level = properties["picture_level"].ToObject<int>();

                        if (properties["extra_modify_desc"] != null)
                            newRule.extra_description = properties["extra_modify_desc"].ToString();

                        if (properties["build_steps"] != null && properties["build_steps"].Type == JTokenType.Array)
                        {
                            newRule.build_steps = properties["build_steps"].ToObject<List<string>>();
                        }

                        if (properties["preferred_components"] != null && properties["preferred_components"].Type == JTokenType.Array)
                        {
                            var components = new List<ComponentInfo>();
                            foreach (var comp in properties["preferred_components"])
                            {
                                var componentInfo = new ComponentInfo();
                                if (comp["component_name"] != null)
                                    componentInfo.component_name = comp["component_name"].ToString();
                                if (comp["component_menu_path"] != null)
                                    componentInfo.component_menu_path = comp["component_menu_path"].ToString();
                                components.Add(componentInfo);
                            }
                            newRule.preferred_components = components;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        LogWarning($"[FigmaMakeUGUI] Failed to parse properties JSON: {jsonEx.Message}");
                    }
                }

                // 创建资产文件
                AssetDatabase.CreateAsset(newRule, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                LogInfo($"[FigmaMakeUGUI] Created new FigmaUGUIRuleObject for UI '{uiName}' at path: {assetPath}");

                return Response.Success($"Successfully created FigmaUGUIRuleObject for UI '{uiName}'.",
                    new
                    {
                        uiName = uiName,
                        assetPath = assetPath,
                        rule = JObject.FromObject(BuildUIRule(newRule))
                    });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Response.Error($"Failed to create UI rule for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取UI制作规则和方案
        /// </summary>
        private object GetUIRule(JObject args)
        {
            string uiName = args["name"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for get_rule.");

            try
            {
                // 搜索相关的 FigmaUGUIObject
                FigmaUGUIRuleObject figmaObj = FindFigmaUGUIObject(uiName);

                if (figmaObj == null)
                {
                    return Response.Success($"No UI rule found for '{uiName}'. Consider creating a FigmaUGUIObject asset.",
                        new
                        {
                            uiName = uiName,
                            foundObject = false,
                            suggestion = "Create a FigmaUGUIObject asset to define UI creation rules"
                        });
                }

                // 构建制作方案
                var rule = BuildUIRule(figmaObj);

                return Response.Success($"Found UI rule for '{uiName}'.",
                    new
                    {
                        uiName = uiName,
                        foundObject = true,
                        rule = rule
                    });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Response.Error($"Failed to get UI rule for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 添加UI修改记录
        /// </summary>
        private object AddModifyRecord(JObject args)
        {
            string uiName = args["name"]?.ToString();
            string modify_desc = args["modify_desc"]?.ToString() ?? "UI modification";

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for add_modify.");

            try
            {
                // 查找对应的 FigmaUGUIObject
                FigmaUGUIRuleObject figmaObj = FindFigmaUGUIObject(uiName);

                if (figmaObj == null)
                {
                    return Response.Error($"No FigmaUGUIObject found for UI '{uiName}'. Please create one first.");
                }

                // 确保 modify_records 列表已初始化
                if (figmaObj.modify_records == null)
                {
                    figmaObj.modify_records = new List<string>();
                }

                // 创建时间戳记录
                string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string recordEntry = $"[{timestamp}] {modify_desc}";

                // 添加记录
                figmaObj.modify_records.Add(recordEntry);

                // 标记资产为脏数据并保存
                EditorUtility.SetDirty(figmaObj);
                string assetPath = AssetDatabase.GetAssetPath(figmaObj);
                AssetDatabase.SaveAssets();

                LogInfo($"[FigmaMakeUGUI] Added modify record for UI '{uiName}': {modify_desc}");

                return Response.Success($"Modify record added to FigmaUGUIObject for UI '{uiName}'.",
                    new
                    {
                        uiName = uiName,
                        modify_desc = modify_desc,
                        assetPath = assetPath,
                        timestamp = timestamp
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add modify record for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取UI修改记录
        /// </summary>
        private object GetModifyRecords(JObject args)
        {
            string uiName = args["name"]?.ToString();

            try
            {
                if (string.IsNullOrEmpty(uiName))
                {
                    // 返回所有UI的修改记录
                    var allRecords = new Dictionary<string, object>();
                    int totalCount = 0;

                    // 查找所有 FigmaUGUIObject
                    string[] guids = AssetDatabase.FindAssets($"t:FigmaUGUIObject");

                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        FigmaUGUIRuleObject figmaObj = AssetDatabase.LoadAssetAtPath<FigmaUGUIRuleObject>(assetPath);

                        if (figmaObj != null && !string.IsNullOrEmpty(figmaObj.name))
                        {
                            var records = figmaObj.modify_records ?? new List<string>();
                            allRecords[figmaObj.name] = new
                            {
                                assetPath = assetPath,
                                recordCount = records.Count,
                                records = records.ToArray()
                            };
                            totalCount += records.Count;
                        }
                    }

                    return Response.Success($"Retrieved modify records for all UIs ({totalCount} total records).",
                        new
                        {
                            totalUIs = allRecords.Count,
                            totalRecords = totalCount,
                            records = allRecords
                        });
                }
                else
                {
                    // 返回指定UI的修改记录
                    FigmaUGUIRuleObject figmaObj = FindFigmaUGUIObject(uiName);

                    if (figmaObj == null)
                    {
                        return Response.Success($"No FigmaUGUIObject found for UI '{uiName}'.",
                            new
                            {
                                uiName = uiName,
                                recordCount = 0,
                                records = new string[0]
                            });
                    }

                    var records = figmaObj.modify_records ?? new List<string>();

                    return Response.Success($"Retrieved {records.Count} modify record(s) for UI '{uiName}'.",
                        new
                        {
                            uiName = uiName,
                            recordCount = records.Count,
                            assetPath = AssetDatabase.GetAssetPath(figmaObj),
                            records = records.ToArray()
                        });
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get modify records: {e.Message}");
            }
        }

        /// <summary>
        /// 清除UI修改记录
        /// </summary>
        private object ClearModifyRecords(JObject args)
        {
            string uiName = args["name"]?.ToString();

            try
            {
                if (string.IsNullOrEmpty(uiName))
                {
                    // 清除所有UI的修改记录
                    int totalCleared = 0;
                    var clearedAssets = new List<string>();

                    // 查找所有 FigmaUGUIObject
                    string[] guids = AssetDatabase.FindAssets($"t:FigmaUGUIObject");

                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        FigmaUGUIRuleObject figmaObj = AssetDatabase.LoadAssetAtPath<FigmaUGUIRuleObject>(assetPath);

                        if (figmaObj != null && figmaObj.modify_records != null && figmaObj.modify_records.Count > 0)
                        {
                            totalCleared += figmaObj.modify_records.Count;
                            figmaObj.modify_records.Clear();
                            EditorUtility.SetDirty(figmaObj);
                            clearedAssets.Add(assetPath);
                        }
                    }

                    // 保存所有修改的资产
                    AssetDatabase.SaveAssets();

                    LogInfo($"[FigmaMakeUGUI] Cleared all modify records ({totalCleared} records from {clearedAssets.Count} assets)");

                    return Response.Success($"Cleared all modify records ({totalCleared} records from {clearedAssets.Count} assets).",
                        new
                        {
                            clearedRecords = totalCleared,
                            clearedAssets = clearedAssets,
                            remainingRecords = 0
                        });
                }
                else
                {
                    // 清除指定UI的修改记录
                    FigmaUGUIRuleObject figmaObj = FindFigmaUGUIObject(uiName);

                    if (figmaObj == null)
                    {
                        return Response.Success($"No FigmaUGUIObject found for UI '{uiName}' to clear.",
                            new
                            {
                                uiName = uiName,
                                clearedRecords = 0,
                                clearedAssets = new string[0]
                            });
                    }

                    int clearedRecords = 0;
                    string assetPath = null;

                    if (figmaObj.modify_records != null && figmaObj.modify_records.Count > 0)
                    {
                        clearedRecords = figmaObj.modify_records.Count;
                        figmaObj.modify_records.Clear();
                        EditorUtility.SetDirty(figmaObj);
                        assetPath = AssetDatabase.GetAssetPath(figmaObj);
                    }

                    // 保存修改的资产
                    AssetDatabase.SaveAssets();

                    LogInfo($"[FigmaMakeUGUI] Cleared {clearedRecords} modify records for UI '{uiName}'");

                    return Response.Success($"Cleared {clearedRecords} modify record(s) for UI '{uiName}'.",
                        new
                        {
                            uiName = uiName,
                            clearedRecords = clearedRecords,
                            assetPath = assetPath
                        });
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to clear modify records: {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// 查找相关的FigmaUGUIObject
        /// </summary>
        private FigmaUGUIRuleObject FindFigmaUGUIObject(string uiName)
        {
            // 在全工程中查找所有 FigmaUGUIObject
            string[] guids = AssetDatabase.FindAssets($"t:" + typeof(FigmaUGUIRuleObject).Name);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                FigmaUGUIRuleObject figmaObj = AssetDatabase.LoadAssetAtPath<FigmaUGUIRuleObject>(assetPath);

                if (figmaObj != null && !string.IsNullOrEmpty(figmaObj.name))
                {
                    // 支持模糊匹配和精确匹配
                    if (figmaObj.name.IndexOf(uiName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        uiName.IndexOf(figmaObj.name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return figmaObj;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 构建UI制作规则
        /// </summary>
        private object BuildUIRule(FigmaUGUIRuleObject figmaObj)
        {
            return new
            {
                name = figmaObj.name,
                modify_desc = figmaObj.extra_description,
                figmaUrl = figmaObj.link_url,
                pictureUrl = figmaObj.picture_url,
                pictureLevel = figmaObj.picture_level,
                buildSteps = JArray.FromObject(figmaObj.build_steps),
                preferredComponents = JArray.FromObject(figmaObj.preferred_components),
                assetPath = AssetDatabase.GetAssetPath(figmaObj),
                modificationHistory = JArray.FromObject(figmaObj.modify_records),
                relatedAssets = JArray.FromObject(GetRelatedAssets(figmaObj)),
            };
        }

        /// <summary>
        /// 获取相关资产
        /// </summary>
        private List<JObject> GetRelatedAssets(FigmaUGUIRuleObject figmaObj)
        {
            var relatedAssets = new List<JObject>();
            // 查找相关图片资产
            if (!string.IsNullOrEmpty(figmaObj.picture_url))
            {
                string picturePath = figmaObj.picture_url;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(picturePath);
                if (texture != null)
                {
                    relatedAssets.Add(JObject.FromObject(new
                    {
                        type = "Texture2D",
                        path = picturePath,
                        instance_id = texture.GetInstanceID(),
                        width = texture.width,
                        height = texture.height,
                    }));
                }
            }
            return relatedAssets;
        }
    }
}
