from mcp.server.fastmcp import FastMCP, Context, Image
import logging
from dataclasses import dataclass
from contextlib import asynccontextmanager
from typing import AsyncIterator, Dict, Any, List
from config import config
from tools import register_all_tools
from unity_connection import get_unity_connection, UnityConnection

# Configure logging using settings from config
logging.basicConfig(
    level=getattr(logging, config.log_level),
    format=config.log_format
)
logger = logging.getLogger("unity-mcp-server")

# Global connection state
_unity_connection: UnityConnection = None

@asynccontextmanager
async def server_lifespan(server: FastMCP) -> AsyncIterator[Dict[str, Any]]:
    """Handle server startup and shutdown with improved error handling."""
    global _unity_connection
    logger.info("Unity MCP Server starting up")
    
    # 尝试连接Unity，但不要让启动失败
    try:
        _unity_connection = get_unity_connection()
        logger.info("Successfully connected to Unity on startup")
    except Exception as e:
        logger.warning(f"Could not connect to Unity on startup: {str(e)}")
        logger.info("Server will start without Unity connection. Connection will be attempted when tools are used.")
        _unity_connection = None
    
    try:
        # 始终提供连接对象，即使是None
        # 工具函数将在需要时动态获取连接
        yield {"bridge": _unity_connection}
    except Exception as e:
        logger.error(f"Server lifespan error: {str(e)}")
        raise
    finally:
        # 清理连接资源
        if _unity_connection:
            try:
                _unity_connection.disconnect()
                logger.info("Unity connection closed")
            except Exception as e:
                logger.warning(f"Error closing Unity connection: {str(e)}")
            finally:
                _unity_connection = None
        logger.info("Unity MCP Server shut down")

# Initialize MCP server
mcp = FastMCP(
    "unity-mcp-server",
    lifespan=server_lifespan
)

# Register all tools
register_all_tools(mcp)

# Asset Creation Strategy

# @mcp.prompt()
def function_args_strategy() -> str:
    """Guide for discovering and using Unity MCP tools effectively."""
    return (
        "Available Unity MCP Server Tools:\\n\\n"
        "- `function_call`: execute func once, args from unity-mcp.mdc or other rules.\\n"
        "- `functions_call`: execute funcs in batch, args from unity-mcp.mdc or other rules.\\n\\n"
        "Connection Status:\\n"
        "- Server handles Unity connection automatically\\n"
        "- Connections are established on-demand and cached\\n"
        "- Failed connections will be retried with exponential backoff\\n\\n"
        "Tips:\\n"
        "- Read rules first to choose appropriate func call\\n"
        "- Make sure func args match the expected format from rules\\n"
        "- Use functions_call for batch operations to improve performance\\n"
        "- Check console logs if experiencing connection issues\\n"
    )

# Run the server
if __name__ == "__main__":
    mcp.run(transport='stdio')
