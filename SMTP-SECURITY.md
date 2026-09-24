# SMTP credential recovery

The tracked backend appsettings.json contained a Gmail SMTP password. Treat that
password as compromised, even after removing it from the latest version.

1. Open the sending Gmail account's Google Account > Security > App passwords
   (https://myaccount.google.com/apppasswords). Revoke the exposed app password.
   If the value was the actual account password, change that password instead.
2. Review the Google account's recent security activity and sent mail for misuse.
3. Generate a replacement app password and configure it only on the backend host.
   Use the environment variable `SmtpSettings__Password` through your hosting
   provider's secret settings. Restart the backend after updating it.
4. For local development, add a `SmtpSettings` object with a `Password` property
   to the existing ignored `OMS_Backend/OMS_Backend/appsettings.Local.json`.
   Preserve other settings in that file. Never paste the password into chat,
   documentation, frontend configuration, or tracked appsettings files.
5. Verify password-reset email delivery using a test account, then resolve the
   GitGuardian incident after confirming the old credential is revoked.

Program.cs already loads appsettings.Local.json and then environment variables,
so environment variables override local values. Email delivery requires a fresh
password now that the tracked value is empty.

Deleting the current value does not remove earlier Git commits, clones, or forks.
Credential revocation is the immediate protection. History cleanup requires a
coordinated rewrite of affected branches/tags and force push; collaborators must
replace old clones so the secret is not reintroduced. Do not rewrite shared
history casually. Repository privacy alone does not invalidate the credential.

Run `pwsh -File scripts/check-smtp-secrets.ps1` before committing. The matching
GitHub Actions check rejects nonempty SMTP passwords in tracked appsettings JSON.
This is a focused guard, not a general-purpose secret scanner, and CI runs after
upload: keep GitHub/GitGuardian secret scanning enabled as well.
