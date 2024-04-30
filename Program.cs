using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using System.Text.Json.Serialization;

namespace GoogleDriveLFS
{
    internal class Program
    {
        static readonly JsonSerializerOptions JsonOptions = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var result = new JsonSerializerOptions();
            result.IncludeFields = true;
            result.WriteIndented = false;
            result.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            result.Converters.Add(new JsonStringEnumConverter());
            return result;
        }

        static void Main(string[] args)
        {
            //string FileName = "c:\\Data\\Projects\\drive-test3\\log.txt";
            //File.WriteAllLines(FileName, args);
            //File.AppendAllText(FileName, "\r\n\r\n");
            //while (true)
            //{
            //    string? str = Console.ReadLine();
            //    if (str == null)
            //        return;
            //    File.AppendAllLines(FileName, new string[] { str });
            //}

            string? keyFile = args.Length >= 1 ? args[0] : null;
            string? driveId = args.Length >= 2 ? args[1] : null;

            Console.WriteLine($"Working dir: {Environment.CurrentDirectory}");

            var accountConfig = JsonSerializer.Deserialize<AccountConfig>(System.IO.File.ReadAllText(keyFile), JsonOptions);

            var initializer = new ServiceAccountCredential.Initializer(accountConfig.client_email);
            initializer.Scopes = new string[] { DriveService.Scope.Drive };
            ServiceAccountCredential credential = new ServiceAccountCredential(initializer.FromPrivateKey(accountConfig.private_key));

            // Create the service.
            var service = new DriveService(new BaseClientService.Initializer() { HttpClientInitializer = credential, ApplicationName = "GoogleDriveLFS" });

            if (driveId == null)
            {
                ListTeamDrives(service);
                return;
            }

            var activeDrive = service.Teamdrives.Get(driveId).Execute();
            Console.WriteLine($"Selected drive: {activeDrive.Name}, {activeDrive.Id}");

            if (args.Length > 2)
            {
                for (int i = 2; i < args.Length; i++)
                {
                    using (var reader = new StreamReader(args[i]))
                    {
                        Console.WriteLine($"Progressing commands from: {args[i]}");
                        ProcessCommands(service, activeDrive, reader, Console.Out);
                    }
                }
            }
            else
            {
                ProcessCommands(service, activeDrive, Console.In, Console.Out);
            }
        }

        private static void ListTeamDrives(DriveService service)
        {
            Console.WriteLine("Listing team drives:");
            foreach (var drive in service.Teamdrives.List().Execute().TeamDrives)
            {
                Console.WriteLine($"{drive.Id}: {drive.Name}");
            }
        }

        private static void ProcessCommands(DriveService service, TeamDrive drive, TextReader inputCommands, TextWriter output)
        {
            Console.WriteLine("Processing commands...");

            string? cmdJson;
            while ((cmdJson = inputCommands.ReadLine()) != null)
            {
                Console.WriteLine("Processing: " + cmdJson);
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
                var driveFile = new Google.Apis.Drive.v3.Data.File();
                driveFile.Name = oid;
                driveFile.MimeType = "application/octet-stream";
                driveFile.DriveId = drive.Id;
                driveFile.Parents = new string[] { drive.Id };

                var request = service.Files.Create(driveFile, stream, "application/octet-stream");
                request.SupportsAllDrives = true;
                request.Fields = "id";

                var response = request.Upload();
                if (response.Status != Google.Apis.Upload.UploadStatus.Completed)
                    throw response.Exception;

                ReportComplete(output, oid, null);
                //return request.ResponseBody.Id;
            }
        }

        private static void HandleDownload(DriveService service, TeamDrive drive, string oid, long size, ActionData action, TextWriter output)
        {
            var request = service.Files.List();
            request.Q = $"name='{oid}'";
            request.IncludeItemsFromAllDrives = true;
            request.Fields = "files(id, name, size, mimeType)";
            request.SupportsAllDrives = true;
            var list = request.Execute();
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

                var progress = getRequest.DownloadWithStatus(stream);
                long lastBytes = 0;
                while (progress.Status == Google.Apis.Download.DownloadStatus.Downloading || progress.Status == Google.Apis.Download.DownloadStatus.NotStarted)
                {
                    ReportProgress(output, oid, progress.BytesDownloaded, progress.BytesDownloaded - lastBytes);
                    lastBytes = progress.BytesDownloaded;
                    Thread.Sleep(1000);
                }
                if (progress.Status == Google.Apis.Download.DownloadStatus.Completed)
                {
                    ReportComplete(output, oid, path);
                }
                else if (progress.Status == Google.Apis.Download.DownloadStatus.Failed)
                {
                    ReportError(output, oid, 3, $"Download failed: {progress.Exception.Message}");
                    throw progress.Exception;
                }
            }
        }

        private static void ReportProgress(TextWriter output, string oid, long bytesSoFar, long bytesSinceLast)
        {
            output.Write("  ");
            output.WriteLine(JsonSerializer.Serialize(new CommandData { @event = CommandKind.progress, oid = oid, bytesSoFar = bytesSoFar, bytesSinceLast = bytesSinceLast }, JsonOptions));
        }

        private static void ReportError(TextWriter output, string oid, int code, string message)
        {
            output.Write("  ");
            output.WriteLine(JsonSerializer.Serialize(new CommandData { @event = CommandKind.complete, oid = oid, error = new ErrorData { code = code, message = message } }, JsonOptions));
        }

        private static void ReportComplete(TextWriter output, string oid, string path)
        {
            output.Write("  ");
            output.WriteLine(JsonSerializer.Serialize(new CommandData { @event = CommandKind.complete, oid = oid, path = path }, JsonOptions));
        }
    }
}