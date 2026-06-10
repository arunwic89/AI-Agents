# 🚀 MCP Server Learning Project

**Complete guide for learning Model Context Protocol (MCP) servers for Principal Software Engineer interviews**

## 📚 What's Included

This project contains:
- **MCP_LEARNING_GUIDE.md**: Comprehensive theoretical guide covering architecture, patterns, and interview topics
- **mcp_server_example.py**: Production-ready MCP server implementation
- **mcp_client_example.py**: Client for testing the server
- **test_mcp_server.py**: Complete test suite with 25+ tests
- **requirements.txt**: All dependencies

## 🎯 Learning Objectives

By working through this project, you'll understand:

✅ **Core MCP Concepts**
- Resources vs Tools vs Prompts
- JSON-RPC 2.0 communication protocol
- stdio vs HTTP/SSE transports

✅ **Architecture Patterns**
- Stateless vs stateful server design
- Error handling and validation strategies
- Async operations and performance optimization

✅ **Production Considerations**
- Security (authentication, authorization, sandboxing)
- Scalability (caching, connection pooling, load balancing)
- Observability (logging, metrics, tracing)

✅ **Testing Strategies**
- Unit tests for individual tools
- Integration tests for workflows
- Error handling verification

## 🚀 Quick Start

### 1. Install Dependencies

```bash
cd MCP
pip install -r requirements.txt
```

### 2. Run the Server (Manual Test)

```bash
python mcp_server_example.py
```

The server will start and listen on stdio. You'll see logs indicating it's running.

### 3. Test with Client

In a new terminal:

```bash
python mcp_client_example.py
```

This will run automated tests against all tools:
- ✅ List tools and resources
- ✅ Get user by ID
- ✅ Search users
- ✅ Create new user
- ✅ Calculate statistics
- ✅ Validate email
- ✅ Test error handling

### 4. Interactive Mode

```bash
python mcp_client_example.py --interactive
```

Commands:
- `list_tools` - Show available tools
- `list_resources` - Show available resources
- `call_tool get_user {"user_id": 1}` - Call a tool
- `exit` - Quit

### 5. Run Tests

```bash
pytest test_mcp_server.py -v
```

Expected output: **25+ tests passing** ✅

## 📖 Code Tour

### Server Architecture (`mcp_server_example.py`)

```
┌─────────────────────────────────────┐
│         MCP Server                  │
├─────────────────────────────────────┤
│  Resources (Data Access)            │
│  ├─ db://users                      │
│  └─ config://server                 │
├─────────────────────────────────────┤
│  Tools (Actions)                    │
│  ├─ get_user(user_id)               │
│  ├─ search_users(query, limit)      │
│  ├─ create_user(name, email, role)  │
│  ├─ calculate_stats(metric)         │
│  └─ validate_email(email)           │
├─────────────────────────────────────┤
│  Error Handling & Validation        │
│  ├─ Input validation                │
│  ├─ Business logic checks           │
│  └─ Structured error responses      │
├─────────────────────────────────────┤
│  Logging & Observability            │
│  ├─ Request/response logging        │
│  ├─ Error tracking                  │
│  └─ Performance monitoring hooks    │
└─────────────────────────────────────┘
```

### Key Functions

**Resources** (Read-only data access):
```python
@app.list_resources()  # List available data sources
@app.read_resource()   # Read specific resource by URI
```

**Tools** (Actions AI can take):
```python
@app.list_tools()      # Advertise available tools
@app.call_tool()       # Execute a tool with arguments
```

**Tool Handlers**:
- `get_user_handler()` - Demonstrates error handling
- `search_users_handler()` - Shows search patterns
- `create_user_handler()` - Implements idempotency
- `calculate_stats_handler()` - Async operation example
- `validate_email_handler()` - Simple utility pattern

## 🎤 Interview Preparation

### Demo Flow for Interview

1. **Architecture Overview** (5 min)
   - Explain MCP protocol basics
   - Show server architecture diagram
   - Discuss why you chose certain patterns

2. **Code Walkthrough** (10 min)
   - Walk through `mcp_server_example.py`
   - Highlight key design decisions:
     - Tool registration pattern
     - Error handling strategy
     - Validation approach
     - Idempotency in create_user

3. **Live Demo** (5 min)
   - Run `python mcp_client_example.py`
   - Show successful tool calls
   - Demonstrate error handling
   - Show test output

4. **Testing Strategy** (5 min)
   - Show `test_mcp_server.py`
   - Explain test categories:
     - Happy path tests
     - Error cases
     - Edge cases
     - Integration tests

5. **Production Considerations** (10 min)
   - Discuss from `MCP_LEARNING_GUIDE.md`:
     - Scalability patterns
     - Security model
     - Monitoring approach
     - Deployment strategy

### Key Discussion Points

**Q: Why use MCP instead of REST APIs?**
A: 
- AI-native protocol with tool discovery
- Structured schemas for type safety
- Built-in error recovery patterns
- Optimized for agent-to-service communication

**Q: How would you scale this to 10,000 concurrent users?**
A:
- Move from stdio to HTTP/SSE transport
- Implement connection pooling
- Add Redis for distributed caching
- Use load balancer with sticky sessions
- Implement rate limiting per client

**Q: How do you prevent malicious tool calls?**
A:
- Input validation on all parameters
- Authorization checks before execution
- Audit logging of all operations
- Dry-run mode for destructive operations
- Sandboxing tool execution

**Q: What metrics would you track?**
A:
- Request latency (p50, p95, p99)
- Error rates by tool
- Tool usage distribution
- Resource consumption (CPU, memory)
- Cache hit rates

## 🔍 Code Patterns Demonstrated

### 1. Idempotency Pattern
```python
# In create_user_handler()
# Check for duplicate email before creating
for user in USERS_DB.values():
    if user["email"].lower() == email.lower():
        return {"status": "exists", "user": user}
```

### 2. Structured Error Handling
```python
try:
    result = await handler(arguments)
    return [TextContent(text=json.dumps(result))]
except Exception as e:
    error_response = {
        "status": "error",
        "error": {"type": type(e).__name__, "message": str(e)}
    }
    return [TextContent(text=json.dumps(error_response))]
```

### 3. Async Operations
```python
async def calculate_stats_handler(metric: str):
    # Simulate async processing
    await asyncio.sleep(0.5)
    # Perform calculations
    return results
```

### 4. Input Validation
```python
# Multiple layers of validation
if not isinstance(user_id, int) or user_id < 1:
    raise ValueError("user_id must be a positive integer")

if not validate_email_format(email):
    raise ValueError(f"Invalid email format: {email}")
```

## 📊 Test Coverage

Run tests with coverage:

```bash
pytest test_mcp_server.py -v --cov=mcp_server_example --cov-report=html
```

Open `htmlcov/index.html` to see detailed coverage report.

### Test Categories

- **Happy Path**: 8 tests
- **Error Handling**: 7 tests
- **Validation**: 6 tests
- **Integration**: 3 tests
- **Performance**: 1 test

## 🏗️ Extension Ideas

Practice these for your interview:

1. **Add Database**: Replace in-memory store with PostgreSQL
   ```python
   import asyncpg
   
   async def get_user_handler(user_id: int):
       async with db_pool.acquire() as conn:
           user = await conn.fetchrow("SELECT * FROM users WHERE id = $1", user_id)
   ```

2. **Add Authentication**: Implement API key validation
   ```python
   async def validate_auth(api_key: str) -> bool:
       # Check API key against database
       return await db.check_api_key(api_key)
   ```

3. **Add Caching**: Implement Redis caching
   ```python
   @cached(ttl=300)  # 5-minute cache
   async def get_user_handler(user_id: int):
       # ...
   ```

4. **Add Metrics**: Track tool usage
   ```python
   from prometheus_client import Counter
   
   tool_calls = Counter('mcp_tool_calls', 'Tool invocations', ['tool_name'])
   ```

5. **Add Rate Limiting**: Prevent abuse
   ```python
   from slowapi import Limiter
   
   @limiter.limit("10/minute")
   async def call_tool(name: str, arguments: Any):
       # ...
   ```

## 📚 Additional Resources

- **Official Spec**: https://spec.modelcontextprotocol.io/
- **Python SDK**: https://github.com/modelcontextprotocol/python-sdk
- **Example Servers**: https://github.com/modelcontextprotocol/servers
- **MCP Inspector**: https://github.com/modelcontextprotocol/inspector

## ✅ Pre-Interview Checklist

- [ ] Read through `MCP_LEARNING_GUIDE.md`
- [ ] Run the server and client successfully
- [ ] Run all tests and understand what they're testing
- [ ] Modify code to add a new tool (practice)
- [ ] Be able to explain the architecture diagram
- [ ] Prepare answers to "Discussion Points" questions
- [ ] Practice explaining trade-offs (stateless vs stateful, stdio vs HTTP)
- [ ] Review security considerations
- [ ] Understand scalability patterns
- [ ] Know how to deploy (Docker, Kubernetes)

## 🎯 Principal Engineer Focus Areas

As a principal engineer, emphasize:

1. **System Design**: How would you architect MCP servers at scale?
2. **Trade-offs**: When to use MCP vs gRPC vs REST?
3. **Security**: Zero-trust model, least privilege, audit trails
4. **Observability**: Distributed tracing, log aggregation, alerting
5. **Team Impact**: How would you mentor team on MCP best practices?
6. **Standards**: Establishing conventions, code reviews, documentation

## 💬 Getting Help

If you have questions about the code or concepts:
1. Review `MCP_LEARNING_GUIDE.md` first
2. Check official MCP documentation
3. Run tests to see expected behavior
4. Experiment by modifying the code

Good luck with your interview! 🚀
