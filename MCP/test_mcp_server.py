"""
Unit Tests for MCP Server
==========================
Demonstrates testing strategies for MCP servers.
Run with: pytest test_mcp_server.py -v
"""

import pytest
import asyncio
import json
from mcp_server_example import (
    get_user_handler,
    search_users_handler,
    create_user_handler,
    calculate_stats_handler,
    validate_email_handler,
    USERS_DB,
)


# ============================================================================
# FIXTURE SETUP
# ============================================================================

@pytest.fixture
def reset_db():
    """Reset database to known state before each test"""
    global USERS_DB
    USERS_DB.clear()
    USERS_DB.update({
        1: {"id": 1, "name": "Alice Johnson", "email": "alice@example.com", "role": "admin"},
        2: {"id": 2, "name": "Bob Smith", "email": "bob@example.com", "role": "user"},
        3: {"id": 3, "name": "Carol White", "email": "carol@example.com", "role": "user"},
    })
    yield
    USERS_DB.clear()


# ============================================================================
# GET USER TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_get_user_success(reset_db):
    """Test successful user retrieval"""
    result = await get_user_handler(1)
    
    assert result["status"] == "success"
    assert result["user"]["name"] == "Alice Johnson"
    assert result["user"]["email"] == "alice@example.com"
    assert "retrieved_at" in result


@pytest.mark.asyncio
async def test_get_user_not_found(reset_db):
    """Test error handling for non-existent user"""
    with pytest.raises(ValueError, match="User with ID 9999 not found"):
        await get_user_handler(9999)


@pytest.mark.asyncio
async def test_get_user_invalid_id(reset_db):
    """Test validation of user ID"""
    with pytest.raises(ValueError, match="user_id must be a positive integer"):
        await get_user_handler(-1)
    
    with pytest.raises(ValueError, match="user_id must be a positive integer"):
        await get_user_handler(0)


# ============================================================================
# SEARCH USERS TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_search_users_by_name(reset_db):
    """Test searching users by name"""
    result = await search_users_handler("alice")
    
    assert result["status"] == "success"
    assert result["count"] == 1
    assert result["results"][0]["name"] == "Alice Johnson"


@pytest.mark.asyncio
async def test_search_users_by_email(reset_db):
    """Test searching users by email"""
    result = await search_users_handler("bob@")
    
    assert result["status"] == "success"
    assert result["count"] == 1
    assert result["results"][0]["email"] == "bob@example.com"


@pytest.mark.asyncio
async def test_search_users_case_insensitive(reset_db):
    """Test case-insensitive search"""
    result = await search_users_handler("ALICE")
    
    assert result["status"] == "success"
    assert result["count"] == 1


@pytest.mark.asyncio
async def test_search_users_no_results(reset_db):
    """Test search with no matching results"""
    result = await search_users_handler("nonexistent")
    
    assert result["status"] == "success"
    assert result["count"] == 0
    assert result["results"] == []


@pytest.mark.asyncio
async def test_search_users_limit(reset_db):
    """Test search result limit"""
    result = await search_users_handler("", limit=2)
    
    assert result["status"] == "success"
    assert result["count"] <= 2


# ============================================================================
# CREATE USER TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_create_user_success(reset_db):
    """Test successful user creation"""
    result = await create_user_handler("David Brown", "david@example.com", "user")
    
    assert result["status"] == "created"
    assert result["user"]["name"] == "David Brown"
    assert result["user"]["email"] == "david@example.com"
    assert result["user"]["role"] == "user"
    assert "id" in result["user"]
    assert "created_at" in result


@pytest.mark.asyncio
async def test_create_user_duplicate_email(reset_db):
    """Test idempotency - duplicate email returns existing user"""
    result = await create_user_handler("Alice Clone", "alice@example.com", "user")
    
    assert result["status"] == "exists"
    assert result["user"]["name"] == "Alice Johnson"  # Original user


@pytest.mark.asyncio
async def test_create_user_invalid_name(reset_db):
    """Test validation of user name"""
    with pytest.raises(ValueError, match="Name must be at least 2 characters"):
        await create_user_handler("A", "test@example.com", "user")


@pytest.mark.asyncio
async def test_create_user_invalid_email(reset_db):
    """Test validation of email format"""
    with pytest.raises(ValueError, match="Invalid email format"):
        await create_user_handler("Test User", "not-an-email", "user")


@pytest.mark.asyncio
async def test_create_user_invalid_role(reset_db):
    """Test validation of user role"""
    with pytest.raises(ValueError, match="Invalid role"):
        await create_user_handler("Test User", "test@example.com", "superadmin")


# ============================================================================
# CALCULATE STATS TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_calculate_stats_total_users(reset_db):
    """Test total users calculation"""
    result = await calculate_stats_handler("total_users")
    
    assert result["status"] == "success"
    assert result["metric"] == "total_users"
    assert result["value"] == 3


@pytest.mark.asyncio
async def test_calculate_stats_users_by_role(reset_db):
    """Test users by role breakdown"""
    result = await calculate_stats_handler("users_by_role")
    
    assert result["status"] == "success"
    assert result["breakdown"]["admin"] == 1
    assert result["breakdown"]["user"] == 2


@pytest.mark.asyncio
async def test_calculate_stats_activity_summary(reset_db):
    """Test activity summary"""
    result = await calculate_stats_handler("activity_summary")
    
    assert result["status"] == "success"
    assert result["summary"]["total_users"] == 3
    assert result["summary"]["admin_users"] == 1
    assert result["summary"]["regular_users"] == 2


@pytest.mark.asyncio
async def test_calculate_stats_invalid_metric(reset_db):
    """Test error handling for unknown metric"""
    with pytest.raises(ValueError, match="Unknown metric"):
        await calculate_stats_handler("invalid_metric")


# ============================================================================
# VALIDATE EMAIL TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_validate_email_valid():
    """Test validation of valid emails"""
    valid_emails = [
        "test@example.com",
        "user.name@domain.co.uk",
        "first+last@company.org",
    ]
    
    for email in valid_emails:
        result = await validate_email_handler(email)
        assert result["is_valid"] is True, f"Failed for {email}"


@pytest.mark.asyncio
async def test_validate_email_invalid():
    """Test validation of invalid emails"""
    invalid_emails = [
        "not-an-email",
        "@domain.com",
        "user@",
        "user name@domain.com",
    ]
    
    for email in invalid_emails:
        result = await validate_email_handler(email)
        assert result["is_valid"] is False, f"Should fail for {email}"


# ============================================================================
# INTEGRATION TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_create_and_retrieve_user(reset_db):
    """Integration test: create then retrieve user"""
    # Create user
    create_result = await create_user_handler("Eve Wilson", "eve@example.com", "user")
    user_id = create_result["user"]["id"]
    
    # Retrieve user
    get_result = await get_user_handler(user_id)
    
    assert get_result["user"]["name"] == "Eve Wilson"
    assert get_result["user"]["email"] == "eve@example.com"


@pytest.mark.asyncio
async def test_create_and_search_user(reset_db):
    """Integration test: create then search for user"""
    # Create user
    await create_user_handler("Frank Miller", "frank@example.com", "guest")
    
    # Search for user
    search_result = await search_users_handler("frank")
    
    assert search_result["count"] == 1
    assert search_result["results"][0]["name"] == "Frank Miller"


# ============================================================================
# PERFORMANCE TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_concurrent_user_creation(reset_db):
    """Test handling of concurrent operations"""
    users = [
        ("User1", "user1@example.com", "user"),
        ("User2", "user2@example.com", "user"),
        ("User3", "user3@example.com", "user"),
    ]
    
    # Create users concurrently
    tasks = [create_user_handler(name, email, role) for name, email, role in users]
    results = await asyncio.gather(*tasks)
    
    # Verify all succeeded
    assert all(r["status"] == "created" for r in results)
    
    # Verify unique IDs
    ids = [r["user"]["id"] for r in results]
    assert len(ids) == len(set(ids))  # All unique


# ============================================================================
# ERROR HANDLING TESTS
# ============================================================================

@pytest.mark.asyncio
async def test_error_response_structure():
    """Test that errors return consistent structure"""
    try:
        await get_user_handler(9999)
    except ValueError as e:
        # Error should have clear message
        assert "not found" in str(e).lower()


# ============================================================================
# RUN TESTS
# ============================================================================

if __name__ == "__main__":
    pytest.main([__file__, "-v", "--tb=short"])
