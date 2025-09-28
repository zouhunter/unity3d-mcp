"""
Unity层次结构应用工具，包含GameObject预制体应用和连接操作功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_hierarchy_apply_tools(mcp: FastMCP):
    @mcp.tool("hierarchy_apply")
    def hierarchy_apply(
        ctx: Context,
        action: Annotated[str, Field(
            title="应用操作类型",
            description="要执行的应用操作: apply(应用预制体)",
            examples=["apply"]
        )],
        target_object: Annotated[str, Field(
            title="目标对象",
            description="目标GameObject标识符（用于应用操作）",
            examples=["Player", "Canvas/UI/Button", "Enemy_01"]
        )],
        prefab_path: Annotated[Optional[str], Field(
            title="预制体路径",
            description="预制体路径",
            default=None,
            examples=["Assets/Prefabs/Player.prefab", "Prefabs/Enemy.prefab"]
        )] = None,
        apply_type: Annotated[Optional[str], Field(
            title="应用类型",
            description="应用类型: connect_to_prefab(连接到预制体), apply_prefab_changes(应用预制体更改), break_prefab_connection(断开预制体连接)",
            default="connect_to_prefab",
            examples=["connect_to_prefab", "apply_prefab_changes", "break_prefab_connection"]
        )] = "connect_to_prefab",
        force_apply: Annotated[Optional[bool], Field(
            title="强制应用",
            description="是否强制创建连接（覆盖现有连接）",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity层次结构应用工具，用于处理GameObject预制体应用和连接操作。

        支持多种预制体操作，适用于：
        - 预制体连接：将GameObject连接到预制体
        - 预制体应用：应用预制体的更改到实例
        - 连接断开：断开GameObject与预制体的连接
        - 强制应用：覆盖现有的预制体连接

        Returns:
            包含应用操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 应用操作相关数据
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["apply"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证目标对象参数
            if not target_object or not isinstance(target_object, str):
                return {
                    "success": False,
                    "error": "target_object参数不能为空且必须是字符串类型",
                    "data": None
                }
            
            # 验证应用类型
            valid_apply_types = ["connect_to_prefab", "apply_prefab_changes", "break_prefab_connection"]
            if apply_type not in valid_apply_types:
                return {
                    "success": False,
                    "error": f"无效的应用类型: '{apply_type}'。支持的类型: {valid_apply_types}",
                    "data": None
                }
            
            # 验证预制体路径（某些操作需要）
            if apply_type in ["connect_to_prefab", "apply_prefab_changes"]:
                if not prefab_path:
                    return {
                        "success": False,
                        "error": f"应用类型'{apply_type}'需要提供prefab_path参数",
                        "data": None
                    }
                
                if not prefab_path.endswith(".prefab"):
                    return {
                        "success": False,
                        "error": "预制体路径必须以.prefab结尾",
                        "data": None
                    }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "data": None
                }
            
            # 准备发送给Unity的参数
            params = {
                "action": action,
                "target_object": target_object,
                "apply_type": apply_type,
                "force_apply": force_apply
            }
            
            # 添加预制体路径参数
            if prefab_path:
                params["prefab_path"] = prefab_path
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("hierarchy_apply", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"层次结构应用操作'{action}'执行完成",
                    "data": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "data": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "data": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"层次结构应用失败: {str(e)}",
                "data": None
            }
