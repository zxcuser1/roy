#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p bin
g++ -O3 -std=c++17 src/sw/lab5_z0_single.cpp src/sw/lab5_z0_tb_sw.cpp -o bin/lab5_z0_single
g++ -O3 -std=c++17 -fopenmp src/sw/lab5_z0_openmp.cpp src/sw/lab5_z0_tb_sw.cpp -o bin/lab5_z0_openmp
echo "Built Linux executables in bin/"
