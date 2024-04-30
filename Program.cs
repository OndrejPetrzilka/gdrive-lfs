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

namespace GoogleDriveLFS
{
    internal class Program
    {
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
            string? keyFile = args.Length >= 1 ? args[0] : null;
            string? driveId = args.Length >= 2 ? args[1] : null;


            Log($"Working dir: {Environment.CurrentDirectory}");

            var accountConfig = JsonSerializer.Deserialize<AccountConfig>(System.IO.File.ReadAllText(keyFile), JsonOptions);

            var initializer = new ServiceAccountCredential.Initializer(accountConfig.client_email) { Scopes = new string[] { DriveService.Scope.Drive } };
            ServiceAccountCredential credential = new ServiceAccountCredential(initializer.FromPrivateKey(accountConfig.private_key));

            // Create the service.
            var service = new DriveService(new BaseClientService.Initializer() { HttpClientInitializer = credential, ApplicationName = "GoogleDriveLFS" });

            if (driveId == null)
            {
                ListTeamDrives(service);
                return;
            }

            var activeDrive = service.Teamdrives.Get(driveId).Execute();
            Log($"Selected drive: {activeDrive.Name}, {activeDrive.Id}");

            if (args.Length > 2)
            {
                LogStream = Console.Out;
                for (int i = 2; i < args.Length; i++)
                {
                    using (var reader = new StreamReader(args[i]))
                    {
                        Log($"Progressing commands from: {args[i]}");
                        ProcessCommands(service, activeDrive, reader, Console.Out);
                    }
                }
            }
            else
            {
                ProcessCommands(service, activeDrive, Console.In, Console.Out);
            }
        }

        private static void Log(string msg)
        {
            if (LogStream != null)
            {
                LogStream.WriteLine(msg);
            }
        }

        private static void ListTeamDrives(DriveService service)
        {
            Log("Listing team drives:");
            foreach (var drive in service.Teamdrives.List().Execute().TeamDrives)
            {
                Log($"{drive.Id}: {drive.Name}");
            }
        }

        private static void ProcessCommands(DriveService service, TeamDrive drive, TextReader inputCommands, TextWriter output)
        {
            Log("Processing commands...");

            string? cmdJson;
            while ((cmdJson = inputCommands.ReadLine()) != null)
            {
                Log("Processing: " + cmdJson);
                var cmd = JsonSerializer.Deserialize<CommandData>(cmdJson, JsonOptions);

                switch (cmd.@event)
                {
                    case CommandKind.init: break;
                    case CommandKind.upload: HandleUpload(service, drive, cmd.oid, cmd.size, cmd.path, cmd.action, output); break;
                    case CommandKind.download: HandleDownload(service, drive, cmd.oid, cmd.size, cmd.action, output); break;
                    case CommandKind.terminate: return;
                }
            }
        }

        private static void HandleUpload(DriveService service, TeamDrive drive, string oid, long size, string path, ActionData action, TextWriter output)
        {
            using (var stream = System.IO.File.OpenRead(path))
            {
                var listRequest = service.Files.List();
                listRequest.Q = $"name='{oid}'";
                listRequest.Fields = "files(id)";
                listRequest.Corpora = "drive";
                listRequest.DriveId = drive.Id;
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
                    driveFile.DriveId = drive.Id;
                    driveFile.Parents = new string[] { drive.Id };

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

        private static void HandleDownload(DriveService service, TeamDrive drive, string oid, long size, ActionData action, TextWriter output)
        {
            var listRequest = service.Files.List();
            listRequest.Q = $"name='{oid}'";
            listRequest.Fields = "files(id)";
            listRequest.Corpora = "drive";
            listRequest.DriveId = drive.Id;
            listRequest.IncludeItemsFromAllDrives = true;
            listRequest.SupportsAllDrives = true;
            var list = listRequest.Execute();
            if (list.Files.Count == 0)
            {
                ReportError(output, oid, 2, "File not found");
                return;
            }

            var path = Path.GetTempFileName();
            using (var stream = System.IO.File.Create(path))
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

                var progress = getRequest.DownloadWithStatus(stream);
                if (progress.Status != Google.Apis.Download.DownloadStatus.Completed)
                {
                    ReportError(output, oid, 3, $"Download failed: {progress.Exception.Message}");
                }
                else
                {
                    ReportComplete(output, oid, path);
                }
            }
        }

        private static void SendCommand(TextWriter output, string cmd)
        {
            if (output != LogStream)
            {
                Log("-> " + cmd);
            }
            output.WriteLine(cmd);
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