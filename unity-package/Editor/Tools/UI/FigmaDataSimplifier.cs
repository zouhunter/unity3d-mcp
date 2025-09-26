using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Figma数据简化器 - 将复杂的Figma节点数据简化为AI友好且token高效的格式
    /// </summary>
    public static class FigmaDataSimplifier
    {
        /// <summary>
        /// 简化的节点数据结构
        /// </summary>
        [Serializable]
        public class SimplifiedNode
        {
            public string id;              // 节点ID
            public string name;            // 节点名称
            public string type;            // 节点类型 (FRAME, TEXT, RECTANGLE等)
            public bool visible = true;    // 是否可见

            // 文本相关
            public string text;            // 文本内容
            public TextStyle textStyle;    // 文本样式

            // 样式相关
            public ColorInfo backgroundColor; // 背景色
            public ColorInfo textColor;      // 文字颜色
            public float cornerRadius;       // 圆角
            public bool hasImage;            // 是否需要下载为图片
            public string imageRef;          // 图片引用

            // 布局相关
            public LayoutInfo layout;        // 布局信息

            // UGUI锚点信息（直接在节点中）
            public float[] anchoredPos;      // 锚点位置 [x, y]
            public float[] sizeDelta;        // 尺寸增量 [width, height]
            public float[] anchorMin;        // 最小锚点 [x, y]
            public float[] anchorMax;        // 最大锚点 [x, y]
            public float[] pivot;            // 轴心点 [x, y]

            public List<SimplifiedNode> children; // 子节点

            // 组件列表（仅在根节点包含）
            public List<string> components;   // 组件ID列表
        }

        /// <summary>
        /// 文本样式信息
        /// </summary>
        [Serializable]
        public class TextStyle
        {
            public string fontFamily;      // 字体族
            public string fontWeight;      // 字体粗细
            public float fontSize;         // 字体大小
            public string textAlign;       // 文本对齐
            public float lineHeight;       // 行高
        }

        /// <summary>
        /// 颜色信息
        /// </summary>
        [Serializable]
        public class ColorInfo
        {
            public float r, g, b, a;       // RGBA值
            public string hex;             // 十六进制颜色值
            public string type;            // 颜色类型 (SOLID, GRADIENT等)
        }

        /// <summary>
        /// 布局信息
        /// </summary>
        [Serializable]
        public class LayoutInfo
        {
            public string layoutMode;      // 布局模式 (VERTICAL, HORIZONTAL等)
            public string alignItems;      // 对齐方式
            public float itemSpacing;      // 间距
            public float[] padding;        // 内边距 [left, top, right, bottom]
        }

        /// <summary>
        /// Figma约束信息（仅用于内部计算）
        /// </summary>
        private class ConstraintInfo
        {
            public string horizontal;  // 水平约束 (LEFT, RIGHT, CENTER, LEFT_RIGHT, SCALE)
            public string vertical;    // 垂直约束 (TOP, BOTTOM, CENTER, TOP_BOTTOM, SCALE)
        }


        /// <summary>
        /// 简化Figma节点数据并转换为UGUI锚点信息
        /// </summary>
        /// <param name="figmaNode">原始Figma节点数据</param>
        /// <param name="maxDepth">最大深度，默认无限制</param>
        /// <param name="convertToUGUI">是否转换为UGUI锚点信息，默认true</param>
        /// <param name="cleanupRedundantData">是否清理冗余数据，默认true</param>
        /// <returns>简化后的节点数据</returns>
        public static SimplifiedNode SimplifyNode(JToken figmaNode, int maxDepth = -1, bool convertToUGUI = true, bool cleanupRedundantData = true)
        {
            if (figmaNode == null || maxDepth == 0)
                return null;

            // 如果节点不可见，直接返回null，不进行解析
            bool visible = figmaNode["visible"]?.ToObject<bool?>() ?? true;
            if (!visible)
                return null;

            var simplified = new SimplifiedNode
            {
                id = figmaNode["id"]?.ToString(),
                name = figmaNode["name"]?.ToString(),
                type = figmaNode["type"]?.ToString(),
                visible = true // 由于已经过滤了不可见节点，这里总是true
            };

            // 临时提取位置和尺寸用于UGUI锚点计算（后续会被清理）
            float[] tempPosition = null;
            float[] tempSize = null;
            var absoluteBoundingBox = figmaNode["absoluteBoundingBox"];
            if (absoluteBoundingBox != null)
            {
                tempPosition = new float[]
                {
                    (float)Math.Round(absoluteBoundingBox["x"]?.ToObject<float>() ?? 0, 2),
                    (float)Math.Round(absoluteBoundingBox["y"]?.ToObject<float>() ?? 0, 2)
                };
                tempSize = new float[]
                {
                    (float)Math.Round(absoluteBoundingBox["width"]?.ToObject<float>() ?? 0, 2),
                    (float)Math.Round(absoluteBoundingBox["height"]?.ToObject<float>() ?? 0, 2)
                };
            }

            // 提取文本内容和样式
            ExtractTextInfo(figmaNode, simplified);

            // 提取样式信息
            ExtractStyleInfo(figmaNode, simplified);

            // 提取布局信息
            ExtractLayoutInfo(figmaNode, simplified);

            // 判断是否需要下载为图片
            simplified.hasImage = IsDownloadableNode(figmaNode);

            // 递归处理子节点
            var children = figmaNode["children"];
            if (children != null && children.Type == JTokenType.Array)
            {
                simplified.children = new List<SimplifiedNode>();
                foreach (var child in children) // 处理所有子节点
                {
                    var nextDepth = maxDepth > 0 ? maxDepth - 1 : -1; // 如果maxDepth为-1则保持无限制
                    var simplifiedChild = SimplifyNode(child, nextDepth);
                    if (simplifiedChild != null)
                    {
                        simplified.children.Add(simplifiedChild);
                    }
                }

                // 如果没有子节点，设为null节省空间
                if (simplified.children.Count == 0)
                    simplified.children = null;
            }

            // 转换为UGUI锚点信息
            if (convertToUGUI)
            {
                ConvertNodeToUGUI(figmaNode, simplified);
                // 转换完成后清理不必要的数据（如果不需要保留布局信息）
                if (!cleanupRedundantData)
                {
                    CleanupAfterUGUIConversion(simplified);
                }
            }

            return simplified;
        }

        /// <summary>
        /// 转换单个节点为UGUI锚点信息
        /// </summary>
        /// <param name="figmaNode">原始Figma节点</param>
        /// <param name="node">简化节点</param>
        private static void ConvertNodeToUGUI(JToken figmaNode, SimplifiedNode node)
        {
            if (figmaNode == null || node == null)
                return;

            // 直接转换当前节点
            ConvertToUGUIAnchors(figmaNode, node, null);
        }

        /// <summary>
        /// 提取文本信息
        /// </summary>
        private static void ExtractTextInfo(JToken node, SimplifiedNode simplified)
        {
            // 文本内容
            simplified.text = node["characters"]?.ToString();

            // 文本样式
            var style = node["style"];
            if (style != null && style.Type == JTokenType.Object)
            {
                simplified.textStyle = new TextStyle
                {
                    fontFamily = style["fontFamily"]?.ToString(),
                    fontWeight = style["fontWeight"]?.ToString(),
                    fontSize = (float)Math.Round(style["fontSize"]?.ToObject<float>() ?? 0, 2),
                    textAlign = style["textAlignHorizontal"]?.ToString(),
                    lineHeight = (float)Math.Round(style["lineHeightPx"]?.ToObject<float>() ?? 0, 2)
                };
            }
        }

        /// <summary>
        /// 提取样式信息
        /// </summary>
        private static void ExtractStyleInfo(JToken node, SimplifiedNode simplified)
        {
            // 背景色
            var fills = node["fills"];
            if (fills != null && fills.Type == JTokenType.Array && fills.Any())
            {
                var firstFill = fills.First();
                if (firstFill != null && firstFill.Type == JTokenType.Object)
                {
                    simplified.backgroundColor = ExtractColor(firstFill);
                }
            }

            // 文字颜色
            if (simplified.textStyle != null && fills != null && fills.Type == JTokenType.Array && fills.Any())
            {
                var firstFill = fills.First();
                if (firstFill != null && firstFill.Type == JTokenType.Object)
                {
                    simplified.textColor = ExtractColor(firstFill);
                }
            }

            // 圆角
            simplified.cornerRadius = (float)Math.Round(node["cornerRadius"]?.ToObject<float>() ?? 0, 2);

            // 图片信息 - 检查是否包含图片引用
            if (fills != null && fills.Type == JTokenType.Array)
            {
                foreach (var fill in fills)
                {
                    if (fill != null && fill.Type == JTokenType.Object && fill["type"]?.ToString() == "IMAGE")
                    {
                        simplified.imageRef = fill["imageRef"]?.ToString();
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 提取颜色信息
        /// </summary>
        private static ColorInfo ExtractColor(JToken fill)
        {
            if (fill == null || fill.Type != JTokenType.Object) return null;

            var colorInfo = new ColorInfo
            {
                type = fill["type"]?.ToString()
            };

            var color = fill["color"];
            if (color != null && color.Type == JTokenType.Object)
            {
                colorInfo.r = (float)Math.Round(color["r"]?.ToObject<float>() ?? 0, 2);
                colorInfo.g = (float)Math.Round(color["g"]?.ToObject<float>() ?? 0, 2);
                colorInfo.b = (float)Math.Round(color["b"]?.ToObject<float>() ?? 0, 2);
                colorInfo.a = (float)Math.Round(color["a"]?.ToObject<float>() ?? 1, 2);

                // 转换为十六进制
                int r = Mathf.RoundToInt(colorInfo.r * 255);
                int g = Mathf.RoundToInt(colorInfo.g * 255);
                int b = Mathf.RoundToInt(colorInfo.b * 255);
                colorInfo.hex = $"#{r:X2}{g:X2}{b:X2}";
            }

            return colorInfo;
        }

        /// <summary>
        /// 提取布局信息
        /// </summary>
        private static void ExtractLayoutInfo(JToken node, SimplifiedNode simplified)
        {
            var layoutMode = node["layoutMode"]?.ToString();
            if (!string.IsNullOrEmpty(layoutMode))
            {
                simplified.layout = new LayoutInfo
                {
                    layoutMode = layoutMode,
                    alignItems = node["primaryAxisAlignItems"]?.ToString() ?? node["counterAxisAlignItems"]?.ToString(),
                    itemSpacing = (float)Math.Round(node["itemSpacing"]?.ToObject<float>() ?? 0, 2)
                };

                // 内边距
                var paddingLeft = (float)Math.Round(node["paddingLeft"]?.ToObject<float>() ?? 0, 2);
                var paddingTop = (float)Math.Round(node["paddingTop"]?.ToObject<float>() ?? 0, 2);
                var paddingRight = (float)Math.Round(node["paddingRight"]?.ToObject<float>() ?? 0, 2);
                var paddingBottom = (float)Math.Round(node["paddingBottom"]?.ToObject<float>() ?? 0, 2);

                if (paddingLeft > 0 || paddingTop > 0 || paddingRight > 0 || paddingBottom > 0)
                {
                    simplified.layout.padding = new float[] { paddingLeft, paddingTop, paddingRight, paddingBottom };
                }
            }
        }


        /// <summary>
        /// 将简化的节点数据转换为紧凑的JSON字符串
        /// </summary>
        /// <param name="simplifiedNode">简化的节点数据</param>
        /// <param name="prettyPrint">是否格式化输出，默认false以减少token</param>
        /// <returns>JSON字符串</returns>
        public static string ToCompactJson(SimplifiedNode simplifiedNode, bool prettyPrint = false)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore, // 忽略null值
                DefaultValueHandling = DefaultValueHandling.Ignore, // 忽略默认值
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None
            };

            return JsonConvert.SerializeObject(simplifiedNode, settings);
        }

        /// <summary>
        /// 批量简化多个节点
        /// </summary>
        /// <param name="figmaNodes">原始节点数据字典</param>
        /// <param name="maxDepth">最大深度，默认无限制</param>
        /// <returns>简化后的节点字典</returns>
        public static Dictionary<string, SimplifiedNode> SimplifyNodes(JObject figmaNodes, int maxDepth = -1)
        {
            var result = new Dictionary<string, SimplifiedNode>();

            if (figmaNodes == null) return result;

            foreach (var kvp in figmaNodes)
            {
                var nodeData = kvp.Value["document"];
                if (nodeData != null)
                {
                    var simplified = SimplifyNode(nodeData, maxDepth);
                    if (simplified != null)
                    {
                        // 提取并简化 components
                        var componentsData = kvp.Value["components"];
                        if (componentsData != null && componentsData.Type == JTokenType.Object)
                        {
                            simplified.components = ExtractComponentIds(componentsData);
                        }

                        result[kvp.Key] = simplified;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 提取组件ID列表
        /// </summary>
        /// <param name="componentsData">组件数据对象</param>
        /// <returns>组件ID列表</returns>
        private static List<string> ExtractComponentIds(JToken componentsData)
        {
            var componentIds = new List<string>();

            if (componentsData == null || componentsData.Type != JTokenType.Object)
                return componentIds;

            foreach (var property in ((JObject)componentsData).Properties())
            {
                // property.Name 就是组件ID
                componentIds.Add(property.Name);
            }

            return componentIds;
        }

        /// <summary>
        /// 生成AI友好的节点摘要
        /// </summary>
        /// <param name="simplifiedNode">简化的节点数据</param>
        /// <returns>文本摘要</returns>
        public static string GenerateNodeSummary(SimplifiedNode simplifiedNode)
        {
            if (simplifiedNode == null) return "";

            var summary = new List<string>();

            // 基本信息
            summary.Add($"节点: {simplifiedNode.name} ({simplifiedNode.type})");

            // 优先显示UGUI信息，其次是原始尺寸
            if (simplifiedNode.sizeDelta != null)
            {
                summary.Add($"UGUI尺寸: {simplifiedNode.sizeDelta[0]:F0}x{simplifiedNode.sizeDelta[1]:F0}");
                if (simplifiedNode.anchoredPos != null)
                    summary.Add($"锚点位置: [{simplifiedNode.anchoredPos[0]:F1}, {simplifiedNode.anchoredPos[1]:F1}]");
                if (simplifiedNode.anchorMin != null && simplifiedNode.anchorMax != null)
                    summary.Add($"锚点: [{simplifiedNode.anchorMin[0]:F2},{simplifiedNode.anchorMin[1]:F2}] - [{simplifiedNode.anchorMax[0]:F2},{simplifiedNode.anchorMax[1]:F2}]");
            }
            // 如果没有UGUI信息，就不显示尺寸了，因为我们已经移除了原始size字段

            if (!string.IsNullOrEmpty(simplifiedNode.text))
            {
                summary.Add($"文本: \"{simplifiedNode.text}\"");
                if (simplifiedNode.textStyle != null)
                {
                    summary.Add($"字体: {simplifiedNode.textStyle.fontFamily} {simplifiedNode.textStyle.fontSize:F0}px");
                }
            }

            if (simplifiedNode.backgroundColor != null)
            {
                summary.Add($"背景: {simplifiedNode.backgroundColor.hex}");
            }

            if (simplifiedNode.hasImage)
            {
                summary.Add("包含图片");
            }

            if (simplifiedNode.layout != null)
            {
                summary.Add($"布局: {simplifiedNode.layout.layoutMode}");
            }

            if (simplifiedNode.children != null && simplifiedNode.children.Count > 0)
            {
                summary.Add($"子节点: {simplifiedNode.children.Count}个");
            }

            if (simplifiedNode.components != null && simplifiedNode.components.Count > 0)
            {
                summary.Add($"组件: {simplifiedNode.components.Count}个");
            }

            return string.Join(", ", summary);
        }

        /// <summary>
        /// 计算数据压缩率
        /// </summary>
        /// <param name="originalJson">原始JSON</param>
        /// <param name="simplifiedJson">简化后的JSON</param>
        /// <returns>压缩率百分比</returns>
        public static float CalculateCompressionRatio(string originalJson, string simplifiedJson)
        {
            if (string.IsNullOrEmpty(originalJson) || string.IsNullOrEmpty(simplifiedJson))
                return 0f;

            float originalSize = originalJson.Length;
            float simplifiedSize = simplifiedJson.Length;

            return (1f - simplifiedSize / originalSize) * 100f;
        }

        /// <summary>
        /// 提取关键节点信息（进一步压缩）
        /// </summary>
        /// <param name="simplifiedNode">简化的节点</param>
        /// <returns>关键信息字典</returns>
        public static Dictionary<string, object> ExtractKeyInfo(SimplifiedNode simplifiedNode)
        {
            var keyInfo = new Dictionary<string, object>
            {
                ["id"] = simplifiedNode.id,
                ["name"] = simplifiedNode.name,
                ["type"] = simplifiedNode.type,
                ["size"] = simplifiedNode.sizeDelta != null ? $"{simplifiedNode.sizeDelta[0]:F0}x{simplifiedNode.sizeDelta[1]:F0}" : "0x0"
            };

            // 只添加非空的关键信息
            if (!string.IsNullOrEmpty(simplifiedNode.text))
                keyInfo["text"] = simplifiedNode.text;

            if (simplifiedNode.textStyle?.fontSize > 0)
                keyInfo["fontSize"] = simplifiedNode.textStyle.fontSize;

            if (simplifiedNode.backgroundColor?.hex != null)
                keyInfo["bgColor"] = simplifiedNode.backgroundColor.hex;

            if (simplifiedNode.hasImage)
                keyInfo["hasImage"] = true;

            if (simplifiedNode.layout?.layoutMode != null)
                keyInfo["layout"] = simplifiedNode.layout.layoutMode;

            if (simplifiedNode.children?.Count > 0)
            {
                keyInfo["childCount"] = simplifiedNode.children.Count;
                // 只包含子节点的关键信息
                keyInfo["children"] = simplifiedNode.children.Select(child => new
                {
                    id = child.id,
                    name = child.name,
                    type = child.type,
                    text = child.text,
                    hasImage = child.hasImage
                }).Where(child => !string.IsNullOrEmpty(child.text) || child.hasImage).ToList();
            }

            if (simplifiedNode.components?.Count > 0)
            {
                keyInfo["componentCount"] = simplifiedNode.components.Count;
                keyInfo["components"] = simplifiedNode.components;
            }

            return keyInfo;
        }

        /// <summary>
        /// 生成超简洁的AI提示文本
        /// </summary>
        /// <param name="simplifiedNode">简化的节点</param>
        /// <returns>AI提示文本</returns>
        public static string GenerateAIPrompt(SimplifiedNode simplifiedNode)
        {
            var parts = new List<string>();

            // 基础结构
            parts.Add($"{simplifiedNode.name}({simplifiedNode.type})");

            // 尺寸（只在重要时显示）
            if (simplifiedNode.sizeDelta != null && (simplifiedNode.sizeDelta[0] > 100 || simplifiedNode.sizeDelta[1] > 100))
                parts.Add($"{simplifiedNode.sizeDelta[0]:F0}x{simplifiedNode.sizeDelta[1]:F0}");

            // 文本内容
            if (!string.IsNullOrEmpty(simplifiedNode.text))
            {
                var text = simplifiedNode.text.Length > 20 ?
                    simplifiedNode.text.Substring(0, 20) + "..." :
                    simplifiedNode.text;
                parts.Add($"\"{text}\"");

                if (simplifiedNode.textStyle?.fontSize > 0)
                    parts.Add($"{simplifiedNode.textStyle.fontSize:F0}px");
            }

            // 颜色（只显示主要颜色）
            if (simplifiedNode.backgroundColor?.hex != null &&
                simplifiedNode.backgroundColor.hex != "#FFFFFF" &&
                simplifiedNode.backgroundColor.hex != "#000000")
            {
                parts.Add(simplifiedNode.backgroundColor.hex);
            }

            // 特殊标记
            if (simplifiedNode.hasImage) parts.Add("📷");
            if (simplifiedNode.layout?.layoutMode == "HORIZONTAL") parts.Add("→");
            if (simplifiedNode.layout?.layoutMode == "VERTICAL") parts.Add("↓");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// 批量生成AI提示文本
        /// </summary>
        /// <param name="nodes">节点字典</param>
        /// <returns>AI友好的结构化文本</returns>
        public static string GenerateBatchAIPrompt(Dictionary<string, SimplifiedNode> nodes)
        {
            var result = new List<string>();

            foreach (var kvp in nodes) // 处理所有节点
            {
                var nodePrompt = GenerateAIPrompt(kvp.Value);
                result.Add($"• {nodePrompt}");

                // 显示重要子节点
                if (kvp.Value.children != null)
                {
                    var importantChildren = kvp.Value.children
                        .Where(child => !string.IsNullOrEmpty(child.text) || child.hasImage); // 显示所有重要子节点

                    foreach (var child in importantChildren)
                    {
                        var childPrompt = GenerateAIPrompt(child);
                        result.Add($"  ◦ {childPrompt}");
                    }
                }
            }

            return string.Join("\n", result);
        }

        #region UGUI锚点转换

        /// <summary>
        /// 将Figma节点转换为UGUI锚点信息
        /// </summary>
        /// <param name="figmaNode">原始Figma节点</param>
        /// <param name="node">简化节点</param>
        /// <param name="parentNode">父节点</param>
        public static void ConvertToUGUIAnchors(JToken figmaNode, SimplifiedNode node, SimplifiedNode parentNode = null)
        {
            if (figmaNode == null || node == null)
                return;

            // 从Figma节点提取位置和尺寸
            var absoluteBoundingBox = figmaNode["absoluteBoundingBox"];
            if (absoluteBoundingBox == null)
                return;

            float nodeWidth = (float)Math.Round(absoluteBoundingBox["width"]?.ToObject<float>() ?? 0, 2);
            float nodeHeight = (float)Math.Round(absoluteBoundingBox["height"]?.ToObject<float>() ?? 0, 2);
            float nodeX = (float)Math.Round(absoluteBoundingBox["x"]?.ToObject<float>() ?? 0, 2);
            float nodeY = (float)Math.Round(absoluteBoundingBox["y"]?.ToObject<float>() ?? 0, 2);

            // 父节点信息（从UGUI信息推断，如果没有则使用当前节点作为默认）
            float parentWidth = nodeWidth;
            float parentHeight = nodeHeight;
            float parentX = 0;
            float parentY = 0;

            if (parentNode?.sizeDelta != null)
            {
                parentWidth = parentNode.sizeDelta[0];
                parentHeight = parentNode.sizeDelta[1];
                // 父节点的世界位置需要从其锚点信息计算，这里简化处理
                parentX = parentNode.anchoredPos?[0] ?? 0;
                parentY = parentNode.anchoredPos?[1] ?? 0;
            }

            // 计算相对于父节点的位置
            float relativeX = nodeX - parentX;
            float relativeY = nodeY - parentY;

            // 设置sizeDelta（实际尺寸）
            node.sizeDelta = new float[] { nodeWidth, nodeHeight };

            // 直接从Figma节点提取约束信息
            var constraintsToken = figmaNode["constraints"];
            ConstraintInfo constraints = null;
            if (constraintsToken != null && constraintsToken.Type == JTokenType.Object)
            {
                constraints = new ConstraintInfo
                {
                    horizontal = constraintsToken["horizontal"]?.ToString(),
                    vertical = constraintsToken["vertical"]?.ToString()
                };
            }

            if (constraints != null)
            {
                CalculateAnchorsFromConstraints(constraints, relativeX, relativeY, nodeWidth, nodeHeight,
                    parentWidth, parentHeight, node);
            }
            else
            {
                // 默认锚点计算（基于位置推断）
                CalculateDefaultAnchors(relativeX, relativeY, nodeWidth, nodeHeight,
                    parentWidth, parentHeight, node);
            }

            // 设置默认轴心点
            node.pivot = new float[] { 0.5f, 0.5f };
        }

        /// <summary>
        /// 根据约束信息计算锚点
        /// </summary>
        private static void CalculateAnchorsFromConstraints(ConstraintInfo constraints,
            float relativeX, float relativeY, float nodeWidth, float nodeHeight,
            float parentWidth, float parentHeight, SimplifiedNode node)
        {
            // 水平锚点计算
            switch (constraints.horizontal)
            {
                case "LEFT":
                    node.anchorMin = new float[] { 0, node.anchorMin?[1] ?? 0.5f };
                    node.anchorMax = new float[] { 0, node.anchorMax?[1] ?? 0.5f };
                    node.anchoredPos = new float[] { relativeX + nodeWidth * 0.5f, node.anchoredPos?[1] ?? 0 };
                    break;

                case "RIGHT":
                    node.anchorMin = new float[] { 1, node.anchorMin?[1] ?? 0.5f };
                    node.anchorMax = new float[] { 1, node.anchorMax?[1] ?? 0.5f };
                    node.anchoredPos = new float[] { relativeX + nodeWidth * 0.5f - parentWidth, node.anchoredPos?[1] ?? 0 };
                    node.sizeDelta = new float[] { nodeWidth, node.sizeDelta?[1] ?? nodeHeight };
                    break;

                case "CENTER":
                    node.anchorMin = new float[] { 0.5f, node.anchorMin?[1] ?? 0.5f };
                    node.anchorMax = new float[] { 0.5f, node.anchorMax?[1] ?? 0.5f };
                    node.anchoredPos = new float[] { relativeX + nodeWidth * 0.5f - parentWidth * 0.5f, node.anchoredPos?[1] ?? 0 };
                    node.sizeDelta = new float[] { nodeWidth, node.sizeDelta?[1] ?? nodeHeight };
                    break;

                case "LEFT_RIGHT":
                case "SCALE":
                    node.anchorMin = new float[] { 0, node.anchorMin?[1] ?? 0.5f };
                    node.anchorMax = new float[] { 1, node.anchorMax?[1] ?? 0.5f };
                    // offsetMin和offsetMax已移除
                    node.sizeDelta = new float[] { 0, node.sizeDelta?[1] ?? nodeHeight };
                    break;

                default:
                    // 默认居中
                    node.anchorMin = new float[] { 0.5f, node.anchorMin?[1] ?? 0.5f };
                    node.anchorMax = new float[] { 0.5f, node.anchorMax?[1] ?? 0.5f };
                    node.anchoredPos = new float[] { relativeX + nodeWidth * 0.5f - parentWidth * 0.5f, node.anchoredPos?[1] ?? 0 };
                    node.sizeDelta = new float[] { nodeWidth, node.sizeDelta?[1] ?? nodeHeight };
                    break;
            }

            // 垂直锚点计算（Unity坐标系Y轴向上，Figma向下）
            switch (constraints.vertical)
            {
                case "TOP":
                    node.anchorMin = new float[] { node.anchorMin?[0] ?? 0.5f, 1 };
                    node.anchorMax = new float[] { node.anchorMax?[0] ?? 0.5f, 1 };
                    node.anchoredPos = new float[] { node.anchoredPos?[0] ?? 0, -relativeY - nodeHeight * 0.5f };
                    node.sizeDelta = new float[] { node.sizeDelta?[0] ?? nodeWidth, nodeHeight };
                    break;

                case "BOTTOM":
                    node.anchorMin = new float[] { node.anchorMin?[0] ?? 0.5f, 0 };
                    node.anchorMax = new float[] { node.anchorMax?[0] ?? 0.5f, 0 };
                    node.anchoredPos = new float[] { node.anchoredPos?[0] ?? 0, -relativeY - nodeHeight * 0.5f + parentHeight };
                    node.sizeDelta = new float[] { node.sizeDelta?[0] ?? nodeWidth, nodeHeight };
                    break;

                case "CENTER":
                    node.anchorMin = new float[] { node.anchorMin?[0] ?? 0.5f, 0.5f };
                    node.anchorMax = new float[] { node.anchorMax?[0] ?? 0.5f, 0.5f };
                    node.anchoredPos = new float[] { node.anchoredPos?[0] ?? 0, -relativeY - nodeHeight * 0.5f + parentHeight * 0.5f };
                    node.sizeDelta = new float[] { node.sizeDelta?[0] ?? nodeWidth, nodeHeight };
                    break;

                case "TOP_BOTTOM":
                case "SCALE":
                    node.anchorMin = new float[] { node.anchorMin?[0] ?? 0.5f, 0 };
                    node.anchorMax = new float[] { node.anchorMax?[0] ?? 0.5f, 1 };
                    // offsetMin和offsetMax已移除
                    node.sizeDelta = new float[] { node.sizeDelta?[0] ?? nodeWidth, 0 };
                    break;

                default:
                    // 默认居中
                    node.anchorMin = new float[] { node.anchorMin?[0] ?? 0.5f, 0.5f };
                    node.anchorMax = new float[] { node.anchorMax?[0] ?? 0.5f, 0.5f };
                    node.anchoredPos = new float[] { node.anchoredPos?[0] ?? 0, -relativeY - nodeHeight * 0.5f + parentHeight * 0.5f };
                    node.sizeDelta = new float[] { node.sizeDelta?[0] ?? nodeWidth, nodeHeight };
                    break;
            }

            // 设置默认轴心点
            node.pivot = new float[] { 0.5f, 0.5f };

            // 偏移值计算已移除，因为offsetMin和offsetMax是冗余的
        }

        /// <summary>
        /// 计算默认锚点（当没有约束信息时）
        /// </summary>
        private static void CalculateDefaultAnchors(float relativeX, float relativeY, float nodeWidth, float nodeHeight,
            float parentWidth, float parentHeight, SimplifiedNode node)
        {
            // 基于位置推断锚点类型
            float centerX = relativeX + nodeWidth * 0.5f;
            float centerY = relativeY + nodeHeight * 0.5f;

            // 计算相对位置比例
            float xRatio = centerX / parentWidth;
            float yRatio = centerY / parentHeight;

            // 判断水平锚点
            if (xRatio < 0.25f)
            {
                // 靠左
                node.anchorMin = new float[] { 0, 0.5f };
                node.anchorMax = new float[] { 0, 0.5f };
                node.anchoredPos = new float[] { centerX, 0 };
            }
            else if (xRatio > 0.75f)
            {
                // 靠右
                node.anchorMin = new float[] { 1, 0.5f };
                node.anchorMax = new float[] { 1, 0.5f };
                node.anchoredPos = new float[] { centerX - parentWidth, 0 };
            }
            else
            {
                // 居中
                node.anchorMin = new float[] { 0.5f, 0.5f };
                node.anchorMax = new float[] { 0.5f, 0.5f };
                node.anchoredPos = new float[] { centerX - parentWidth * 0.5f, 0 };
            }

            // 判断垂直锚点（转换坐标系）
            if (yRatio < 0.25f)
            {
                // 靠上（Unity坐标系）
                node.anchorMin[1] = 1;
                node.anchorMax[1] = 1;
                node.anchoredPos[1] = -centerY;
            }
            else if (yRatio > 0.75f)
            {
                // 靠下（Unity坐标系）
                node.anchorMin[1] = 0;
                node.anchorMax[1] = 0;
                node.anchoredPos[1] = parentHeight - centerY;
            }
            else
            {
                // 居中
                node.anchorMin[1] = 0.5f;
                node.anchorMax[1] = 0.5f;
                node.anchoredPos[1] = parentHeight * 0.5f - centerY;
            }

            node.sizeDelta = new float[] { nodeWidth, nodeHeight };
            node.pivot = new float[] { 0.5f, 0.5f };

            // CalculateOffsets方法已移除
        }


        /// <summary>
        /// 批量转换节点为UGUI锚点信息
        /// </summary>
        /// <param name="rootNode">根节点</param>
        public static void ConvertAllToUGUI(SimplifiedNode rootNode)
        {
            if (rootNode == null) return;

            // 为根节点设置默认UGUI信息
            if (rootNode.sizeDelta == null)
            {
                rootNode.anchorMin = new float[] { 0.5f, 0.5f };
                rootNode.anchorMax = new float[] { 0.5f, 0.5f };
                rootNode.anchoredPos = new float[] { 0, 0 };
                rootNode.sizeDelta = new float[] { 100, 100 };
                rootNode.pivot = new float[] { 0.5f, 0.5f };
            }

            // 递归处理子节点
            ConvertChildrenToUGUI(rootNode);
        }

        /// <summary>
        /// 递归转换子节点（这个方法已经不再使用，因为转换逻辑已经整合到SimplifyNode中）
        /// </summary>
        private static void ConvertChildrenToUGUI(SimplifiedNode parentNode)
        {
            // 这个方法保留是为了向后兼容，但实际上UGUI转换已经在SimplifyNode过程中完成
            // 不需要额外的递归处理
        }

        #endregion

        #region 下载判断逻辑

        /// <summary>
        /// 智能分析节点，判断是否需要下载为图片
        /// </summary>
        private static bool IsDownloadableNode(JToken node)
        {
            if (node == null) return false;

            string nodeType = node["type"]?.ToString();
            bool visible = node["visible"]?.ToObject<bool?>() ?? true;

            if (!visible) return false;

            // 1. 包含图片引用的节点
            if (HasImageRef(node))
            {
                return true;
            }

            // 2. Vector类型节点（矢量图形）
            if (nodeType == "VECTOR" || nodeType == "BOOLEAN_OPERATION")
            {
                return true;
            }

            // 3. 有填充且非简单颜色的节点
            if (HasComplexFills(node))
            {
                return true;
            }

            // 4. 有描边的节点
            if (HasStrokes(node))
            {
                return true;
            }

            // 5. 有效果的节点（阴影、模糊等）
            if (HasEffects(node))
            {
                return true;
            }

            // 6. 椭圆节点
            if (nodeType == "ELLIPSE")
            {
                return true;
            }

            // 7. 有圆角的矩形
            if (nodeType == "RECTANGLE" && HasRoundedCorners(node))
            {
                return true;
            }

            // 8. 复杂的Frame（包含多个子元素且有样式）
            if (nodeType == "FRAME" && IsComplexFrame(node))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查节点是否包含图片引用
        /// </summary>
        private static bool HasImageRef(JToken node)
        {
            var fills = node["fills"];
            if (fills != null)
            {
                foreach (var fill in fills)
                {
                    if (fill["type"]?.ToString() == "IMAGE" && fill["imageRef"] != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检查是否有复杂填充（渐变、图片等）
        /// </summary>
        private static bool HasComplexFills(JToken node)
        {
            var fills = node["fills"];
            if (fills != null)
            {
                foreach (var fill in fills)
                {
                    string fillType = fill["type"]?.ToString();
                    if (fillType == "GRADIENT_LINEAR" ||
                        fillType == "GRADIENT_RADIAL" ||
                        fillType == "GRADIENT_ANGULAR" ||
                        fillType == "IMAGE")
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检查是否有描边
        /// </summary>
        private static bool HasStrokes(JToken node)
        {
            var strokes = node["strokes"];
            return strokes != null && strokes.HasValues;
        }

        /// <summary>
        /// 检查是否有效果
        /// </summary>
        private static bool HasEffects(JToken node)
        {
            var effects = node["effects"];
            return effects != null && effects.HasValues;
        }

        /// <summary>
        /// 检查是否有圆角
        /// </summary>
        private static bool HasRoundedCorners(JToken node)
        {
            var cornerRadius = node["cornerRadius"];
            if (cornerRadius != null)
            {
                float radius = cornerRadius.ToObject<float>();
                return radius > 0;
            }

            var rectangleCornerRadii = node["rectangleCornerRadii"];
            if (rectangleCornerRadii != null)
            {
                foreach (var radius in rectangleCornerRadii)
                {
                    if (radius.ToObject<float>() > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查是否为复杂Frame
        /// </summary>
        private static bool IsComplexFrame(JToken node)
        {
            var children = node["children"];
            if (children == null || !children.HasValues)
                return false;

            // 如果Frame有背景色、效果或者包含多个不同类型的子元素，认为是复杂Frame
            if (HasComplexFills(node) || HasEffects(node) || HasStrokes(node))
                return true;

            // 检查子元素数量和类型多样性
            int childCount = children.Count();
            if (childCount > 3) // 超过3个子元素的复杂布局
                return true;

            return false;
        }

        /// <summary>
        /// 转换为UGUI后清理不必要的数据
        /// </summary>
        /// <param name="rootNode">根节点</param>
        private static void CleanupAfterUGUIConversion(SimplifiedNode rootNode)
        {
            if (rootNode == null) return;

            // 由于我们已经移除了position、size和transform字段，
            // 这里主要是为了保持方法的完整性，实际上不需要做太多清理

            // 递归处理子节点
            if (rootNode.children != null)
            {
                foreach (var child in rootNode.children)
                {
                    CleanupAfterUGUIConversion(child);
                }
            }
        }

        #endregion

        #region 使用示例和工具方法

        /// <summary>
        /// 获取节点的UGUI布局参数字符串（用于MCP调用）
        /// </summary>
        /// <param name="node">简化节点</param>
        /// <returns>UGUI布局参数</returns>
        public static string GetUGUILayoutParams(SimplifiedNode node)
        {
            if (node?.sizeDelta == null) return "";
            var parts = new List<string>();

            if (node.anchoredPos != null)
                parts.Add($"\"anchored_pos\": [{node.anchoredPos[0]:F2}, {node.anchoredPos[1]:F2}]");

            if (node.sizeDelta != null)
                parts.Add($"\"size_delta\": [{node.sizeDelta[0]:F2}, {node.sizeDelta[1]:F2}]");

            if (node.anchorMin != null)
                parts.Add($"\"anchor_min\": [{node.anchorMin[0]:F2}, {node.anchorMin[1]:F2}]");

            if (node.anchorMax != null)
                parts.Add($"\"anchor_max\": [{node.anchorMax[0]:F2}, {node.anchorMax[1]:F2}]");

            if (node.pivot != null)
                parts.Add($"\"pivot\": [{node.pivot[0]:F2}, {node.pivot[1]:F2}]");

            return "{" + string.Join(", ", parts) + "}";
        }

        /// <summary>
        /// 生成MCP布局调用代码
        /// </summary>
        /// <param name="node">简化节点</param>
        /// <param name="parentPath">父节点路径</param>
        /// <returns>MCP调用代码</returns>
        public static string GenerateMCPLayoutCall(SimplifiedNode node, string parentPath = "")
        {
            if (node?.sizeDelta == null) return "";

            string nodePath = string.IsNullOrEmpty(parentPath) ? node.name : $"{parentPath}/{node.name}";
            string layoutParams = GetUGUILayoutParams(node);

            return $"ugui_layout(path=\"{nodePath}\", action=\"do_layout\", {layoutParams.Trim('{', '}')})";
        }

        /// <summary>
        /// 批量生成所有节点的MCP布局调用代码
        /// </summary>
        /// <param name="rootNode">根节点</param>
        /// <param name="parentPath">父路径</param>
        /// <returns>MCP调用代码列表</returns>
        public static List<string> GenerateAllMCPLayoutCalls(SimplifiedNode rootNode, string parentPath = "")
        {
            var calls = new List<string>();

            if (rootNode == null) return calls;

            // 为当前节点生成调用
            var call = GenerateMCPLayoutCall(rootNode, parentPath);
            if (!string.IsNullOrEmpty(call))
            {
                calls.Add(call);
            }

            // 递归处理子节点
            if (rootNode.children != null)
            {
                string currentPath = string.IsNullOrEmpty(parentPath) ? rootNode.name : $"{parentPath}/{rootNode.name}";
                foreach (var child in rootNode.children)
                {
                    calls.AddRange(GenerateAllMCPLayoutCalls(child, currentPath));
                }
            }

            return calls;
        }

        /// <summary>
        /// 生成完整的MCP批量调用代码
        /// </summary>
        /// <param name="rootNode">根节点</param>
        /// <returns>完整的functions_call代码</returns>
        public static string GenerateBatchMCPCall(SimplifiedNode rootNode)
        {
            var calls = GenerateAllMCPLayoutCalls(rootNode);
            if (calls.Count == 0) return "";

            var funcCalls = calls.Select(call => $"{{\"func\": \"ugui_layout\", \"args\": {{{call.Substring(call.IndexOf('(') + 1).TrimEnd(')')}}}}}");

            return $"functions_call(funcs=[{string.Join(", ", funcCalls)}])";
        }

        #endregion
    }
}