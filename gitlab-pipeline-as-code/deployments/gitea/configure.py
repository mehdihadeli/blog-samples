import configparser
import os
from pathlib import Path


def load_config(path):
    parser = configparser.ConfigParser(interpolation=None)
    parser.optionxform = str
    contents = path.read_text() if path.exists() else ""
    parser.read_string("[DEFAULT]\n" + contents)
    return parser


destination = Path("/data/gitea/conf/app.ini")
settings = load_config(Path("/setup/app.ini"))
current = load_config(destination)
current.remove_option("webhook", "ALLOWED_HOST_LIST")
current.defaults().update(settings.defaults())
for section in settings.sections():
    if not current.has_section(section):
        current.add_section(section)
    for key, value in settings._sections[section].items():
        current.set(section, key, value)

server = current["server"]
server["ROOT_URL"] = f"http://localhost:{os.environ.get('GITEA_HTTP_PORT', '3000')}/"
server["SSH_PORT"] = os.environ.get("GITEA_SSH_PORT", "22222")
destination.parent.mkdir(parents=True, exist_ok=True)
temporary = destination.with_suffix(".ini.tmp")
with temporary.open("w") as output:
    for key, value in current.defaults().items():
        output.write(f"{key} = {value}\n")
    for section in current.sections():
        output.write(f"\n[{section}]\n")
        for key, value in current._sections[section].items():
            output.write(f"{key} = {value}\n")
os.chmod(temporary, 0o600)
os.chown(temporary, 1000, 1000)
temporary.replace(destination)
print("Applied Gitea config; existing installation secrets preserved.")