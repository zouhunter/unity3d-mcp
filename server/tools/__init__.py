from .function_call import register_function_call_tools
from .functions_call import register_functions_call_tools

def register_all_tools(mcp):
    """Register all refactored tools with the MCP server."""
    print("Registering Unity MCP Server refactored tools...")
    register_function_call_tools(mcp)
    register_functions_call_tools(mcp)
    print("Unity MCP Server tool registration complete.")
