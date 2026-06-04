@echo off
dotnet run --project Content.Server --configuration Tools --property:TieredPGO=true,TieredCompilationQuickJit=true,PublishReadyToRun=false
pause
