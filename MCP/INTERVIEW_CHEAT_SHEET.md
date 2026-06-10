# 🎯 MCP Interview Cheat Sheet

**Quick reference for Principal Software Engineer interview**

## 30-Second Elevator Pitch

> "MCP (Model Context Protocol) is an open protocol by Anthropic that standardizes how AI assistants connect to external tools and data. It's like OpenAPI/Swagger for AI agents - providing tool discovery, structured schemas, and standardized communication. I've built production-ready MCP servers with focus on security, scalability, and reliability."

---

## Core Architecture (Draw This)

```
┌─────────────┐
│  AI Agent   │
│  (Client)   │
└──────┬──────┘
       │ JSON-RPC 2.0
       │ (stdio/HTTP/SSE)
       │
┌──────▼──────────────┐
│   MCP Server        │
├─────────────────────┤
│ Resources  │ Tools  │
│ (Read)     │ (Act)  │
└──────┬──────────┬───┘
       │          │
   ┌───▼──┐   ┌──▼────┐
   │ Data │   │Actions│
   └──────┘   └───────┘
```

---

## Key Concepts (30 seconds each)

### Resources
- **What**: Data sources (files, databases, APIs)
- **Read-only**: AI can read but not modify
- **URI-based**: `db://users`, `file:///path/to/doc`

### Tools
- **What**: Functions AI can invoke
- **Actions**: Can read AND write
- **Discoverable**: AI learns capabilities via schemas

### Prompts
- **What**: Reusable prompt templates
- **Standardized**: Consistent task patterns
- **Parameterized**: Accept arguments

---

## Critical Design Patterns

### 1. Idempotency (Always mention this!)
```python
# BAD: Create duplicate on retry
def create_user(name):
    return db.insert({"name": name})

# GOOD: Check before creating
def create_user(name, idempotency_key):
    existing = db.find(idempotency_key)
    if existing:
        return existing
    return db.insert({"name": name, "key": idempotency_key})
```

### 2. Error Handling
```python
# Return structured errors
{
    "status": "error",
    "error": {
        "code": "RESOURCE_NOT_FOUND",
        "message": "User 123 not found",
        "retryable": False
    }
}
```

### 3. Input Validation
```python
# Validate BEFORE execution
if not isinstance(user_id, int) or user_id < 1:
    raise ValueError("Invalid user_id")
```

---

## Security Model (Must Know)

1. **Authentication**: Who is calling?
   - API keys, OAuth 2.0, mTLS
   
2. **Authorization**: What can they do?
   - RBAC (Role-Based Access Control)
   - Permission checks before tool execution
   
3. **Sandboxing**: Isolate tool execution
   - Separate process/container
   - Resource limits (CPU, memory, time)
   
4. **Audit Logging**: Track everything
   - Who, what, when, result
   - Immutable logs for compliance

---

## Scalability Patterns

### Vertical Scaling (Single Server)
- ✅ Connection pooling
- ✅ In-memory caching (LRU)
- ✅ Async/await for I/O

### Horizontal Scaling (Multiple Servers)
- ✅ Stateless design
- ✅ Distributed cache (Redis)
- ✅ Load balancer
- ✅ Sticky sessions (if stateful)

### Key Metrics to Track
```
- Request latency (p50, p95, p99)
- Error rate by tool
- Tool usage frequency
- Cache hit rate
- Active connections
```

---

## Transport Comparison

| Feature      | stdio | HTTP/SSE |
|--------------|-------|----------|
| **Use Case** | Local | Remote   |
| **Security** | Process isolation | TLS + Auth |
| **Scale**    | Single client | Multiple clients |
| **Latency**  | Low (~1ms) | Medium (~10ms) |
| **Deploy**   | Simple | Complex |

**Interview Tip**: "stdio for development and single-user; HTTP/SSE for production multi-tenant systems"

---

## Production Checklist (Mention These)

### Reliability
- [ ] Health check endpoint
- [ ] Circuit breakers for external calls
- [ ] Retry logic with exponential backoff
- [ ] Graceful degradation
- [ ] Timeout on all operations

### Observability
- [ ] Structured logging (JSON)
- [ ] Distributed tracing (OpenTelemetry)
- [ ] Metrics (Prometheus)
- [ ] Alerting (error rate, latency)

### Performance
- [ ] Connection pooling
- [ ] Caching strategy
- [ ] Async operations
- [ ] Batching where possible
- [ ] Database query optimization

### Security
- [ ] Authentication on all endpoints
- [ ] Authorization checks
- [ ] Input validation
- [ ] Rate limiting
- [ ] Audit logging

---

## Common Interview Questions

### Q1: "Why MCP instead of REST?"
**Answer**: "MCP is AI-native with built-in tool discovery, structured schemas for type safety, and optimized for agent-to-service communication. REST requires hand-written API documentation and wasn't designed for autonomous agents."

### Q2: "How would you scale to 10,000 concurrent users?"
**Answer**: "Switch to HTTP/SSE transport, implement stateless design with Redis for sessions, add load balancer with health checks, implement rate limiting per client, use connection pooling for databases, and deploy on Kubernetes with HPA (Horizontal Pod Autoscaler)."

### Q3: "How do you prevent malicious AI from deleting data?"
**Answer**: 
1. Authorization layer - check permissions before execution
2. Audit logging - track all operations
3. Dry-run mode - preview before execution
4. Human-in-the-loop for destructive operations
5. Sandboxing - execute in isolated environment
6. Rate limiting - prevent abuse

### Q4: "What if a tool call takes 30 seconds?"
**Answer**: "Implement async pattern: return task ID immediately, provide separate get_task_status tool, use webhooks/SSE for completion notification. Add timeout defaults (5-10s) with ability to override for known long-running operations."

### Q5: "How do you handle versioning?"
**Answer**: "Include version in tool names (get_user_v2), maintain backward compatibility for deprecated tools, use semantic versioning, provide migration guides, and sunset old versions with advance notice."

---

## Code Demo Flow (5 minutes)

1. **Show server**: `python mcp_server_example.py`
2. **Run tests**: `pytest test_mcp_server.py -v`
3. **Live demo**: `python mcp_client_example.py`
4. **Highlight**:
   - Tool registration
   - Error handling
   - Validation
   - Idempotency in create_user

---

## Technical Deep Dives (If Asked)

### JSON-RPC 2.0 Message Format
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_user",
    "arguments": {"user_id": 1}
  },
  "id": 1
}
```

### Tool Schema (JSON Schema)
```json
{
  "name": "get_user",
  "description": "Get user by ID",
  "inputSchema": {
    "type": "object",
    "properties": {
      "user_id": {"type": "integer"}
    },
    "required": ["user_id"]
  }
}
```

### Resource URI Patterns
- `file:///path/to/file` - File system
- `db://table_name` - Database table
- `api://endpoint` - REST API
- `cache://key` - Cache entry

---

## Deployment Example

```yaml
# Kubernetes Deployment
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
        image: mcp-server:1.0
        ports:
        - containerPort: 8080
        env:
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: db-secret
              key: url
        resources:
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
```

---

## Advanced Topics (Principal Level)

### Multi-Tenancy
- Tenant isolation (data, rate limits)
- Per-tenant configuration
- Shared vs dedicated resources

### Disaster Recovery
- Regular backups
- Multi-region deployment
- Failover strategy
- RTO/RPO targets

### Cost Optimization
- Auto-scaling based on load
- Spot instances for dev/test
- Cache expensive operations
- Rightsizing resources

### Team Leadership
- Establish coding standards
- Code review guidelines
- Documentation requirements
- Training junior engineers

---

## Final Advice

✅ **Show, don't just tell**: Have code ready to demonstrate
✅ **Discuss trade-offs**: Every decision has pros/cons
✅ **Think production**: Security, scale, reliability
✅ **Use concrete numbers**: "p95 latency under 100ms"
✅ **Leadership mindset**: How would you guide a team?

---

## Quick Stats to Memorize

- **JSON-RPC version**: 2.0
- **Default timeout**: 5-10 seconds
- **Recommended cache TTL**: 5-15 minutes
- **Rate limit**: 10-100 requests/minute
- **Connection pool size**: 10-50 connections
- **Target latency p95**: < 100ms

---

## Emergency Backup Questions

If you blank, fall back to:
1. "Let me draw the architecture..."
2. "There are trade-offs here, such as..."
3. "In production, I'd also consider..."
4. "From a security perspective..."
5. "For observability, I'd track..."

**Good luck! 🚀**
