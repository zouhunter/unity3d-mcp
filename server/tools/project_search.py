"""
Unity项目搜索工具，包含项目资源搜索功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_project_search_tools(mcp: FastMCP):
    @mcp.tool("project_search")
    def project_search(
        ctx: Context,
        search_target: Annotated[str, Field(
            title="搜索目标类型",
            description="要搜索的目标类型: asset(资源文件), script(脚本文件), scene(场景文件), prefab(预制体), material(材质), texture(纹理)",
            examples=["asset", "script", "scene", "prefab", "material", "texture"]
        )],
        query: Annotated[str, Field(
            title="搜索查询",
            description="搜索关键词或文件名模式",
            examples=["Player*", "*.cs", "MainScene", "UI_*"]
        )],
        folder: Annotated[Optional[str], Field(
            title="搜索文件夹",
            description="限制搜索范围的文件夹路径，留空搜索整个项目",
            default=None,
            examples=["Assets/Scripts", "Assets/Prefabs", "Assets/Scenes"]
        )] = None,
        include_packages: Annotated[bool, Field(
            title="包含包文件",
            description="是否在搜索结果中包含Packages目录下的文件",
            default=False
        )] = False,
        case_sensitive: Annotated[bool, Field(
            title="区分大小写",
            description="搜索时是否区分大小写",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity项目搜索工具，用于在项目中搜索各种类型的资源和文件。（二级工具）

        支持多种搜索类型和过滤条件，适用于：
        - 快速定位：找到特定名称的资源文件
        - 批量处理：获取同类型文件列表进行批量操作
        - 项目清理：查找未使用或重复的资源
        - 依赖分析：查找资源之间的引用关系

        """
        
        return get_common_call_response("project_search")
