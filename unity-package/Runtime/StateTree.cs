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

                if (!string.IsNullOrEmpty(cur.key) && ctx != null && ctx.TryGetValue(cur.key, out JToken token))
                {
                    keyToLookup = ConvertTokenToKey(token);
                }

                if (!cur.select.TryGetValue(keyToLookup, out var next) &&
                    !cur.select.TryGetValue(Default, out next))
                {
                    var supportedKeys = cur.select.Keys
                        .Where(k => k?.ToString() != Default)
                        .Select(k => k?.ToString() ?? "null")
                        .ToList();

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
    }
}