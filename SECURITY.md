# Security policy

Downora downloads and processes content from the internet, so security reports are welcome.

## Reporting

Please report a vulnerability privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**. Do not post details in a public issue. Include the Downora version (About page), what you did, and what happened.

You can expect an answer within a week. Fixes ship in the next release and are credited if you wish.

## What counts

- A link, title, file name or file that makes Downora run code, write outside the download folder, or leak a cookie, token or password.
- A way to make Downora download content it should not, or to bypass the opt-in for browser sign-in.
- A vulnerable dependency that Downora's build does not already catch.

Downora only downloads content the user owns or is allowed to access. It does not bypass DRM, paywalls or other access controls, and reports that ask for that are out of scope.

## What was reviewed

See [docs/security-review.md](docs/security-review.md) for the threat model, the findings fixed so far and the risks that are accepted.
