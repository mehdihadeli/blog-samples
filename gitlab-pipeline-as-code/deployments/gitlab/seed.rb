require 'json'
require 'fileutils'
require 'openssl'

settings = JSON.parse(File.read('/bootstrap-settings.json'))
root = User.find_by_username('root')
unless root
  organization = Gitlab::DatabaseImporters::AdminOrganizationImporter.organization_for_admin
  response = Users::CreateService.new(User.new(admin: true), {
    username: 'root', name: 'Administrator', admin: true,
    email: settings.fetch('gitlab').fetch('admin_email'),
    password: ENV.fetch('GITLAB_ROOT_PASSWORD'), skip_confirmation: true,
    organization_id: organization.id, organization_access_level: :owner
  }).execute
  raise "Unable to create local administrator: #{response.message}" unless response.success?

  root = response.payload.fetch(:user)
end
root.skip_reconfirmation!
root.update!(email: settings.fetch('gitlab').fetch('admin_email'))

application_settings = ApplicationSetting.current
application_settings.signup_enabled = false
gitea = URI.parse(settings.fetch('gitea').fetch('url'))
application_settings.outbound_local_requests_whitelist = (
  application_settings.outbound_local_requests_whitelist + ["#{gitea.host}:#{gitea.port}"]
).uniq
application_settings.ci_jwt_signing_key ||= OpenSSL::PKey::RSA.new(2048).to_pem
application_settings.ci_job_token_signing_key ||= OpenSSL::PKey::RSA.new(2048).to_pem
application_settings.save!
FileUtils.mkdir_p('/bootstrap-state', mode: 0700)
path = '/bootstrap-state/gitlab-api-token'
existing = File.exist?(path) ? PersonalAccessToken.find_by_token(File.read(path).strip) : nil
unless existing&.active?
  root.personal_access_tokens.where(name: 'local-compose-bootstrap', revoked: false).find_each(&:revoke!)
  token = root.personal_access_tokens.create!(
    name: 'local-compose-bootstrap', scopes: ['api'], expires_at: 7.days.from_now.to_date
  )
  File.write(path, token.token, mode: 'w', perm: 0600)
end
puts 'GitLab root account configured; bootstrap API token stored in private volume.'