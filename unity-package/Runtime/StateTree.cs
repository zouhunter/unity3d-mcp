using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using System.Text;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    public class StateTree
    {
        public string key;                         // 当前层变量
        public Dictionary<object, StateTree> select = new();
        public HashSet<string> optionalParams = new(); // 存储可选参数的key
        public Func<JObject, object> func;     // 叶子函数
        public const string Default = "*";          // 通配标识
        public string ErrorMessage;//执行错误信息

        /* 隐式转换：Action → 叶子节点 */
        public static implicit operator StateTree(Func<JObject, object> a) => new() { func = a };

        /* 运行：沿树唯一路径（JObject 上下文） */
        public object Run(JObject ctx)
        {
            var cur = this;
            while (cur.func == null)
            {
                object keyToLookup = Default;
                StateTree next = null;

                // 首先检查是否有常规的key匹配
                if (!string.IsNullOrEmpty(cur.key) && ctx != null && ctx.TryGetValue(cur.key, out JToken token))
                {
                    keyToLookup = ConvertTokenToKey(token);
                    cur.select.TryGetValue(keyToLookup, out next);
                }

                // 如果没有找到常规匹配，检查可选参数
                if (next == null && ctx != null)
                {
                    // 查找所有可选参数键
                    foreach (var kvp in cur.select)
                    {
                        if (kvp.Key == null) continue; // 跳过null键

                        string key = kvp.Key.ToString();
                        if (!string.IsNullOrEmpty(key) && cur.optionalParams.Contains(key))
                        {
                            // 检查参数是否存在且不为空
                            if (ctx.TryGetValue(key, out JToken paramToken) &&
                                paramToken != null &&
                                paramToken.Type != JTokenType.Null &&
                                !string.IsNullOrEmpty(paramToken.ToString()))
                            {
                                next = kvp.Value;
                                break; // 找到第一个匹配的可选参数就使用它
                            }
                        }
                    }
                }

                // 如果还是没有找到，尝试默认分支
                if (next == null && !cur.select.TryGetValue(Default, out next))
                {
                    var supportedKeys = cur.select.Keys
                        .Where(k => k?.ToString() != Default && !(cur.optionalParams.Contains(k?.ToString())))
                        .Select(k => k?.ToString() ?? "null")
                        .ToList();

                    // 添加可选参数到支持的键列表
                    var optionalKeys = cur.select.Keys
                        .Where(k => k != null && cur.optionalParams.Contains(k.ToString()))
                        .Select(k => k.ToString() + " (optional)")
                        .ToList();

                    supportedKeys.AddRange(optionalKeys);

                    string supportedKeysList = supportedKeys.Count > 0
                        ? string.Join(", ", supportedKeys)
                        : "none";

                    ErrorMessage = $"Invalid value '{keyToLookup}' for key '{cur.key}'. Supported values: [{supportedKeysList}]";
                    return null;
                }
                cur = next;
            }
            return cur.func?.Invoke(ctx);
        }

        private static object ConvertTokenToKey(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return Default;

            if (token.Type == JTokenType.Integer)
            {
                long longVal = token.Value<long>();
                if (longVal <= int.MaxValue && longVal >= int.MinValue)
                {
                    return (int)longVal;
                }
                return longVal;
            }

            if (token.Type == JTokenType.Float)
            {
                return token.Value<double>();
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            if (token.Type == JTokenType.String)
            {
                return token.Value<string>();
            }

            if (token is JValue jv && jv.Value != null)
            {
                return jv.Value;
            }

            return token.ToString();
        }

        /* 美化打印（Unicode 框线） */
        public void Print(StringBuilder sb, string indent = "", bool last = true, string parentEdgeLabel = null)
        {
            // 根节点：打印标题
            if (string.IsNullOrEmpty(indent))
            {
                sb.AppendLine($"{indent}StateTree");
            }

            // 若当前节点有 key，打印一次节点 key（避免与父边标签重复）
            string edgesIndent = indent;
            if (!string.IsNullOrEmpty(key) && key != parentEdgeLabel)
            {
                sb.AppendLine($"{indent}└─ {key}:");
                edgesIndent = indent + "   ";
            }

            // 枚举并打印当前节点的边（entry.Key 为边标签）
            var entries = select.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isLastChild = i == entries.Count - 1;
                string connector = isLastChild ? "└─" : "├─";
                string label = entry.Key?.ToString() == Default ? "*" : entry.Key?.ToString();
                
                // 如果是可选参数，添加(option)标识
                if (!string.IsNullOrEmpty(label) && optionalParams.Contains(label))
                {
                    label = label + "(option)";
                }

                if (entry.Value.func != null)
                {
                    string actionName = entry.Value.func.Method?.Name ?? "Anonymous";
                    sb.AppendLine($"{edgesIndent}{connector} {label} → {actionName}");
                }
                else
                {
                    // 打印边标签
                    sb.AppendLine($"{edgesIndent}{connector} {label}");
                    // 递归到子节点；如果子节点的 key 与边标签不同，则在子层级打印该 key
                    string nextIndent = edgesIndent + (isLastChild ? "   " : "│  ");
                    entry.Value.Print(sb, nextIndent, isLastChild, label);
                }
            }
        }
        /// <summary>
        /// 重写ToString方法，用于打印状态树
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            Print(sb);
            return sb.ToString();
        }
    }
}