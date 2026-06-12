# Research Agent with Web Search in .NET

This sample shows a small .NET research-agent pipeline that turns a topic into a structured `ResearchBrief`.

It is intentionally runnable without cloud credentials. Instead of calling a live provider, it uses:

- a local catalog of existing posts
- a local search corpus
- a deterministic search client that ranks documents by token overlap
- a synthesis step that emits JSON the next pipeline stage can consume

## Run the sample

```bash
dotnet run --project ./src/ResearchAgent.Sample -- --topic "research agent web search in .NET"
```

Optional arguments:

```bash
dotnet run --project ./src/ResearchAgent.Sample -- --topic "research agent web search in .NET" --max-iterations 3
```

## What it demonstrates

- bounded search iteration
- overlap detection against existing posts
- source ranking and deduplication
- typed brief generation instead of free-form prose
- a review-friendly JSON checkpoint between research and writing

## Run the tests

```bash
dotnet test ./tests/ResearchAgent.Sample.Tests/ResearchAgent.Sample.Tests.csproj
```

## Production note

The sample keeps `IWebSearchClient` separate from `ResearchAgent`. In a production system, that boundary is where you would swap in a real web-search implementation backed by Microsoft Agent Framework or another provider.
