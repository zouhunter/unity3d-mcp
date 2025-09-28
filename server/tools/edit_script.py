"""
Unity脚本编辑工具，包含C#脚本的创建、读取、更新、删除等功能。
"""
import json
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_edit_script_tools(mcp: FastMCP):
    @mcp.tool("edit_script")
    def edit_script(
        ctx: Context,
        action: Annotated[str, Field(
            title="操作类型",
            description="操作类型：create(创建), read(读取), update(更新), delete(删除)",
            examples=["create", "read", "update", "delete"]
        )],
        name: Annotated[Optional[str], Field(
            title="脚本名称",
            description="脚本名称（不含.cs扩展名）",
            default=None,
            examples=["PlayerController", "GameManager", "EnemyAI"]
        )] = None,
        path: Annotated[Optional[str], Field(
            title="脚本路径",
            description="脚本资产路径",
            default=None,
            examples=["Assets/Scripts/PlayerController.cs", "Scripts/GameManager.cs"]
        )] = None,
        lines: Annotated[Optional[List[str]], Field(
            title="代码内容",
            description="C#代码内容（已换行的字符串数组）",
            default=None,
            examples=[
                ["using UnityEngine;", "public class PlayerController : MonoBehaviour", "{", "    void Start()", "    {", "    }", "}"]
            ]
        )] = None,
        script_type: Annotated[Optional[str], Field(
            title="脚本类型",
            description="脚本类型：MonoBehaviour, ScriptableObject等",
            default="MonoBehaviour",
            examples=["MonoBehaviour", "ScriptableObject", "EditorWindow", "CustomEditor"]
        )] = "MonoBehaviour",
        namespace: Annotated[Optional[str], Field(
            title="命名空间",
            description="命名空间",
            default=None,
            examples=["MyGame", "MyGame.Player", "MyGame.Managers"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity脚本编辑工具，用于管理C#脚本文件。

        支持多种脚本操作，适用于：
        - 脚本创建：创建新的C#脚本文件
        - 脚本读取：读取现有脚本的内容
        - 脚本更新：修改现有脚本的内容
        - 脚本删除：删除不需要的脚本文件
        - 代码生成：自动生成常用脚本模板

        Returns:
            包含脚本操作结果的字典：
            {
                "success": bool,        # 操作是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 脚本相关数据（如代码内容）
                "error": str|None      # 错误信息（如果有的话）
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["create", "read", "update", "delete"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证脚本名称参数
            if action in ["create", "update"]:
                if not name:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供name参数",
                        "data": None
                    }
            
            # 验证脚本路径参数
            if action in ["read", "update", "delete"]:
                if not path:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供path参数",
                        "data": None
                    }
                
                if not path.endswith(".cs"):
                    return {
                        "success": False,
                        "error": "脚本路径必须以.cs结尾",
                        "data": None
                    }
            
            # 验证代码内容参数
            if action in ["create", "update"]:
                if not lines:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供lines参数",
                        "data": None
                    }
                
                if not isinstance(lines, list):
                    return {
                        "success": False,
                        "error": "lines参数必须是字符串数组",
                        "data": None
                    }
            
            # 验证脚本类型
            valid_script_types = ["MonoBehaviour", "ScriptableObject", "EditorWindow", "CustomEditor", "PropertyDrawer"]
            if script_type not in valid_script_types:
                return {
                    "success": False,
                    "error": f"无效的脚本类型: '{script_type}'。支持的类型: {valid_script_types}",
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
            
            # 添加脚本名称参数
            if name:
                params["name"] = name
            
            # 添加脚本路径参数
            if path:
                params["path"] = path
            
            # 添加代码内容参数
            if lines:
                params["lines"] = lines
            
            # 添加脚本类型参数
            if script_type:
                params["script_type"] = script_type
            
            # 添加命名空间参数
            if namespace:
                params["namespace"] = namespace
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("edit_script", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"脚本操作'{action}'执行完成",
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
                "error": f"脚本编辑失败: {str(e)}",
                "data": None
            }
