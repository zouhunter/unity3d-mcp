"""
Unity层次结构应用工具，包含GameObject预制体应用和连接操作功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_hierarchy_apply_tools(mcp: FastMCP):
    @mcp.tool("hierarchy_apply")
    def hierarchy_apply(
        ctx: Context,
        action: Annotated[str, Field(
            title="应用操作类型",
            description="要执行的应用操作: apply(应用预制体)",
            examples=["apply"]
        )],
        target_object: Annotated[str, Field(
            title="目标对象",
            description="目标GameObject标识符（用于应用操作）",
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )],
        prefab_path: Annotated[Optional[str], Field(
            title="预制体路径",
            description="预制体路径",
            default=None,
            examples=["Assets/Prefabs/Player.prefab", "Prefabs/Enemy.prefab"]
        )] = None,
        apply_type: Annotated[Optional[str], Field(
            title="应用类型",
            description="应用类型: connect_to_prefab(连接到预制体), apply_prefab_changes(应用预制体更改), break_prefab_connection(断开预制体连接)",
            default="connect_to_prefab",
            examples=["connect_to_prefab", "apply_prefab_changes", "break_prefab_connection"]
        )] = "connect_to_prefab",
        force_apply: Annotated[Optional[bool], Field(
            title="强制应用",
            description="是否强制创建连接（覆盖现有连接）",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity层次结构应用工具，用于处理GameObject预制体应用和连接操作。（二级工具）

        支持多种预制体操作，适用于：
        - 预制体连接：将GameObject连接到预制体
        - 预制体应用：应用预制体的更改到实例
        - 连接断开：断开GameObject与预制体的连接
        - 强制应用：覆盖现有的预制体连接
        """
        
        return get_common_call_response("hierarchy_apply")
