"""
Configuration settings for the Unity MCP Server.
This file contains all configurable args for the server.
"""

from dataclasses import dataclass

@dataclass
class ServerConfig:
    """Main configuration class for the MCP server."""
    
    # Network settings
    unity_host: str = "127.0.0.1"
    unity_port_start: int = 6400
    unity_port_end: int = 6405
    
    # Connection settings
    connection_timeout: float = 5.0  # 5s timeout
    buffer_size: int = 16 * 1024 * 1024  # 16MB buffer
    smart_port_discovery: bool = True  # Enable smart port discovery
    
    # Logging settings
    log_level: str = "INFO"
    log_format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    
    # Server settings
    max_retries: int = 3
    retry_delay: float = 1.0

# Create a global config instance
config = ServerConfig() 