@echo off
cd /d "%~dp0frontend"
"C:\Program Files\nodejs\npm.cmd" --prefix "E:\ETPL-04\Jatin\Transferdata\CRM\frontend" run dev -- --host 0.0.0.0 > "%~dp0frontend\frontend.local.stdout.log" 2> "%~dp0frontend\frontend.local.stderr.log"
