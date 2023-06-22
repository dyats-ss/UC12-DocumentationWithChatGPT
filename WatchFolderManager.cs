#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2023 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareX
{
    /// <summary>
    /// Manages a collection of watched folders. Provides functionality to add, remove, 
    /// and update the folders in the watch list, and to control their active state.
    /// </summary>
    public class WatchFolderManager : IDisposable
    {
        /// <summary>
        /// List of folders currently being watched.
        /// </summary>
        public List<WatchFolder> WatchFolders { get; private set; }

        /// <summary>
        /// Updates the current list of watched folders. Any existing folders in the list 
        /// are unregistered before the list is refreshed.
        /// </summary>
        public void UpdateWatchFolders()
        {
            if (WatchFolders != null)
            {
                UnregisterAllWatchFolders();
            }

            WatchFolders = new List<WatchFolder>();

            foreach (WatchFolderSettings defaultWatchFolderSetting in Program.DefaultTaskSettings.WatchFolderList)
            {
                AddWatchFolder(defaultWatchFolderSetting, Program.DefaultTaskSettings);
            }

            foreach (HotkeySettings hotkeySetting in Program.HotkeysConfig.Hotkeys)
            {
                foreach (WatchFolderSettings watchFolderSetting in hotkeySetting.TaskSettings.WatchFolderList)
                {
                    AddWatchFolder(watchFolderSetting, hotkeySetting.TaskSettings);
                }
            }
        }

        /// <summary>
        /// Finds a watched folder in the list that matches the provided settings. 
        /// </summary>
        /// <param name="watchFolderSetting">The settings of the watched folder to find.</param>
        /// <returns>The watched folder if it exists, otherwise null.</returns>
        private WatchFolder FindWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            return WatchFolders.FirstOrDefault(watchFolder => watchFolder.Settings == watchFolderSetting);
        }

        /// <summary>
        /// Checks if a folder with the provided settings exists in the watched folders list.
        /// </summary>
        /// <param name="watchFolderSetting">The settings of the watched folder to find.</param>
        /// <returns>True if a watched folder with the given settings exists in the list, otherwise false.</returns>
        private bool IsExist(WatchFolderSettings watchFolderSetting)
        {
            return FindWatchFolder(watchFolderSetting) != null;
        }

        /// <summary>
        /// Adds a new watched folder to the list. If a folder with the same settings 
        /// already exists in the list, this method will not add a duplicate. If the 
        /// watched folder list of the provided task settings does not contain the 
        /// new watched folder setting, it will be added. 
        /// </summary>
        /// <param name="watchFolderSetting">The settings for the new watched folder.</param>
        /// <param name="taskSettings">The task settings for the new watched folder. 
        /// These settings will be copied to ensure the original settings are not 
        /// unintentionally modified by the watched folder.</param>
        public void AddWatchFolder(WatchFolderSettings watchFolderSetting, TaskSettings taskSettings)
        {
            if (!IsExist(watchFolderSetting))
            {
                if (!taskSettings.WatchFolderList.Contains(watchFolderSetting))
                {
                    taskSettings.WatchFolderList.Add(watchFolderSetting);
                }

                WatchFolder watchFolder = new WatchFolder();
                watchFolder.Settings = watchFolderSetting;
                watchFolder.TaskSettings = taskSettings;

                watchFolder.FileWatcherTrigger += origPath =>
                {
                    TaskSettings taskSettingsCopy = TaskSettings.GetSafeTaskSettings(taskSettings);
                    string destPath = origPath;

                    if (watchFolderSetting.MoveFilesToScreenshotsFolder)
                    {
                        string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettingsCopy);
                        string fileName = Path.GetFileName(origPath);
                        destPath = TaskHelpers.HandleExistsFile(screenshotsFolder, fileName, taskSettingsCopy);
                        FileHelpers.CreateDirectoryFromFilePath(destPath);
                        File.Move(origPath, destPath);
                    }

                    UploadManager.UploadFile(destPath, taskSettingsCopy);
                };

                WatchFolders.Add(watchFolder);

                if (taskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
            }
        }

        /// <summary>
        /// Removes a watched folder from the list. If the folder exists in the list, 
        /// it will also be removed from the watched folder list of the associated task settings.
        /// </summary>
        /// <param name="watchFolderSetting">The settings of the watched folder to remove.</param>
        public void RemoveWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            using (WatchFolder watchFolder = FindWatchFolder(watchFolderSetting))
            {
                if (watchFolder != null)
                {
                    watchFolder.TaskSettings.WatchFolderList.Remove(watchFolderSetting);
                    WatchFolders.Remove(watchFolder);
                }
            }
        }

        /// <summary>
        /// Updates the active state of a watched folder to match its associated task settings. 
        /// If the task settings indicate the folder should be active, this method will enable it; 
        /// otherwise, it will be disposed.
        /// </summary>
        /// <param name="watchFolderSetting">The settings of the watched folder to update.</param>
        public void UpdateWatchFolderState(WatchFolderSettings watchFolderSetting)
        {
            WatchFolder watchFolder = FindWatchFolder(watchFolderSetting);
            if (watchFolder != null)
            {
                if (watchFolder.TaskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
                else
                {
                    watchFolder.Dispose();
                }
            }
        }

        /// <summary>
        /// Unregisters all folders in the watched folders list.
        /// If the list is null or empty, the method does nothing.
        /// </summary>
        public void UnregisterAllWatchFolders()
        {
            if (WatchFolders != null)
            {
                foreach (WatchFolder watchFolder in WatchFolders)
                {
                    if (watchFolder != null)
                    {
                        watchFolder.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the WatchFolderManager, which includes unregistering all watched folders.
        /// </summary>
        public void Dispose()
        {
            UnregisterAllWatchFolders();
        }
    }
}
