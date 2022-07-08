﻿using Gamelib.Util;
using GameLib.Plugin.Ubisoft.Model;
using Microsoft.Win32;

namespace GameLib.Plugin.Ubisoft;

internal static class UbisoftGameFactory
{
    public static List<UbisoftGame> GetGames(string? installDir, UbisoftCatalog? catalog = null)
    {
        List<UbisoftGame> games = new();

        using var regKey = RegistryUtil.GetKey(RegistryHive.LocalMachine, @"SOFTWARE\Ubisoft\Launcher\Installs", true);

        if (string.IsNullOrEmpty(installDir) || regKey is null)
            return games;

        foreach (var regKeyGameId in regKey.GetSubKeyNames())
        {
            using var regKeyGame = regKey.OpenSubKey(regKeyGameId);
            if (regKeyGame is null)
                continue;

            var game = new UbisoftGame()
            {
                GameId = regKeyGameId,
                InstallDir = PathUtil.Sanitize((string?)regKeyGame.GetValue("InstallDir")) ?? string.Empty,
                Language = (string)regKeyGame.GetValue("Language", string.Empty)!,
            };

            game.GameName = Path.GetFileName(game.InstallDir) ?? string.Empty;
            game.InstallDate = PathUtil.GetCreationTime(game.InstallDir) ?? DateTime.MinValue;
            game.WorkingDir = game.InstallDir;
            game.LaunchString = $"uplay://launch/{game.GameId}";

            AddCatalogData(game, catalog);

            games.Add(game);
        }

        return games;
    }

    private static void AddCatalogData(UbisoftGame game, UbisoftCatalog? catalog = null)
    {
        if (catalog?.Catalog
            .Where(p => p.UplayId.ToString() == game.GameId)
            .FirstOrDefault((UbisoftCatalogItem?)null) is not { } catalogItem)
        {
            return;
        }

        // get executable, executable path and working dir
        List<UbisoftProductInformation.Executable>? exeList = null;

        exeList ??= catalogItem.GameInfo?.root?.start_game?.offline?.executables;
        exeList ??= catalogItem.GameInfo?.root?.start_game?.online?.executables;

        if (exeList is not null)
        {
            foreach (var exe in exeList
                .Where(p => !string.IsNullOrEmpty(p.path?.relative)))
            {
                game.ExecutablePath = PathUtil.Sanitize(Path.Combine(game.InstallDir, exe.path!.relative!))!;
                game.GameName = exe.shortcut_name ?? game.GameName;

                game.WorkingDir = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty;
                if (exe.working_directory?.register?.StartsWith("HKEY") == false)
                    game.WorkingDir = PathUtil.Sanitize(exe.working_directory.register)!;

                if (!PathUtil.IsExecutable(game.ExecutablePath))
                    continue;

                game.Executable = Path.GetFileName(game.ExecutablePath) ?? string.Empty;
                break;
            }
        }

        // get Game name
        string? tmpVal = catalogItem.GameInfo?.root?.name;
        if (!string.IsNullOrEmpty(tmpVal))
            tmpVal = GetLocalizedValue(catalogItem, tmpVal, tmpVal);

        if (tmpVal is "NAME" or "GAMENAME")
            tmpVal = null;

        if (string.IsNullOrEmpty(tmpVal))
            tmpVal = catalogItem.GameInfo?.root?.installer?.game_identifier;

        if (!string.IsNullOrEmpty(tmpVal))
            game.GameName = tmpVal;

        // get help URL
        tmpVal = catalogItem.GameInfo?.root?.help_url;
        if (!string.IsNullOrEmpty(tmpVal))
            game.HelpUrl = GetLocalizedValue(catalogItem, tmpVal, tmpVal);

        // get Facebook URL
        tmpVal = catalogItem.GameInfo?.root?.facebook_url;
        if (!string.IsNullOrEmpty(tmpVal))
            game.FacebookUrl = GetLocalizedValue(catalogItem, tmpVal, tmpVal);

        // get homepage URL
        tmpVal = catalogItem.GameInfo?.root?.homepage_url;
        if (!string.IsNullOrEmpty(tmpVal))
            game.HomepageUrl = GetLocalizedValue(catalogItem, tmpVal, tmpVal);

        // get forum URL
        tmpVal = catalogItem.GameInfo?.root?.forum_url;
        if (!string.IsNullOrEmpty(tmpVal))
            game.ForumUrl = GetLocalizedValue(catalogItem, tmpVal, tmpVal);
    }

    private static string GetLocalizedValue(UbisoftCatalogItem catalogItem, string name, string defaultValue)
    {
        try
        {
            var value = catalogItem.GameInfo?.localizations?.@default?[name];
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        catch { /* ignored */ }

        return defaultValue;
    }
}
