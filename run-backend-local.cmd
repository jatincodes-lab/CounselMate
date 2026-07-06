@echo off
cd /d "%~dp0backend"
"C:\Program Files\dotnet\dotnet.exe" run --project "E:\ETPL-04\Jatin\Transferdata\CRM\backend\EducationCrm.Api.csproj" --urls "http://localhost:5078" > "%~dp0backend\api.local.stdout.log" 2> "%~dp0backend\api.local.stderr.log"
