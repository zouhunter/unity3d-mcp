using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using System.Text;

namespace UnityMcp.Tools
{
    public class StateTree
    {
        public string key;                         // 当前层变量
        public Dictionary<object, StateTree> select = new();
        public Action Act;                         // 叶子函数
        public const string Default = "*";          // 通配标识

        /* 隐式转换：Action → 叶子节点 */
        public static implicit operator StateTree(Action a) => new() { Act = a };

        /* 运行：沿树唯一路径 */
        public void Run(IReadOnlyDictionary<string, object> ctx)
        {
            var cur = this;
            while (cur.Act == null)
            {
                if (!cur.select.TryGetValue(ctx.TryGetValue(cur.key!, out var v) ? v! : Default, out var next) &&
                    !cur.select.TryGetValue(Default, out next))
                    break;
                cur = next;
            }
            cur.Act?.Invoke();
        }

        /* 美化打印（Unicode 框线） */
        public void Print(StringBuilder sb, string indent = "", bool last = true)
        {
            // 打印当前节点
            if (!string.IsNullOrEmpty(key))
            {
                sb.AppendLine($"{indent}└─ {key}");
            }
            else
            {
                sb.AppendLine($"{indent}StateTree");
            }

            // 获取所有子节点
            var entries = select.ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                bool isLastChild = i == entries.Count - 1;

                // 构建新的缩进
                string newIndent = indent + (last ? "   " : "│  ");

                // 打印子节点
                if (entry.Value.Act != null)
                {
                    // 叶子节点（有Action）
                    string actionName = entry.Value.Act.Method?.Name ?? "Anonymous";
                    string connector = isLastChild ? "└─" : "├─";

                    if (entry.Key.ToString() == Default)
                    {
                        sb.AppendLine($"{newIndent}{connector} * → {actionName}");
                    }
                    else
                    {
                        sb.AppendLine($"{newIndent}{connector} {entry.Key} → {actionName}");
                    }
                }
                else
                {
                    // 非叶子节点（有子节点）
                    string connector = isLastChild ? "└─" : "├─";

                    if (entry.Key.ToString() == Default)
                    {
                        sb.AppendLine($"{newIndent}{connector} *");
                    }
                    else
                    {
                        sb.AppendLine($"{newIndent}{connector} {entry.Key}");
                    }

                    // 递归打印子节点
                    entry.Value.Print(sb, newIndent, isLastChild);
                }
            }
        }
    }
}