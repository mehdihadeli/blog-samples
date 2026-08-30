import * as vscode from "vscode";
import { PkceAuth } from "./auth";
import { DiagnosticLogger } from "./logger";

const VENDOR = "agentgateway-deepseek";
const MODEL_ID = "deepseek-smart";

export class AgentGatewayProvider implements vscode.LanguageModelChatProvider {
  private readonly changed = new vscode.EventEmitter<void>();
  readonly onDidChangeLanguageModelChatInformation = this.changed.event;

  constructor(
    private readonly auth: PkceAuth,
    private readonly logger: DiagnosticLogger,
  ) {}

  async provideLanguageModelChatInformation(
    _options: vscode.PrepareLanguageModelChatModelOptions,
    _token: vscode.CancellationToken,
  ): Promise<vscode.LanguageModelChatInformation[]> {
    const signedIn = await this.auth.hasSession();
    return [
      {
        id: MODEL_ID,
        name: MODEL_ID,
        displayName: "DeepSeek Smart (AgentGateway)",
        family: "deepseek",
        version: "agentgateway",
        isUserSelectable: true,
        isBYOK: true,
        detail: signedIn
          ? "Authenticated AgentGateway DeepSeek route"
          : "Sign in to use AgentGateway DeepSeek",
        tooltip: signedIn
          ? "AgentGateway DeepSeek route"
          : "Sign in to AgentGateway",
        maxInputTokens: 640000,
        maxOutputTokens: 4096,
        capabilities: { toolCalling: false, imageInput: false, vision: false },
        configurationSchema: {
          type: "object",
          properties: { temperature: { type: "number", default: 0.7 } },
        },
      } as vscode.LanguageModelChatInformation,
    ];
  }

  async provideLanguageModelChatResponse(
    _modelInfo: vscode.LanguageModelChatInformation,
    messages: readonly vscode.LanguageModelChatRequestMessage[],
    options: vscode.ProvideLanguageModelChatResponseOptions,
    progress: vscode.Progress<vscode.LanguageModelResponsePart>,
    token: vscode.CancellationToken,
  ): Promise<void> {
    const accessToken = await this.auth.getAccessToken();
    const config = vscode.workspace.getConfiguration("agentgateway");
    const baseUrl = config
      .get<string>("baseUrl", "http://localhost:14002/v1")
      .replace(/\/$/u, "");
    this.logger.info("provider.request.start", {
      baseUrl,
      model: MODEL_ID,
      messageCount: messages.length,
    });
    const controller = new AbortController();
    const cancellation = token.onCancellationRequested(() =>
      controller.abort(),
    );
    try {
      const response = await fetch(`${baseUrl}/chat/completions`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${accessToken}`,
          "Content-Type": "application/json",
        },
        signal: controller.signal,
        body: JSON.stringify({
          model: MODEL_ID,
          messages: messages.map((message) => ({
            role: message.role,
            content: this.textOf(message.content),
          })),
          stream: true,
          stream_options: { include_usage: true },
          max_tokens: options.modelOptions?.max_tokens ?? 4096,
          temperature: options.modelOptions?.temperature ?? 0.7,
        }),
      });
      this.logger.info("provider.request.response", {
        status: response.status,
        ok: response.ok,
      });
      if (!response.ok)
        throw new Error(
          `AgentGateway request failed (${response.status}): ${await response.text()}`,
        );
      if (!response.body)
        throw new Error("AgentGateway returned an empty response stream.");
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      while (true) {
        const result = await reader.read();
        if (result.done) break;
        buffer += decoder.decode(result.value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";
        for (const line of lines) {
          if (!line.startsWith("data: ")) continue;
          const data = line.slice(6);
          if (data === "[DONE]") {
            this.logger.info("provider.stream.done");
            return;
          }
          try {
            const chunk = JSON.parse(data) as {
              choices?: Array<{ delta?: { content?: string } }>;
            };
            const content = chunk.choices?.[0]?.delta?.content;
            if (content)
              progress.report(new vscode.LanguageModelTextPart(content));
          } catch {}
        }
      }
    } finally {
      cancellation.dispose();
      this.logger.info("provider.request.finished");
    }
  }

  async provideTokenCount(
    _modelInfo: vscode.LanguageModelChatInformation,
    text: string | vscode.LanguageModelChatRequestMessage,
    _token: vscode.CancellationToken,
  ): Promise<number> {
    const value = typeof text === "string" ? text : this.textOf(text.content);
    return Math.ceil(value.length / 4);
  }

  private textOf(
    content: vscode.LanguageModelChatRequestMessage["content"],
  ): string {
    return content
      .map((part) =>
        part instanceof vscode.LanguageModelTextPart ? part.value : "",
      )
      .join("");
  }

  refresh(): void {
    this.changed.fire();
  }
}

export { VENDOR };
