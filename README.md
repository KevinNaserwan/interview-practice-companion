# interview-practice-companion

Windows desktop overlay for authorized interview practice. Captures microphone or system audio with explicit consent, transcribes questions, and generates structured Indonesian or English answer suggestions through `ai.meetsin.id`.

## Safety boundary

Practice and consented conversations only. No stealth mode, screen-capture evasion, automatic typing, key injection, hidden scraping, or integration with assessment applications.

## Requirements

- Windows 10/11 x64
- API credential for `ai.meetsin.id`
- Microphone permission when microphone capture is used

The distributed installer is self-contained and does not require a separate .NET runtime.

## Build

```powershell
dotnet restore
dotnet build InterviewPracticeCompanion.sln -c Release --no-restore
dotnet test InterviewPracticeCompanion.sln -c Release --no-build
dotnet publish src/InterviewPracticeCompanion/InterviewPracticeCompanion.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/publish
```

Compile `installer/InterviewPracticeCompanion.nsi` with NSIS 3 to produce:

```text
artifacts/InterviewPracticeCompanion-Setup-x64.exe
```

## Signed release

Smart App Control requires a valid public-trust signature. The workflow at `.github/workflows/signed-release.yml` builds, signs, verifies, and uploads the installer. It uses GitHub OIDC, so no certificate private key or Azure client secret is stored in GitHub.

One-time Azure setup:

1. Create an Azure Artifact Signing account, complete identity validation, and create a Public Trust certificate profile.
2. Create a Microsoft Entra application/service principal.
3. Add a federated credential for this repository and the `production-signing` GitHub environment. Subject: `repo:KevinNaserwan/interview-practice-companion:environment:production-signing`.
4. Grant that service principal the `Artifact Signing Certificate Profile Signer` role on the signing account or profile.
5. In GitHub repository **Settings → Environments**, create `production-signing`. Optional but recommended: require reviewer approval.
6. Add these GitHub environment secrets:
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
7. Add these GitHub environment variables:
   - `ARTIFACT_SIGNING_ENDPOINT`, such as `https://weu.codesigning.azure.net/`
   - `ARTIFACT_SIGNING_ACCOUNT`
   - `ARTIFACT_SIGNING_PROFILE`

Run **Actions → Signed Windows release → Run workflow**, or push a tag such as `v1.0.1`. Download the signed installer from the workflow artifact. The workflow fails unless both the application and installer report a valid Authenticode signature.

## Privacy

- API key stored in Windows Credential Manager.
- Audio remains in memory; no PCM/WAV recording is written to disk.
- Logs exclude audio, transcripts, prompts, suggestions, API keys, authorization headers, and response bodies.
- Clear removes transcript and suggestion from the UI session.

## Configuration

Defaults:

- API base URL: `https://ai.meetsin.id/v1`
- Model: `luna-5.6`
- Language: Indonesian
- Capture source: microphone

The API adapter currently targets OpenAI-compatible transcription and chat-completion endpoints. Confirm provider endpoint compatibility before production use.

## License

No license granted yet. Add a license before third-party distribution or contribution.
