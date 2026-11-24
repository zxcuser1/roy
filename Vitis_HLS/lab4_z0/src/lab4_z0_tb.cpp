#include "lab4_z0.h"

#include <algorithm>
#include <cerrno>
#include <chrono>
#include <climits>
#include <cstdlib>
#include <iomanip>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

#ifdef _WIN32
#include <conio.h>
#endif
#ifdef USE_OPENMP
#include <omp.h>
#endif

namespace {
std::size_t parse_rows(const char* s)
{
    if (!s || !*s || *s == '-') {
        throw std::invalid_argument("ROWS must be a positive integer");
    }
    errno = 0;
    char* end = nullptr;
    const unsigned long long val = std::strtoull(s, &end, 10);
    if (errno || *end != '\0' || val == 0 || val > static_cast<unsigned long long>(PTRDIFF_MAX)) {
        throw std::invalid_argument("Invalid ROWS value");
    }
    return static_cast<std::size_t>(val);
}

void pause_for_key()
{
    std::cout << "Press any key to exit..." << std::flush;
#ifdef _WIN32
    (void)_getch();
#else
    (void)std::cin.get();
#endif
}
}  // namespace

int main(int argc, char** argv)
{
    try {
        std::size_t rows = 8192;
        bool no_pause = false;
        bool rows_supplied = false;
        for (int arg = 1; arg < argc; ++arg) {
            const std::string option = argv[arg];
            if (option == "--no-pause") {
                no_pause = true;
            } else if (!rows_supplied) {
                rows = parse_rows(argv[arg]);
                rows_supplied = true;
            } else {
                throw std::invalid_argument("Usage: lab4_z0_*.exe [ROWS] [--no-pause]");
            }
        }

        std::vector<din_type> a(rows), b(rows);
        std::vector<dout_type> result(rows), expected(rows);
        for (std::size_t i = 0; i < rows; ++i) {
            a[i] = static_cast<int>((i * 17ULL) % 199) - 99;
            b[i] = static_cast<int>((i * 31ULL + 3ULL) % 199) - 99;
            expected[i] = a[i] * b[i];
        }

        // Correctness check is deliberately outside the timed region.
        lab4_z0(a.data(), b.data(), result.data(), rows);
        if (result != expected) {
            std::cerr << "FAIL: result does not match reference\n";
            return 1;
        }

        using clock = std::chrono::steady_clock;
        std::vector<double> measurements_ns;
        measurements_ns.reserve(CALLS);
        for (int run = 0; run < CALLS; ++run) {
            const auto start = clock::now();
            lab4_z0(a.data(), b.data(), result.data(), rows);
            const auto stop = clock::now();
            measurements_ns.push_back(
                std::chrono::duration<double, std::nano>(stop - start).count());
            // Also verify every timed run, without including the check in timing.
            if (result != expected) {
                std::cerr << "FAIL on call " << (run + 1) << '\n';
                return 1;
            }
        }

        std::sort(measurements_ns.begin(), measurements_ns.end());
        const double median_ns = measurements_ns[CALLS / 2];
        long long checksum = 0;
        for (const dout_type val : result) checksum += val;

#ifdef USE_OPENMP
        std::cout << "Mode: OpenMP\nThreads (max): " << omp_get_max_threads() << '\n';
#else
        std::cout << "Mode: single-thread\nThreads: 1\n";
#endif
        std::cout << "ROWS: " << rows << "\nCALLS: " << CALLS
                  << "\nCorrectness: PASS\n"
                  << std::fixed << std::setprecision(3)
                  << "Median function time: " << median_ns << " ns ("
                  << median_ns / 1e6 << " ms)\n"
                  << "Checksum: " << checksum << "\n";
        if (!no_pause) pause_for_key();
        return 0;
    } catch (const std::exception& e) {
        std::cerr << "ERROR: " << e.what() << '\n';
        return 2;
    }
}
