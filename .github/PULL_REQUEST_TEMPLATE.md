## Summary
<!-- What changed and why -->

## Test plan
- [ ] `dotnet restore ARTR.Veyra.sln --locked-mode`
- [ ] `dotnet build ARTR.Veyra.sln -c Release`
- [ ] `dotnet test ARTR.Veyra.sln -c Release`
- [ ] Manual smoke of `/_veyra/health/live` if runtime behavior changed
