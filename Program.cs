using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Google.Apis.Upload;
using System.Collections;
using Google.Apis.Download;

namespace GoogleDriveLFS
{
    internal class Program
    {
        const string ConfigName = ".gdrivelfs";
        static readonly JsonSerializerOptions JsonOptions;
        static TextWriter? LogStream;

        static Program()
        {
            JsonOptions = new JsonSerializerOptions();
            JsonOptions.IncludeFields = true;
            JsonOptions.WriteIndented = false;
            JsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        static void Main(string[] args)
        {
            var configPath = args.Length > 0 ? args[0] : ConfigName;
            if (string.IsNullOrEmpty(configPath))
            {
                Console.Error.WriteLine($"{ConfigName} not found, working directory: {Environment.CurrentDirectory}");
                return;
            }

            var config = JsonSerializer.Deserialize<Config>(System.IO.File.ReadAllText(configPath), JsonOptions);

            if (config.attach_debugger)
            {
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(1000);
                }
                Debugger.Break();
            }

            var initializer = new ServiceAccountCredential.Initializer(config.client_email) { Scopes = new string[] { DriveService.Scope.Drive } };
            ServiceAccountCredential credential = new ServiceAccountCredential(initializer.FromPrivateKey(config.private_key));

            // Create the service.
            //using (var log = OpenLog(config.log_path))
            {
                //LogStream = log;

                using (var service = new DriveService(new BaseClientService.Initializer() { HttpClientInitializer = credential, ApplicationName = "GoogleDriveLFS" }))
                {

                    if (config.drive_id == null)
                    {
                        ListTeamDrives(service);
                        return;
                    }

                    Log($"Working directory: {Environment.CurrentDirectory}");

                    if (config.input_files != null)
                    {
                        foreach (var file in config.input_files)
                        {
                            using (var reader = new StreamReader(file))
                            {
                                Log($"Progressing commands from: {file}");
                                ProcessCommands(service, config.drive_id, reader, Console.Out);
                            }
                        }
                    }
                    else
                    {
                        Log($"Progressing commands from STDIN, STDIN Redirected {Console.IsInputRedirected}, STDOUT Redirected {Console.IsOutputRedirected}");
                        //using (var input = Console.IsInputRedirected ? new StreamReader(Console.OpenStandardInput(65536)) : Console.In)
                        //using (var output = Console.IsOutputRedirected ? new StreamWriter(Console.OpenStandardOutput(65536)) : Console.Out)
                        {
                            ProcessCommands(service, config.drive_id, Console.In, Console.Out);
                        }
                    }
                }
            }
        }

        private static StreamWriter? OpenLog(string logPath)
        {
            if (logPath != null && Environment.ProcessPath != null)
            {
                logPath = logPath.Replace("~", Path.GetDirectoryName(Environment.ProcessPath));
            }
            if (logPath != null)
            {
                logPath = Path.ChangeExtension(logPath, $"{Process.GetCurrentProcess().Id}" + Path.GetExtension(logPath));
            }
            return logPath != null ? System.IO.File.CreateText(logPath) : null;
        }

        private static bool TryGetCurrentDirConfig(out string path)
        {
            if (Environment.ProcessPath != null)
            {
                path = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), ConfigName);
                return System.IO.File.Exists(path);
            }
            path = null;
            return false;
        }

        private static void Log(string msg)
        {
            Console.Error.WriteLine(msg);
            //if (LogStream != null)
            //{
            //    LogStream.WriteLine(msg);
            //    LogStream.Flush();
            //}
        }

        private static void ListTeamDrives(DriveService service)
        {
            Log("Listing team drives:");
            foreach (var drive in service.Teamdrives.List().Execute().TeamDrives)
            {
                Log($"{drive.Id}: {drive.Name}");
            }
        }

        private static void ProcessCommands(DriveService service, string driveId, TextReader inputCommands, TextWriter output)
        {
            Log("Processing commands...");

            while (true)
            {
                string? cmdJson = inputCommands.ReadLine();
                if (string.IsNullOrWhiteSpace(cmdJson))
                {
                    Thread.Sleep(100);
                    continue;
                }

                Log("Processing: " + cmdJson);
                var cmd = JsonSerializer.Deserialize<CommandData>(cmdJson, JsonOptions);

                switch (cmd.@event)
                {
                    case CommandKind.init: SendCommand(output, "{ }"); break;
                    case CommandKind.upload: HandleUpload(service, driveId, cmd.oid, cmd.size, cmd.path, cmd.action, output); break;
                    case CommandKind.download: HandleDownload(service, driveId, cmd.oid, cmd.size, cmd.action, output); break;
                    case CommandKind.terminate: Log("Processing commands...done"); return;
                }
            }
            ;
        }

        private static void HandleUpload(DriveService service, string driveId, string oid, long size, string path, ActionData action, TextWriter output)
        {
            using (var stream = System.IO.File.OpenRead(path))
            {
                var listRequest = service.Files.List();
                listRequest.Q = $"name='{oid}'";
                listRequest.Fields = "files(id)";
                listRequest.Corpora = "drive";
                listRequest.DriveId = driveId;
                listRequest.IncludeItemsFromAllDrives = true;
                listRequest.SupportsAllDrives = true;
                var list = listRequest.Execute();

                ResumableUpload<Google.Apis.Drive.v3.Data.File, Google.Apis.Drive.v3.Data.File> request;

                if (list.Files.Count > 0)
                {
                    string fileId = list.Files[0].Id;

                    // Only include fields which should be changed
                    var file = new Google.Apis.Drive.v3.Data.File();
                    file.MimeType = "application/octet-stream";

                    var updateRequest = service.Files.Update(file, fileId, stream, "application/octet-stream");
                    updateRequest.SupportsAllDrives = true;
                    request = updateRequest;
                }
                else
                {
                    var driveFile = new Google.Apis.Drive.v3.Data.File();
                    driveFile.Name = oid;
                    driveFile.MimeType = "application/octet-stream";
                    driveFile.DriveId = driveId;
                    driveFile.Parents = new string[] { driveId };

                    var createRequest = service.Files.Create(driveFile, stream, "application/octet-stream");
                    createRequest.SupportsAllDrives = true;
                    createRequest.Fields = "id";
                    request = createRequest;
                }

                long bytes = 0;
                request.ProgressChanged += progress =>
                {
                    ReportProgress(output, oid, progress.BytesSent, progress.BytesSent - bytes);
                    bytes = progress.BytesSent;
                };

                var response = request.Upload();
                if (response.Status != UploadStatus.Completed)
                {
                    ReportError(output, oid, 3, $"Upload failed: {response.Exception.Message}");
                }
                else
                {
                    Log($"File uploaded: {request.ResponseBody.Id}");
                    ReportComplete(output, oid, null);
                }
            }
        }

        private static void HandleDownload(DriveService service, string driveId, string oid, long size, ActionData action, TextWriter output)
        {
            var listRequest = service.Files.List();
            listRequest.Q = $"name='{oid}'";
            listRequest.Fields = "files(id)";
            listRequest.Corpora = "drive";
            listRequest.DriveId = driveId;
            listRequest.IncludeItemsFromAllDrives = true;
            listRequest.SupportsAllDrives = true;
            var list = listRequest.Execute();
            if (list.Files.Count == 0)
            {
                ReportError(output, oid, 2, "File not found");
                return;
            }

            string tmpPath = "";
            FileStream? stream = default;
            try
            {
                tmpPath = GetLfsTempFile(oid);
                stream = System.IO.File.Create(tmpPath);
            }
            catch (Exception e)
            {
                ReportError(output, oid, 4, "Could not create temporary file " + e.Message);
                return;
            }

            IDownloadProgress? progress;
            using (stream)
            {
                var file = list.Files[0];
                var getRequest = service.Files.Get(file.Id);
                getRequest.SupportsAllDrives = true;

                long bytes = 0;
                getRequest.MediaDownloader.ProgressChanged += progress =>
                {
                    ReportProgress(output, oid, progress.BytesDownloaded, progress.BytesDownloaded - bytes);
                    bytes = progress.BytesDownloaded;
                };

                progress = getRequest.DownloadWithStatus(stream);
            }

            if (progress.Status != DownloadStatus.Completed)
            {
                ReportError(output, oid, 3, $"Download failed: {progress.Exception.Message}");
            }
            else
            {
                ReportComplete(output, oid, tmpPath);
            }
        }

        private static string GetLfsTempFile(string oid)
        {
            // Git LFS uses rename to move tmp files to their destination. But rename does not work across
            // drives, so we have to make sure that the tmp file is on the same drive as the repository.
            var pwd = Directory.GetCurrentDirectory();
            var tmpDir = Path.Combine(pwd, ".tmplfs");
            if (!Directory.Exists(tmpDir))
            {
                Directory.CreateDirectory(tmpDir);
            }
            return Path.Combine(tmpDir, oid);
        }

        private static void SendCommand(TextWriter output, string cmd)
        {
            if (output != LogStream)
            {
                Log("-> " + cmd);
            }
            output.Write(cmd + "\n");
            output.Flush();
        }

        private static void ReportProgress(TextWriter output, string oid, long bytesSoFar, long bytesSinceLast)
        {
            SendCommand(output, JsonSerializer.Serialize(new CommandData { @event = CommandKind.progress, oid = oid, bytesSoFar = bytesSoFar, bytesSinceLast = bytesSinceLast }, JsonOptions));
        }

        private static void ReportError(TextWriter output, string oid, int code, string message)
        {
            SendCommand(output, JsonSerializer.Serialize(new CommandData { @event = CommandKind.complete, oid = oid, error = new ErrorData { code = code, message = message } }, JsonOptions));
        }

        private static void ReportComplete(TextWriter output, string oid, string path)
        {
            SendCommand(output, JsonSerializer.Serialize(new CommandData { @event = CommandKind.complete, oid = oid, path = path }, JsonOptions));
        }
    }
}