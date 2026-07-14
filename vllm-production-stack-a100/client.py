import argparse
import json

from openai import OpenAI


def build_client(base_url: str, api_key: str) -> OpenAI:
    return OpenAI(base_url=base_url.rstrip("/") + "/v1", api_key=api_key)


def list_models(client: OpenAI) -> None:
    models = client.models.list()
    print(json.dumps(models.model_dump(), indent=2))


def completion(client: OpenAI, model: str, prompt: str, max_tokens: int) -> None:
    result = client.completions.create(
        model=model,
        prompt=prompt,
        max_tokens=max_tokens,
        temperature=0,
    )
    print(json.dumps(result.model_dump(), indent=2))


def prefix_demo(client: OpenAI, model: str, max_tokens: int) -> None:
    prompts = [
        "You are an internal operations assistant. Summarize this runbook: step one warms the cache, step two serves the request.",
        "You are an internal operations assistant. Summarize this runbook: step one warms the cache, step two serves the request. Then explain why repeated prefixes help.",
    ]
    for prompt in prompts:
        completion(client, model, prompt, max_tokens)


def main() -> None:
    parser = argparse.ArgumentParser(description="Small vLLM Production Stack client")
    parser.add_argument("--base-url", default="http://localhost:30080")
    parser.add_argument("--api-key", default="not-needed")
    parser.add_argument("--model", default="Qwen/Qwen3-0.6B")
    parser.add_argument("--max-tokens", type=int, default=64)
    parser.add_argument("command", choices=["models", "completion", "prefix-demo"])
    parser.add_argument("prompt", nargs="?", default="Explain what prefix-aware routing does.")
    args = parser.parse_args()

    client = build_client(args.base_url, args.api_key)

    if args.command == "models":
        list_models(client)
    elif args.command == "completion":
        completion(client, args.model, args.prompt, args.max_tokens)
    else:
        prefix_demo(client, args.model, args.max_tokens)


if __name__ == "__main__":
    main()
