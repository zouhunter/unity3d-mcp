"""
GM指令执行工具
执行游戏管理指令，用于游戏调试和测试
"""

from typing import Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_gm_command_tools(mcp: FastMCP):
    @mcp.tool("gm_command")
    def gm_command(
        ctx: Context,
        action: str = Field(
            ...,
            title="操作类型",
            description="操作类型",
            examples=["execute", "list", "help", "validate", "get_result"]
        ),
        command: str = Field(
            ...,
            title="GM指令",
            description="GM指令内容",
            examples=["add_item sword 1", "set_level 10", "give_gold 1000", "teleport 100 200"]
        ),
        parameters: Optional[List[str]] = Field(
            None,
            title="参数列表",
            description="指令参数列表",
            examples=[["sword", "1"], ["10"], ["1000"], ["100", "200"]]
        ),
        target_player: Optional[str] = Field(
            None,
            title="目标玩家",
            description="目标玩家ID或名称",
            examples=["Player1", "12345", "all"]
        ),
        execute_immediately: Optional[bool] = Field(
            True,
            title="立即执行",
            description="是否立即执行指令"
        ),
        validate_before_execute: Optional[bool] = Field(
            True,
            title="执行前验证",
            description="是否在执行前验证指令"
        ),
        log_execution: Optional[bool] = Field(
            True,
            title="记录执行",
            description="是否记录指令执行日志"
        ),
        return_result: Optional[bool] = Field(
            True,
            title="返回结果",
            description="是否返回执行结果"
        ),
        timeout: Optional[int] = Field(
            30,
            title="超时时间",
            description="指令执行超时时间（秒）",
            examples=[10, 30, 60]
        ),
        retry_count: Optional[int] = Field(
            3,
            title="重试次数",
            description="指令执行失败时的重试次数",
            examples=[1, 3, 5]
        ),
        require_confirmation: Optional[bool] = Field(
            False,
            title="需要确认",
            description="是否需要确认后执行"
        ),
        dry_run: Optional[bool] = Field(
            False,
            title="试运行",
            description="是否只验证不实际执行"
        ),
        context: Optional[Dict[str, Any]] = Field(
            None,
            title="执行上下文",
            description="指令执行上下文信息",
            examples=[{"scene": "MainScene", "level": 1, "mode": "debug"}]
        ),
        permissions: Optional[List[str]] = Field(
            None,
            title="权限列表",
            description="执行指令所需的权限",
            examples=[["admin", "debug"], ["gm", "test"]]
        ),
        category: Optional[str] = Field(
            None,
            title="指令分类",
            description="指令分类",
            examples=["item", "player", "world", "system", "debug"]
        ),
        description: Optional[str] = Field(
            None,
            title="指令描述",
            description="指令描述",
            examples=["添加物品到玩家背包", "设置玩家等级", "给予金币"]
        ),
        usage: Optional[str] = Field(
            None,
            title="使用说明",
            description="指令使用说明",
            examples=["add_item <item_id> <count>", "set_level <level>", "give_gold <amount>"]
        ),
        examples: Optional[List[str]] = Field(
            None,
            title="使用示例",
            description="指令使用示例",
            examples=[["add_item sword 1", "set_level 10"], ["give_gold 1000"]]
        ),
        aliases: Optional[List[str]] = Field(
            None,
            title="指令别名",
            description="指令别名列表",
            examples=[["ai", "additem"], ["sl", "setlevel"]]
        ),
        min_parameters: Optional[int] = Field(
            0,
            title="最少参数",
            description="最少参数数量"
        ),
        max_parameters: Optional[int] = Field(
            None,
            title="最多参数",
            description="最多参数数量"
        ),
        parameter_types: Optional[List[str]] = Field(
            None,
            title="参数类型",
            description="参数类型列表",
            examples=[["string", "int"], ["int"], ["float"]]
        ),
        is_dangerous: Optional[bool] = Field(
            False,
            title="危险指令",
            description="是否为危险指令"
        ),
        requires_game_running: Optional[bool] = Field(
            True,
            title="需要游戏运行",
            description="是否需要游戏正在运行"
        ),
        requires_editor: Optional[bool] = Field(
            False,
            title="需要编辑器",
            description="是否需要在编辑器中执行"
        ),
        platform_specific: Optional[bool] = Field(
            False,
            title="平台特定",
            description="是否为平台特定指令"
        ),
        version: Optional[str] = Field(
            None,
            title="版本",
            description="指令版本",
            examples=["1.0", "2.1", "3.0"]
        ),
        deprecated: Optional[bool] = Field(
            False,
            title="已弃用",
            description="是否已弃用"
        ),
        replacement: Optional[str] = Field(
            None,
            title="替代指令",
            description="替代指令名称",
            examples=["new_add_item", "updated_set_level"]
        )
    ) -> Dict[str, Any]:
        """
        GM指令执行工具
        
        支持的操作:
        - execute: 执行GM指令
        - list: 列出可用指令
        - help: 获取指令帮助
        - validate: 验证指令
        - get_result: 获取执行结果
        """
        try:
            unity_conn = get_unity_connection()
            
            # 构建命令参数
            cmd = {
                "action": action,
                "command": command
            }
            
            # 添加可选参数
            optional_params = {
                "parameters": parameters,
                "target_player": target_player,
                "execute_immediately": execute_immediately,
                "validate_before_execute": validate_before_execute,
                "log_execution": log_execution,
                "return_result": return_result,
                "timeout": timeout,
                "retry_count": retry_count,
                "require_confirmation": require_confirmation,
                "dry_run": dry_run,
                "context": context,
                "permissions": permissions,
                "category": category,
                "description": description,
                "usage": usage,
                "examples": examples,
                "aliases": aliases,
                "min_parameters": min_parameters,
                "max_parameters": max_parameters,
                "parameter_types": parameter_types,
                "is_dangerous": is_dangerous,
                "requires_game_running": requires_game_running,
                "requires_editor": requires_editor,
                "platform_specific": platform_specific,
                "version": version,
                "deprecated": deprecated,
                "replacement": replacement
            }
            
            for key, value in optional_params.items():
                if value is not None:
                    cmd[key] = value
            
            # 发送命令到Unity
            result = unity_conn.send_command_with_retry("gm_command", cmd)
            
            return {
                "success": True,
                "message": f"GM command operation '{action}' completed successfully",
                "data": result.get("data", {}),
                "error": None
            }
            
        except Exception as e:
            return {
                "success": False,
                "message": f"GM command operation '{action}' failed",
                "data": {},
                "error": str(e)
            }
