﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Files;
using dnSpy.Contracts.Files.TreeView;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.Plugin;
using dnSpy.Contracts.ToolBars;
using dnSpy.Files.Tabs.Dialogs;
using dnSpy.Shared.UI.Menus;
using dnSpy.Shared.UI.ToolBars;
using Microsoft.Win32;

namespace dnSpy.Files.Tabs {
	[ExportAutoLoaded]
	sealed class OpenFileInit : IAutoLoaded {
		readonly IFileTreeView fileTreeView;
		readonly IAppWindow appWindow;

		[ImportingConstructor]
		OpenFileInit(IFileTreeView fileTreeView, IAppWindow appWindow) {
			this.fileTreeView = fileTreeView;
			this.appWindow = appWindow;
			appWindow.MainWindowCommands.Add(ApplicationCommands.Open, (s, e) => { Open(); e.Handled = true; }, (s, e) => e.CanExecute = true);
		}

		static readonly string DotNetAssemblyOrModuleFilter = ".NET Executables (*.exe, *.dll, *.netmodule, *.winmd)|*.exe;*.dll;*.netmodule;*.winmd|All files (*.*)|*.*";

		void Open() {
			var openDlg = new OpenFileDialog {
				Filter = DotNetAssemblyOrModuleFilter,
				RestoreDirectory = true,
				Multiselect = true,
			};
			if (openDlg.ShowDialog() != true)
				return;
			OpenFiles(fileTreeView, appWindow.MainWindow, openDlg.FileNames);
		}

		public static void OpenFiles(IFileTreeView fileTreeView, Window ownerWindow, IEnumerable<string> filenames) {
			var fileLoader = new FileLoader(fileTreeView.FileManager, ownerWindow);
			var loadedFiles = fileLoader.Load(filenames.Select(a => DnSpyFileInfo.CreateFile(a)));
			var file = loadedFiles.Length == 0 ? null : loadedFiles[loadedFiles.Length - 1];
			if (file != null) {
				Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
					var node = fileTreeView.FindNode(file);
					fileTreeView.TreeView.SelectItems(new IFileTreeNodeData[] { node });
				}));
			}
		}
	}

	[ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_FILE_GUID, Header = "_Open...", InputGestureText = "Ctrl+O", Icon = "Open", Group = MenuConstants.GROUP_APP_MENU_FILE_OPEN, Order = 0)]
	sealed class MenuFileOpenCommand : MenuItemCommand {
		public MenuFileOpenCommand()
			: base(ApplicationCommands.Open) {
		}
	}

	[ExportToolBarButton(OwnerGuid = ToolBarConstants.APP_TB_GUID, Icon = "Open", ToolTip = "Open (Ctrl+O)", Group = ToolBarConstants.GROUP_APP_TB_MAIN_OPEN, Order = 0)]
	sealed class ToolbarFileOpenCommand : ToolBarButtonCommand {
		public ToolbarFileOpenCommand()
			: base(ApplicationCommands.Open) {
		}
	}

	[ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_FILE_GUID, Header = "Open from _GAC...", Icon = "AssemblyListGAC", Group = MenuConstants.GROUP_APP_MENU_FILE_OPEN, Order = 10)]
	sealed class OpenFromGacCommand : MenuItemBase {
		readonly IFileTreeView fileTreeView;
		readonly IAppWindow appWindow;

		[ImportingConstructor]
		OpenFromGacCommand(IFileTreeView fileTreeView, IAppWindow appWindow) {
			this.fileTreeView = fileTreeView;
			this.appWindow = appWindow;
		}

		public override void Execute(IMenuItemContext context) {
			var win = new OpenFromGACDlg();
			const bool syntaxHighlight = true;
			var vm = new OpenFromGACVM(syntaxHighlight);
			win.DataContext = vm;
			win.Owner = appWindow.MainWindow;
			if (win.ShowDialog() != true)
				return;
			OpenFileInit.OpenFiles(fileTreeView, appWindow.MainWindow, win.SelectedItems.Select(a => a.Path));
		}
	}

	[ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_FILE_GUID, Header = "Open L_ist...", Icon = "AssemblyList", Group = MenuConstants.GROUP_APP_MENU_FILE_OPEN, Order = 20)]
	sealed class OpenListCommand : MenuItemBase {
		readonly IAppWindow appWindow;
		readonly IFileListLoader fileListLoader;
		readonly FileListManager fileListManager;
		readonly IMessageBoxManager messageBoxManager;
		readonly IFileManager fileManager;

		[ImportingConstructor]
		OpenListCommand(IAppWindow appWindow, IFileListLoader fileListLoader, FileListManager fileListManager, IMessageBoxManager messageBoxManager, IFileManager fileManager) {
			this.appWindow = appWindow;
			this.fileListLoader = fileListLoader;
			this.fileListManager = fileListManager;
			this.messageBoxManager = messageBoxManager;
			this.fileManager = fileManager;
		}

		public override bool IsEnabled(IMenuItemContext context) {
			return fileListLoader.CanLoad;
		}

		public override void Execute(IMenuItemContext context) {
			if (!fileListLoader.CanLoad)
				return;

			var win = new OpenFileListDlg();
			const bool syntaxHighlight = true;
			var vm = new OpenFileListVM(syntaxHighlight, fileListManager, labelMsg => messageBoxManager.Ask<string>(labelMsg, ownerWindow: win, verifier: s => string.IsNullOrEmpty(s) ? "Missing name" : string.Empty));
			win.DataContext = vm;
			win.Owner = appWindow.MainWindow;
			if (win.ShowDialog() != true)
				return;

			var flvm = win.SelectedItems.FirstOrDefault();
			var oldSelected = fileListManager.SelectedFileList;
			if (flvm != null) {
				fileListManager.Add(flvm.FileList);
				fileListManager.SelectedFileList = flvm.FileList;
			}

			vm.Save();

			if (flvm == null)
				return;
			var fileList = flvm.FileList;
			if (fileList == oldSelected)
				return;

			fileListLoader.Load(fileList, new FileLoader(fileManager, appWindow.MainWindow));
		}
	}

	[ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_FILE_GUID, Header = "_Reload All Assemblies", Group = MenuConstants.GROUP_APP_MENU_FILE_OPEN, Order = 30)]
	sealed class ReloadCommand : MenuItemBase {
		readonly IFileListLoader fileListLoader;

		[ImportingConstructor]
		ReloadCommand(IFileListLoader fileListLoader) {
			this.fileListLoader = fileListLoader;
		}

		public override bool IsEnabled(IMenuItemContext context) {
			return fileListLoader.CanReload;
		}

		public override void Execute(IMenuItemContext context) {
			fileListLoader.Reload();
		}
	}
}