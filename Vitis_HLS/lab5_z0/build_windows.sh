#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p bin
CXX=x86_64-w64-mingw32-g++-posix
command -v "$CXX" >/dev/null || { echo 'Install g++-mingw-w64-x86-64-posix' >&2; exit 1; }
"$CXX" -O3 -std=c++17 -static src/sw/lab5_z0_single.cpp src/sw/lab5_z0_tb_sw.cpp -o bin/lab5_z0_single.exe
"$CXX" -O3 -std=c++17 -static -fopenmp src/sw/lab5_z0_openmp.cpp src/sw/lab5_z0_tb_sw.cpp -o bin/lab5_z0_openmp.exe
echo "Built Windows EXE in bin/"
