#!/usr/bin/env python3
from pathlib import Path
import csv
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

root = Path(__file__).resolve().parent
r = list(csv.DictReader((root/"performance.csv").open(encoding="utf-8")))
x = [int(a["ROWS"]) for a in r]
single = [int(a["cpu_single_ns"]) for a in r]
omp = [int(a["cpu_openmp_ns"]) for a in r]
fpga = [int(a["fpga_hls_ns"]) for a in r]
assets = root/"assets"
assets.mkdir(exist_ok=True)
labels = ["8 192", "32 768", "131 072", "524 288", "2 097 152"]
plt.rcParams.update({"figure.figsize": (9, 4.4), "font.size": 10, "axes.grid": True, "grid.alpha": 0.25})

def finish(name):
    plt.xticks(x, labels, rotation=13)
    plt.xlabel("Количество элементов массива ROWS")
    plt.legend(frameon=False)
    plt.tight_layout()
    plt.savefig(assets/name, dpi=190, bbox_inches="tight")
    plt.close()

plt.figure()
for data,marker,name in [(single,"o","CPU, один поток"),(omp,"s","CPU, OpenMP"),(fpga,"^","FPGA, оценка HLS")]:
    plt.plot(x,[v/1e6 for v in data], "-"+marker, linewidth=2,label=name)
plt.yscale("log"); plt.xscale("log",base=4)
plt.ylabel("Время выполнения, мс (логарифмическая шкала)")
plt.title("Время выполнения функции по размеру массива")
finish("time_vs_rows.png")

plt.figure()
plt.plot(x,[a/b for a,b in zip(single,fpga)],"-o",linewidth=2,label="CPU single / FPGA")
plt.plot(x,[a/b for a,b in zip(omp,fpga)],"-s",linewidth=2,label="OpenMP / FPGA")
plt.axhline(1,linestyle="--",linewidth=1,label="Одинаковое время")
plt.xscale("log",base=4)
plt.ylabel("Отношение времён, раз")
plt.title("Сравнение с оценкой HLS (>1: быстрее FPGA)")
finish("fpga_ratios.png")

plt.figure()
plt.plot(x,[a/b for a,b in zip(single,omp)],"-o",linewidth=2,label="OpenMP относительно одного потока")
plt.axhline(1,linestyle="--",linewidth=1,label="Нет ускорения")
plt.xscale("log",base=4)
plt.ylabel("CPU single / OpenMP, раз")
plt.title("Эффект многопоточности на CPU")
finish("openmp_speedup.png")
