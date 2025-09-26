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
using UnityEngine.Networking;
using System.Collections;

namespace UnityMCP.Tools
{
    /// <summary>
    /// UI规则管理工具，负责管理UI制作方案和修改记录
    /// 对应方法名: ui_rule_manage
    /// </summary>
    [ToolName("ui_rule_manage", "UI管理")]
    public class UIRuleManage : StateMethodBase
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        protected override MethodKey[] CreateKeys()
        {
            return new[]
            {
                new MethodKey("action", "Operation type: create_rule(create production plan), get_rule(get production plan), get_prototype_pic(get prototype picture as base64), add_modify(add modification record), record_names(batch record node naming info), get_names(get node naming info), record_sprites(batch record sprite info), get_sprites(get sprite info)", false),
                new MethodKey("name", "UI name, used for finding and recording", false),
                new MethodKey("modify_desc", "Modification description", true),
                new MethodKey("save_path", "Save path, used to create new FigmaUGUIRuleObject", true),
                new MethodKey("properties", "Property data, JSON formatted string", true),
                new MethodKey("names_data", "JSON object with node_id:{name,originName} pairs {\"node_id1\":{\"name\":\"new_name1\",\"originName\":\"orig_name1\"}} or simple node_id:node_name pairs {\"node_id1\":\"node_name1\"} - Required for record_names", true),
                new MethodKey("sprites_data", "JSON object with node_id:fileName pairs {\"node_id1\":\"file_name1\",\"node_id2\":\"file_name2\"} - Required for record_sprites", true),
                new MethodKey("auto_load_sprites", "Automatically load sprites from Assets folder based on fileName (default: true)", true)
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
                    .Leaf("get_prototype_pic", GetPrototypePic)
                    .Leaf("add_modify", AddModifyRecord)
                    .Leaf("record_names", RecordNodeNames)
                    .Leaf("get_names", GetNodeNames)
                    .Leaf("record_sprites", RecordNodeSprites)
                    .Leaf("get_sprites", GetNodeSprites)
                .Build();
        }

        /// <summary>
        /// 创建UI制作规则
        /// </summary>
        private object CreateUIRule(StateTreeContext ctx)
        {
            string uiName = ctx["name"]?.ToString();
            string savePath = ctx["save_path"]?.ToString();
            string propertiesJson = ctx["properties"]?.ToString();

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
                UIDefineRuleObject newRule = ScriptableObject.CreateInstance<UIDefineRuleObject>();

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
                            newRule.img_save_to = properties["picture_url"].ToString();

                        if (properties["prototype_pic"] != null)
                            newRule.prototype_pic = properties["prototype_pic"].ToString();

                        if (properties["image_scale"] != null)
                            newRule.image_scale = properties["image_scale"].ToObject<int>();

                        if (properties["descriptions"] != null)
                            newRule.descriptions = properties["descriptions"].ToString();
                        // 注意：descriptions和preferred_components现在从McpSettings中获取
                        // 不再从properties中解析这些字段
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
        private object GetUIRule(StateTreeContext ctx)
        {
            string uiName = ctx["name"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for get_rule.");

            try
            {
                // 搜索相关的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    // 即使没有找到特定的UI规则，也可以返回全局的构建步骤和环境配置
                    var mcpSettings = McpSettings.Instance;
                    return Response.Success($"No specific UI rule found for '{uiName}', but global build configuration is available.",
                        new
                        {
                            uiName = uiName,
                            foundObject = false,
                            suggestion = "Create a UIDefineRule asset to define UI creation rules",
                        });
                }

                // 使用ctx.AsyncReturn处理异步操作
                LogInfo($"[UIRuleManage] 启动异步获取UI规则: {uiName}");
                return ctx.AsyncReturn(GetUIRuleCoroutine(figmaObj, uiName));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Response.Error($"Failed to get UI rule for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取原型图片（Base64格式）
        /// </summary>
        private object GetPrototypePic(StateTreeContext ctx)
        {
            string uiName = ctx["name"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for get_prototype_pic.");

            try
            {
                // 搜索相关的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Error($"No UIDefineRule found for UI '{uiName}'. Please create one first.");
                }

                // 检查是否有prototype_pic路径
                if (string.IsNullOrEmpty(figmaObj.prototype_pic))
                {
                    return Response.Success($"No prototype picture path found for UI '{uiName}'.", new
                    {
                        uiName = uiName,
                        hasPrototypePic = false,
                        prototypePicPath = "",
                        prototypePicBase64 = (string)null
                    });
                }

                // 使用ctx.AsyncReturn处理异步操作
                LogInfo($"[UIRuleManage] 启动异步获取原型图片: {uiName}");
                return ctx.AsyncReturn(GetPrototypePicCoroutine(figmaObj, uiName));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return Response.Error($"Failed to get prototype picture for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取原型图片的协程
        /// </summary>
        private IEnumerator GetPrototypePicCoroutine(UIDefineRuleObject figmaObj, string uiName)
        {
            LogInfo($"[UIRuleManage] 启动协程获取原型图片: {uiName}");

            string prototypePicBase64 = null;

            // 启动图片加载协程
            yield return LoadImageAsBase64(figmaObj.prototype_pic, (base64Result) =>
            {
                prototypePicBase64 = base64Result;
            });

            bool hasPrototypePic = !string.IsNullOrEmpty(prototypePicBase64);

            LogInfo($"[UIRuleManage] 原型图片加载完成: {uiName}, 成功: {hasPrototypePic}");

            yield return Response.Success($"Retrieved prototype picture for UI '{uiName}'.", new
            {
                uiName = uiName,
                hasPrototypePic = hasPrototypePic,
                prototypePicPath = figmaObj.prototype_pic,
                prototypePicBase64 = prototypePicBase64,
                assetPath = AssetDatabase.GetAssetPath(figmaObj)
            });
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
                // 查找对应的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Error($"No UIDefineRule found for UI '{uiName}'. Please create one first.");
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

                return Response.Success($"Modify record added to UIDefineRule for UI '{uiName}'.",
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
        /// 批量记录节点命名信息
        /// </summary>
        private object RecordNodeNames(JObject args)
        {
            string uiName = args["name"]?.ToString();
            string namesDataJson = args["names_data"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for record_names.");

            if (string.IsNullOrEmpty(namesDataJson))
                return Response.Error("'names_data' is required for record_names. Provide JSON object: {\"node_id1\":{\"name\":\"new_name1\",\"originName\":\"orig_name1\"}} or simple {\"node_id1\":\"node_name1\"}");

            try
            {
                // 查找对应的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Error($"No UIDefineRule found for UI '{uiName}'. Please create one first.");
                }

                // 确保 node_names 列表已初始化
                if (figmaObj.node_names == null)
                {
                    figmaObj.node_names = new List<NodeRenameInfo>();
                }

                int addedCount = 0;
                int updatedCount = 0;

                // 处理批量节点信息 - 支持两种格式
                try
                {
                    JObject namesObject = JObject.Parse(namesDataJson);
                    foreach (var kvp in namesObject)
                    {
                        string nodeId = kvp.Key;
                        string nodeName = null;
                        string originName = null;

                        // 检查值的类型：字符串（简单格式）或对象（详细格式）
                        if (kvp.Value is JValue jValue && jValue.Type == JTokenType.String)
                        {
                            // 简单格式：{"node_id": "node_name"}
                            nodeName = jValue.ToString();
                        }
                        else if (kvp.Value is JObject jObject)
                        {
                            // 详细格式：{"node_id": {"name": "new_name", "originName": "orig_name"}}
                            nodeName = jObject["name"]?.ToString();
                            originName = jObject["originName"]?.ToString();
                        }

                        if (!string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(nodeName))
                        {
                            var existingNode = figmaObj.node_names.FirstOrDefault(n => n.id == nodeId);
                            if (existingNode != null)
                            {
                                existingNode.name = nodeName;
                                if (!string.IsNullOrEmpty(originName))
                                {
                                    existingNode.originName = originName;
                                }
                                updatedCount++;
                            }
                            else
                            {
                                figmaObj.node_names.Add(new NodeRenameInfo
                                {
                                    id = nodeId,
                                    name = nodeName,
                                    originName = originName ?? ""
                                });
                                addedCount++;
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    return Response.Error($"Failed to parse names_data JSON: {jsonEx.Message}");
                }

                if (addedCount == 0 && updatedCount == 0)
                {
                    return Response.Error("No valid node naming data found in names_data object.");
                }

                // 标记资产为脏数据并保存
                EditorUtility.SetDirty(figmaObj);
                string assetPath = AssetDatabase.GetAssetPath(figmaObj);
                AssetDatabase.SaveAssets();

                LogInfo($"[UIRuleManage] Batch recorded node names for UI '{uiName}': {addedCount} added, {updatedCount} updated");

                return Response.Success($"Batch node names recorded for UI '{uiName}': {addedCount} added, {updatedCount} updated.",
                    new
                    {
                        uiName = uiName,
                        addedCount = addedCount,
                        updatedCount = updatedCount,
                        totalNodes = figmaObj.node_names.Count,
                        assetPath = assetPath
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to record node names for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 获取节点命名信息
        /// </summary>
        private object GetNodeNames(JObject args)
        {
            string uiName = args["name"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for get_names.");

            try
            {
                // 查找对应的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Success($"No UIDefineRule found for UI '{uiName}'.",
                        new
                        {
                            uiName = uiName,
                            nodeCount = 0,
                            nodes = new object[0]
                        });
                }

                var nodeNames = figmaObj.node_names ?? new List<NodeRenameInfo>();

                return Response.Success($"Retrieved {nodeNames.Count} node name(s) for UI '{uiName}'.",
                    new
                    {
                        uiName = uiName,
                        nodeCount = nodeNames.Count,
                        assetPath = AssetDatabase.GetAssetPath(figmaObj),
                        nodes = nodeNames.Select(n => new { id = n.id, name = n.name, originName = n.originName }).ToArray()
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get node names for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 批量记录节点Sprite信息
        /// </summary>
        private object RecordNodeSprites(JObject args)
        {
            string uiName = args["name"]?.ToString();
            string spritesDataJson = args["sprites_data"]?.ToString();
            bool autoLoadSprites = args["auto_load_sprites"]?.ToObject<bool>() ?? true;

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for record_sprites.");

            if (string.IsNullOrEmpty(spritesDataJson))
                return Response.Error("'sprites_data' is required for record_sprites. Provide JSON object: {\"node_id1\":\"file_name1\",\"node_id2\":\"file_name2\"}");

            try
            {
                // 查找对应的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Error($"No UIDefineRule found for UI '{uiName}'. Please create one first.");
                }

                // 确保 node_sprites 列表已初始化
                if (figmaObj.node_sprites == null)
                {
                    figmaObj.node_sprites = new List<NodeSpriteInfo>();
                }

                int addedCount = 0;
                int updatedCount = 0;
                int loadedSpritesCount = 0;

                // 处理批量Sprite信息 - 键值对格式
                try
                {
                    JObject spritesObject = JObject.Parse(spritesDataJson);
                    foreach (var kvp in spritesObject)
                    {
                        string nodeId = kvp.Key;
                        string fileName = kvp.Value?.ToString();

                        if (!string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(fileName))
                        {
                            var existingSprite = figmaObj.node_sprites.FirstOrDefault(s => s.id == nodeId);
                            if (existingSprite != null)
                            {
                                existingSprite.fileName = fileName;

                                // 自动载入Sprite
                                if (autoLoadSprites)
                                {
                                    var loadedSprite = LoadSpriteFromPath(figmaObj.img_save_to, fileName);
                                    if (loadedSprite != null)
                                    {
                                        existingSprite.sprite = loadedSprite;
                                        loadedSpritesCount++;
                                    }
                                }

                                updatedCount++;
                            }
                            else
                            {
                                var newSpriteInfo = new NodeSpriteInfo { id = nodeId, fileName = fileName };

                                // 自动载入Sprite
                                if (autoLoadSprites)
                                {
                                    var loadedSprite = LoadSpriteFromPath(figmaObj.img_save_to, fileName);
                                    if (loadedSprite != null)
                                    {
                                        newSpriteInfo.sprite = loadedSprite;
                                        loadedSpritesCount++;
                                    }
                                }

                                figmaObj.node_sprites.Add(newSpriteInfo);
                                addedCount++;
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    return Response.Error($"Failed to parse sprites_data JSON: {jsonEx.Message}");
                }

                if (addedCount == 0 && updatedCount == 0)
                {
                    return Response.Error("No valid sprite data found in sprites_data object.");
                }

                // 标记资产为脏数据并保存
                EditorUtility.SetDirty(figmaObj);
                string assetPath = AssetDatabase.GetAssetPath(figmaObj);
                AssetDatabase.SaveAssets();

                LogInfo($"[UIRuleManage] Batch recorded node sprites for UI '{uiName}': {addedCount} added, {updatedCount} updated, {loadedSpritesCount} sprites loaded");

                return Response.Success($"Batch node sprites recorded for UI '{uiName}': {addedCount} added, {updatedCount} updated" +
                    (autoLoadSprites ? $", {loadedSpritesCount} sprites loaded" : ""),
                    new
                    {
                        uiName = uiName,
                        addedCount = addedCount,
                        updatedCount = updatedCount,
                        loadedSpritesCount = loadedSpritesCount,
                        totalSprites = figmaObj.node_sprites.Count,
                        assetPath = assetPath,
                        autoLoadSprites = autoLoadSprites
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to record node sprites for '{uiName}': {e.Message}");
            }
        }

        /// <summary>
        /// 从指定路径载入Sprite
        /// </summary>
        private Sprite LoadSpriteFromPath(string imgSaveTo, string fileName)
        {
            if (string.IsNullOrEmpty(imgSaveTo) || string.IsNullOrEmpty(fileName))
                return null;

            // 构建完整的文件路径
            string fullPath = System.IO.Path.Combine(imgSaveTo, fileName);

            // 尝试加载Sprite
            Sprite loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (loadedSprite != null)
            {
                return loadedSprite;
            }

            // 如果直接加载失败，尝试查找文件
            string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string[] foundAssets = AssetDatabase.FindAssets(fileNameWithoutExt + " t:Sprite");

            if (foundAssets.Length > 0)
            {
                // 优先选择在指定路径下的文件
                foreach (string guid in foundAssets)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.StartsWith(imgSaveTo))
                    {
                        Sprite foundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        if (foundSprite != null)
                        {
                            return foundSprite;
                        }
                    }
                }

                // 如果在指定路径下没找到，使用第一个找到的
                string firstAssetPath = AssetDatabase.GUIDToAssetPath(foundAssets[0]);
                Sprite firstSprite = AssetDatabase.LoadAssetAtPath<Sprite>(firstAssetPath);
                if (firstSprite != null)
                {
                    return firstSprite;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取节点Sprite信息
        /// </summary>
        private object GetNodeSprites(JObject args)
        {
            string uiName = args["name"]?.ToString();

            if (string.IsNullOrEmpty(uiName))
                return Response.Error("'name' is required for get_sprites.");

            try
            {
                // 查找对应的 UIDefineRule
                UIDefineRuleObject figmaObj = FindUIDefineRule(uiName);

                if (figmaObj == null)
                {
                    return Response.Success($"No UIDefineRule found for UI '{uiName}'.",
                        new
                        {
                            uiName = uiName,
                            spriteCount = 0,
                            sprites = new object[0]
                        });
                }

                var nodeSprites = figmaObj.node_sprites ?? new List<NodeSpriteInfo>();

                return Response.Success($"Retrieved {nodeSprites.Count} sprite(s) for UI '{uiName}'.",
                    new
                    {
                        uiName = uiName,
                        spriteCount = nodeSprites.Count,
                        assetPath = AssetDatabase.GetAssetPath(figmaObj),
                        sprites = nodeSprites.Select(s => new { id = s.id, fileName = s.fileName }).ToArray()
                    });
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to get node sprites for '{uiName}': {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// 使用协程方式获取UI规则（不包含原型图片）
        /// </summary>
        private IEnumerator GetUIRuleCoroutine(UIDefineRuleObject figmaObj, string uiName)
        {
            LogInfo($"[UIRuleManage] 启动协程获取UI规则: {uiName}");
            // 获取McpSettings中的配置
            var mcpSettings = McpSettings.Instance;

            // 读取optimize_rule_path的文本内容
            string optimizeRuleContent = "";
            string optimizeRuleMessage = "";

            if (!string.IsNullOrEmpty(figmaObj.optimize_rule_path))
            {
                try
                {
                    string fullPath = GetFullRulePath(figmaObj.optimize_rule_path);
                    if (File.Exists(fullPath))
                    {
                        optimizeRuleContent = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                        optimizeRuleMessage = "UI布局优化规则已加载";
                        LogInfo($"[UIRuleManage] 成功读取优化规则文件: {fullPath}");
                    }
                    else
                    {
                        optimizeRuleMessage = "UI布局信息需要下载 - 文件不存在";
                        LogWarning($"[UIRuleManage] 优化规则文件不存在: {fullPath}");
                    }
                }
                catch (Exception e)
                {
                    optimizeRuleMessage = $"UI布局信息需要下载 - 读取失败: {e.Message}";
                    LogError($"[UIRuleManage] 读取优化规则文件失败: {e.Message}");
                }
            }
            else
            {
                optimizeRuleMessage = "UI布局信息需要下载 - 未设置优化规则路径";
            }

            // 构建UI规则信息（不包含designPic）
            var rule = new
            {
                name = figmaObj.name,
                figmaUrl = figmaObj.link_url,
                pictureUrl = figmaObj.img_save_to,
                prototypePic = figmaObj.prototype_pic,
                optimizeRulePath = figmaObj.optimize_rule_path,
                optimizeRuleContent = optimizeRuleContent,
                optimizeRuleMessage = optimizeRuleMessage,
                imageScale = figmaObj.image_scale,
                descriptions = GenerateMarkdownDescription(mcpSettings.uiSettings?.ui_build_steps ?? McpUISettingsProvider.GetDefaultBuildSteps(), mcpSettings.uiSettings?.ui_build_enviroments ?? McpUISettingsProvider.GetDefaultBuildEnvironments(), figmaObj.descriptions),
                assetPath = AssetDatabase.GetAssetPath(figmaObj),
                rename_count = figmaObj.node_names.Count,
                sprite_count = figmaObj.node_sprites.Count
            };

            LogInfo($"[UIRuleManage] UI规则构建完成: {uiName}");

            yield return Response.Success($"Found UI rule for '{uiName}'.", new
            {
                uiName = uiName,
                foundObject = true,
                rule = rule
            });
        }

        /// <summary>
        /// 加载图片并转换为Base64（协程版本）
        /// </summary>
        private IEnumerator LoadImageAsBase64(string imagePath, Action<string> callback)
        {
            LogInfo($"[UIRuleManage] 开始加载图片: {imagePath}");

            // 判断是本地路径还是网络路径
            if (IsNetworkPath(imagePath))
            {
                // 网络路径：使用UnityWebRequest下载
                yield return LoadNetworkImageAsBase64(imagePath, callback);
            }
            else
            {
                // 本地路径：直接读取文件
                yield return LoadLocalImageAsBase64(imagePath, callback);
            }
        }

        /// <summary>
        /// 加载网络图片并转换为Base64
        /// </summary>
        private IEnumerator LoadNetworkImageAsBase64(string url, Action<string> callback)
        {
            LogInfo($"[UIRuleManage] 从网络加载图片: {url}");

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 30; // 30秒超时
                request.SetRequestHeader("User-Agent", "Unity-MCP-UIRuleManager/1.0");

                var operation = request.SendWebRequest();
                float startTime = Time.realtimeSinceStartup;

                // 等待下载完成
                while (!operation.isDone)
                {
                    // 检查超时
                    if (Time.realtimeSinceStartup - startTime > 30f)
                    {
                        request.Abort();
                        LogError($"[UIRuleManage] 网络图片下载超时: {url}");
                        callback?.Invoke(null);
                        yield break;
                    }
                    yield return null;
                }

                // 检查下载结果
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        byte[] imageData = request.downloadHandler.data;
                        string base64String = Convert.ToBase64String(imageData);

                        // 根据Content-Type添加数据URI前缀
                        string contentType = request.GetResponseHeader("Content-Type") ?? "image/png";
                        string dataUri = $"data:{contentType};base64,{base64String}";

                        LogInfo($"[UIRuleManage] 网络图片转换为Base64成功，大小: {imageData.Length} bytes");
                        callback?.Invoke(dataUri);
                    }
                    catch (Exception e)
                    {
                        LogError($"[UIRuleManage] 网络图片Base64转换失败: {e.Message}");
                        callback?.Invoke(null);
                    }
                }
                else
                {
                    LogError($"[UIRuleManage] 网络图片下载失败: {request.error}");
                    callback?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// 加载本地图片并转换为Base64
        /// </summary>
        private IEnumerator LoadLocalImageAsBase64(string filePath, Action<string> callback)
        {
            LogInfo($"[UIRuleManage] 从本地加载图片: {filePath}");

            // 规范化路径
            string fullPath = GetFullImagePath(filePath);

            if (!File.Exists(fullPath))
            {
                LogError($"[UIRuleManage] 本地图片文件不存在: {fullPath}");
                callback?.Invoke(null);
                yield break;
            }

            // 在协程中处理文件读取，避免阻塞
            byte[] imageData = null;
            string errorMessage = null;

            // 使用协程分帧读取大文件
            yield return ReadFileInChunks(fullPath, (data, error) =>
            {
                imageData = data;
                errorMessage = error;
            });

            if (!string.IsNullOrEmpty(errorMessage))
            {
                LogError($"[UIRuleManage] 本地图片读取失败: {errorMessage}");
                callback?.Invoke(null);
                yield break;
            }

            if (imageData == null || imageData.Length == 0)
            {
                LogError($"[UIRuleManage] 本地图片数据为空: {fullPath}");
                callback?.Invoke(null);
                yield break;
            }

            // 根据文件扩展名确定MIME类型
            string extension = Path.GetExtension(fullPath).ToLower();
            string mimeType = GetMimeTypeFromExtension(extension);

            // 转换为Base64
            string base64String = Convert.ToBase64String(imageData);
            string dataUri = $"data:{mimeType};base64,{base64String}";

            LogInfo($"[UIRuleManage] 本地图片转换为Base64成功，大小: {imageData.Length} bytes");
            callback?.Invoke(dataUri);
        }

        /// <summary>
        /// 分块读取文件以避免阻塞（协程版本）
        /// </summary>
        private IEnumerator ReadFileInChunks(string filePath, Action<byte[], string> callback)
        {
            const int chunkSize = 1024 * 1024; // 1MB chunks
            var chunks = new List<byte[]>();
            string errorMessage = null;

            FileStream fileStream = null;

            // 在协程外部处理异常，避免在try-catch中使用yield return
            bool initSuccess = false;
            long totalSize = 0;

            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                totalSize = fileStream.Length;
                initSuccess = true;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }

            if (!initSuccess || !string.IsNullOrEmpty(errorMessage))
            {
                fileStream?.Dispose();
                callback?.Invoke(null, errorMessage ?? "Failed to open file");
                yield break;
            }

            // 读取文件数据
            long bytesRead = 0;
            while (bytesRead < totalSize)
            {
                try
                {
                    int currentChunkSize = (int)Math.Min(chunkSize, totalSize - bytesRead);
                    byte[] chunk = new byte[currentChunkSize];

                    int actualRead = fileStream.Read(chunk, 0, currentChunkSize);
                    if (actualRead > 0)
                    {
                        if (actualRead < currentChunkSize)
                        {
                            // 调整数组大小
                            Array.Resize(ref chunk, actualRead);
                        }
                        chunks.Add(chunk);
                        bytesRead += actualRead;
                    }
                    else
                    {
                        // 没有更多数据可读
                        break;
                    }
                }
                catch (Exception e)
                {
                    errorMessage = e.Message;
                    break;
                }

                // 每读取一块就yield一次，避免阻塞
                yield return null;
            }

            // 清理资源
            fileStream?.Close();
            fileStream?.Dispose();

            if (!string.IsNullOrEmpty(errorMessage))
            {
                callback?.Invoke(null, errorMessage);
                yield break;
            }

            // 合并所有块
            int totalLength = chunks.Sum(c => c.Length);
            byte[] result = new byte[totalLength];
            int offset = 0;

            foreach (var chunk in chunks)
            {
                Array.Copy(chunk, 0, result, offset, chunk.Length);
                offset += chunk.Length;
            }

            callback?.Invoke(result, null);
        }

        /// <summary>
        /// 判断是否为网络路径
        /// </summary>
        private bool IsNetworkPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取图片的完整本地路径
        /// </summary>
        /// <summary>
        /// 获取图片的完整本地路径，兼容filePath为全路径或相对路径
        /// </summary>
        private string GetFullImagePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            // 如果已经是绝对路径，直接返回
            if (Path.IsPathRooted(filePath) && File.Exists(filePath))
            {
                return filePath;
            }

            // 如果路径以Assets开头，拼接到项目根目录
            if (filePath.StartsWith("Assets"))
            {
                string absPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                if (File.Exists(absPath))
                    return absPath;
            }

            // 尝试拼接到Assets目录下
            string assetsPath = Path.Combine(Application.dataPath, filePath);
            if (File.Exists(assetsPath))
                return assetsPath;

            // 如果都找不到，返回原始路径（可能是错误路径）
            return filePath;
        }

        /// <summary>
        /// 获取优化规则文件的完整本地路径，兼容filePath为全路径或相对路径
        /// </summary>
        private string GetFullRulePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            // 如果已经是绝对路径，直接返回
            if (Path.IsPathRooted(filePath))
            {
                return filePath;
            }

            // 如果路径以Assets开头，拼接到项目根目录
            if (filePath.StartsWith("Assets"))
            {
                string absPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                return absPath;
            }

            // 尝试拼接到Assets目录下
            string assetsPath = Path.Combine(Application.dataPath, filePath);
            return assetsPath;
        }

        /// <summary>
        /// 根据文件扩展名获取MIME类型
        /// </summary>
        private string GetMimeTypeFromExtension(string extension)
        {
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".webp":
                    return "image/webp";
                case ".svg":
                    return "image/svg+xml";
                default:
                    return "image/png"; // 默认为PNG
            }
        }

        /// <summary>
        /// 查找相关的UIDefineRule
        /// </summary>
        private UIDefineRuleObject FindUIDefineRule(string uiName)
        {
            // 在全工程中查找所有 UIDefineRule
            string[] guids = AssetDatabase.FindAssets($"t:" + typeof(UIDefineRuleObject).Name);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                UIDefineRuleObject figmaObj = AssetDatabase.LoadAssetAtPath<UIDefineRuleObject>(assetPath);

                if (figmaObj != null)
                {
                    var objName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    if (objName.ToLower() == uiName.ToLower())
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
        private object BuildUIRule(UIDefineRuleObject figmaObj)
        {
            // 获取McpSettings中的配置
            var mcpSettings = McpSettings.Instance;

            // 读取optimize_rule_path的文本内容
            string optimizeRuleContent = "";
            string optimizeRuleMessage = "";

            if (!string.IsNullOrEmpty(figmaObj.optimize_rule_path))
            {
                try
                {
                    string fullPath = GetFullRulePath(figmaObj.optimize_rule_path);
                    if (File.Exists(fullPath))
                    {
                        optimizeRuleContent = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                        optimizeRuleMessage = "UI布局优化规则已加载";
                    }
                    else
                    {
                        optimizeRuleMessage = "UI布局信息需要下载 - 文件不存在";
                    }
                }
                catch (Exception e)
                {
                    optimizeRuleMessage = $"UI布局信息需要下载 - 读取失败: {e.Message}";
                }
            }
            else
            {
                optimizeRuleMessage = "UI布局信息需要下载 - 未设置优化规则路径";
            }

            return new
            {
                name = figmaObj.name,
                figmaUrl = figmaObj.link_url,
                pictureUrl = figmaObj.img_save_to,
                optimizeRulePath = figmaObj.optimize_rule_path,
                optimizeRuleContent = optimizeRuleContent,
                optimizeRuleMessage = optimizeRuleMessage,
                imageScale = figmaObj.image_scale,
                descriptions = figmaObj.descriptions,
                // 使用McpUISettingsProvider中的配置替代原来的descriptions和preferred_components
                buildSteps = JArray.FromObject(mcpSettings.uiSettings?.ui_build_steps ?? McpUISettingsProvider.GetDefaultBuildSteps()),
                buildEnvironments = JArray.FromObject(mcpSettings.uiSettings?.ui_build_enviroments ?? McpUISettingsProvider.GetDefaultBuildEnvironments()),
                assetPath = AssetDatabase.GetAssetPath(figmaObj)
            };
        }

        /// <summary>
        /// 生成包含构建步骤、构建环境和附加条件的Markdown描述文本
        /// </summary>
        private string GenerateMarkdownDescription(List<string> buildSteps, List<string> buildEnvironments, string additionalConditions)
        {
            var markdown = new System.Text.StringBuilder();

            // 添加标题
            markdown.AppendLine("# UI构建规则说明");
            markdown.AppendLine();

            // 添加构建步骤
            if (buildSteps != null && buildSteps.Count > 0)
            {
                markdown.AppendLine("## 🔨 构建步骤");
                markdown.AppendLine();
                for (int i = 0; i < buildSteps.Count; i++)
                {
                    markdown.AppendLine($"{i + 1}. {buildSteps[i]}");
                }
                markdown.AppendLine();
            }

            // 添加构建环境
            if (buildEnvironments != null && buildEnvironments.Count > 0)
            {
                markdown.AppendLine("## 🌐 构建环境");
                markdown.AppendLine();
                foreach (var env in buildEnvironments)
                {
                    markdown.AppendLine($"- {env}");
                }
                markdown.AppendLine();
            }

            // 添加附加条件
            if (!string.IsNullOrEmpty(additionalConditions))
            {
                markdown.AppendLine("## 📋 附加条件");
                markdown.AppendLine();
                markdown.AppendLine(additionalConditions);
                markdown.AppendLine();
            }

            return markdown.ToString().TrimEnd();
        }

    }
}
