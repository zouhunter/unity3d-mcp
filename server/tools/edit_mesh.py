"""
Unity网格编辑工具，包含3D网格的导入、导出、优化和处理功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_mesh_tools(mcp: FastMCP):
    @mcp.tool("edit_mesh")
    def edit_mesh(
        ctx: Context,
        action: Annotated[str, Field(
            title="网格操作类型",
            description="要执行的网格操作: import(导入), export(导出), optimize(优化), generate_uv(生成UV), calculate_normals(计算法线)",
            examples=["import", "export", "optimize", "generate_uv", "calculate_normals"]
        )],
        mesh_path: Annotated[str, Field(
            title="网格文件路径",
            description="网格文件的路径，可以是Assets内路径或外部文件路径",
            examples=["Assets/Models/character.fbx", "D:/Models/building.obj", "Models/weapon.dae"]
        )],
        target_path: Annotated[Optional[str], Field(
            title="目标路径",
            description="导入或导出的目标路径",
            default=None,
            examples=["Assets/Models/imported_model.fbx", "D:/Exports/optimized_mesh.obj"]
        )] = None,
        import_settings: Annotated[Optional[Dict[str, Any]], Field(
            title="导入设置",
            description="网格导入时的设置参数",
            default=None,
            examples=[
                {"scale_factor": 1.0, "generate_colliders": True},
                {"import_materials": True, "optimize_mesh": True}
            ]
        )] = None,
        optimization_level: Annotated[Optional[str], Field(
            title="优化级别",
            description="网格优化的级别：low(低), medium(中), high(高)",
            default="medium",
            examples=["low", "medium", "high"]
        )] = "medium"
    ) -> Dict[str, Any]:
        """Unity网格编辑工具，用于导入、导出、优化和处理3D网格资源。

        支持多种网格处理功能，适用于：
        - 模型导入：从外部文件导入3D模型到项目中
        - 网格优化：减少面数和提升性能
        - UV生成：为模型自动生成UV坐标
        - 法线计算：重新计算模型的顶点法线
        """
        return get_common_call_response("edit_mesh")
