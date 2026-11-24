@echo off
setlocal
cd /d "%~dp0"
echo ================== SINGLE-THREAD ==================
for %%R in (8192 32768 131072 524288 2097152) do (
  echo.
  bin\lab4_z0_single.exe %%R --no-pause
  if errorlevel 1 goto :error
)
echo.
echo ===================== OPENMP ======================
for %%R in (8192 32768 131072 524288 2097152) do (
  echo.
  bin\lab4_z0_openmp.exe %%R --no-pause
  if errorlevel 1 goto :error
)
echo.
echo All checks passed.
pause
exit /b 0
:error
echo Failed with code %ERRORLEVEL%.
pause
exit /b 1
