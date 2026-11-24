#include "lab5_z0_sw.h"
#include <chrono>
#include <cstring>
#include <iomanip>
#include <iostream>
#ifdef _OPENMP
#include <omp.h>
#endif
#ifdef _WIN32
#include <conio.h>
#endif

int main(int argc, char* argv[])
{
    bool no_wait = false, quick = false;
    for (int k = 1; k < argc; ++k) {
        if (std::strcmp(argv[k], "--no-wait") == 0) no_wait = true;
        else if (std::strcmp(argv[k], "--quick") == 0) quick = true;
        else { std::cerr << "Unknown argument: " << argv[k] << '\n'; return 2; }
    }

    const int sizes[] = {8192, 32768, 131072, 524288, 2097152};
    int count = quick ? 1 : 5;
    int runs = quick ? 3 : 33;
#ifdef _OPENMP
    omp_set_dynamic(0);
    const char* mode = "openmp";
    int threads = omp_get_max_threads();
#else
    const char* mode = "single";
    int threads = 1;
#endif
    std::cout << "rows,mode,threads,runs,total_ms,avg_ms,checksum\n";

    for (int test = 0; test < count; ++test) {
        int rows = sizes[test];
        int* A = new int[rows];
        int* B = new int[rows];
        int* RES = new int[rows];
        int* prod = new int[rows];
        int* sum = new int[rows];
        int* prod2 = new int[rows];
        int* sum2 = new int[rows];

        for (int i = 0; i < rows; ++i) {
            A[i] = (i % 1001) - 500;
            B[i] = ((i * 7) % 1001) - 500;
        }

        // Проверка перед замером.
        lab5_z0(A, B, RES, rows, prod, sum, prod2, sum2);
        for (int i = 0; i < rows; ++i) {
            if (RES[i] != A[i] * A[i] + B[i] * B[i]) {
                std::cerr << "ERROR at i=" << i << '\n';
                delete[] A; delete[] B; delete[] RES;
                delete[] prod; delete[] sum; delete[] prod2; delete[] sum2;
                return 1;
            }
        }

        auto start = std::chrono::steady_clock::now();
        for (int k = 0; k < runs; ++k)
            lab5_z0(A, B, RES, rows, prod, sum, prod2, sum2);
        auto stop = std::chrono::steady_clock::now();

        long long checksum = 0;
        for (int i = 0; i < rows; ++i) {
            if (RES[i] != A[i] * A[i] + B[i] * B[i]) {
                std::cerr << "ERROR at i=" << i << '\n';
                delete[] A; delete[] B; delete[] RES;
                delete[] prod; delete[] sum; delete[] prod2; delete[] sum2;
                return 1;
            }
            checksum += RES[i];
        }

        double total_ms = std::chrono::duration<double, std::milli>(stop - start).count();
        std::cout << rows << ',' << mode << ',' << threads << ',' << runs << ','
                  << std::fixed << std::setprecision(6)
                  << total_ms << ',' << total_ms / runs << ',' << checksum << '\n';

        delete[] A; delete[] B; delete[] RES;
        delete[] prod; delete[] sum; delete[] prod2; delete[] sum2;
    }

    std::cout.flush();
    if (!no_wait) {
        std::cerr << "Press a key to exit...\n";
#ifdef _WIN32
        _getch();
#else
        std::cin.get(); // Enter on Linux.
#endif
    }
    return 0;
}
