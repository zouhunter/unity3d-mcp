"""
Unity材质编辑工具，包含材质的创建、修改和管理功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_material_tools(mcp: FastMCP):
    @mcp.tool("edit_material")
    def edit_material(
        ctx: Context,
        action: Annotated[str, Field(
            title="材质操作类型",
            description="要执行的材质操作: create(创建), modify(修改属性), duplicate(复制), delete(删除), apply_texture(应用纹理)",
            examples=["create", "modify", "duplicate", "delete", "apply_texture"]
        )],
        material_path: Annotated[str, Field(
            title="材质路径",
            description="材质文件的Assets路径",
            examples=["Assets/Materials/PlayerMaterial.mat", "Materials/Ground.mat"]
        )],
        shader_name: Annotated[Optional[str], Field(
            title="着色器名称",
            description="要使用的着色器名称，仅在create和modify操作时使用",
            default=None,
            examples=["Standard", "Universal Render Pipeline/Lit", "Unlit/Color"]
        )] = None,
        properties: Annotated[Optional[Dict[str, Any]], Field(
            title="材质属性",
            description="要设置的材质属性键值对，仅在create和modify操作时使用",
            default=None,
            examples=[
                {"_Color": [1.0, 0.0, 0.0, 1.0]},
                {"_Metallic": 0.5, "_Smoothness": 0.8},
                {"_MainTex": "Assets/Textures/diffuse.png"}
            ]
        )] = None,
        texture_path: Annotated[Optional[str], Field(
            title="纹理路径",
            description="要应用的纹理文件路径，仅在apply_texture操作时使用",
            default=None,
            examples=["Assets/Textures/brick.png", "Textures/wood_diffuse.jpg"]
        )] = None,
        texture_slot: Annotated[Optional[str], Field(
            title="纹理插槽",
            description="要应用纹理的插槽名称，仅在apply_texture操作时使用",
            default="_MainTex",
            examples=["_MainTex", "_BumpMap", "_MetallicGlossMap"]
        )] = "_MainTex"
    ) -> Dict[str, Any]:
        """Unity材质编辑工具，用于创建、修改和管理材质资源。

        支持完整的材质编辑功能，适用于：
        - 材质创建：从零开始创建新材质
        - 属性调整：修改材质的各种属性参数
        - 纹理管理：应用和更换材质纹理
        - 批量处理：对多个材质进行统一操作
        """
        return get_common_call_response("edit_material")
