"""
Unity MCP 核心调用工具，包含单个和批量Unity函数调用功能。

⚠️ 重要说明：
- 所有MCP函数调用（除single_call和batch_call外）都必须通过此文件中的函数调用
- 不能直接调用hierarchy_create、edit_gameobject、manage_editor等函数
- 必须使用single_call进行单个函数调用，或使用batch_call进行批量调用
- 这是Unity MCP系统的核心调用入口，所有其他工具函数都通过这里转发到Unity
"""
import json
from typing import Annotated, List, Dict, Any, Literal
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection

# 公共的错误提示文本
def get_common_call_response(func_name: str) -> dict:
    """
    获取公共的错误响应格式
    
    Args:
        func_name: 函数名称
        
    Returns:
        标准的错误响应字典
    """
    return {
        "success": False,
        "error": "请使用 single_call(func='{func_name}', args={{...}}) 来调用此函数".format(func_name=func_name),
        "data": None
    }

def register_call_tools(mcp: FastMCP):
    @mcp.tool("single_call")
    def single_call(
        ctx: Context,
        func: Annotated[str, Field(
            title="Unity函数名称",
            description="要调用的Unity函数名称。⚠️ 重要：所有MCP函数调用（除single_call和batch_call外）都必须通过此函数调用",
            examples=["hierarchy_create", "edit_gameobject", "manage_editor", "gameplay", "console_write"]
        )],
        args: Annotated[Dict[str, Any], Field(
            title="函数参数",
            description="传递给Unity函数的参数字典。参数格式必须严格按照目标函数的定义，所有参数都通过此args字典传递",
            default_factory=dict,
            examples=[
                {"source": "primitive", "name": "Cube", "primitive_type": "Cube"},
                {"path": "Player", "action": "add_component", "component_type": "Rigidbody"},
                {"action": "play"}
            ]
        )] = {}
    ) -> Dict[str, Any]:
        """单个函数调用工具，用于调用所有Unity MCP函数。（一级工具）
        
        ⚠️ 重要说明：
        - 所有MCP函数调用（除single_call和batch_call外）都必须通过此函数调用
        - 不能直接调用hierarchy_create、edit_gameobject等函数，必须通过single_call
        - func参数指定要调用的函数名，args参数传递该函数所需的所有参数
        
        支持的函数包括但不限于：
        - hierarchy_create: 创建GameObject
        - edit_gameobject: 编辑GameObject
        - manage_editor: 编辑器管理
        - gameplay: 游戏玩法控制
        - console_write: 控制台输出
        - 以及其他所有MCP工具函数
        """
        
        try:
            # 验证函数名称
            if not func or not isinstance(func, str):
                return {
                    "success": False,
                    "error": "函数名称无效或为空",
                    "result": None
                }
            
            # 验证参数类型
            if not isinstance(args, dict):
                return {
                    "success": False,
                    "error": "参数必须是对象类型",
                    "result": None
                }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "result": None
                }
            
            # 准备发送给Unity的参数
            params = {
                "func": func,
                "args": args
            }
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("single_call", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "result": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "result": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "result": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"函数调用失败: {str(e)}",
                "result": None
            }

    @mcp.tool("batch_call")
    def batch_call(
        ctx: Context,
        funcs: Annotated[List[Dict[str, Any]], Field(
            title="函数调用列表",
            description="要批量执行的Unity函数调用列表，按顺序执行。⚠️ 重要：每个元素必须包含func和args字段，func指定要调用的MCP函数名，args传递该函数的所有参数",
            min_length=1,
            max_length=50,
            examples=[
                [
                    {"func": "hierarchy_create", "args": {"source": "primitive", "primitive_type": "Cube", "name": "Enemy"}},
                    {"func": "edit_gameobject", "args": {"path": "Enemy", "action": "add_component", "component_type": "Rigidbody"}}
                ],
                [
                    {"func": "manage_editor", "args": {"action": "play"}},
                    {"func": "gameplay", "args": {"action": "screenshot", "format": "PNG"}},
                    {"func": "manage_editor", "args": {"action": "stop"}}
                ]
            ]
        )]
    ) -> Dict[str, Any]:
        """批量函数调用工具，可以按顺序调用Unity中的多个MCP函数并收集所有返回值。（一级工具）
        
        ⚠️ 重要说明：
        - 所有MCP函数调用（除single_call和batch_call外）都必须通过此函数调用
        - 每个函数调用元素必须包含func（函数名）和args（参数字典）字段
        - 函数名必须是有效的MCP函数名，如hierarchy_create、edit_gameobject等
        - 参数格式必须严格按照目标函数的定义

        支持事务性操作和批量处理，常用场景：
        - 创建并配置GameObject：创建 → 设置属性 → 添加组件
        - 场景管理：播放 → 截图 → 停止
        - 批量创建：创建多个不同类型的GameObject
        - UI创建：创建Canvas → 创建UI元素 → 设置布局
        """
        
        # 获取Unity连接实例
        bridge = get_unity_connection()
        
        try:
            # 验证输入参数
            if not isinstance(funcs, list):
                return {
                    "success": False,
                    "results": [],
                    "errors": ["funcs参数必须是数组类型"],
                    "total_calls": 0,
                    "successful_calls": 0,
                    "failed_calls": 1
                }
            
            # 基本的函数调用格式验证
            for i, func_call in enumerate(funcs):
                if not isinstance(func_call, dict):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用必须是对象类型"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
                
                if "func" not in func_call or not isinstance(func_call.get("func"), str):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用的func字段无效或为空"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
                
                if "args" not in func_call or not isinstance(func_call.get("args"), dict):
                    return {
                        "success": False,
                        "results": [],
                        "errors": [f"第{i+1}个函数调用的args字段必须是对象类型"],
                        "total_calls": len(funcs),
                        "successful_calls": 0,
                        "failed_calls": 1
                    }
            
            # 准备发送给Unity的参数，保持架构一致性
            params = {
                "func": "batch_call",
                "args": funcs
            }
            
            # 使用带重试机制的命令发送到Unity的functions_call处理器
            result = bridge.send_command_with_retry("batch_call", params, max_retries=1)
            
            # Unity的functions_call处理器返回的结果已经是完整的格式，直接返回data部分
            if isinstance(result, dict) and "data" in result:
                return result["data"]
            else:
                # 如果返回格式不符合预期，包装成标准格式
                return {
                    "success": True,
                    "results": [result],
                    "errors": [None],
                    "total_calls": len(funcs),
                    "successful_calls": 1,
                    "failed_calls": 0
                }
            
        except Exception as e:
            return {
                "success": False,
                "results": [],
                "errors": [f"批量调用转发失败: {str(e)}"],
                "total_calls": len(funcs) if isinstance(funcs, list) else 0,
                "successful_calls": 0,
                "failed_calls": 1
            }
