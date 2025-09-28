"""
ScriptableObject管理工具
管理Unity中的ScriptableObject资源，包括创建、修改、删除和获取信息
"""

from typing import Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_edit_scriptableobject_tools(mcp: FastMCP):
    @mcp.tool("edit_scriptableobject")
    def edit_scriptableobject(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["create", "modify", "delete", "get_info", "search", "duplicate", "move", "rename"]
        ),
        path: str = Field(
            ...,
            title="ScriptableObject路径",
            description="ScriptableObject路径，Unity标准格式：Assets/Data/MyData.asset",
            examples=["Assets/Data/PlayerData.asset", "Assets/Settings/GameSettings.asset", "Assets/Config/LevelConfig.asset"]
        ),
        script_type: Optional[str] = Field(
            None,
            title="脚本类型",
            description="脚本类型名称",
            examples=["PlayerData", "GameSettings", "LevelConfig", "ItemDatabase"]
        ),
        properties: Optional[Dict[str, Any]] = Field(
            None,
            title="属性字典",
            description="属性字典，用于设置ScriptableObject的属性",
            examples=[{"playerName": "Player1", "level": 1, "health": 100}]
        ),
        destination: Optional[str] = Field(
            None,
            title="目标路径",
            description="目标路径（移动/复制时使用）",
            examples=["Assets/Data/PlayerDataCopy.asset", "Assets/Backup/GameSettings.asset"]
        ),
        query: Optional[str] = Field(
            None,
            title="搜索模式",
            description="搜索模式，如*.asset",
            examples=["*.asset", "Player*", "Settings*"]
        ),
        recursive: Optional[bool] = Field(
            True,
            title="递归搜索",
            description="是否递归搜索子文件夹"
        ),
        force: Optional[bool] = Field(
            False,
            title="强制执行",
            description="是否强制执行操作（覆盖现有文件等）"
        ),
        create_folder: Optional[bool] = Field(
            True,
            title="创建文件夹",
            description="是否自动创建不存在的文件夹"
        ),
        backup: Optional[bool] = Field(
            True,
            title="备份",
            description="是否在修改前备份原文件"
        ),
        validate_properties: Optional[bool] = Field(
            True,
            title="验证属性",
            description="是否验证属性类型和值"
        ),
        apply_immediately: Optional[bool] = Field(
            True,
            title="立即应用",
            description="是否立即应用更改"
        ),
        mark_dirty: Optional[bool] = Field(
            True,
            title="标记为脏",
            description="是否标记资源为已修改"
        ),
        save_assets: Optional[bool] = Field(
            True,
            title="保存资源",
            description="是否保存资源到磁盘"
        ),
        refresh_assets: Optional[bool] = Field(
            True,
            title="刷新资源",
            description="是否刷新资源数据库"
        )
    ) -> Dict[str, Any]:
        """
        ScriptableObject管理工具（二级工具）
        
        支持的操作:
        - create: 创建ScriptableObject
        - modify: 修改ScriptableObject属性
        - delete: 删除ScriptableObject
        - get_info: 获取ScriptableObject信息
        - search: 搜索ScriptableObject
        - duplicate: 复制ScriptableObject
        - move: 移动/重命名ScriptableObject
        - rename: 移动/重命名ScriptableObject（与move相同）
        """
        return get_common_call_response("edit_scriptableobject")
