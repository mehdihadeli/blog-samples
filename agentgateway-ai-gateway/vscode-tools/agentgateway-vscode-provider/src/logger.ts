import * as vscode from "vscode";

export class DiagnosticLogger {
  constructor(private readonly output: vscode.OutputChannel) {}

  info(step: string, details?: Record<string, unknown>): void {
    const suffix = details ? ` ${JSON.stringify(details)}` : "";
    this.output.appendLine(`[${new Date().toISOString()}] ${step}${suffix}`);
  }

  error(step: string, error: unknown): void {
    this.info(`${step}.error`, {
      message: error instanceof Error ? error.message : String(error),
    });
  }
}
