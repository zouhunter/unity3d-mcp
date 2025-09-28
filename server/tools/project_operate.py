"""
Unity项目操作工具，包含项目资源管理功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_project_operate_tools(mcp: FastMCP):
    @mcp.tool("project_operate")
    def project_operate(
        ctx: Context,
        action: Annotated[str, Field(
            title="项目操作类型",
            description="要执行的项目操作: refresh(刷新项目), import_asset(导入资源), export_package(导出包), create_folder(创建文件夹), delete_asset(删除资源)",
            examples=["refresh", "import_asset", "export_package", "create_folder", "delete_asset"]
        )],
        target_path: Annotated[Optional[str], Field(
            title="目标路径",
            description="操作的目标路径，根据action类型而定",
            default=None,
            examples=["Assets/NewFolder", "Assets/Models/model.fbx", "Assets/Scripts"]
        )] = None,
        source_path: Annotated[Optional[str], Field(
            title="源路径",
            description="源文件路径，仅在import_asset操作时使用",
            default=None,
            examples=["D:/Models/character.fbx", "C:/Textures/grass.png"]
        )] = None,
        package_name: Annotated[Optional[str], Field(
            title="包名称",
            description="导出包的名称，仅在export_package操作时使用",
            default=None,
            examples=["MyAssets.unitypackage", "ScriptsPackage"]
        )] = None,
        include_dependencies: Annotated[bool, Field(
            title="包含依赖",
            description="是否包含资源的依赖项，适用于export_package操作",
            default=True
        )] = True
    ) -> Dict[str, Any]:
        """Unity项目操作工具，用于执行各种项目管理操作。（二级工具）

        支持多种项目操作，适用于：
        - 资源管理：导入外部资源文件到项目中
        - 项目组织：创建文件夹结构，删除不需要的资源
        - 包管理：导出资源包用于分享或备份
        - 项目维护：刷新项目状态，清理无效引用

        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="project_operate", args={"action": "refresh"})
        
        return get_common_call_response("project_operate")
