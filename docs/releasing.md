# Releasing

The GitHub Actions workflow at `.github/workflows/build-msix.yml` builds `PeopleCodeIDECompanion` on a GitHub-hosted Windows runner in `Release|x64`, creates a test-signing certificate whose subject matches the app manifest publisher, signs the existing single-project MSIX output, and uploads the results as workflow artifacts.

After a `main` push or pull request run completes, open the workflow run in GitHub and download:

- `PeopleCodeIDECompanion-msix` for the generated `.msix`
- `PeopleCodeIDECompanion-certificate` for the generated `.cer`
- `PeopleCodeIDECompanion-package` for a zip that bundles the `.msix`, the matching `.cer`, and a short install note for internal testing

The current package is test-signed. On another Windows PC, Windows may block installation until the included certificate from that same workflow run is trusted on that machine. Import the downloaded `.cer` into an appropriate trusted certificate store before opening the `.msix`.
