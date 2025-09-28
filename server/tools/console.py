"""
Unity控制台操作工具，包含控制台读取和写入功能。
"""
from typing import Annotated, List, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_console_tools(mcp: FastMCP):
    @mcp.tool("console_read")
    def console_read(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="控制台操作类型: get(获取日志-无堆栈跟踪), get_full(获取日志-包含堆栈跟踪), clear(清空控制台)",
            examples=["get", "get_full", "clear"]
        )],
        types: Annotated[Optional[List[str]], Field(
            title="消息类型列表",
            description="要获取的消息类型，可选择多种类型组合",
            default=None,
            examples=[
                ["error", "warning", "log"],
                ["error"],
                ["warning", "log"]
            ]
        )] = None,
        count: Annotated[Optional[int], Field(
            title="最大消息数",
            description="限制返回的消息数量，不设置则获取全部消息",
            default=None,
            ge=1,
            examples=[10, 50, 100]
        )] = None,
        filterText: Annotated[Optional[str], Field(
            title="文本过滤器",
            description="过滤包含指定文本的日志消息，支持模糊匹配",
            default=None,
            examples=["Error", "NullReference", "GameObject"]
        )] = None,
        format: Annotated[str, Field(
            title="输出格式",
            description="控制台输出格式类型",
            default="detailed",
            examples=["plain", "detailed", "json"]
        )] = "detailed"
    ) -> Dict[str, Any]:
        """Unity控制台读取工具，可以读取或清空Unity编辑器控制台消息。（二级工具）

        支持多种操作模式和灵活的过滤选项，适用于：
        - 调试信息收集：获取错误和警告消息
        - 日志分析：过滤特定内容的消息
        - 控制台管理：清空控制台历史记录

        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="console_read", args={"action": "get", "types": ["error", "warning"]})
        
        return get_common_call_response("console_read")

    @mcp.tool("console_write")
    def console_write(
        ctx: Context,
        action: Annotated[str, Field(
            title="日志类型",
            description="要写入的日志消息类型，不同类型在Unity控制台中有不同的显示效果和颜色",
            examples=["error", "warning", "log", "assert", "exception"]
        )],
        message: Annotated[str, Field(
            title="日志消息",
            description="要写入到Unity控制台的具体消息内容",
            examples=[
                "GameObject not found",
                "Player health is low",
                "Loading scene completed",
                "NullReferenceException occurred"
            ]
        )],
        tag: Annotated[Optional[str], Field(
            title="日志标签",
            description="用于分类和过滤日志的标签，便于在控制台中筛选相关消息",
            default=None,
            examples=["Player", "GameManager", "UI", "Network"]
        )] = None,
        context: Annotated[Optional[str], Field(
            title="上下文对象",
            description="相关的GameObject名称，点击日志时可在Hierarchy中高亮显示该对象",
            default=None,
            examples=["Player", "MainCamera", "Canvas", "GameManager"]
        )] = None,
        condition: Annotated[Optional[str], Field(
            title="断言条件",
            description="断言条件表达式，仅在action为'assert'时使用，用于描述断言失败的条件",
            default=None,
            examples=["health > 0", "player != null", "lives >= 0"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity控制台写入工具，可以向Unity编辑器控制台写入不同类型的日志消息。

        支持多种日志级别和丰富的标签功能，适用于：
        - 调试输出：记录程序运行状态和变量值
        - 错误报告：输出异常和错误信息
        - 性能监控：记录关键操作的时间和状态
        - 用户反馈：显示游戏状态和提示信息
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="console_write", args={"action": "log", "message": "Hello Unity!"})
        
        return get_common_call_response("console_write")
