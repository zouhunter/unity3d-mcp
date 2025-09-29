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
            description="搜索的类型: by_name(按名称), by_id(按ID), by_tag(按标签), by_layer(按层级), by_component(按组件), by_query(通用查询)",
            default="by_name",
            examples=["by_name", "by_id", "by_tag", "by_layer", "by_component", "by_query"]
        )] = "by_name",
        select_many: Annotated[bool, Field(
            title="查找多个匹配项",
            description="是否查找所有匹配的项目",
            default=True
        )] = True,
        include_hierarchy: Annotated[bool, Field(
            title="包含完整层级信息",
            description="是否包含所有子对象的完整层级数据",
            default=False
        )] = False,
        include_inactive: Annotated[bool, Field(
            title="包含非活动对象",
            description="是否在搜索结果中包含非活动的GameObject",
            default=False
        )] = False,
        use_regex: Annotated[bool, Field(
            title="使用正则表达式",
            description="是否使用正则表达式进行搜索",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity层次结构搜索工具，用于查找场景中的GameObject。（二级工具）
        
        支持多种搜索方式，适用于：
        - 对象定位：快速找到特定名称的GameObject
        - 批量操作：查找所有符合条件的对象
        - 调试分析：检查场景中的对象状态
        - 自动化脚本：获取对象列表进行处理
        - 层级结构：获取完整的父子关系数据
        
        新功能：
        - include_hierarchy: 获取完整的层级结构，包含所有子对象的完整数据
        - 支持多种搜索类型：按名称、ID、标签、层级、组件等
        - 支持正则表达式和通配符搜索
        - 默认搜索功能：当没有指定search_type时自动使用按名称搜索
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="hierarchy_search", args={"query": "Player", "search_type": "name"})
        
        return get_common_call_response("hierarchy_search")

