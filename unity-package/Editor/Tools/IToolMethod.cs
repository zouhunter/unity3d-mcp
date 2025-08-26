using Newtonsoft.Json.Linq;

namespace UnityMcp.Tools
{
    /// <summary>
    /// 工具方法接口，所有具体工具类都应实现此接口
    /// </summary>
    public interface IToolMethod
    {
        /// <summary>
        /// 执行工具方法
        /// </summary>
        /// <param name="args">参数对象</param>
        /// <returns>执行结果</returns>
        object ExecuteMethod(JObject args);
        /// <summary>
        /// 预览方法
        /// </summary>
        /// <returns>预览结果</returns>
        string Preview();
    }
}