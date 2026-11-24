@echo off
if not exist bin mkdir bin
g++ -O3 -std=c++17 -static src\sw\lab5_z0_single.cpp src\sw\lab5_z0_tb_sw.cpp -o bin\lab5_z0_single.exe
if errorlevel 1 exit /b 1
g++ -O3 -std=c++17 -static -fopenmp src\sw\lab5_z0_openmp.cpp src\sw\lab5_z0_tb_sw.cpp -o bin\lab5_z0_openmp.exe
if errorlevel 1 exit /b 1
echo Ready: bin\lab5_z0_single.exe and bin\lab5_z0_openmp.exe
