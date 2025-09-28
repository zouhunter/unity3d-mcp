"""
Unity对象删除工具，包含GameObject、资源和其他Unity对象的删除功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_object_delete_tools(mcp: FastMCP):
    @mcp.tool("object_delete")
    def object_delete(
        ctx: Context,
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="要删除的对象的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Assets/Materials/OldMaterial.mat"]
        )] = None,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="要删除的对象的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        confirm: Annotated[Optional[bool], Field(
            title="强制确认",
            description="是否强制显示确认对话框：true=总是确认，false/unset=智能确认（≤3个对象自动删除，>3个显示对话框）",
            default=None
        )] = None
    ) -> Dict[str, Any]:
        """Unity对象删除工具，用于删除GameObject、资源和其他Unity对象。（二级工具）

        支持多种删除方式和智能确认机制，适用于：
        - 场景对象删除：删除场景中的GameObject
        - 资源删除：删除项目中的资源文件
        - 批量删除：删除多个对象
        - 安全删除：带确认机制的删除操作
        """
        
        return get_common_call_response("object_delete")
