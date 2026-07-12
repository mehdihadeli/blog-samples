import os

from openai import OpenAI


def main() -> None:
    client = OpenAI(
        base_url=os.getenv("VLLM_BASE_URL", "http://127.0.0.1:8000/v1"),
        api_key=os.getenv("VLLM_API_KEY", "changeme-local"),
    )

    response = client.chat.completions.create(
        model=os.getenv("VLLM_MODEL", "Qwen/Qwen3-0.6B"),
        messages=[
            {
                "role": "system",
                "content": "You answer briefly and directly.",
            },
            {
                "role": "user",
                "content": "Explain why vLLM is useful in a small home lab.",
            },
        ],
        max_tokens=160,
        temperature=0.2,
    )

    print(response.choices[0].message.content)


if __name__ == "__main__":
    main()