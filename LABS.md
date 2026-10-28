# Роевой интеллект — лабораторные работы

В этом репозитории собраны четыре лабораторные работы.

| Лабораторная | Расположение | Стек |
| --- | --- | --- |
| ЛР1 — 2D моделирование роя | Корень репозитория (`SwarmLab1.Core`, `SwarmLab1.Desktop`) | .NET 10, Avalonia |
| ЛР2 — 3D моделирование роя | [`Lab2`](Lab2/README.md) | .NET 9, Avalonia |
| ЛР3 — метод потенциальных полей | [`Lab3`](Lab3/README.md) | .NET 9, ASP.NET Core |
| ЛР4 — алгоритм роя сальп (SSA) | [`Lab4`](Lab4/README.md) | .NET 10, ASP.NET Core |

## Запуск

Нужен соответствующий .NET SDK. Команды выполняются из корня репозитория:

```bash
# ЛР1
dotnet run --project SwarmLab1.Desktop

# ЛР2
dotnet run --project Lab2/SwarmLab2.Desktop

# ЛР3
dotnet run --project Lab3/SwarmLab3.csproj

# ЛР4
dotnet run --project Lab4/SSA_Lab4.csproj
```

См. README внутри каждой лабораторной для подробностей и дополнительных требований.

> ЛР2, ЛР3 и ЛР4 перенесены из исходных ZIP-архивов без изменения их содержимого. Команды сборки в текущей среде не запускались.
