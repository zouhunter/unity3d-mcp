"""
预制体管理工具
专门管理Unity中的预制体资源，提供预制体的创建、修改、复制、实例化等操作
"""

from typing import Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_prefab_tools(mcp: FastMCP):
    @mcp.tool("edit_prefab")
    def edit_prefab(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["create", "modify", "duplicate", "get_info", "search", "instantiate", "unpack", "pack", "create_variant", "connect_to_prefab", "apply_changes", "revert_changes", "break_connection"]
        ),
        path: str = Field(
            ...,
            title="预制体资源路径",
            description="预制体资源路径，Unity标准格式：Assets/Prefabs/PrefabName.prefab",
            examples=["Assets/Prefabs/Player.prefab", "Assets/Prefabs/Enemy.prefab"]
        ),
        source_object: Optional[str] = Field(
            None,
            title="源GameObject名称或路径",
            description="源GameObject名称或路径（创建时使用）",
            examples=["Player", "Enemy", "Canvas/UI/Button"]
        ),
        destination: Optional[str] = Field(
            None,
            title="目标路径",
            description="目标路径（复制时使用）",
            examples=["Assets/Prefabs/PlayerCopy.prefab", "Assets/Prefabs/EnemyVariant.prefab"]
        ),
        query: Optional[str] = Field(
            None,
            title="搜索模式",
            description="搜索模式，如*.prefab",
            examples=["*.prefab", "Player*", "Enemy*"]
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
        prefab_variant: Optional[bool] = Field(
            False,
            title="预制体变体",
            description="是否创建预制体变体"
        ),
        unpack_mode: Optional[str] = Field(
            None,
            title="解包模式",
            description="解包模式：Completely, OutermostRoot",
            examples=["Completely", "OutermostRoot"]
        ),
        pack_mode: Optional[str] = Field(
            None,
            title="打包模式",
            description="打包模式：Default, ReuseExisting",
            examples=["Default", "ReuseExisting"]
        ),
        connect_to_prefab: Optional[bool] = Field(
            None,
            title="连接到预制体",
            description="是否连接到预制体"
        ),
        apply_prefab_changes: Optional[bool] = Field(
            None,
            title="应用预制体更改",
            description="是否应用预制体更改"
        ),
        revert_prefab_changes: Optional[bool] = Field(
            None,
            title="还原预制体更改",
            description="是否还原预制体更改"
        ),
        break_prefab_connection: Optional[bool] = Field(
            None,
            title="断开预制体连接",
            description="是否断开预制体连接"
        ),
        prefab_type: Optional[str] = Field(
            None,
            title="预制体类型",
            description="预制体类型：Regular, Variant",
            examples=["Regular", "Variant"]
        ),
        parent_prefab: Optional[str] = Field(
            None,
            title="父预制体路径",
            description="父预制体路径（变体时使用）",
            examples=["Assets/Prefabs/BaseEnemy.prefab"]
        ),
        scene_path: Optional[str] = Field(
            None,
            title="场景路径",
            description="场景路径（实例化时使用）",
            examples=["Assets/Scenes/MainScene.unity"]
        ),
        position: Optional[List[float]] = Field(
            None,
            title="位置坐标",
            description="位置坐标 [x, y, z]",
            examples=[[0, 0, 0], [1, 2, 3]]
        ),
        rotation: Optional[List[float]] = Field(
            None,
            title="旋转角度",
            description="旋转角度 [x, y, z]",
            examples=[[0, 0, 0], [0, 90, 0]]
        ),
        scale: Optional[List[float]] = Field(
            None,
            title="缩放比例",
            description="缩放比例 [x, y, z]",
            examples=[[1, 1, 1], [2, 2, 2]]
        ),
        parent_path: Optional[str] = Field(
            None,
            title="父对象名称或路径",
            description="父对象名称或路径",
            examples=["Canvas", "Environment", "Player"]
        )
    ) -> Dict[str, Any]:
        """
        预制体管理工具
        
        支持的操作:
        - create: 创建预制体
        - modify: 修改预制体
        - duplicate: 复制预制体
        - get_info: 获取预制体信息
        - search: 搜索预制体
        - instantiate: 实例化预制体
        - unpack: 解包预制体
        - pack: 打包预制体
        - create_variant: 创建预制体变体
        - connect_to_prefab: 连接到预制体
        - apply_changes: 应用预制体更改
        - revert_changes: 还原预制体更改
        - break_connection: 断开预制体连接
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "path": path
            }
            
            # 添加可选参数
            optional_params = {
                "source_object": source_object,
                "destination": destination,
                "query": query,
                "recursive": recursive,
                "force": force,
                "prefab_variant": prefab_variant,
                "unpack_mode": unpack_mode,
                "pack_mode": pack_mode,
                "connect_to_prefab": connect_to_prefab,
                "apply_prefab_changes": apply_prefab_changes,
                "revert_prefab_changes": revert_prefab_changes,
                "break_prefab_connection": break_prefab_connection,
                "prefab_type": prefab_type,
                "parent_prefab": parent_prefab,
                "scene_path": scene_path,
                "position": position,
                "rotation": rotation,
                "scale": scale,
                "parent_path": parent_path
            }
            
            for key, value in optional_params.items():
                if value is not None:
                    cmd[key] = value
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("edit_prefab", cmd)
            
            return {
                "success": True,
                "message": f"Prefab operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"Prefab operation '{action}' failed",
                "data": {},
                "error": str(e)
            }
