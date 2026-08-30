import * as vscode from "vscode";
import { PkceAuth } from "./auth";
import { AgentGatewayProvider, VENDOR } from "./provider";
import { DiagnosticLogger } from "./logger";

export async function activate(
  context: vscode.ExtensionContext,
): Promise<void> {
  const output = vscode.window.createOutputChannel("AgentGateway");
  const logger = new DiagnosticLogger(output);
  const auth = new PkceAuth(context, logger);
  const provider = new AgentGatewayProvider(auth, logger);
  context.subscriptions.push(output);
  logger.info("extension.activated", {
    version: context.extension.packageJSON.version,
  });
  context.subscriptions.push(
    vscode.lm.registerLanguageModelChatProvider(VENDOR, provider),
    vscode.window.registerUriHandler({
      handleUri: (uri) => {
        logger.info("extension.uri-received", {
          authority: uri.authority,
          path: uri.path,
          queryLength: uri.query.length,
        });
        if (auth.isLogoutCallback(uri)) {
          logger.info("extension.logout-callback-received");
          void vscode.window.showInformationMessage(
            "Logged out of Keycloak and AgentGateway.",
          );
          return;
        }
        void auth.handleCallback(uri).then(
          () => {
            provider.refresh();
            void vscode.window.showInformationMessage(
              "Signed in to AgentGateway.",
            );
          },
          (error: unknown) => (
            logger.error("extension.callback-failed", error),
            void vscode.window.showErrorMessage(
              error instanceof Error ? error.message : String(error),
            )
          ),
        );
      },
    }),
    vscode.commands.registerCommand("agentgateway.login", async () => {
      logger.info("command.login");
      await auth.signIn();
    }),
    vscode.commands.registerCommand("agentgateway.logout", async () => {
      await auth.signOut();
      provider.refresh();
      void vscode.window.showInformationMessage(
        "Signed out of AgentGateway. Keycloak logout opened in your browser.",
      );
    }),
    vscode.commands.registerCommand("agentgateway.openSettings", () =>
      vscode.commands.executeCommand(
        "workbench.action.openSettings",
        "agentgateway",
      ),
    ),
    vscode.commands.registerCommand("agentgateway.showLogs", () =>
      output.show(),
    ),
  );
  await activateCopilotChat(logger);
  provider.refresh();
}

export function deactivate(): void {}

async function activateCopilotChat(logger: DiagnosticLogger): Promise<void> {
  try {
    await vscode.extensions.getExtension("github.copilot-chat")?.activate();
  } catch (error) {
    logger.error("extension.copilot-chat-activation-failed", error);
  }
}
