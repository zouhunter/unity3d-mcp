"""
Unity层次结构搜索工具，包含GameObject的搜索功能。
"""
from typing import Annotated, Dict, Any
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_hierarchy_search_tools(mcp: FastMCP):
    @mcp.tool("hierarchy_search")
    def hierarchy_search(
        ctx: Context,
        query: Annotated[str, Field(
            title="搜索查询",
            description="要搜索的GameObject名称或名称模式",
            examples=["Player", "Enemy*", "*Camera", "UI_*"]
        )],
        search_type: Annotated[str, Field(
            title="搜索类型",
            description="搜索的类型: name(按名称), tag(按标签), component(按组件)",
            default="name",
            examples=["name", "tag", "component"]
        )] = "name",
        include_inactive: Annotated[bool, Field(
            title="包含非活动对象",
            description="是否在搜索结果中包含非活动的GameObject",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity层次结构搜索工具，用于查找场景中的GameObject。（二级工具）
        支持多种搜索方式，适用于：
        - 对象定位：快速找到特定名称的GameObject
        - 批量操作：查找所有符合条件的对象
        - 调试分析：检查场景中的对象状态
        - 自动化脚本：获取对象列表进行处理
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="hierarchy_search", args={"query": "Player", "search_type": "name"})
        
        return get_common_call_response("hierarchy_search")

