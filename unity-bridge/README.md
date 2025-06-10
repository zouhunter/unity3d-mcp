# Unity-MCP（Unity上下文协议工具）
轻量级、可扩展的 Unity 编辑器上下文通信工具

支持插件间、Unity与外部服务的高效通信，提升自动化与开发效率
## 功能
支持插件内外部通信，通过协议规范化插件行为调用

支持Unity与外部服务（如MCP Server）的桥接通信

提供菜单注册、方法执行、数据读取等标准工具

支持多端消息分发机制（本地运行、远程服务器均可适配）

支持扩展式 Tool 注册机制，方便构建自动化开发流水线

兼容Unity 编辑器各版本，支持 .NET Framework 4.x 脚本运行时

## 快速使用
引入 UnityMCP 插件至你的 Unity 项目（推荐放入 Assets/Plugins/UnityMCP/ 目录）

在代码中注册自定义 Tool：

csharp
复制
编辑
using mcp.server.fastmcp;
using unity_connection;

public class MCPRegister
{
    public static void Register(FastMCP mcp)
    {
        mcp.Tool("hello", async (ctx, param) =>
        {
            return "Hello, MCP!";
        });
    }
}
在 Unity 编辑器中启动 MCP Server：

csharp
复制
编辑
FastMCPServer.StartServer(MCPRegister.Register);
可使用外部脚本或插件通过 WebSocket/HTTP 调用 MCP 指令（例如：自动构建、版本打包等）

## 示例：注册菜单调用工具
csharp
复制
编辑
mcp.Tool("execute_menu_item", async (ctx, param) =>
{
    string menuPath = param["menu_path"];
    UnityEditor.EditorApplication.ExecuteMenuItem(menuPath);
    return $"Executed: {menuPath}";
});
与外部服务集成方式
支持通过 WebSocket 快速对接 Web IDE、CI 工具或远程编辑器服务

可与本地 Node/Python 脚本协同，远程触发 Unity 编辑器事件

支持与 Git、版本管理工具联动（如自动打包并提交变更）

## @已对接系统
平台名称	描述	版本
Matey	Matey 项目	支持所有版本
Unity 编辑器	C#/编辑器扩展环境	Unity 2018.4+ / 2020+
.NET	C# 脚本引擎	.NET Framework 4.x

## 项目目录结构建议
markdown
复制
编辑
Assets/
└── Plugins/
    └── UnityMCP/
        ├── MCPServer.cs
        ├── ToolRegistry.cs
        ├── unity_connection.cs
        └── README.md
## 开发建议
所有 Tool 均建议使用异步方法，避免阻塞主线程

使用命名规范（如 tool_name:sub_command）避免 Tool 名冲突

Tool 返回值建议为 JSON 格式，便于调试与日志记录

