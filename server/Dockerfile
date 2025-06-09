# 使用官方 Python 镜像
FROM python:3.11-slim

# 设置工作目录
WORKDIR /app

# 安装依赖
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# 拷贝应用文件
COPY server.py .

# 运行 uvicorn，监听 0.0.0.0:8000
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]