﻿using CommandLine;
using LANCommander.Launcher.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class CommandLineService
    {
        private readonly SDK.Client Client;
        private readonly GameService GameService;
        private readonly DownloadService DownloadService;
        private readonly ImportService ImportService;
        private readonly ProfileService ProfileService;

        private Settings Settings = SettingService.GetSettings();

        public CommandLineService(SDK.Client Client, GameService GameService, ImportService ImportService, ProfileService profileService)
        {
            Client = Client;
            GameService = GameService;
            ImportService = ImportService;
            ProfileService = profileService;
        }

        public async Task ParseCommandLineAsync(string[] args)
        {
            await Client.ValidateTokenAsync();

                
            var result = Parser.Default.ParseArguments
                <
                    RunScriptCommandLineOptions,
                    InstallCommandLineOptions,
                    ImportCommandLineOptions,
                    LoginCommandLineOptions,
                    LogoutCommandLineOptions,
                    ChangeAliasCommandLineOptions
                >(args);

            await result.WithParsedAsync<RunScriptCommandLineOptions>(async (options) =>
            {
                switch (options.Type)
                {
                    case SDK.Enums.ScriptType.Install:
                        await Client.Scripts.RunInstallScriptAsync(options.InstallDirectory, options.GameId);
                        break;

                    case SDK.Enums.ScriptType.Uninstall:
                        await Client.Scripts.RunUninstallScriptAsync(options.InstallDirectory, options.GameId);
                        break;

                    case SDK.Enums.ScriptType.BeforeStart:
                        await Client.Scripts.RunBeforeStartScriptAsync(options.InstallDirectory, options.GameId);
                        break;

                    case SDK.Enums.ScriptType.AfterStop:
                        await Client.Scripts.RunAfterStopScriptAsync(options.InstallDirectory, options.GameId);
                        break;

                    case SDK.Enums.ScriptType.NameChange:
                        await Client.Scripts.RunNameChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewName);
                        break;

                    case SDK.Enums.ScriptType.KeyChange:
                        await Client.Scripts.RunKeyChangeScriptAsync(options.InstallDirectory, options.GameId, options.NewKey);
                        break;
                }
            });

            await result.WithParsedAsync<InstallCommandLineOptions>(async (options) =>
            {
                Console.WriteLine($"Downloading and installing game with ID {options.GameId}...");

                try
                {
                    var game = await GameService.Get(options.GameId);

                    await DownloadService.Add(game);
                    await DownloadService.Install();

                    game = await GameService.Get(options.GameId);

                    Console.WriteLine($"Successfully installed {game.Title} to directory {game.InstallDirectory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Game could not be installed: {ex.Message}");
                }
            });

            await result.WithParsedAsync<UninstallCommandLineOptions>(async (options) =>
            {
                Console.WriteLine($"Uninstalling game with ID {options.GameId}...");

                try
                {
                    var game = await GameService.Get(options.GameId);

                    await GameService.UninstallAsync(game);

                    Console.WriteLine($"Game successfully uninstalled from {game.InstallDirectory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Game could not be uninstalled: {ex.Message}");
                }
            });

            await result.WithParsedAsync<ImportCommandLineOptions>(async (options) =>
            {
                Console.WriteLine("Importing games from server...");

                ImportService.OnImportComplete += async () =>
                {
                    Console.WriteLine("Import complete!");
                };

                ImportService.OnImportFailed += async () =>
                {
                    Console.WriteLine("Import failed!");
                };

                await ImportService.ImportAsync();
            });

            await result.WithParsedAsync<LoginCommandLineOptions>(async (options) =>
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(options.ServerAddress))
                        options.ServerAddress = Settings.Authentication.ServerAddress;

                    if (String.IsNullOrWhiteSpace(options.ServerAddress))
                        throw new ArgumentException("A server address must be specified");

                    Client.UseServerAddress(options.ServerAddress);

                    await Client.AuthenticateAsync(options.Username, options.Password);

                    Console.WriteLine("Logged in!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });

            await result.WithParsedAsync<LogoutCommandLineOptions>(async (options) =>
            {
                await Client.LogoutAsync();

                Settings.Authentication.AccessToken = "";
                Settings.Authentication.RefreshToken = "";
                Settings.Authentication.OfflineMode = false;

                SettingService.SaveSettings(Settings);
            });

            await result.WithParsedAsync<ChangeAliasCommandLineOptions>(async (options) =>
            {
                await ProfileService.ChangeAlias(options.Alias);

                Console.WriteLine($"Changed current user's alias from {Settings.Profile.Alias} to {options.Alias}");
            });
        }
    }
}
