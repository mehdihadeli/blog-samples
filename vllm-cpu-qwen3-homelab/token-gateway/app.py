import json
import os
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import unquote

import httpx
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse, Response, StreamingResponse
from redis.asyncio import Redis
from starlette.background import BackgroundTask
from transformers import AutoTokenizer


UPSTREAM_URL = os.environ.get("UPSTREAM_URL", "http://vllm:8000").rstrip("/")
UPSTREAM_API_KEY = os.environ.get("UPSTREAM_API_KEY", "changeme-production")
REDIS_URL = os.environ.get("REDIS_URL", "redis://redis:6379/0")
TOKENIZER_MODEL = os.environ.get("TOKENIZER_MODEL", "Qwen/Qwen3-0.6B")
DAILY_TOKEN_LIMIT = int(os.environ.get("DAILY_TOKEN_LIMIT", "200000"))
ADMIN_API_KEY = os.environ.get("ADMIN_API_KEY", "changeme-admin")

LIMITED_PATHS = {
    "/v1/chat/completions",
    "/v1/completions",
    "/v1/responses",
}

HOP_BY_HOP_HEADERS = {
    "connection",
    "keep-alive",
    "proxy-authenticate",
    "proxy-authorization",
    "te",
    "trailer",
    "transfer-encoding",
    "upgrade",
    "content-length",
}

app = FastAPI()


@app.on_event("startup")
async def on_startup() -> None:
    app.state.http = httpx.AsyncClient(timeout=httpx.Timeout(600.0, connect=30.0))
    app.state.redis = Redis.from_url(REDIS_URL, decode_responses=True)
    app.state.tokenizer = AutoTokenizer.from_pretrained(TOKENIZER_MODEL, trust_remote_code=True)


@app.on_event("shutdown")
async def on_shutdown() -> None:
    await app.state.http.aclose()
    await app.state.redis.aclose()


def utc_day_key() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%d")


def seconds_until_utc_midnight() -> int:
    now = datetime.now(timezone.utc)
    next_day = (now + timedelta(days=1)).replace(hour=0, minute=0, second=0, microsecond=0)
    return max(1, int((next_day - now).total_seconds()))


def bearer_user_key(request: Request) -> str:
    authorization = request.headers.get("authorization", "")
    scheme, _, token = authorization.partition(" ")
    if scheme.lower() != "bearer" or not token:
        raise HTTPException(status_code=401, detail="Missing bearer token")
    return token.strip()


def require_admin_key(request: Request) -> None:
    admin_key = request.headers.get("x-admin-key", "").strip()
    if not admin_key or admin_key != ADMIN_API_KEY:
        raise HTTPException(status_code=403, detail="Invalid admin key")


def flatten_content(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        parts: list[str] = []
        for item in value:
            parts.extend(flatten_content(item))
        return parts
    if isinstance(value, dict):
        parts: list[str] = []
        for key in ("content", "text", "input", "prompt"):
            if key in value:
                parts.extend(flatten_content(value[key]))
        return parts
    return [str(value)]


def payload_text(payload: dict[str, Any]) -> str:
    parts: list[str] = []
    if "messages" in payload:
        for message in payload.get("messages", []):
            if isinstance(message, dict):
                role = message.get("role")
                if role:
                    parts.append(str(role))
                parts.extend(flatten_content(message.get("content")))
    if "prompt" in payload:
        parts.extend(flatten_content(payload.get("prompt")))
    if "input" in payload:
        parts.extend(flatten_content(payload.get("input")))
    return "\n".join(part for part in parts if part)


def requested_completion_tokens(payload: dict[str, Any]) -> int:
    for key in ("max_tokens", "max_completion_tokens"):
        value = payload.get(key)
        if isinstance(value, int) and value > 0:
            return value
    return 0


def estimate_prompt_tokens(payload: dict[str, Any]) -> int:
    text = payload_text(payload)
    if not text:
        return 0
    encoded = app.state.tokenizer.encode(text, add_special_tokens=False)
    return len(encoded)


async def reserve_tokens(user_key: str, amount: int) -> tuple[str, int]:
    budget_key = f"token_budget:{user_key}:{utc_day_key()}"
    current_total = await app.state.redis.incrby(budget_key, amount)
    if current_total == amount:
        await app.state.redis.expire(budget_key, seconds_until_utc_midnight())
    if current_total > DAILY_TOKEN_LIMIT:
        await app.state.redis.decrby(budget_key, amount)
        raise HTTPException(
            status_code=429,
            detail={
                "message": "Daily token budget exceeded",
                "daily_limit": DAILY_TOKEN_LIMIT,
                "attempted_reservation": amount,
                "current_usage": current_total - amount,
            },
        )
    return budget_key, current_total


async def refund_tokens(budget_key: str, amount: int) -> None:
    if amount > 0:
        await app.state.redis.decrby(budget_key, amount)


def budget_key_for_day(user_key: str, day: str) -> str:
    return f"token_budget:{user_key}:{day}"


def normalize_day(day: str | None) -> str:
    if day is None or not day.strip():
        return utc_day_key()
    candidate = day.strip()
    try:
        datetime.strptime(candidate, "%Y-%m-%d")
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="Invalid day format, expected YYYY-MM-DD") from exc
    return candidate


async def read_budget_usage(user_key: str, day: str) -> dict[str, Any]:
    redis_key = budget_key_for_day(user_key, day)
    raw_usage = await app.state.redis.get(redis_key)
    usage = int(raw_usage or 0)
    ttl = await app.state.redis.ttl(redis_key)
    remaining = max(0, DAILY_TOKEN_LIMIT - usage)
    return {
        "user_key": user_key,
        "day": day,
        "daily_limit": DAILY_TOKEN_LIMIT,
        "current_usage": usage,
        "remaining": remaining,
        "redis_key": redis_key,
        "expires_in_seconds": max(ttl, 0),
    }


def upstream_headers(request: Request) -> dict[str, str]:
    headers: dict[str, str] = {}
    for name, value in request.headers.items():
        lower_name = name.lower()
        if lower_name in HOP_BY_HOP_HEADERS or lower_name == "authorization":
            continue
        headers[name] = value
    headers["Authorization"] = f"Bearer {UPSTREAM_API_KEY}"
    return headers


def response_headers(headers: httpx.Headers) -> dict[str, str]:
    return {name: value for name, value in headers.items() if name.lower() not in HOP_BY_HOP_HEADERS}


@app.get("/health")
async def health() -> Response:
    response = await app.state.http.get(f"{UPSTREAM_URL}/health")
    return Response(content=response.content, status_code=response.status_code, media_type=response.headers.get("content-type"))


@app.get("/admin/token-budget")
async def get_token_budget(request: Request, user_key: str, day: str | None = None) -> JSONResponse:
    require_admin_key(request)
    normalized_user_key = unquote(user_key).strip()
    if not normalized_user_key:
        raise HTTPException(status_code=400, detail="Missing user_key")
    budget = await read_budget_usage(normalized_user_key, normalize_day(day))
    return JSONResponse(content=budget)


@app.delete("/admin/token-budget")
async def reset_token_budget(request: Request, user_key: str, day: str | None = None) -> JSONResponse:
    require_admin_key(request)
    normalized_user_key = unquote(user_key).strip()
    if not normalized_user_key:
        raise HTTPException(status_code=400, detail="Missing user_key")
    normalized_day = normalize_day(day)
    budget_before = await read_budget_usage(normalized_user_key, normalized_day)
    redis_key = budget_before["redis_key"]
    deleted = await app.state.redis.delete(redis_key)
    return JSONResponse(
        content={
            "user_key": normalized_user_key,
            "day": normalized_day,
            "deleted": bool(deleted),
            "previous_usage": budget_before["current_usage"],
            "daily_limit": DAILY_TOKEN_LIMIT,
            "remaining": DAILY_TOKEN_LIMIT,
        }
    )


@app.api_route("/v1/{path:path}", methods=["GET", "POST", "PUT", "PATCH", "DELETE"])
async def proxy_v1(path: str, request: Request) -> Response:
    full_path = f"/v1/{path}"
    body = await request.body()
    budget_key: str | None = None
    reserved_tokens = 0

    if request.method.upper() == "POST" and full_path in LIMITED_PATHS:
        try:
            payload = json.loads(body.decode("utf-8") or "{}")
        except json.JSONDecodeError as exc:
            raise HTTPException(status_code=400, detail="Invalid JSON request body") from exc

        user_key = bearer_user_key(request)
        prompt_tokens = estimate_prompt_tokens(payload)
        completion_tokens = requested_completion_tokens(payload)
        reserved_tokens = prompt_tokens + completion_tokens
        if reserved_tokens <= 0:
            reserved_tokens = max(prompt_tokens, 1)
        budget_key, _ = await reserve_tokens(user_key, reserved_tokens)

        stream = bool(payload.get("stream", False))
        upstream = f"{UPSTREAM_URL}{full_path}"
        headers = upstream_headers(request)

        if stream:
            stream_response = await app.state.http.send(
                app.state.http.build_request(request.method, upstream, content=body, headers=headers, params=request.query_params),
                stream=True,
            )

            if stream_response.status_code >= 400:
                error_body = await stream_response.aread()
                await stream_response.aclose()
                if budget_key:
                    await refund_tokens(budget_key, reserved_tokens)
                return Response(
                    content=error_body,
                    status_code=stream_response.status_code,
                    headers=response_headers(stream_response.headers),
                    media_type=stream_response.headers.get("content-type"),
                )

            return StreamingResponse(
                stream_response.aiter_raw(),
                status_code=stream_response.status_code,
                headers=response_headers(stream_response.headers),
                media_type=stream_response.headers.get("content-type"),
                background=BackgroundTask(stream_response.aclose),
            )

        upstream_response = await app.state.http.request(
            request.method,
            upstream,
            content=body,
            headers=headers,
            params=request.query_params,
        )

        if upstream_response.status_code >= 400:
            if budget_key:
                await refund_tokens(budget_key, reserved_tokens)
            return Response(
                content=upstream_response.content,
                status_code=upstream_response.status_code,
                headers=response_headers(upstream_response.headers),
                media_type=upstream_response.headers.get("content-type", "application/json"),
            )

        actual_total = reserved_tokens
        try:
            response_json = upstream_response.json()
            usage = response_json.get("usage", {})
            total_tokens = usage.get("total_tokens")
            if isinstance(total_tokens, int) and total_tokens >= 0:
                actual_total = total_tokens
        except ValueError:
            response_json = None

        if budget_key and reserved_tokens > actual_total:
            await refund_tokens(budget_key, reserved_tokens - actual_total)

        content = upstream_response.content if response_json is None else json.dumps(response_json).encode("utf-8")
        return Response(
            content=content,
            status_code=upstream_response.status_code,
            headers=response_headers(upstream_response.headers),
            media_type=upstream_response.headers.get("content-type", "application/json"),
        )

    upstream_response = await app.state.http.request(
        request.method,
        f"{UPSTREAM_URL}{full_path}",
        content=body,
        headers=upstream_headers(request),
        params=request.query_params,
    )
    return Response(
        content=upstream_response.content,
        status_code=upstream_response.status_code,
        headers=response_headers(upstream_response.headers),
        media_type=upstream_response.headers.get("content-type"),
    )


@app.exception_handler(HTTPException)
async def http_exception_handler(_: Request, exc: HTTPException) -> JSONResponse:
    return JSONResponse(status_code=exc.status_code, content={"error": exc.detail})