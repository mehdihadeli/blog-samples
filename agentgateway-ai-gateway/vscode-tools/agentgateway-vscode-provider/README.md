# AgentGateway DeepSeek VS Code Provider

Small reference extension for this repository's `llm-vscode` listener. It
implements OAuth 2.0 Authorization Code with PKCE against Keycloak and sends
the resulting bearer token to the OpenAI-compatible AgentGateway route.

## Run locally

1. Start the sample Compose stack so Keycloak is available on `8080` and the
   VS Code LLM route is available on `4002`.
2. Open this folder in VS Code and run `npm install`.
3. Run `npm run compile`.
4. Press `F5` from a VS Code extension host launch configuration, or package
   the extension with `npm run package` and install the generated VSIX.
5. Run `AgentGateway: Sign In`, complete the Keycloak login, then select
   `DeepSeek Smart (AgentGateway)` in Chat.

To diagnose login or request failures, run `AgentGateway: Show Logs`. The
`AgentGateway` output channel records each auth and request step, status code,
callback path, and token expiry timing. It never records token, code, state, or
PKCE verifier values.

## Defaults

| Setting         | Value                                                 |
| --------------- | ----------------------------------------------------- |
| LLM route       | `http://localhost:5000/vscode/v1`                     |
| Keycloak issuer | `http://localhost:5000/auth/realms/agentgateway`      |
| Public client   | `agentgateway-vscode`                                 |
| Redirect URI    | `vscode://agentgateway.vscode-provider/auth/callback` |
| OAuth method    | Authorization Code with S256 PKCE                     |
| JWT audience    | `agentgateway`                                        |

These values match the local Compose port mapping in `samples/agentgateway-ai-gateway/deployments/docker-compose.yaml` and the route configuration in `samples/agentgateway-ai-gateway/deployments/agentgateway-config.yaml`.
and the `agentgateway-vscode` client in the sample Keycloak realm. For a
remote deployment, change `agentgateway.baseUrl`, `agentgateway.issuer`, and
the Keycloak client's redirect URI configuration.

## Security

The extension is a public OAuth client and contains no client secret. It keeps
access and refresh tokens in VS Code `SecretStorage`, validates the callback
state, uses S256 PKCE, and never logs token values. The gateway remains the
only component holding `DEEPSEEK_API_KEY`.
