using System;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 用于标记工具方法类的名称的特性
    /// 优先级高于通过类名自动转换的snake_case命名
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ToolNameAttribute : Attribute
    {
        /// <summary>
        /// 工具方法的名称
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// 初始化 ToolNameAttribute
        /// </summary>
        /// <param name="toolName">工具方法的名称，通常使用snake_case格式</param>
        public ToolNameAttribute(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

            ToolName = toolName;
        }
    }
}
