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
    connection_timeout: float = 120.0  # 增加到120秒超时，减少连接问题
    buffer_size: int = 16 * 1024 * 1024  # 16MB buffer
    smart_port_discovery: bool = True   # 重新启用智能端口发现，配合新的端口切换逻辑
    ping_timeout: float = 3.0  # ping命令的单独超时设置
    connection_retry_delay: float = 1.0  # 连接重试延迟
    
    # Port switching settings
    port_failure_timeout: float = 60.0  # 端口失败后的冷却时间（秒）
    max_failed_ports: int = 10  # 最大的失败端口记录数
    
    # Logging settings
    log_level: str = "INFO"
    log_format: str = "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    
    # Server settings
    max_retries: int = 3
    retry_delay: float = 1.0
    
    # Advanced connection settings
    connection_health_check_interval: float = 30.0  # 连接健康检查间隔（秒）
    auto_port_switching: bool = True  # 启用自动端口切换
    port_scan_timeout: float = 1.0  # 端口扫描超时时间

# Create a global config instance
config = ServerConfig() 