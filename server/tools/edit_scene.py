"""
场景管理工具
管理Unity场景，包括加载、保存、创建和获取层级结构
"""

from typing import Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_scene_tools(mcp: FastMCP):
    @mcp.tool("edit_scene")
    def edit_scene(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["load", "save", "create", "get_hierarchy"]
        ),
        name: str = Field(
            ...,
            title="场景名称",
            description="场景名称",
            examples=["MainMenu", "Level1", "TestScene"]
        ),
        path: str = Field(
            ...,
            title="资产路径",
            description="资产路径",
            examples=["Assets/Scenes/MainMenu.unity", "Assets/Scenes/", "Assets/Scenes/Level1.unity"]
        ),
        build_index: int = Field(
            ...,
            title="构建索引",
            description="构建索引",
            examples=[0, 1, 2]
        )
    ) -> Dict[str, Any]:
        """
        场景管理工具
        
        支持的操作:
        - load: 加载场景
        - save: 保存场景
        - create: 创建场景
        - get_hierarchy: 获取场景层级
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "name": name,
                "path": path,
                "build_index": build_index
            }
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("edit_scene", cmd)
            
            return {
                "success": True,
                "message": f"Scene operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"Scene operation '{action}' failed",
                "data": {},
                "error": str(e)
            }
