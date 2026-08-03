# Security policy

## Security and trust

WindowsGSH modules execute with the same Windows permissions as the WindowsGSH application. WindowsGSH cannot review, sign, or guarantee arbitrary third-party modules, so only run module code you trust.

## Download modules safely

Obtain this module from the official [WindowsGSH.Icarus repository](https://github.com/WindowsGSH/WindowsGSH.Icarus) or another source you have independently verified. Review the module source and manifest before importing it, and be cautious of repackaged downloads or unexpected executable files.

## Protect credentials and server data

Join/admin passwords, prospect/player data, logs, configuration, and backups may contain sensitive information. Restrict access to the managed server directory and backup destination. Do not publish passwords, private addresses, personal paths, or unredacted Saved data in issues or diagnostics.

## Report a vulnerability

Do not open a public issue for an unpatched vulnerability or exposed credential. Use GitHub's private security-reporting facility for this repository where available, or contact the WindowsGSH maintainers privately before disclosure.

## Include in a report

Include the affected module and WindowsGSH versions, a clear description, reproduction steps, impact, relevant sanitized logs, and any proposed mitigation. Remove all credentials, personal paths, and server data.

## Supported versions

Only the current module release receives security fixes unless a repository notice explicitly states otherwise. Upgrade to the latest supported WindowsGSH and module versions before reporting an issue.
