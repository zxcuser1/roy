#include "lab4_z0.h"

#ifdef USE_OPENMP
#include <omp.h>
#endif

void lab4_z0(const din_type* in_A, const din_type* in_B,
             dout_type* out_RES, std::size_t rows)
{
#ifdef USE_OPENMP
#pragma omp parallel for schedule(static)
#endif
    for (std::ptrdiff_t i = 0; i < static_cast<std::ptrdiff_t>(rows); ++i) {
        out_RES[i] = in_A[i] * in_B[i];
    }
}
