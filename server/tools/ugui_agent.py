"""
Defines the ugui_agent tool for Unity UGUI.
"""
from typing import List, Dict, Any
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection

def register_ugui_agent_tools(mcp: FastMCP):
    """Registers the ugui_agent tool with the MCP server."""

    @mcp.tool()
    def ugui_agent(
        ctx: Context,
        action: str = None,
        schema: str = None,
        target: str = None
    ) -> Dict[str, Any]:
        """agent like a ugui developer,can get or create schema.

        Args:
            ctx: The MCP context.
            action: Operation ('get_ui' or 'create_ui').
            schema: ui stacture like 登录界面 [0, 0, 1920, 1080] (Container: {})
                                     - 左侧区域 [0, 0, 960, 1080] (Container: {})
                                       - Logo 图片 [50, 50, 200, 200] (Image: {"src": "logo.png"})
                                     - 右侧区域 [960, 0, 960, 1080] (Container: {})
                                       - 用户名输入框 [100, 100, 300, 40] (TextInput: {"placeholder": "请输入用户名"})
                                       - 密码输入框 [100, 160, 300, 40] (TextInput: {"placeholder": "请输入密码", "type": "password"})
                                       - 登录按钮 [100, 220, 100, 40] (Button: {"text": "登录"})
                                       - 注册按钮 [220, 220, 100, 40] (Button: {"text": "注册"})
                                       - 提示信息 [100, 300, 300, 40] (Label: {"text": "欢迎登录"})
            target:ui name from hierarchy
        Returns:
            Dictionary with results. For 'get_ui', includes 'schema' (messages).
        """
        
        # Get the connection instance
        bridge = get_unity_connection()

        # Set defaults if values are None
        action = action if action is not None else 'get_ui'
        schema = schema if schema is not None else ""
        target = target if target is not None else ''
        
        # Normalize action if it's a string
        if isinstance(action, str):
            action = action.lower()
        
        # Prepare parameters for the C# handler
        params_dict = {
            "action": action,
            "schema": schema,
            "target": target
        }
        # Remove None values
        params = {k: v for k, v in params_dict.items() if v is not None}
        # Forward the command using the bridge's send_command method
        return bridge.send_command("ugui_agent", params) 