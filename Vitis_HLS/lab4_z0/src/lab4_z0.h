#pragma once

#include <cstddef>

using din_type = int;
using dout_type = int;

// Number of timed calls prescribed by the lab assignment.
constexpr int CALLS = 33;

// The software benchmark uses a runtime ROWS value; the HLS accelerator is separate.
void lab4_z0(const din_type* in_A, const din_type* in_B,
             dout_type* out_RES, std::size_t rows);
