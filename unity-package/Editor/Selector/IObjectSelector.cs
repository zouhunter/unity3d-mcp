using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityMcp.Models;

namespace UnityMcp.Tools
{
    /// <summary>
    /// Unity对象选择器接口
    /// 定义统一的对象查找方法
    /// </summary>
    public interface IObjectSelector
    {
        /// <summary>
        /// 创建当前方法支持的参数键列表
        /// </summary>
        MethodKey[] CreateKeys();
        /// <summary>
        /// 构建对象查找状态树
        /// </summary>
        StateTree BuildStateTree();
    }
}
