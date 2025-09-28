"""
Unity编辑器管理工具，包含编辑器状态控制、场景管理、设置配置等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_manage_editor_tools(mcp: FastMCP):
    @mcp.tool("manage_editor")
    def manage_editor(
        ctx: Context,
        action: Annotated[str, Field(
            title="编辑器操作类型",
            description="要执行的编辑器操作: play(播放模式), pause(暂停), stop(停止), save_scene(保存场景), load_scene(加载场景), get_state(获取状态), set_setting(设置配置)",
            examples=["play", "pause", "stop", "save_scene", "load_scene", "get_state"]
        )],
        scene_path: Annotated[Optional[str], Field(
            title="场景路径",
            description="场景文件的路径，仅在load_scene和save_scene操作时使用",
            default=None,
            examples=["Assets/Scenes/MainScene.unity", "Scenes/Level1.unity"]
        )] = None,
        setting_key: Annotated[Optional[str], Field(
            title="设置键名",
            description="要设置的配置项键名，仅在set_setting操作时使用",
            default=None,
            examples=["Quality", "Resolution", "VSyncCount"]
        )] = None,
        setting_value: Annotated[Optional[str], Field(
            title="设置值",
            description="要设置的配置项值，仅在set_setting操作时使用",
            default=None,
            examples=["High", "1920x1080", "1"]
        )] = None,
        force: Annotated[Optional[bool], Field(
            title="强制执行",
            description="是否强制执行操作，忽略某些检查和警告",
            default=False
        )] = False
    ) -> Dict[str, Any]:
        """Unity编辑器管理工具，用于控制编辑器的各种状态和操作。

        支持多种编辑器管理功能，适用于：
        - 自动化测试：控制编辑器播放模式
        - 项目管理：场景的保存和加载
        - 开发流程：编辑器状态查询和配置
        - CI/CD集成：自动化构建和测试

        Returns:
            包含编辑器操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 操作相关数据（如编辑器状态）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["play", "pause", "stop", "save_scene", "load_scene", "get_state", "set_setting"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证场景相关参数
            if action in ["save_scene", "load_scene"]:
                if not scene_path:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供scene_path参数",
                        "data": None
                    }
                
                if not scene_path.endswith(".unity"):
                    return {
                        "success": False,
                        "error": "场景路径必须以.unity结尾",
                        "data": None
                    }
            
            # 验证设置相关参数
            if action == "set_setting":
                if not setting_key:
                    return {
                        "success": False,
                        "error": "set_setting操作需要提供setting_key参数",
                        "data": None
                    }
                
                if not setting_value:
                    return {
                        "success": False,
                        "error": "set_setting操作需要提供setting_value参数",
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
            params = {"action": action}
            
            # 添加场景路径参数
            if scene_path:
                params["scenePath"] = scene_path
            
            # 添加设置参数
            if setting_key:
                params["settingKey"] = setting_key
            if setting_value:
                params["settingValue"] = setting_value
            
            # 添加强制执行参数
            if force:
                params["force"] = force
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("manage_editor", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"编辑器操作'{action}'执行完成",
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
                "error": f"编辑器操作失败: {str(e)}",
                "data": None
            }
