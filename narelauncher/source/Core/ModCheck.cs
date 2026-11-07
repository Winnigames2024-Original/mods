using System;
using System.Collections.Generic;
using System.IO;

namespace NareLauncher.Core
{
    /// <summary>
    /// Проверка папки модов: что не подойдёт этой сборке и почему.
    ///
    /// Смысл простой — Minecraft с несовместимым модом не запускается и
    /// пишет это стеной текста в логе. Прочитать то же самое из jar заранее
    /// куда понятнее, чем ловить вылет при старте.
    /// </summary>
    public static class ModCheck
    {
        public enum Kind
        {
            Good,       // всё в порядке
            Game,       // мод для другой версии игры
            Loader,     // мод под другой загрузчик
            Twice,      // два файла одного и того же мода
            Broken,     // jar не читается
            Unknown     // мод о себе ничего не пишет
        }

        public class Problem
        {
            public LocalMod Mod;
            public ModJar About;
            public Kind Kind;
            public string Text = "";

            /// <summary>Стоит ли предлагать отключить.</summary>
            public bool Bad
            {
                get { return Kind == Kind.Game || Kind == Kind.Loader || Kind == Kind.Twice || Kind == Kind.Broken; }
            }
        }

        /// <summary>Проверяет все моды сборки. Возвращает только то, о чём есть что сказать.</summary>
        public static List<Problem> Scan(string projectType, InstalledVersion build)
        {
            var found = new List<Problem>();
            if (build == null) return found;

            var mods = ModManager.List(projectType, build.Id);
            var byId = new Dictionary<string, LocalMod>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in mods)
            {
                if (!mod.Enabled) continue;            // выключенный мешать не может

                var about = ModJar.Read(mod.Path);

                if (projectType != "mod")
                {
                    continue;                          // ресурспаки и шейдеры такой проверке не поддаются
                }

                if (!about.Known)
                {
                    if (!Readable(mod.Path))
                    {
                        found.Add(new Problem
                        {
                            Mod = mod, About = about, Kind = Kind.Broken,
                            Text = "Файл не читается как jar — скачался не до конца или это не мод."
                        });
                        continue;
                    }

                    found.Add(new Problem
                    {
                        Mod = mod, About = about, Kind = Kind.Unknown,
                        Text = "Мод не пишет о себе ни версию игры, ни загрузчик — проверить нечем."
                    });
                    continue;
                }

                bool? fits = about.Fits(build.BaseVersion, build.ModrinthLoader);

                if (fits == false)
                {
                    bool loaderWrong = !string.IsNullOrEmpty(about.Loader)
                            && !string.IsNullOrEmpty(build.ModrinthLoader)
                            && !string.Equals(about.Loader, build.ModrinthLoader, StringComparison.OrdinalIgnoreCase)
                            && !(about.Loader == "fabric" && build.ModrinthLoader == "quilt");

                    found.Add(new Problem
                    {
                        Mod = mod, About = about,
                        Kind = loaderWrong ? Kind.Loader : Kind.Game,
                        Text = loaderWrong
                            ? "Мод для " + about.LoaderRu + ", а сборка на " + build.LoaderRu + "."
                            : "Мод для " + ModJar.Pretty(about.GameRaw) + ", а сборка — Minecraft " + build.BaseVersion + "."
                    });
                    continue;
                }

                // два файла одного мода — обычно старый остался после обновления
                if (!string.IsNullOrEmpty(about.Id))
                {
                    LocalMod already;
                    if (byId.TryGetValue(about.Id, out already))
                    {
                        found.Add(new Problem
                        {
                            Mod = mod, About = about, Kind = Kind.Twice,
                            Text = "Тот же мод уже лежит в файле " + already.FileName +
                                   " — Forge не запустится с двумя копиями."
                        });
                        continue;
                    }
                    byId[about.Id] = mod;
                }
            }

            return found;
        }

        private static bool Readable(string path)
        {
            try
            {
                using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
                    return zip.Entries.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>Выключает мод, переименовывая файл — удалять чужое не наше дело.</summary>
        public static void TurnOff(Problem problem)
        {
            if (problem == null || problem.Mod == null) return;
            if (!problem.Mod.Enabled) return;

            ModManager.Toggle(problem.Mod);
            Log.Info("Отключён мод " + problem.Mod.FileName + ": " + problem.Text);
        }

        /// <summary>Короткий итог для строки состояния.</summary>
        public static string Summary(List<Problem> problems, int total)
        {
            if (problems == null) return "";

            int bad = 0;
            foreach (var p in problems) if (p.Bad) bad++;

            if (total == 0) return "Модов в сборке нет.";
            if (bad == 0) return "Проверено " + total + " — всё подходит этой сборке.";

            return "Проверено " + total + ", не подойдёт: " + bad + ".";
        }
    }
}
