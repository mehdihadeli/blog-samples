pid_file = "/tmp/vault-agent.pid"

vault {
  address = "http://vault:8200"
}

auto_auth {
  method {
    type = "approle"
    config = {
      role_id_file_path   = "/vault/credentials/users-role-id"
      secret_id_file_path = "/vault/credentials/users-secret-id"
      remove_secret_id_file_after_reading = false
    }
  }

  sink {
    type = "file"
    config = {
      path = "/tmp/vault-token"
      mode = 0600
    }
  }
}

template {
  destination = "/vault/secrets/appsettings.json"
  perms       = 0600
  source      = "/vault/config/appsettings.ctmpl"
}