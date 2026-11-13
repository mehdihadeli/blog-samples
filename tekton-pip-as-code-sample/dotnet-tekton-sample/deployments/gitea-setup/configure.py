import configparser
import os
from pathlib import Path
import shutil


def load_config(path):
    parser = configparser.ConfigParser(interpolation=None)
    parser.optionxform = str
    contents = path.read_text() if path.exists() else ""
    parser.read_string("[DEFAULT]\n" + contents)
    return parser


destination = Path("/data/gitea/conf/app.ini")
settings = load_config(Path("/setup/app.ini"))
current = load_config(destination)
before = dict(current.defaults()), {
    section: dict(current.items(section)) for section in current.sections()
}
if settings.has_option("security", "ALLOWED_HOST_LIST"):
    current.remove_option("webhook", "ALLOWED_HOST_LIST")
current.defaults().update(settings.defaults())
for section in settings.sections():
    if not current.has_section(section):
        current.add_section(section)
    for key, value in settings._sections[section].items():
        current.set(section, key, value)
after = dict(current.defaults()), {
    section: dict(current.items(section)) for section in current.sections()
}
if before != after:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists():
        shutil.copy2(destination, destination.with_suffix(".ini.bak"))
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
    print("Applied local Gitea settings; existing keys preserved.")
else:
    print("Gitea settings already current.")