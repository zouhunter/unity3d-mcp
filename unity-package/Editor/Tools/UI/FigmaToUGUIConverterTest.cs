using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Figma到UGUI转换器测试脚本
    /// </summary>
    public static class FigmaToUGUIConverterTest
    {
        [MenuItem("Unity MCP/Tools/Test Figma to UGUI Converter")]
        public static void RunTest()
        {
            try
            {
                // 查找原始数据文件
                string[] possiblePaths = {
                    "Assets/FigmaAssets/original_nodes_QpRcCoIvLt6If1TikSTVj1_20250925_185453.txt",
                    "Assets/FigmaAssets/original_nodes_QpRcCoIvLt6If1TikSTVj1_20250925_185453.json",
                    "Assets/FigmaAssets/nodes_QpRcCoIvLt6If1TikSTVj1_20250925_185453.txt",
                    "Assets/FigmaAssets/nodes_QpRcCoIvLt6If1TikSTVj1_20250925_185453.json"
                };

                string filePath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        filePath = path;
                        break;
                    }
                }

                if (filePath == null)
                {
                    // 尝试查找任何包含该文件key的文件
                    var files = Directory.GetFiles("Assets/FigmaAssets", "*QpRcCoIvLt6If1TikSTVj1*", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        filePath = files[0];
                        Debug.Log($"找到文件: {filePath}");
                    }
                    else
                    {
                        Debug.LogError("未找到Figma数据文件，请确保文件存在于Assets/FigmaAssets目录下");
                        return;
                    }
                }

                Debug.Log($"正在加载文件: {filePath}");

                // 读取文件内容
                string jsonContent = File.ReadAllText(filePath);

                // 解析JSON
                JObject figmaData = JObject.Parse(jsonContent);

                // 查找节点数据
                JToken nodeData = FindNodeData(figmaData);
                if (nodeData == null)
                {
                    Debug.LogError("未找到有效的节点数据");
                    return;
                }

                Debug.Log("开始转换Figma数据到UGUI...");
                Debug.Log($"节点数据类型: {nodeData.Type}");
                if (nodeData.Type == JTokenType.Object)
                {
                    Debug.Log($"节点属性: {string.Join(", ", ((JObject)nodeData).Properties().Select(p => p.Name))}");
                }

                // 使用FigmaDataSimplifier进行转换，保留布局信息
                var simplifiedNode = FigmaDataSimplifier.SimplifyNode(nodeData, -1, true, true);

                if (simplifiedNode == null)
                {
                    Debug.LogError("转换失败");
                    return;
                }

                // 输出转换结果
                OutputResults(simplifiedNode);

                // 保存转换结果
                SaveResults(simplifiedNode, filePath);

                Debug.Log("转换完成！");
            }
            catch (Exception ex)
            {
                Debug.LogError($"转换过程中发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 查找节点数据
        /// </summary>
        private static JToken FindNodeData(JObject figmaData)
        {
            // 尝试不同的数据结构
            var possiblePaths = new string[]
            {
                "nodes.27:1386.document",
                "27:1386.document",
                "nodes.27-1386.document",
                "27-1386.document",
                "document",
                "nodes"
            };

            foreach (var path in possiblePaths)
            {
                var token = figmaData.SelectToken(path);
                if (token != null)
                {
                    Debug.Log($"找到节点数据路径: {path}");
                    return token;
                }
            }

            // 如果没有找到，尝试查找第一个包含document的节点
            foreach (var property in figmaData.Properties())
            {
                if (property.Value is JObject obj && obj.ContainsKey("document"))
                {
                    Debug.Log($"找到节点数据: {property.Name}.document");
                    return obj["document"];
                }
            }

            return null;
        }

        /// <summary>
        /// 输出转换结果到控制台
        /// </summary>
        private static void OutputResults(FigmaDataSimplifier.SimplifiedNode rootNode)
        {
            Debug.Log("=== Figma到UGUI转换结果 ===");
            Debug.Log($"根节点: {rootNode.name} ({rootNode.type})");
            if (rootNode.sizeDelta != null)
                Debug.Log($"UGUI尺寸: {rootNode.sizeDelta[0]}x{rootNode.sizeDelta[1]}");

            if (rootNode.sizeDelta != null)
            {
                Debug.Log("--- UGUI锚点信息 ---");
                if (rootNode.anchoredPos != null)
                    Debug.Log($"锚点位置: [{rootNode.anchoredPos[0]:F2}, {rootNode.anchoredPos[1]:F2}]");
                if (rootNode.sizeDelta != null)
                    Debug.Log($"尺寸增量: [{rootNode.sizeDelta[0]:F2}, {rootNode.sizeDelta[1]:F2}]");
                if (rootNode.anchorMin != null)
                    Debug.Log($"最小锚点: [{rootNode.anchorMin[0]:F2}, {rootNode.anchorMin[1]:F2}]");
                if (rootNode.anchorMax != null)
                    Debug.Log($"最大锚点: [{rootNode.anchorMax[0]:F2}, {rootNode.anchorMax[1]:F2}]");
                if (rootNode.pivot != null)
                    Debug.Log($"轴心点: [{rootNode.pivot[0]:F2}, {rootNode.pivot[1]:F2}]");
            }

            // 输出子节点信息
            if (rootNode.children != null && rootNode.children.Count > 0)
            {
                Debug.Log($"\n--- 子节点 ({rootNode.children.Count}个) ---");
                OutputChildrenInfo(rootNode.children, 1);
            }

            // 生成MCP调用代码
            Debug.Log("\n=== MCP批量布局调用代码 ===");
            var mcpCalls = FigmaDataSimplifier.GenerateAllMCPLayoutCalls(rootNode);
            foreach (var call in mcpCalls.Take(10)) // 只显示前10个
            {
                Debug.Log(call);
            }

            if (mcpCalls.Count > 10)
            {
                Debug.Log($"... 还有 {mcpCalls.Count - 10} 个调用");
            }

            // 生成批量调用代码
            Debug.Log("\n=== 完整批量调用代码 ===");
            var batchCall = FigmaDataSimplifier.GenerateBatchMCPCall(rootNode);
            if (batchCall.Length > 2000)
            {
                Debug.Log(batchCall.Substring(0, 2000) + "...[截断]");
            }
            else
            {
                Debug.Log(batchCall);
            }
        }

        /// <summary>
        /// 输出子节点信息
        /// </summary>
        private static void OutputChildrenInfo(List<FigmaDataSimplifier.SimplifiedNode> children, int depth)
        {
            if (depth > 3) return; // 限制深度避免输出过多

            string indent = new string(' ', depth * 2);

            foreach (var child in children.Take(5)) // 只显示前5个子节点
            {
                string info = $"{indent}• {child.name} ({child.type})";
                if (child.sizeDelta != null)
                    info += $" {child.sizeDelta[0]:F0}x{child.sizeDelta[1]:F0}";
                if (!string.IsNullOrEmpty(child.text))
                    info += $" \"{child.text.Substring(0, Math.Min(20, child.text.Length))}\"";
                if (child.hasImage)
                    info += " 📷";

                Debug.Log(info);

                if (child.sizeDelta != null && depth <= 2)
                {
                    if (child.anchoredPos != null)
                        Debug.Log($"{indent}  锚点: [{child.anchoredPos[0]:F1}, {child.anchoredPos[1]:F1}]");
                }

                if (child.children != null && child.children.Count > 0 && depth < 3)
                {
                    OutputChildrenInfo(child.children, depth + 1);
                }
            }

            if (children.Count > 5)
            {
                Debug.Log($"{indent}... 还有 {children.Count - 5} 个子节点");
            }
        }

        /// <summary>
        /// 保存转换结果到文件
        /// </summary>
        private static void SaveResults(FigmaDataSimplifier.SimplifiedNode rootNode, string originalFilePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(originalFilePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);

                // 保存简化的JSON
                string simplifiedPath = Path.Combine(directory, $"{fileNameWithoutExt}_simplified_with_ugui.json");
                string simplifiedJson = FigmaDataSimplifier.ToCompactJson(rootNode, true);
                File.WriteAllText(simplifiedPath, simplifiedJson);
                Debug.Log($"简化数据已保存到: {simplifiedPath}");

                // 保存MCP调用代码
                string mcpCallsPath = Path.Combine(directory, $"{fileNameWithoutExt}_mcp_calls.txt");
                var mcpCalls = FigmaDataSimplifier.GenerateAllMCPLayoutCalls(rootNode);
                string mcpContent = "// 单独的MCP调用\n" + string.Join("\n", mcpCalls) + "\n\n";
                mcpContent += "// 批量调用代码\n" + FigmaDataSimplifier.GenerateBatchMCPCall(rootNode);
                File.WriteAllText(mcpCallsPath, mcpContent);
                Debug.Log($"MCP调用代码已保存到: {mcpCallsPath}");

                // 保存节点摘要
                string summaryPath = Path.Combine(directory, $"{fileNameWithoutExt}_summary.txt");
                string summary = GenerateDetailedSummary(rootNode);
                File.WriteAllText(summaryPath, summary);
                Debug.Log($"详细摘要已保存到: {summaryPath}");

                // 刷新AssetDatabase
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"保存结果时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成详细摘要
        /// </summary>
        private static string GenerateDetailedSummary(FigmaDataSimplifier.SimplifiedNode rootNode)
        {
            var summary = new System.Text.StringBuilder();

            summary.AppendLine("=== Figma到UGUI转换详细摘要 ===");
            summary.AppendLine($"转换时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine();

            summary.AppendLine($"根节点: {rootNode.name} ({rootNode.type})");
            if (rootNode.sizeDelta != null)
                summary.AppendLine($"UGUI尺寸: {rootNode.sizeDelta[0]}x{rootNode.sizeDelta[1]}");

            if (rootNode.sizeDelta != null)
            {
                summary.AppendLine("UGUI锚点信息:");
                AppendUGUIInfo(summary, rootNode, "  ");
            }

            summary.AppendLine();
            summary.AppendLine("=== 节点层级结构 ===");
            AppendNodeHierarchy(summary, rootNode, 0);

            summary.AppendLine();
            summary.AppendLine("=== 统计信息 ===");
            var stats = CalculateStats(rootNode);
            summary.AppendLine($"总节点数: {stats.totalNodes}");
            summary.AppendLine($"文本节点数: {stats.textNodes}");
            summary.AppendLine($"图片节点数: {stats.imageNodes}");
            summary.AppendLine($"最大深度: {stats.maxDepth}");

            return summary.ToString();
        }

        /// <summary>
        /// 添加UGUI信息到摘要
        /// </summary>
        private static void AppendUGUIInfo(System.Text.StringBuilder summary, FigmaDataSimplifier.SimplifiedNode node, string indent)
        {
            if (node.anchoredPos != null)
                summary.AppendLine($"{indent}锚点位置: [{node.anchoredPos[0]:F2}, {node.anchoredPos[1]:F2}]");
            if (node.sizeDelta != null)
                summary.AppendLine($"{indent}尺寸增量: [{node.sizeDelta[0]:F2}, {node.sizeDelta[1]:F2}]");
            if (node.anchorMin != null)
                summary.AppendLine($"{indent}最小锚点: [{node.anchorMin[0]:F2}, {node.anchorMin[1]:F2}]");
            if (node.anchorMax != null)
                summary.AppendLine($"{indent}最大锚点: [{node.anchorMax[0]:F2}, {node.anchorMax[1]:F2}]");
            if (node.pivot != null)
                summary.AppendLine($"{indent}轴心点: [{node.pivot[0]:F2}, {node.pivot[1]:F2}]");
        }

        /// <summary>
        /// 添加节点层级到摘要
        /// </summary>
        private static void AppendNodeHierarchy(System.Text.StringBuilder summary, FigmaDataSimplifier.SimplifiedNode node, int depth)
        {
            if (depth > 4) return; // 限制深度

            string indent = new string(' ', depth * 2);
            string info = $"{indent}• {node.name} ({node.type})";

            if (node.sizeDelta != null)
                info += $" {node.sizeDelta[0]:F0}x{node.sizeDelta[1]:F0}";
            if (!string.IsNullOrEmpty(node.text))
                info += $" \"{node.text.Substring(0, Math.Min(30, node.text.Length))}\"";
            if (node.hasImage)
                info += " 📷";

            summary.AppendLine(info);

            if (node.children != null)
            {
                foreach (var child in node.children.Take(10)) // 限制显示数量
                {
                    AppendNodeHierarchy(summary, child, depth + 1);
                }

                if (node.children.Count > 10)
                {
                    summary.AppendLine($"{indent}  ... 还有 {node.children.Count - 10} 个子节点");
                }
            }
        }

        /// <summary>
        /// 计算统计信息
        /// </summary>
        private static (int totalNodes, int textNodes, int imageNodes, int maxDepth) CalculateStats(FigmaDataSimplifier.SimplifiedNode node)
        {
            return CalculateStatsRecursive(node, 0);
        }

        private static (int totalNodes, int textNodes, int imageNodes, int maxDepth) CalculateStatsRecursive(FigmaDataSimplifier.SimplifiedNode node, int currentDepth)
        {
            int totalNodes = 1;
            int textNodes = !string.IsNullOrEmpty(node.text) ? 1 : 0;
            int imageNodes = node.hasImage ? 1 : 0;
            int maxDepth = currentDepth;

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    var childStats = CalculateStatsRecursive(child, currentDepth + 1);
                    totalNodes += childStats.totalNodes;
                    textNodes += childStats.textNodes;
                    imageNodes += childStats.imageNodes;
                    maxDepth = Math.Max(maxDepth, childStats.maxDepth);
                }
            }

            return (totalNodes, textNodes, imageNodes, maxDepth);
        }
    }
}
