# OmniSupport Agent (C#)

Single-agent, code-based Foundry app for:
- Mobile phone repair complaints
- Printer sales recommendations
- Contract renewal workflows

The app uses:
- Microsoft Foundry deployed model (Phi-4-mini-instruct)
- APIM-protected HTTP tool endpoints (for CRM integration)

## Run

1. Update `appsettings.json`:
- `Foundry.ApiKey`
- `Apim.BaseUrl`
- `Apim.SubscriptionKey`

2. Build and run:

```powershell
dotnet run --project .\OmniSupport.Agent.csproj
```

## Current Tool Contracts

Configured tools:
- `CreateServiceTicket`
- `GetContractDetails`
- `RenewContract`

Each tool is invoked via HTTP POST to the APIM route configured in `Apim.ToolRoutes`.

## Next Steps

1. Replace APIM stubs with real backend implementations.
2. Add auth hardening (Entra ID at APIM).
3. Add tracing/evaluation in Foundry monitor.
