"""
Unity组件编辑工具，包含组件的获取、设置属性等功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_component_tools(mcp: FastMCP):
    @mcp.tool("edit_component")
    def edit_component(
        ctx: Context,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="GameObject的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="GameObject的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )] = None,
        action: Annotated[Optional[str], Field(
            title="操作类型",
            description="操作类型: get_component_propertys(获取组件属性), set_component_propertys(设置组件属性)",
            default="get_component_propertys",
            examples=["get_component_propertys", "set_component_propertys"]
        )] = "get_component_propertys",
        component_type: Annotated[Optional[str], Field(
            title="组件类型",
            description="组件类型名称（继承自Component的类型名称）",
            default=None,
            examples=["Rigidbody", "BoxCollider", "AudioSource", "Light", "Transform"]
        )] = None,
        properties: Annotated[Optional[Dict[str, Any]], Field(
            title="属性字典",
            description="要设置的属性字典（用于set_component_propertys操作）",
            default=None,
            examples=[
                {"mass": 2.0, "drag": 0.5},
                {"volume": 0.8, "pitch": 1.2},
                {"color": [1.0, 0.0, 0.0, 1.0]}
            ]
        )] = None
    ) -> Dict[str, Any]:
        """Unity组件编辑工具，用于获取和设置GameObject组件的属性。（二级工具）

        支持多种组件操作，适用于：
        - 属性获取：获取组件的所有属性值
        - 属性设置：设置组件的特定属性值
        - 组件查询：查找特定类型的组件
        - 批量操作：同时设置多个属性
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档

        
        # 实际调用请使用 single_call 函数

        
        # 示例：single_call(func="edit_component", args={...})

        
        

        
        return get_common_call_response("edit_component")
