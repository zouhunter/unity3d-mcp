"""
批量函数调用工具，包含单个和批量Unity函数调用功能。
"""
import json
from typing import Annotated, List, Dict, Any, Literal
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection

def register_extra_tools(mcp: FastMCP):
    @mcp.tool("extra_call")
    def extra_call(
        ctx: Context,
        func: Annotated[str, Field(
            title="Unity函数名称",
            description="要调用的Unity函数名称，支持的函数包括: gameplay(游戏控制), hierarchy_create(创建GameObject), manage_editor(编辑器管理), project_search(项目搜索), edit_gameobject(编辑对象), console_read(控制台读取), python_runner(Python执行)",
            examples=["gameplay", "hierarchy_create", "manage_editor", "project_search"]
        )],
        args: Annotated[Dict[str, Any], Field(
            title="函数参数",
            description="传递给Unity函数的参数字典，格式依据具体函数而定",
            default_factory=dict,
            examples=[
                {"action": "screenshot", "format": "PNG"},
                {"from": "primitive", "primitive_type": "Cube", "name": "MyCube"},
                {"action": "play"},
                {"search_target": "script", "query": "Player"}
            ]
        )] = {}
    ) -> Dict[str, Any]:
        """单个函数调用工具，可以调用Unity-MCP系统中的指定函数。

        支持所有已注册的Unity工具函数，提供统一的调用接口。
        常用操作包括游戏控制、对象创建、编辑器管理等。

        Returns:
            包含函数调用结果的字典：
            {
                "success": bool,     # 调用是否成功
                "result": Any,       # 函数返回的结果数据
                "error": str|None    # 错误信息（如果有的话）
            }
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
            result = bridge.send_command_with_retry("extra_call", params, max_retries=2)
            
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

    @mcp.tool("extra_batch_calls")
    def extra_batch_calls(
        ctx: Context,
        funcs: Annotated[List[Dict[str, Any]], Field(
            title="函数调用列表",
            description="要批量执行的Unity函数调用列表，按顺序执行。每个元素必须包含func和args字段",
            min_length=1,
            max_length=50,
            examples=[
                [
                    {"func": "hierarchy_create", "args": {"from": "primitive", "primitive_type": "Cube", "name": "Enemy"}},
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
        """批量函数调用工具，可以按顺序调用Unity中的多个函数并收集所有返回值。

        支持事务性操作和批量处理，常用场景：
        - 创建并配置GameObject：创建 → 设置属性 → 添加组件
        - 场景管理：播放 → 截图 → 停止
        - 批量创建：创建多个不同类型的GameObject

        Returns:
            包含所有函数调用结果的详细统计字典：
            {
                "success": bool,           # 整体批量操作是否成功
                "results": [Any, ...],     # 每个函数调用的返回结果数组
                "errors": [str|None, ...], # 每个函数调用的错误信息数组
                "total_calls": int,        # 总调用次数
                "successful_calls": int,   # 成功调用次数
                "failed_calls": int        # 失败调用次数
            }
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
                "func": "extra_batch_calls",
                "args": funcs
            }
            
            # 使用带重试机制的命令发送到Unity的functions_call处理器
            result = bridge.send_command_with_retry("extra_batch_calls", params, max_retries=1)
            
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
