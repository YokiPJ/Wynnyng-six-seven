using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

internal static class Program
{
    private const string Repository = "YokiPJ/Wynnyng-six-seven";
    private const string Branch = "codex/phase-0-1a";
    private const long MaximumExpandedBytes = 536870912;

    [DataContract] private sealed class BranchInfo
    { [DataMember(Name = "commit")] public CommitInfo Commit { get; set; } }
    [DataContract] private sealed class CommitInfo
    { [DataMember(Name = "sha")] public string Sha { get; set; } }
    [DataContract] private sealed class Receipt
    { [DataMember(Name = "succeeded")] public bool Succeeded { get; set; } }

    private static int Main(string[] arguments)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (arguments.Length == 1 && arguments[0] == "--self-test") return SelfTest();
        Console.WriteLine("WYNNYNG - atualizar, testar, gerar e jogar");
        Console.WriteLine("Partida de teste com dois times e CPU. O jogo Unity sera gerado no seu PC.");
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Este preparador deve ser executado no Windows.");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string storage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wynnyng");
            Directory.CreateDirectory(storage);
            string sha;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Wynnyng-Builder/1.0");
                Console.WriteLine("Consultando a versao mais recente do repositorio...");
                byte[] metadata = client.GetByteArrayAsync("https://api.github.com/repos/" + Repository + "/branches/" + Branch).GetAwaiter().GetResult();
                using (var input = new MemoryStream(metadata))
                    sha = ((BranchInfo)new DataContractJsonSerializer(typeof(BranchInfo)).ReadObject(input))?.Commit?.Sha;
                if (sha == null || sha.Length != 40 || sha.Any(c => !Uri.IsHexDigit(c)))
                    throw new InvalidDataException("O GitHub nao retornou uma revisao valida.");
                string project = Path.Combine(storage, "versions", sha);
                if (!File.Exists(Path.Combine(project, ".download-complete")))
                {
                    string staging = Path.Combine(storage, "downloads", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(staging);
                    try
                    {
                        string archivePath = Path.Combine(staging, "project.zip");
                        Console.WriteLine("Baixando projeto " + sha.Substring(0, 8) + "...");
                        using (Stream remote = client.GetStreamAsync("https://codeload.github.com/" + Repository + "/zip/" + sha).GetAwaiter().GetResult())
                        using (FileStream output = File.Create(archivePath)) remote.CopyTo(output);
                        string extracted = Path.Combine(staging, "source");
                        ExtractValidated(archivePath, extracted);
                        string[] candidates = Directory.GetDirectories(extracted);
                        if (candidates.Length != 1 || !File.Exists(Path.Combine(candidates[0], "Packages", "manifest.json")) ||
                            !File.Exists(Path.Combine(candidates[0], "tools", "Build-and-Play.ps1")))
                            throw new InvalidDataException("O ZIP nao contem o projeto e o gerador esperados.");
                        // A prior incomplete download is kept for inspection rather than overwritten.
                        if (Directory.Exists(project)) Directory.Move(project, project + ".incomplete-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(Path.GetDirectoryName(project));
                        Directory.Move(candidates[0], project);
                        File.WriteAllText(Path.Combine(project, ".download-complete"), sha);
                    }
                    finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                }

                string game = Path.Combine(project, "Builds", "Windows", "Wynnyng.exe");
                if (HasCompletedBuild(project, game))
                {
                    Console.WriteLine("Versao atual ja preparada. Abrindo Wynnyng...");
                    Process.Start(new ProcessStartInfo(game) { WorkingDirectory = Path.GetDirectoryName(game), UseShellExecute = true });
                    return 0;
                }

                if (Process.GetProcessesByName("Unity").Length > 0)
                    throw new InvalidOperationException("Feche as janelas do Unity e abra este preparador novamente para gerar o jogo.");
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe");
                if (!File.Exists(powershell)) throw new FileNotFoundException("Windows PowerShell nao encontrado.");
                string script = Path.Combine(project, "tools", "Build-and-Play.ps1");
                Console.WriteLine("A primeira geracao pode levar alguns minutos. Os testes e a compilacao usam o Unity instalado.");
                using (Process process = Process.Start(new ProcessStartInfo(powershell,
                    "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) + " -ProjectPath " + Quote(project))
                    { WorkingDirectory = project, UseShellExecute = false }))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException("A geracao falhou. Os logs estao em " + Path.Combine(project, "work", "WindowsBuild"));
                }
                if (!HasCompletedBuild(project, game)) throw new InvalidDataException("O Unity nao produziu um player completo.");
                Console.WriteLine("Jogo gerado em: " + game);
                return 0;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("\n" + exception.Message);
            Console.Error.WriteLine("A pasta original do seu clone permanece preservada. Pressione Enter para fechar.");
            if (!Console.IsInputRedirected) Console.ReadLine();
            return 1;
        }
    }

    private static string Quote(string value)
    {
        if (value.Contains("\"")) throw new ArgumentException("Caminho contem aspas invalidas.");
        return "\"" + value.TrimEnd('\\') + "\"";
    }

    private static bool HasCompletedBuild(string project, string game)
    {
        string receipt = Path.Combine(project, "work", "WindowsBuild", "build-result.json");
        if (!File.Exists(game) || !File.Exists(receipt) ||
            !File.Exists(Path.Combine(Path.GetDirectoryName(game), "UnityPlayer.dll")) ||
            !Directory.Exists(Path.Combine(Path.GetDirectoryName(game), "Wynnyng_Data"))) return false;
        try
        {
            using (FileStream input = File.OpenRead(receipt))
                return ((Receipt)new DataContractJsonSerializer(typeof(Receipt)).ReadObject(input)).Succeeded;
        }
        catch (SerializationException) { return false; }
    }

    private static void ExtractValidated(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        string prefix = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long expanded = 0;
        using (ZipArchive archive = ZipFile.OpenRead(archivePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string path = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O ZIP contem um caminho fora da pasta do projeto.");
                expanded = checked(expanded + entry.Length);
                if (expanded > MaximumExpandedBytes) throw new InvalidDataException("O ZIP excedeu o limite deste preparador.");
            }
        }
        ZipFile.ExtractToDirectory(archivePath, destination);
    }

    private static int SelfTest()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "WynnyngBootstrapTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            if (Quote("C:\\Project Folder\\file.ps1") != "\"C:\\Project Folder\\file.ps1\"")
                throw new Exception("Argument quoting failed.");
            bool quoteRejected = false;
            try { Quote("bad\"path"); } catch (ArgumentException) { quoteRejected = true; }
            if (!quoteRejected) throw new Exception("Invalid quote was accepted.");
            string archive = Path.Combine(temporary, "bad.zip");
            using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (StreamWriter file = new StreamWriter(zip.CreateEntry("../escape.txt").Open())) file.Write("fixture");
            bool traversalRejected = false;
            try { ExtractValidated(archive, Path.Combine(temporary, "extract")); }
            catch (InvalidDataException) { traversalRejected = true; }
            if (!traversalRejected || File.Exists(Path.Combine(temporary, "escape.txt")))
                throw new Exception("Archive traversal was not rejected.");
            string game = Path.Combine(temporary, "Builds", "Windows", "Wynnyng.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(game));
            File.WriteAllText(game, "test fixture, not a player");
            if (HasCompletedBuild(temporary, game)) throw new Exception("Incomplete build accepted.");
            Directory.CreateDirectory(Path.Combine(temporary, "work", "WindowsBuild"));
            File.WriteAllText(Path.Combine(temporary, "work", "WindowsBuild", "build-result.json"), "{\"succeeded\":true}");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game), "UnityPlayer.dll"), "fixture");
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game), "Wynnyng_Data"));
            if (!HasCompletedBuild(temporary, game)) throw new Exception("Completed receipt was not read.");
            Console.WriteLine("PASS: quoting, traversal rejection and completed-build checks. No Unity player was run.");
            return 0;
        }
        finally { Directory.Delete(temporary, true); }
    }
}
