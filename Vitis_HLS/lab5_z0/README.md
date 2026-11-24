# Vitis HLS lab5_z0 — программная часть

Алгоритм: `RES[i] = (A[i] + B[i])² - 2*A[i]*B[i]`; независимый эталон `A[i]*A[i]+B[i]*B[i]`.

- `src/sw/lab5_z0_single.cpp` — однопоточные функции `func1`–`func4` с массивами `int`.
- `src/sw/lab5_z0_openmp.cpp` — те же функции, `#pragma omp parallel for`.
- `src/sw/lab5_z0_tb_sw.cpp` — общий тест, 5 размеров ROWS, по 33 запуска, вывод CSV.
- `report/Программная_часть_lab5_z0.md` — описание эксперимента и результаты.

### Windows EXE

В Windows с MinGW-w64: `build_windows.bat`.

В Ubuntu с пакетом `g++-mingw-w64-x86-64-posix`: `bash build_windows.sh`.

Будут созданы `bin/lab5_z0_single.exe` и `bin/lab5_z0_openmp.exe`.
Для OpenMP в PowerShell: `$env:OMP_NUM_THREADS = "12"`.

Запуск: `bin\lab5_z0_single.exe` или `bin\lab5_z0_openmp.exe`. Результат остаётся на экране до нажатия клавиши. Флаг `--no-wait` отключает ожидание; `--quick` — один размер, три запуска (быстрая проверка).

### Linux

`bash build_linux.sh`, затем `./bin/lab5_z0_single --no-wait` и `OMP_NUM_THREADS=12 ./bin/lab5_z0_openmp --no-wait`.

Время CPU/OpenMP в сохранённых CSV измерено ранее на Ubuntu для того же алгоритма, но до упрощения исходного кода. После пересборки новых исходников результаты времени следует измерить повторно. Контрольные суммы совпадают. Windows EXE необходимо проверить на Windows.
