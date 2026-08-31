import * as crypto from "node:crypto";
import * as vscode from "vscode";
import { DiagnosticLogger } from "./logger";

type StoredTokens = {
  accessToken: string;
  idToken?: string;
  refreshToken?: string;
  expiresAt: number;
};

const TOKENS_KEY = "agentgateway.oauth.tokens";
const PENDING_KEY = "agentgateway.oauth.pending";
const PENDING_GLOBAL_KEY = "agentgateway.oauth.pending";
const REDIRECT_URI = "vscode://agentgateway.vscode-provider/auth/callback";
const LOGOUT_REDIRECT_URI =
  "vscode://agentgateway.vscode-provider/auth/logout-callback";

function base64Url(bytes: Uint8Array): string {
  return Buffer.from(bytes).toString("base64url");
}

function randomUrlValue(): string {
  return base64Url(crypto.randomBytes(32));
}

function sha256Base64Url(value: string): string {
  return base64Url(crypto.createHash("sha256").update(value).digest());
}

export class PkceAuth {
  private pending?: { state: string; verifier: string };

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly logger: DiagnosticLogger,
  ) {}

  get redirectUri(): string {
    return REDIRECT_URI;
  }

  private settings(): { issuer: string; clientId: string; scope: string } {
    const config = vscode.workspace.getConfiguration("agentgateway");
    return {
      issuer: config
        .get<string>("issuer", "http://localhost:5000/auth/realms/agentgateway")
        .replace(/\/$/u, ""),
      clientId: config.get<string>("clientId", "agentgateway-vscode"),
      scope: config.get<string>("scope", "openid profile email"),
    };
  }

  private async readTokens(): Promise<StoredTokens | undefined> {
    const raw = await this.context.secrets.get(TOKENS_KEY);
    if (!raw) return undefined;
    try {
      const parsed = JSON.parse(raw) as StoredTokens;
      return typeof parsed.accessToken === "string" &&
        typeof parsed.expiresAt === "number"
        ? parsed
        : undefined;
    } catch {
      return undefined;
    }
  }

  async hasSession(): Promise<boolean> {
    const signedIn = (await this.readTokens()) !== undefined;
    this.logger.info("auth.session.check", { signedIn });
    return signedIn;
  }

  async signIn(): Promise<void> {
    const { issuer, clientId, scope } = this.settings();
    this.logger.info("auth.signin.start", {
      issuer,
      clientId,
      scope,
      redirectUri: REDIRECT_URI,
    });
    const state = randomUrlValue();
    const verifier = randomUrlValue();
    this.pending = { state, verifier };
    await this.context.secrets.store(PENDING_KEY, JSON.stringify(this.pending));
    await this.context.globalState.update(PENDING_GLOBAL_KEY, this.pending);
    const authorization = new URL(`${issuer}/protocol/openid-connect/auth`);
    authorization.search = new URLSearchParams({
      client_id: clientId,
      response_type: "code",
      redirect_uri: REDIRECT_URI,
      scope,
      state,
      code_challenge: sha256Base64Url(verifier),
      code_challenge_method: "S256",
    }).toString();
    await vscode.env.openExternal(vscode.Uri.parse(authorization.toString()));
    this.logger.info("auth.signin.browser-opened", { issuer, clientId });
  }

  async handleCallback(uri: vscode.Uri): Promise<void> {
    const query = new URLSearchParams(uri.query);
    this.logger.info("auth.callback.received", {
      authority: uri.authority,
      path: uri.path,
      queryKeys: [...query.keys()],
    });
    const callbackPath = uri.path.replace(/\/+$/u, "") || "/";
    if (callbackPath !== "/auth/callback") {
      throw new Error(`Unexpected AgentGateway callback path: ${uri.path}`);
    }
    const pending = this.pending ?? (await this.readPending());
    this.logger.info("auth.callback.pending-state", {
      found: pending !== undefined,
      source: this.pending ? "memory" : "storage",
    });
    this.pending = undefined;
    await this.context.secrets.delete(PENDING_KEY);
    await this.context.globalState.update(PENDING_GLOBAL_KEY, undefined);
    if (!pending) {
      throw new Error("No pending AgentGateway sign-in request.");
    }
    if (query.get("state") !== pending.state) {
      this.logger.info("auth.callback.state-mismatch");
      throw new Error("AgentGateway sign-in state did not match.");
    }
    const error = query.get("error");
    if (error) throw new Error(`Keycloak sign-in failed: ${error}`);
    const code = query.get("code");
    if (!code)
      throw new Error(
        "Keycloak callback did not contain an authorization code.",
      );

    const { issuer, clientId } = this.settings();
    const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "authorization_code",
        client_id: clientId,
        code,
        redirect_uri: REDIRECT_URI,
        code_verifier: pending.verifier,
      }).toString(),
    });
    this.logger.info("auth.callback.token-exchange", {
      status: response.status,
      ok: response.ok,
    });
    if (!response.ok)
      throw new Error(`Keycloak token exchange failed (${response.status}).`);
    const token = (await response.json()) as {
      access_token?: string;
      id_token?: string;
      refresh_token?: string;
      expires_in?: number;
    };
    if (!token.access_token)
      throw new Error(
        "Keycloak token response did not contain an access token.",
      );
    await this.saveTokens({
      accessToken: token.access_token,
      idToken: token.id_token,
      refreshToken: token.refresh_token,
      expiresAt: Date.now() + Math.max(30, token.expires_in ?? 300) * 1000,
    });
    this.logger.info("auth.callback.success", {
      expiresInSeconds: Math.max(30, token.expires_in ?? 300),
      hasRefreshToken: Boolean(token.refresh_token),
    });
  }

  async getAccessToken(): Promise<string> {
    const tokens = await this.readTokens();
    this.logger.info("auth.token.read", { found: tokens !== undefined });
    if (!tokens)
      throw new Error("Sign in to AgentGateway before using its models.");
    if (tokens.expiresAt > Date.now() + 30_000) {
      this.logger.info("auth.token.valid", {
        expiresInSeconds: Math.floor((tokens.expiresAt - Date.now()) / 1000),
      });
      return tokens.accessToken;
    }
    if (!tokens.refreshToken)
      throw new Error("AgentGateway session expired. Sign in again.");

    const { issuer, clientId } = this.settings();
    const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "refresh_token",
        client_id: clientId,
        refresh_token: tokens.refreshToken,
      }).toString(),
    });
    this.logger.info("auth.token.refresh", {
      status: response.status,
      ok: response.ok,
    });
    if (!response.ok) {
      await this.signOut();
      throw new Error("AgentGateway session expired. Sign in again.");
    }
    const token = (await response.json()) as {
      access_token?: string;
      id_token?: string;
      refresh_token?: string;
      expires_in?: number;
    };
    if (!token.access_token)
      throw new Error(
        "Keycloak refresh response did not contain an access token.",
      );
    await this.saveTokens({
      accessToken: token.access_token,
      idToken: token.id_token ?? tokens.idToken,
      refreshToken: token.refresh_token ?? tokens.refreshToken,
      expiresAt: Date.now() + Math.max(30, token.expires_in ?? 300) * 1000,
    });
    this.logger.info("auth.token.refresh-success", {
      expiresInSeconds: Math.max(30, token.expires_in ?? 300),
    });
    return token.access_token;
  }

  async signOut(): Promise<void> {
    this.logger.info("auth.signout");
    const tokens = await this.readTokens();
    const { issuer, clientId } = this.settings();
    this.pending = undefined;
    await this.context.secrets.delete(PENDING_KEY);
    await this.context.globalState.update(PENDING_GLOBAL_KEY, undefined);
    await this.context.secrets.delete(TOKENS_KEY);
    const logout = new URL(`${issuer}/protocol/openid-connect/logout`);
    logout.search = new URLSearchParams({
      client_id: clientId,
      post_logout_redirect_uri: LOGOUT_REDIRECT_URI,
      ...(tokens?.idToken ? { id_token_hint: tokens.idToken } : {}),
    }).toString();
    await vscode.env.openExternal(vscode.Uri.parse(logout.toString()));
    this.logger.info("auth.signout.browser-opened", { issuer, clientId });
  }

  isLogoutCallback(uri: vscode.Uri): boolean {
    return uri.path.replace(/\/+$/u, "") === "/auth/logout-callback";
  }

  private async saveTokens(tokens: StoredTokens): Promise<void> {
    await this.context.secrets.store(TOKENS_KEY, JSON.stringify(tokens));
  }

  private async readPending(): Promise<
    { state: string; verifier: string } | undefined
  > {
    const raw = await this.context.secrets.get(PENDING_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as { state?: string; verifier?: string };
        if (
          typeof parsed.state === "string" &&
          typeof parsed.verifier === "string"
        ) {
          return { state: parsed.state, verifier: parsed.verifier };
        }
      } catch {}
    }
    const stored = this.context.globalState.get<{
      state?: string;
      verifier?: string;
    }>(PENDING_GLOBAL_KEY);
    return stored &&
      typeof stored.state === "string" &&
      typeof stored.verifier === "string"
      ? { state: stored.state, verifier: stored.verifier }
      : undefined;
  }
}
