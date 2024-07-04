﻿using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.SDK.Helpers;
using System.Diagnostics;
using System.IO.Compression;

namespace LANCommander.Services
{
    public class ServerService : BaseDatabaseService<Server>
    {
        private readonly GameService GameService;

        public ServerService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor, GameService gameService) : base(dbContext, httpContextAccessor)
        {
            GameService = gameService;
        }

        public async Task<Server> Import(Guid objectKey)
        {
            var settings = SettingService.GetSettings();

            var importArchivePath = ArchiveService.GetArchiveFileLocation(objectKey.ToString());

            using (var importArchive = ZipFile.OpenRead(importArchivePath))
            {
                var manifest = ManifestHelper.Deserialize<SDK.Models.Server>(await importArchive.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                var server = await Get(manifest.Id);

                var exists = server != null;

                if (!exists)
                    server = new Server();

                server.Id = manifest.Id;
                server.Name = manifest.Name;
                server.Autostart = manifest.Autostart;
                server.AutostartMethod = (Data.Enums.ServerAutostartMethod)(int)manifest.AutostartMethod;
                server.AutostartDelay = manifest.AutostartDelay;
                server.WorkingDirectory = Path.Combine(settings.Servers.StoragePath, server.Name.SanitizeFilename());
                server.ProcessTerminationMethod = manifest.ProcessTerminationMethod;
                server.OnStartScriptPath = manifest.OnStartScriptPath;
                server.OnStopScriptPath = manifest.OnStopScriptPath;

                if (manifest.Game.Id != Guid.Empty)
                {
                    var gameExists = GameService.Exists(manifest.Game.Id);

                    if (gameExists)
                        server.GameId = manifest.Game.Id;
                }

                #region Consoles
                if (server.ServerConsoles == null)
                    server.ServerConsoles = new List<ServerConsole>();

                foreach (var serverConsole in server.ServerConsoles)
                {
                    var manifestConsole = manifest.ServerConsoles.FirstOrDefault(c => c.Id == serverConsole.Id);

                    if (manifestConsole != null)
                    {
                        serverConsole.Name = manifestConsole.Name;
                        serverConsole.Type = (Data.Enums.ServerConsoleType)(int)manifestConsole.Type;
                        serverConsole.Path = manifestConsole.Path;
                        serverConsole.Host = manifestConsole.Host;
                        serverConsole.Port = manifestConsole.Port;
                        // serverConsole.Password = manifestConsole.Password;
                    }
                    else
                        server.ServerConsoles.Remove(serverConsole);
                }

                if (manifest.ServerConsoles != null)
                {
                    foreach (var manifestConsole in manifest.ServerConsoles.Where(mc => !server.ServerConsoles.Any(c => c.Id != mc.Id)))
                    {
                        server.ServerConsoles.Add(new ServerConsole()
                        {
                            Id = manifestConsole.Id,
                            Name = manifestConsole.Name,
                            Type = (Data.Enums.ServerConsoleType)(int)manifestConsole.Type,
                            Path = manifestConsole.Path,
                            Host = manifestConsole.Host,
                            Port = manifestConsole.Port,
                            // Password = manifestConsole.Password
                        });
                    }
                }
                #endregion

                #region HTTP Paths
                if (server.HttpPaths == null)
                    server.HttpPaths = new List<ServerHttpPath>();

                foreach (var httpPath in server.HttpPaths)
                {
                    var manifestHttpPath = manifest.HttpPaths.FirstOrDefault(p => p.Id == httpPath.Id);

                    if (manifestHttpPath != null)
                    {
                        httpPath.LocalPath = manifestHttpPath.LocalPath.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
                        httpPath.Path = manifestHttpPath.Path;
                    }
                    else
                        server.HttpPaths.Remove(httpPath);
                }

                if (manifest.HttpPaths != null)
                {
                    foreach (var manifestPath in manifest.HttpPaths.Where(mp => !server.HttpPaths.Any(p => p.Id != mp.Id)))
                    {
                        server.HttpPaths.Add(new ServerHttpPath()
                        {
                            Id = manifestPath.Id,
                            Path = manifestPath.Path,
                            LocalPath = manifestPath.LocalPath
                        });
                    }
                }
                #endregion

                #region Scripts
                if (server.Scripts == null)
                    server.Scripts = new List<Script>();

                foreach (var script in server.Scripts)
                {
                    var manifestScript = manifest.Scripts.FirstOrDefault(s => s.Id == script.Id);

                    if (manifestScript != null)
                    {
                        script.Contents = await importArchive.ReadAllTextAsync($"Scripts/{script.Id}");
                        script.Description = manifestScript.Description;
                        script.Name = manifestScript.Name;
                        script.RequiresAdmin = manifestScript.RequiresAdmin;
                        script.Type = (Data.Enums.ScriptType)(int)manifestScript.Type;
                    }
                    else
                        server.Scripts.Remove(script);
                }

                if (manifest.Scripts != null)
                {
                    foreach (var manifestScript in manifest.Scripts.Where(ms => !server.Scripts.Any(s => s.Id == ms.Id)))
                    {
                        server.Scripts.Add(new Script()
                        {
                            Id = manifestScript.Id,
                            Contents = await importArchive.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
                            Description = manifestScript.Description,
                            Name = manifestScript.Name,
                            RequiresAdmin = manifestScript.RequiresAdmin,
                            Type = (Data.Enums.ScriptType)(int)manifestScript.Type,
                            CreatedOn = manifestScript.CreatedOn,
                        });
                    }
                }
                #endregion

                #region Actions
                server.Actions = new List<Data.Models.Action>();

                if (manifest.Actions != null && manifest.Actions.Count() > 0)
                foreach (var manifestAction in manifest.Actions)
                {
                    new Data.Models.Action()
                    {
                        Name = manifestAction.Name,
                        Arguments = manifestAction.Arguments,
                        Path = manifestAction.Path,
                        WorkingDirectory = manifestAction.WorkingDirectory,
                        PrimaryAction = manifestAction.IsPrimaryAction,
                        SortOrder = manifestAction.SortOrder,
                    };
                }
                #endregion

                #region Extract Files
                foreach (var entry in importArchive.Entries.Where(a => a.FullName.StartsWith("Files/")))
                {
                    var destination = entry.FullName
                        .Substring(6, entry.FullName.Length - 6)
                        .TrimEnd('/')
                        .Replace('/', Path.DirectorySeparatorChar);

                    destination = Path.Combine(server.WorkingDirectory, destination);

                    if (entry.FullName.EndsWith('/'))
                    {
                        if (!Directory.Exists(destination))
                            Directory.CreateDirectory(destination);
                    }
                    else
                        entry.ExtractToFile(destination, true);
                }
                #endregion

                if (exists)
                    server = await Update(server);
                else
                    server = await Add(server);

                return server;
            }
        }
    }
}
