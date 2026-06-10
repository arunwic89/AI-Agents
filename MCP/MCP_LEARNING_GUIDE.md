# MCP Server Learning Guide for Principal Software Engineer Interview

## 🎯 What is MCP (Model Context Protocol)?

MCP is an **open protocol** created by Anthropic that enables AI assistants to securely connect to external data sources and tools. Think of it as a standardized API contract between AI agents and external services.

### Key Concepts for Interview

1. **Protocol Architecture**
   - Client-Server model with JSON-RPC 2.0
   - Bidirectional communication via stdio, HTTP, or SSE
   - Standardized message format for tool invocation and resource access

2. **Core Components**
   - **Resources**: Data sources (files, databases, APIs)
   - **Tools**: Functions the AI can invoke
   - **Prompts**: Reusable prompt templates
   - **Sampling**: AI model completion requests

3. **Transport Layers**
   - **stdio**: Process-based communication (most common)
   - **HTTP with SSE**: Server-Sent Events for remote servers
   - **Custom transports**: Extensible for specific needs

---

## 🏗️ Architecture Patterns (Principal Engineer Focus)

### 1. **Single-Tenant vs Multi-Tenant**
```
Single-Tenant (stdio):
┌─────────┐ stdio ┌─────────────┐
│ AI Host │◄─────►│ MCP Server  │
└─────────┘       └─────────────┘
                   │
                   ├─ Tool 1
                   ├─ Tool 2
                   └─ Resource N

Multi-Tenant (HTTP/SSE):
┌─────────┐       ┌─────────────┐
│ Client 1│──────►│             │
├─────────┤       │   MCP       │
│ Client 2│──────►│  Gateway    │◄──► Databases
├─────────┤       │             │◄──► APIs
│ Client 3│──────►│             │◄──► Services
└─────────┘       └─────────────┘
```

### 2. **Stateless vs Stateful Servers**
- **Stateless**: Each request is independent (scales horizontally)
- **Stateful**: Maintains session state (requires sticky sessions or distributed cache)

### 3. **Security Patterns**
- **Authentication**: OAuth 2.0, API keys, mTLS
- **Authorization**: RBAC, ABAC, policy-based
- **Sandboxing**: Isolate tool execution
- **Rate Limiting**: Protect backend resources
- **Audit Logging**: Track all tool invocations

---

## 🔧 Tool Design Best Practices

### Idempotency
```python
# BAD: Non-idempotent
def create_user(name: str):
    return db.insert("users", {"name": name})  # Creates duplicate on retry

# GOOD: Idempotent
def create_user(name: str, idempotency_key: str):
    existing = db.find_one({"idempotency_key": idempotency_key})
    if existing:
        return existing
    return db.insert("users", {"name": name, "idempotency_key": idempotency_key})
```

### Error Handling
```python
# Return structured errors
{
    "error": {
        "code": "RESOURCE_NOT_FOUND",
        "message": "User with ID 123 not found",
        "retryable": False,
        "details": {"user_id": 123}
    }
}
```

### Pagination
```python
def list_items(limit: int = 50, cursor: str = None):
    # Use cursor-based pagination for large datasets
    return {
        "items": [...],
        "next_cursor": "eyJpZCI6MTAwfQ==",
        "has_more": True
    }
```

---

## 📊 Performance Considerations

### 1. **Caching Strategy**
```python
from functools import lru_cache
import time

# In-memory caching
@lru_cache(maxsize=1000)
def get_user(user_id: int):
    return db.query(f"SELECT * FROM users WHERE id = {user_id}")

# Redis caching for distributed systems
def get_user_cached(user_id: int):
    cache_key = f"user:{user_id}"
    cached = redis.get(cache_key)
    if cached:
        return json.loads(cached)
    
    user = db.query(f"SELECT * FROM users WHERE id = {user_id}")
    redis.setex(cache_key, 300, json.dumps(user))  # 5-min TTL
    return user
```

### 2. **Async/Await for I/O**
```python
import asyncio

async def fetch_multiple_resources(ids: list[int]):
    tasks = [fetch_resource(id) for id in ids]
    return await asyncio.gather(*tasks)
```

### 3. **Connection Pooling**
```python
from sqlalchemy.pool import QueuePool

engine = create_engine(
    "postgresql://...",
    poolclass=QueuePool,
    pool_size=10,
    max_overflow=20
)
```

---

## 🚀 Production Deployment Patterns

### Container-Based (Docker)
```dockerfile
FROM python:3.11-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
CMD ["python", "-m", "mcp_server"]
```

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mcp-server
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: mcp-server
        image: mcp-server:latest
        resources:
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
```

### Observability (OpenTelemetry)
```python
from opentelemetry import trace, metrics

tracer = trace.get_tracer(__name__)
meter = metrics.get_meter(__name__)

request_counter = meter.create_counter("mcp.requests")

@tracer.start_as_current_span("execute_tool")
def execute_tool(tool_name: str, args: dict):
    request_counter.add(1, {"tool": tool_name})
    # Tool logic here
```

---

## 🧪 Testing Strategy

### Unit Tests
```python
import pytest
from mcp_server import MCPServer

@pytest.fixture
def server():
    return MCPServer()

def test_tool_execution(server):
    result = server.execute_tool("get_user", {"id": 1})
    assert result["name"] == "John Doe"

def test_error_handling(server):
    with pytest.raises(ValueError):
        server.execute_tool("invalid_tool", {})
```

### Integration Tests
```python
async def test_end_to_end():
    async with MCPClient("stdio://python mcp_server.py") as client:
        tools = await client.list_tools()
        assert "get_user" in [t.name for t in tools]
        
        result = await client.call_tool("get_user", {"id": 1})
        assert result["status"] == "success"
```

---

## 💡 Interview Discussion Points

### 1. **Scalability**
- How would you scale an MCP server to handle 10,000 concurrent clients?
- Discuss: Load balancing, horizontal scaling, stateless design

### 2. **Reliability**
- What happens if a tool execution takes 30 seconds?
- Discuss: Timeouts, async processing, circuit breakers, retries

### 3. **Security**
- How do you prevent a malicious AI from deleting production data?
- Discuss: Permission models, sandboxing, audit logs, dry-run mode

### 4. **Monitoring**
- What metrics would you track for an MCP server?
- Discuss: Request latency, error rates, tool usage, resource consumption

### 5. **Design Trade-offs**
- When would you use HTTP vs stdio transport?
- Discuss: Deployment model, security requirements, latency needs

---

## 📚 Advanced Topics

### 1. **Dynamic Tool Registration**
```python
class DynamicMCPServer:
    def __init__(self):
        self.tools = {}
    
    def register_tool(self, name: str, func: callable, schema: dict):
        self.tools[name] = {"func": func, "schema": schema}
    
    def list_tools(self):
        return [{"name": k, "schema": v["schema"]} for k, v in self.tools.items()]
```

### 2. **Tool Composition**
```python
# Allow AI to chain tools
def execute_workflow(steps: list[dict]):
    results = []
    context = {}
    for step in steps:
        tool_name = step["tool"]
        args = {k: v.format(**context) if isinstance(v, str) else v 
                for k, v in step["args"].items()}
        result = execute_tool(tool_name, args)
        context[step.get("output_var", f"step_{len(results)}")] = result
        results.append(result)
    return results
```

### 3. **Streaming Responses**
```python
async def stream_large_file(file_path: str):
    async with aiofiles.open(file_path, 'rb') as f:
        while chunk := await f.read(8192):
            yield {"chunk": chunk.decode(), "done": False}
        yield {"chunk": "", "done": True}
```

---

## 🎓 Sample Interview Questions & Answers

**Q: How would you design an MCP server for a financial trading platform?**

A: Key considerations:
1. **Low Latency**: Use in-process caching, pre-warmed connections
2. **Data Consistency**: Implement optimistic locking, idempotency keys
3. **Audit Trail**: Log every trade decision with full context
4. **Risk Controls**: Rate limiting, position limits, circuit breakers
5. **Real-time Data**: WebSocket connections, event-driven updates

**Q: What's the difference between MCP and traditional REST APIs?**

A: 
- **MCP**: AI-native protocol with tool discovery, structured schemas, conversational context
- **REST**: Human-designed endpoints, requires API docs, stateless by design
- **MCP**: Designed for agent-to-service communication with retries, error recovery
- **REST**: Designed for client-server web applications

**Q: How do you handle long-running operations in MCP?**

A:
1. Return immediately with a task ID
2. Provide a separate "get_task_status" tool
3. Use webhooks or SSE for completion notifications
4. Consider async/await patterns for the client

---

## 🔗 Resources

- **Official Spec**: https://spec.modelcontextprotocol.io/
- **Python SDK**: https://github.com/modelcontextprotocol/python-sdk
- **TypeScript SDK**: https://github.com/modelcontextprotocol/typescript-sdk
- **Example Servers**: https://github.com/modelcontextprotocol/servers

---

## ✅ Principal Engineer Checklist

Before your interview, be prepared to discuss:
- [ ] Architecture decisions (monolithic vs microservices MCP servers)
- [ ] Security models (authentication, authorization, sandboxing)
- [ ] Scalability patterns (horizontal scaling, caching, async processing)
- [ ] Testing strategy (unit, integration, load testing)
- [ ] Monitoring and observability (metrics, logs, traces)
- [ ] Error handling and resilience (retries, circuit breakers, fallbacks)
- [ ] Data consistency guarantees (idempotency, transactions)
- [ ] Performance optimization (connection pooling, batching)
- [ ] Deployment strategies (blue-green, canary, rolling)
- [ ] Cost optimization (resource utilization, auto-scaling)
