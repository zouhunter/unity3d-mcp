"""
Unity GameObject编辑工具，包含GameObject的创建、修改、组件管理等功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_gameobject_tools(mcp: FastMCP):
    @mcp.tool("edit_gameobject")
    def edit_gameobject(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="操作类型: create(创建), modify(修改), get_components(获取组件), add_component(添加组件), remove_component(移除组件), set_parent(设置父对象)",
            examples=["create", "modify", "get_components", "add_component", "remove_component", "set_parent"]
        )],
        path: Annotated[Optional[str], Field(
            title="对象路径",
            description="GameObject的层次结构路径",
            default=None,
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )] = None,
        instance_id: Annotated[Optional[int], Field(
            title="实例ID",
            description="GameObject的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        name: Annotated[Optional[str], Field(
            title="GameObject名称",
            description="GameObject的名称",
            default=None,
            examples=["NewObject", "Player", "Enemy"]
        )] = None,
        tag: Annotated[Optional[str], Field(
            title="标签",
            description="GameObject的标签",
            default=None,
            examples=["Player", "Enemy", "Untagged"]
        )] = None,
        layer: Annotated[Optional[int], Field(
            title="图层",
            description="GameObject的图层",
            default=None,
            examples=[0, 8, 10]
        )] = None,
        parent_id: Annotated[Optional[int], Field(
            title="父对象ID",
            description="父对象的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        parent_path: Annotated[Optional[str], Field(
            title="父对象路径",
            description="父对象的层次结构路径",
            default=None,
            examples=["Canvas", "Player", "Environment"]
        )] = None,
        position: Annotated[Optional[List[float]], Field(
            title="位置",
            description="GameObject的位置坐标 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [1.5, 2.0, -3.0]]
        )] = None,
        rotation: Annotated[Optional[List[float]], Field(
            title="旋转",
            description="GameObject的旋转角度 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [0, 90, 0]]
        )] = None,
        scale: Annotated[Optional[List[float]], Field(
            title="缩放",
            description="GameObject的缩放比例 [x, y, z]",
            default=None,
            examples=[[1, 1, 1], [2.0, 2.0, 2.0]]
        )] = None,
        component_type: Annotated[Optional[str], Field(
            title="组件类型",
            description="要添加或移除的组件类型名称",
            default=None,
            examples=["Rigidbody", "BoxCollider", "AudioSource", "Light"]
        )] = None,
        active: Annotated[Optional[bool], Field(
            title="激活状态",
            description="GameObject的激活状态",
            default=None
        )] = None,
        static_flags: Annotated[Optional[int], Field(
            title="静态标志",
            description="GameObject的静态标志",
            default=None,
            examples=[0, 1, 2, 4]
        )] = None
    ) -> Dict[str, Any]:
        """Unity GameObject编辑工具，用于创建、修改和管理GameObject。（二级工具）

        支持多种GameObject操作，适用于：
        - 对象创建：创建新的GameObject
        - 属性修改：修改GameObject的基本属性
        - 组件管理：添加、移除和获取组件
        - 层次结构：设置父子关系
        - 变换操作：设置位置、旋转、缩放
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="edit_gameobject", args={"path": "Player", "action": "modify"})
        
        return get_common_call_response("edit_gameobject")
