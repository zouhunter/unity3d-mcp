import socket
import json
import logging
import struct
from dataclasses import dataclass
from typing import Dict, Any
from config import config

# Import JSONDecodeError for older Python versions compatibility
try:
    from json import JSONDecodeError
except ImportError:
    # For Python < 3.5
    JSONDecodeError = ValueError

import time

# Configure logging using settings from config
logging.basicConfig(
    level=getattr(logging, config.log_level),
    format=config.log_format
)
logger = logging.getLogger("unity-mcp-server")

@dataclass
class UnityConnection:
    """Manages the socket connection to the Unity Editor."""
    host: str = config.unity_host
    port: int = None  # Will be set during successful connection
    sock: socket.socket = None  # Socket for Unity communication
    failed_ports: dict = None  # Track ports that have failed recently with timestamps
    connection_attempts: int = 0  # Track total connection attempts
    last_connection_time: float = 0  # Track when last connection was made
    last_cleanup_time: float = 0  # Track when failed ports were last cleaned up
    
    def __post_init__(self):
        if self.failed_ports is None:
            self.failed_ports = {}  # {port: failure_timestamp}

    def connect(self, force_reconnect: bool = False) -> bool:
        """Establish a connection to the Unity Editor by trying multiple ports."""
        # 如果有现有连接且不强制重连，先验证连接有效性
        if self.sock and not force_reconnect:
            if self.is_connection_alive():
                logger.debug(f"Reusing existing connection on port {self.port}")
                return True
            else:
                logger.warning(f"Existing connection on port {self.port} is dead, reconnecting...")
                self.disconnect()
                if self.port:
                    self.failed_ports.add(self.port)
        
        self.connection_attempts += 1
        logger.info(f"Starting connection attempt #{self.connection_attempts}")
        
        # 清理过期的失败端口记录
        self._cleanup_expired_failed_ports()
        
        # 获取可尝试的端口列表（排除最近失败的端口）
        available_ports = []
        for port in range(config.unity_port_start, config.unity_port_end + 1):
            if port not in self.failed_ports:
                available_ports.append(port)
        
        # 如果所有端口都失败过，清空失败记录重新开始
        if not available_ports:
            logger.warning("All ports have failed recently, clearing failed ports list and retrying all")
            self.failed_ports.clear()
            available_ports = list(range(config.unity_port_start, config.unity_port_end + 1))
        
        failed_ports_info = {k: f"{v:.1f}s ago" for k, v in self.failed_ports.items()}
        logger.info(f"Trying {len(available_ports)} available ports, failed ports: {failed_ports_info}")
        
        # Use smart port discovery if enabled
        if config.smart_port_discovery:
            # First, scan for active Unity MCP servers on available ports only
            active_ports = []
            logger.debug("Scanning for active Unity MCP servers on available ports...")
            for port in available_ports:
                if self._is_unity_mcp_server_on_port(port):
                    active_ports.append(port)
            
            if active_ports:
                logger.info(f"Found Unity MCP servers on ports: {active_ports}")
                # Try to connect to active Unity MCP servers first
                for port in active_ports:
                    if self._try_connect_to_port(port):
                        # 成功连接后从失败列表中移除（如果存在）
                        self.failed_ports.pop(port, None)
                        return True
                    # _try_connect_to_port 会自动添加失败端口
            
            # If no active Unity MCP servers found, try all available ports in order
            remaining_ports = [p for p in available_ports if p not in active_ports]
            if remaining_ports:
                logger.info(f"No active Unity MCP servers found, trying {len(remaining_ports)} remaining ports...")
                for port in remaining_ports:
                    if self._try_connect_to_port(port):
                        self.failed_ports.pop(port, None)
                        return True
                    # _try_connect_to_port 会自动添加失败端口
        else:
            # Traditional sequential port trying on available ports only
            logger.info(f"Using traditional sequential port connection on {len(available_ports)} available ports...")
            for port in available_ports:
                if self._try_connect_to_port(port):
                    self.failed_ports.pop(port, None)
                    return True
                # _try_connect_to_port 会自动添加失败端口
        
        logger.error(f"Failed to connect to Unity on any port in range {config.unity_port_start}-{config.unity_port_end}")
        return False
    
    def _is_unity_mcp_server_on_port(self, port: int) -> bool:
        """Check if there's an active Unity MCP server on the given port."""
        test_sock = None
        try:
            test_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            test_sock.settimeout(1.0)  # 增加超时时间到1秒
            test_sock.connect((self.host, port))
            
            # Send ping using length-prefixed protocol
            ping_data = b"ping"
            length_prefix = struct.pack('>I', len(ping_data))
            test_sock.sendall(length_prefix + ping_data)
            
            # Try to receive response with length prefix
            try:
                length_data = test_sock.recv(4)
                if len(length_data) != 4:
                    return False
                
                response_length = struct.unpack('>I', length_data)[0]
                if response_length > 1024:  # Sanity check
                    return False
                
                response_data = test_sock.recv(response_length)
                response = response_data.decode('utf-8')
                
                # 更宽松的检查：只要包含pong或success就认为是Unity MCP服务器
                response_lower = response.lower()
                return 'pong' in response_lower or 'success' in response_lower
            except Exception as e:
                logger.debug(f"Error receiving ping response from port {port}: {str(e)}")
                return False
        except Exception as e:
            logger.debug(f"Cannot connect to port {port}: {str(e)}")
            return False
        finally:
            if test_sock:
                try:
                    test_sock.close()
                except:
                    pass
    
    def _try_connect_to_port(self, port: int) -> bool:
        """Try to connect to a specific port."""
        try:
            logger.debug(f"Attempting to connect to Unity at {self.host}:{port}")
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.settimeout(config.connection_timeout)  # 设置连接超时
            self.sock.connect((self.host, port))
            self.port = port  # Store the successful port
            self.last_connection_time = time.time()  # 记录连接时间
            logger.info(f"Successfully connected to Unity at {self.host}:{port}")
            return True
        except Exception as e:
            logger.debug(f"Failed to connect to Unity on port {port}: {str(e)}")
            if self.sock:
                try:
                    self.sock.close()
                except:
                    pass
                self.sock = None
            # 将失败的端口添加到失败列表并记录时间戳
            self.failed_ports[port] = time.time()
            # 限制失败端口记录数量
            if len(self.failed_ports) > config.max_failed_ports:
                # 移除最早的失败记录
                oldest_port = min(self.failed_ports.items(), key=lambda x: x[1])[0]
                self.failed_ports.pop(oldest_port)
                logger.debug(f"Removed oldest failed port record: {oldest_port}")
            return False

    def disconnect(self):
        """Close the connection to the Unity Editor."""
        if self.sock:
            try:
                self.sock.close()
            except Exception as e:
                logger.error(f"Error disconnecting from Unity: {str(e)}")
            finally:
                self.sock = None

    def _recv_exact(self, sock, n_bytes: int) -> bytes:
        """Receive exactly n_bytes from socket."""
        chunks = []
        bytes_received = 0
        sock.settimeout(config.connection_timeout)
        
        while bytes_received < n_bytes:
            chunk = sock.recv(n_bytes - bytes_received)
            if not chunk:
                raise Exception(f"Socket connection broken. Expected {n_bytes} bytes, received {bytes_received}")
            chunks.append(chunk)
            bytes_received += len(chunk)
        
        return b''.join(chunks)
    
    def _send_with_length(self, sock, data: bytes):
        """Send data with length prefix (4 bytes big-endian)."""
        data_len = len(data)
        length_prefix = struct.pack('>I', data_len)  # 4 bytes big-endian unsigned int
        logger.debug(f"Sending message: length={data_len}, length_prefix={length_prefix.hex()}")
        sock.sendall(length_prefix + data)
    
    def receive_full_response(self, sock, buffer_size=config.buffer_size) -> bytes:
        """Receive a complete response from Unity using length-prefixed protocol."""
        try:
            # First, receive the 4-byte length prefix
            length_data = self._recv_exact(sock, 4)
            data_length = struct.unpack('>I', length_data)[0]  # Big-endian unsigned int
            
            logger.debug(f"Expecting message of {data_length} bytes")
            
            # Sanity check to prevent memory issues
            max_message_size = 100 * 1024 * 1024  # 100MB limit
            if data_length > max_message_size:
                raise Exception(f"Message too large: {data_length} bytes (max: {max_message_size})")
            
            # Now receive exactly that many bytes
            data = self._recv_exact(sock, data_length)
            
            logger.info(f"Received complete response ({len(data)} bytes)")
            return data
            
        except socket.timeout:
            logger.warning("Socket timeout during receive")
            raise Exception("Timeout receiving Unity response")
        except Exception as e:
            logger.error(f"Error during receive: {str(e)}")
            raise

    def is_connection_alive(self) -> bool:
        """Check if the socket connection is still alive."""
        if not self.sock:
            return False
        try:
            # Use socket error checking to verify connection status
            error = self.sock.getsockopt(socket.SOL_SOCKET, socket.SO_ERROR)
            return error == 0
        except:
            return False
    
    def send_command_with_retry(self, command_type: str, cmd: Dict[str, Any] = None, max_retries: int = 2) -> Dict[str, Any]:
        """Send command with retry mechanism and smart port switching."""
        last_error = None
        
        for attempt in range(max_retries + 1):
            try:
                return self.send_command(command_type, cmd)
            except Exception as e:
                last_error = e
                error_str = str(e).lower()
                logger.warning(f"Command attempt {attempt + 1} failed: {str(e)}")
                
                # 如果不是最后一次尝试，准备重试
                if attempt < max_retries:
                    # 如果是连接问题，标记当前端口为失败并尝试其他端口
                    if ("connection" in error_str or "broken" in error_str or 
                        "timeout" in error_str or "closed" in error_str):
                        
                        if self.port:
                            logger.info(f"Marking port {self.port} as failed due to connection issue")
                            self.failed_ports[self.port] = time.time()
                        
                        # 断开当前连接
                        self.disconnect()
                        
                        # 强制重新连接到不同端口
                        logger.info(f"Attempting to connect to different port (attempt {attempt + 1})")
                        if not self.connect(force_reconnect=True):
                            logger.warning(f"Could not reconnect to any available port")
                            time.sleep(1)
                            continue
                    else:
                        # 非连接问题，稍作等待
                        time.sleep(0.5)
                    
        # 所有重试都失败了
        raise last_error
    
    def _cleanup_expired_failed_ports(self):
        """Clean up expired failed port records."""
        current_time = time.time()
        
        # 检查是否需要清理（避免过于频繁的清理）
        if current_time - self.last_cleanup_time < 30:  # 30秒清理一次
            return
            
        expired_ports = []
        for port, failure_time in self.failed_ports.items():
            if current_time - failure_time > config.port_failure_timeout:
                expired_ports.append(port)
        
        for port in expired_ports:
            self.failed_ports.pop(port)
            logger.debug(f"Removed expired failed port: {port}")
        
        if expired_ports:
            logger.info(f"Cleaned up {len(expired_ports)} expired failed ports: {expired_ports}")
        
        self.last_cleanup_time = current_time
    
    def send_command(self, command_type: str, cmd: Dict[str, Any] = None) -> Dict[str, Any]:
        """Send a command to Unity and return its response."""
        if not self.sock and not self.connect():
            failed_ports_summary = {k: f"{(time.time() - v):.1f}s ago" for k, v in list(self.failed_ports.items())[:5]}
            raise ConnectionError(f"Not connected to Unity. Recent failed ports: {failed_ports_summary}")
        
        # Special handling for ping command
        if command_type == "ping":
            try:
                logger.debug("Sending ping to verify connection")
                ping_data = b"ping"
                self._send_with_length(self.sock, ping_data)
                response_data = self.receive_full_response(self.sock)
                response = json.loads(response_data.decode('utf-8'))
                
                # 更宽松的ping验证：检查是否包含pong或success
                response_str = str(response).lower()
                if (response.get("status") == "success" or 
                    "pong" in response_str or 
                    "success" in response_str):
                    logger.debug("Ping verification successful")
                    return {"message": "pong"}
                else:
                    logger.warning(f"Unexpected ping response: {response}")
                    # 不要立即关闭连接，给一次机会
                    return {"message": "pong", "warning": "Unexpected response format"}
                    
            except (socket.timeout, socket.error) as e:
                logger.error(f"Ping network error: {str(e)}")
                self.sock = None  # 网络错误时才关闭连接
                raise ConnectionError(f"Network error during ping: {str(e)}")
            except json.JSONDecodeError as e:
                logger.warning(f"Ping response JSON parsing failed: {str(e)}")
                # JSON解析失败但连接可能还在，尝试继续使用
                return {"message": "pong", "warning": "Response parsing failed"}
            except Exception as e:
                logger.error(f"Ping error: {str(e)}")
                # 只在严重错误时关闭连接
                if "connection" in str(e).lower() or "broken" in str(e).lower():
                    self.sock = None
                raise ConnectionError(f"Connection verification failed: {str(e)}")
        
        # Normal command handling
        command = {"type": command_type, "cmd": cmd or {}}
        try:
            # Ensure we have a valid JSON string before sending
            command_json = json.dumps(command, ensure_ascii=False)
            command_data = command_json.encode('utf-8')
            command_size = len(command_data)
            
            if command_size > config.buffer_size / 2:
                logger.warning(f"Large command detected ({command_size} bytes). This might cause issues.")
                
            logger.info(f"Sending command: {command_type} with data size: {command_size} bytes")
            
            # Send with length prefix
            self._send_with_length(self.sock, command_data)
            
            response_data = self.receive_full_response(self.sock)
            try:
                response = json.loads(response_data.decode('utf-8'))
            except (JSONDecodeError, ValueError) as je:
                logger.error(f"JSON decode error: {str(je)}")
                # Log partial response for debugging
                partial_response = response_data.decode('utf-8')[:500] + "..." if len(response_data) > 500 else response_data.decode('utf-8')
                logger.error(f"Partial response: {partial_response}")
                raise Exception(f"Invalid JSON response from Unity: {str(je)}")
            
            if response.get("status") == "error":
                error_message = response.get("error") or response.get("message", "Unknown Unity error")
                logger.error(f"Unity error: {error_message}")
                raise Exception(error_message)
            
            return response.get("result", {})
        except Exception as e:
            error_str = str(e).lower()
            logger.error(f"Communication error with Unity on port {self.port}: {str(e)}")
            
            # 如果是连接相关错误，标记当前端口为失败
            if ("connection" in error_str or "broken" in error_str or 
                "timeout" in error_str or "closed" in error_str):
                if self.port:
                    self.failed_ports[self.port] = time.time()
                    logger.info(f"Added port {self.port} to failed ports list")
            
            self.sock = None
            raise Exception(f"Failed to communicate with Unity on port {self.port}: {str(e)}")

# Global Unity connection
_unity_connection = None

def get_unity_connection() -> UnityConnection:
    """Retrieve or establish a persistent Unity connection with advanced port switching."""
    global _unity_connection
    
    # 如果已存在连接，先验证其可用性
    if _unity_connection is not None:
        try:
            # 验证现有连接是否还有效
            if _unity_connection.sock and _unity_connection.is_connection_alive():
                # 尝试ping验证
                result = _unity_connection.send_command("ping")
                logger.debug(f"Reusing existing Unity connection on port {_unity_connection.port}")
                return _unity_connection
            else:
                logger.warning(f"Existing connection on port {_unity_connection.port} is not alive")
        except Exception as e:
            logger.warning(f"Existing connection validation failed on port {_unity_connection.port}: {str(e)}")
            
        # 现有连接不可用，清理并重新创建
        try:
            _unity_connection.disconnect()
        except:
            pass
        _unity_connection = None
    
    # 创建新连接，带智能端口切换的重试机制
    max_retries = 3
    for attempt in range(max_retries):
        try:
            logger.info(f"Creating new Unity connection (attempt {attempt + 1}/{max_retries})")
            _unity_connection = UnityConnection()
            
            if not _unity_connection.connect():
                failed_ports_summary = {k: f"{(time.time() - v):.1f}s ago" for k, v in list(_unity_connection.failed_ports.items())[:5]}
                failed_ports_info = f" (recent failed ports: {failed_ports_summary})" if failed_ports_summary else ""
                _unity_connection = None
                if attempt < max_retries - 1:
                    logger.warning(f"Connection attempt {attempt + 1} failed{failed_ports_info}, retrying...")
                    time.sleep(1)
                    continue
                else:
                    raise ConnectionError(f"Could not connect to Unity on any port{failed_ports_info}. Ensure the Unity Editor and MCP Bridge are running.")
            
            # 验证新连接（更宽松的验证）
            try:
                result = _unity_connection.send_command("ping")
                logger.info(f"Successfully established new Unity connection on port {_unity_connection.port}")
                return _unity_connection
            except Exception as ping_error:
                logger.warning(f"Connection ping verification failed on port {_unity_connection.port}: {str(ping_error)}")
                # 如果ping失败但连接存在，仍然尝试使用这个连接
                if _unity_connection.sock and _unity_connection.is_connection_alive():
                    logger.info(f"Connection established on port {_unity_connection.port} despite ping verification failure")
                    return _unity_connection
                raise ping_error
                
        except Exception as e:
            logger.error(f"Connection attempt {attempt + 1} failed: {str(e)}")
            if _unity_connection:
                try:
                    _unity_connection.disconnect()
                except:
                    pass
                _unity_connection = None
            
            if attempt < max_retries - 1:
                time.sleep(1)  # 等待1秒后重试
            else:
                failed_ports = getattr(_unity_connection, 'failed_ports', {}) if _unity_connection else {}
                failed_summary = {k: f"{(time.time() - v):.1f}s ago" for k, v in list(failed_ports.items())[:5]}
                failed_info = f" (recent failed ports: {failed_summary})" if failed_summary else ""
                raise ConnectionError(f"Could not establish Unity connection after {max_retries} attempts{failed_info}: {str(e)}")
    
    return _unity_connection 
