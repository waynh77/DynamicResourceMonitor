using System.Diagnostics;

namespace DynamicResourceMonitor
{
    class Program
    {
        // Variabel batas anomali dibuat dinamis (bisa diubah saat runtime)
        static double maxCpuPercent = 40.0;
        static double maxRamMb = 1500.0;
        static bool isRunning = true;

        static void Main(string[] args)
        {
            Dictionary<int, TimeSpan> previousCpuTimes = new Dictionary<int, TimeSpan>();
            DateTime previousCheckTime = DateTime.Now;
            int processorCount = Environment.ProcessorCount;

            Console.CursorVisible = false; // Menyembunyikan kursor agar tampilan lebih rapi

            while (isRunning)
            {
                Process[] processes = Process.GetProcesses();
                DateTime currentCheckTime = DateTime.Now;
                TimeSpan timeDifference = currentCheckTime - previousCheckTime;

                List<string> anomalyReports = new List<string>();
                HashSet<int> currentProcessIds = new HashSet<int>();

                foreach (Process p in processes)
                {
                    currentProcessIds.Add(p.Id);
                    try
                    {
                        if (p.HasExited) continue;

                        TimeSpan currentCpuTime = p.TotalProcessorTime;
                        double ramUsageMb = p.WorkingSet64 / (1024.0 * 1024.0);
                        double cpuUsagePercent = 0;

                        if (previousCpuTimes.TryGetValue(p.Id, out TimeSpan prevCpuTime))
                        {
                            double cpuUsedMs = (currentCpuTime - prevCpuTime).TotalMilliseconds;
                            double totalMsPassed = timeDifference.TotalMilliseconds * processorCount;
                            cpuUsagePercent = (cpuUsedMs / totalMsPassed) * 100;

                            // Jika terdeteksi anomali berdasarkan variabel dinamis
                            if (cpuUsagePercent > maxCpuPercent || ramUsageMb > maxRamMb)
                            {
                                string report = String.Format("{0,-28} | {1,-8} | {2,-8:F2} | {3,-10:F2}",
                                    p.ProcessName.Length > 28 ? p.ProcessName.Substring(0, 28) : p.ProcessName,
                                    p.Id,
                                    cpuUsagePercent,
                                    ramUsageMb);
                                anomalyReports.Add(report);
                            }
                        }
                        // Update cache CPU time
                        previousCpuTimes[p.Id] = currentCpuTime;
                    }
                    catch (Exception)
                    {
                        // Abaikan error Access Denied untuk proses sistem operasi tingkat tinggi
                    }
                }

                // ==========================================
                // RENDER DASHBOARD (TAMPILAN UI)
                // ==========================================
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("=== DYNAMIC RESOURCE MONITOR ===");
                Console.ResetColor();
                Console.WriteLine($"Batas Anomali Saat Ini -> CPU: {maxCpuPercent}% | RAM: {maxRamMb} MB");
                Console.WriteLine("Kontrol: [C] Ubah Batas CPU | [R] Ubah Batas RAM | [Q] Keluar\n");

                Console.WriteLine(String.Format("{0,-28} | {1,-8} | {2,-8} | {3,-10}", "Nama Proses", "ID", "CPU (%)", "RAM (MB)"));
                Console.WriteLine(new String('-', 64));

                if (anomalyReports.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    // Tampilkan daftar aplikasi yang anomali
                    foreach (var report in anomalyReports)
                    {
                        Console.WriteLine(report + " <-- ANOMALI!");
                    }
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nTidak ada aplikasi yang memakan resource tidak wajar. Sistem aman.");
                    Console.ResetColor();
                }

                // Bersihkan memori aplikasi (hapus proses yang sudah di-close dari dictionary)
                var exitedIds = previousCpuTimes.Keys.Where(id => !currentProcessIds.Contains(id)).ToList();
                foreach (var id in exitedIds) previousCpuTimes.Remove(id);

                previousCheckTime = currentCheckTime;

                // Fungsi custom untuk delay sambil menunggu perintah keyboard dari user
                WaitAndListenForInput(2000);
            }

            Console.Clear();
            Console.WriteLine("Monitoring dihentikan.");
            Console.CursorVisible = true;
        }

        // Fungsi agar jeda (delay) tidak membekukan aplikasi. 
        // Selama 2 detik jeda, program akan terus memantau apakah Anda menekan tombol.
        static void WaitAndListenForInput(int delayMs)
        {
            int waited = 0;
            while (waited < delayMs)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.Q) // Quit
                    {
                        isRunning = false;
                        break;
                    }
                    else if (key == ConsoleKey.C) // Ubah CPU
                    {
                        Console.CursorVisible = true;
                        Console.Write("\n[PENGATURAN] Masukkan batas persentase CPU baru (contoh: 25.5): ");
                        if (double.TryParse(Console.ReadLine(), out double newCpu))
                        {
                            maxCpuPercent = newCpu;
                        }
                        Console.CursorVisible = false;
                        break; // Langsung refresh layar
                    }
                    else if (key == ConsoleKey.R) // Ubah RAM
                    {
                        Console.CursorVisible = true;
                        Console.Write("\n[PENGATURAN] Masukkan batas RAM baru dalam MB (contoh: 2000): ");
                        if (double.TryParse(Console.ReadLine(), out double newRam))
                        {
                            maxRamMb = newRam;
                        }
                        Console.CursorVisible = false;
                        break; // Langsung refresh layar
                    }
                }
                Thread.Sleep(100);
                waited += 100;
            }
        }
    }
}