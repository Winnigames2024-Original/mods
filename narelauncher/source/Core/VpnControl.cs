using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace NareLauncher.Core
{
    /// <summary>
    /// Что знаем про VPN на этой машине. Нужно ровно для одного случая: когда
    /// игра или лаунчер не достучались до сервера, а виноват туннель.
    ///
    /// Выключаем только то, что запущено от самого пользователя. Службы и
    /// драйверы не трогаем: на это нужны права администратора, а молча
    /// просить их у человека — плохая идея.
    /// </summary>
    public static class VpnControl
    {
        /// <summary>Имена процессов известных VPN (без .exe, в нижнем регистре).</summary>
        private static readonly string[] Known =
        {
            "radmin vpn", "radminvpn", "rvrdsvc",
            "paper vpn", "papervpn",
            "hamachi-2-ui", "hamachi",
            "nordvpn", "expressvpn", "protonvpn", "surfshark",
            "openvpn-gui", "openvpn", "wireguard", "tunnelbear",
            "windscribe", "cloudflarewarp", "warp-svc", "outline"
        };

        public class Found
        {
            public string Title = "";
            public readonly List<Process> Processes = new List<Process>();
            public bool Adapter;
        }

        /// <summary>Запущенный VPN или null.</summary>
        public static Found Detect()
        {
            var found = new Found();

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    string name;
                    try { name = (process.ProcessName ?? "").ToLowerInvariant(); }
                    catch { continue; }

                    foreach (string known in Known)
                    {
                        if (name != known && name.Replace(" ", "") != known.Replace(" ", "")) continue;

                        found.Processes.Add(process);
                        if (found.Title.Length == 0) found.Title = Pretty(process.ProcessName);
                        break;
                    }
                }
            }
            catch (Exception ex) { Log.Warn("VPN не опознан: " + ex.Message); }

            found.Adapter = HasTunnel();

            if (found.Processes.Count == 0 && !found.Adapter) return null;
            if (found.Title.Length == 0) found.Title = "VPN";

            return found;
        }

        /// <summary>Есть ли поднятый туннельный сетевой адаптер.</summary>
        private static bool HasTunnel()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;

                    string text = ((nic.Name ?? "") + " " + (nic.Description ?? "")).ToLowerInvariant();

                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;
                    if (text.Contains("radmin") || text.Contains("hamachi") ||
                        text.Contains("tap-") || text.Contains("wintun") ||
                        text.Contains("wireguard") || text.Contains("vpn")) return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Закрывает найденные программы VPN. Возвращает строку о том, что
        /// получилось — её можно показать человеку как есть.
        /// </summary>
        public static string TurnOff(Found found)
        {
            if (found == null) return "VPN не найден.";

            int closed = 0, stubborn = 0;

            foreach (var process in found.Processes)
            {
                try
                {
                    if (process.HasExited) continue;

                    // сначала по-хорошему: просим закрыться
                    try { process.CloseMainWindow(); }
                    catch { }

                    if (!process.WaitForExit(1500))
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                    }

                    closed++;
                    Log.Info("VPN закрыт: " + process.ProcessName);
                }
                catch (Exception ex)
                {
                    stubborn++;
                    Log.Warn("VPN не закрылся: " + ex.Message);
                }
            }

            if (closed == 0 && stubborn == 0)
                return "Программу VPN закрывать не пришлось — запущенных не нашлось.";

            if (stubborn > 0)
                return "Закрыто программ: " + closed + ", не поддались: " + stubborn
                     + ". Возможно, VPN работает службой — её останавливают с правами администратора.";

            return "VPN закрыт (" + found.Title + ").";
        }

        private static string Pretty(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return "VPN";

            string lower = processName.ToLowerInvariant();
            if (lower.Contains("radmin")) return "Radmin VPN";
            if (lower.Contains("paper")) return "Paper VPN";
            if (lower.Contains("hamachi")) return "Hamachi";
            if (lower.Contains("wireguard")) return "WireGuard";
            if (lower.Contains("openvpn")) return "OpenVPN";

            return processName;
        }
    }
}
