"""
Unity层次结构创建工具，包含GameObject的创建功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_hierarchy_create_tools(mcp: FastMCP):
    @mcp.tool("hierarchy_create")
    def hierarchy_create(
        ctx: Context,
        source: Annotated[str, Field(
            title="创建来源类型",
            description="GameObject的创建来源: menu(菜单), primitive(基础图元), prefab(预制体), empty(空对象), copy(复制现有对象)",
            examples=["menu", "primitive", "prefab", "empty", "copy"]
        )],
        name: Annotated[str, Field(
            title="对象名称",
            description="要创建的GameObject的名称",
            examples=["Player", "Enemy", "MainCamera", "UI_Canvas"]
        )],
        primitive_type: Annotated[Optional[str], Field(
            title="基础图元类型",
            description="当source为primitive时，指定图元类型",
            default=None,
            examples=["Cube", "Sphere", "Cylinder", "Plane", "Capsule", "Quad"]
        )] = None,
        prefab_path: Annotated[Optional[str], Field(
            title="预制体路径",
            description="当source为prefab时，指定预制体的资源路径",
            default=None,
            examples=["Assets/Prefabs/Player.prefab", "Prefabs/Enemy.prefab"]
        )] = None,
        copy_source: Annotated[Optional[str], Field(
            title="复制源对象",
            description="当source为copy时，指定要复制的GameObject名称",
            default=None,
            examples=["Player", "ExistingObject", "Template"]
        )] = None,
        parent: Annotated[Optional[str], Field(
            title="父对象",
            description="创建的GameObject的父对象名称，留空则在根层级创建",
            default=None,
            examples=["Canvas", "Player", "Environment"]
        )] = None,
        menu_path: Annotated[Optional[str], Field(
            title="菜单路径",
            description="当source为menu时，指定Unity菜单路径",
            default=None,
            examples=["GameObject/3D Object/Cube", "GameObject/UI/Button", "GameObject/Light/Directional Light"]
        )] = None,
        tag: Annotated[Optional[str], Field(
            title="GameObject标签",
            description="设置GameObject的标签",
            default=None,
            examples=["Player", "Enemy", "Untagged", "MainCamera"]
        )] = None,
        layer: Annotated[Optional[int], Field(
            title="GameObject所在层",
            description="设置GameObject的层",
            default=None,
            examples=[0, 8, 10]
        )] = None,
        parent_id: Annotated[Optional[int], Field(
            title="父对象唯一ID",
            description="父对象的实例ID",
            default=None,
            examples=[12345, 67890]
        )] = None,
        position: Annotated[Optional[List[float]], Field(
            title="位置坐标",
            description="位置坐标 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [1.5, 2, -3]]
        )] = None,
        rotation: Annotated[Optional[List[float]], Field(
            title="旋转角度",
            description="旋转角度 [x, y, z]",
            default=None,
            examples=[[0, 0, 0], [0, 90, 0]]
        )] = None,
        scale: Annotated[Optional[List[float]], Field(
            title="缩放比例",
            description="缩放比例 [x, y, z]",
            default=None,
            examples=[[1, 1, 1], [2, 2, 2]]
        )] = None,
        save_as_prefab: Annotated[Optional[bool], Field(
            title="是否保存为预制体",
            description="是否将创建的GameObject保存为预制体",
            default=None
        )] = None,
        set_active: Annotated[Optional[bool], Field(
            title="设置激活状态",
            description="设置GameObject的激活状态",
            default=None
        )] = None
    ) -> Dict[str, Any]:
        """Unity层次结构创建工具，用于在场景中创建各种类型的GameObject。（二级工具）

        支持多种创建方式，适用于：
        - 菜单创建：通过Unity菜单系统创建对象
        - 快速原型制作：创建基础几何体进行测试
        - 场景构建：从预制体创建复杂对象
        - 对象复制：复制现有对象进行批量创建
        - UI构建：创建空对象作为容器

        """
        return get_common_call_response("hierarchy_create")
