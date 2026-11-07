# Исходники NareLauncher

Код лаунчера, из которого собран `../NareLauncher.exe` рядом с этой папкой.

- C# WinForms, .NET Framework 4.8, без сторонних UI-библиотек: все
  элементы (`UI/Controls.cs`) и вся отрисовка 3D-персонажа
  (`UI/PlayerRenderer.cs`, `UI/SkinView3D.cs`) написаны вручную на GDI+.
- `Core/` — работа с версиями, загрузчиками, аккаунтами, скинами,
  каталогами модов и обновлениями.
- `UI/Pages/` — страницы лаунчера: главная, версии, моды, серверы, скины,
  косметика, аккаунты, настройки, консоль.

## Сборка

Откройте `NareLauncher.csproj` в Visual Studio (нужен .NET Framework 4.8
Developer Pack) и соберите в Release.

Либо из консоли, без Visual Studio:

```
dotnet build NareLauncher.csproj -c Release
```

Для сборки net48 на Linux понадобится пакет
`Microsoft.NETFramework.ReferenceAssemblies`.

## Версии

Текущая версия указана в `Properties/AssemblyInfo.cs`, а то, что лаунчер
считает последней, — в `../update.json`.
