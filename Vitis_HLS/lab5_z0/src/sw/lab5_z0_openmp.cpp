#include "lab5_z0_sw.h"

// Поэлементное произведение и сумма.
void func1(const int A[], const int B[], int prod[], int sum[], int rows)
{
#pragma omp parallel for
    for (int i = 0; i < rows; ++i) {
        prod[i] = A[i] * B[i];
        sum[i] = A[i] + B[i];
    }
}

// Удвоение произведений.
void func2(const int prod[], int prod2[], int rows)
{
#pragma omp parallel for
    for (int i = 0; i < rows; ++i)
        prod2[i] = 2 * prod[i];
}

// Квадрат суммы.
void func3(const int sum[], int sum2[], int rows)
{
#pragma omp parallel for
    for (int i = 0; i < rows; ++i)
        sum2[i] = sum[i] * sum[i];
}

// Формирование результата.
void func4(const int sum2[], const int prod2[], int RES[], int rows)
{
#pragma omp parallel for
    for (int i = 0; i < rows; ++i)
        RES[i] = sum2[i] - prod2[i];
}

void lab5_z0(const int A[], const int B[], int RES[], int rows,
             int prod[], int sum[], int prod2[], int sum2[])
{
    func1(A, B, prod, sum, rows);
    func2(prod, prod2, rows);
    func3(sum, sum2, rows);
    func4(sum2, prod2, RES, rows);
}
