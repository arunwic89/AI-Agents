# Evaluation Framework

This folder contains a local evaluation harness for OmniSupport Agent.

## What it measures

- Intent Accuracy: predicted intent vs expected intent
- Tool Accuracy: predicted tool name vs expected tool

## Files

- `test-cases.json` - seeded evaluation dataset
- `appsettings.eval.json` - evaluation runtime settings and thresholds
- `Program.cs` - evaluator runner
- `results/` - generated JSON + CSV reports

## Prerequisites

- Configure Foundry values in `../appsettings.json`:
  - `Foundry.ApiKey`
  - `Foundry.Endpoint`
  - `Foundry.Deployment`

## Run

```powershell
Set-Location d:\MyApps\AI-Agents\omnisupport-agent\evaluation
dotnet run --project .\OmniSupport.Eval.csproj
```

## Output

- `results/latest-report.json`
- `results/latest-report.csv`

Use these outputs for regression tracking between prompt/tool changes.
