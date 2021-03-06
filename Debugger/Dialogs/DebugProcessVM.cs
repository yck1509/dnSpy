﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

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
using System.IO;
using System.Windows.Input;
using dndbg.Engine;
using dnSpy.Debugger.Properties;
using dnSpy.Shared.MVVM;

namespace dnSpy.Debugger.Dialogs {
	sealed class DebugProcessVM : ViewModelBase {
		public IPickFilename PickFilename {
			set { pickFilename = value; }
		}
		IPickFilename pickFilename;

		public IPickDirectory PickDirectory {
			set { pickDirectory = value; }
		}
		IPickDirectory pickDirectory;

		public ICommand PickFilenameCommand {
			get { return new RelayCommand(a => PickNewFilename()); }
		}

		public ICommand PickCurrentDirectoryCommand {
			get { return new RelayCommand(a => PickNewCurrentDirectory()); }
		}

		public static readonly EnumVM[] breakProcessTypeList = new EnumVM[(int)BreakProcessType.Last] {
			new EnumVM(BreakProcessType.None, dnSpy_Debugger_Resources.DbgBreak_Dont),
			new EnumVM(BreakProcessType.CreateProcess, dnSpy_Debugger_Resources.DbgBreak_CreateProcessEvent),
			new EnumVM(BreakProcessType.CreateAppDomain, dnSpy_Debugger_Resources.DbgBreak_FirstCreateAppDomainEvent),
			new EnumVM(BreakProcessType.LoadModule, dnSpy_Debugger_Resources.DbgBreak_FirstLoadModuleEvent),
			new EnumVM(BreakProcessType.LoadClass, dnSpy_Debugger_Resources.DbgBreak_FirstLoadClassEvent),
			new EnumVM(BreakProcessType.CreateThread, dnSpy_Debugger_Resources.DbgBreak_FirstCreateThreadEvent),
			new EnumVM(BreakProcessType.ExeLoadModule, dnSpy_Debugger_Resources.DbgBreak_ExeLoadModuleEvent),
			new EnumVM(BreakProcessType.ExeLoadClass, dnSpy_Debugger_Resources.DbgBreak_ExeFirstLoadClassEvent),
			new EnumVM(BreakProcessType.ModuleCctorOrEntryPoint, dnSpy_Debugger_Resources.DbgBreak_ModuleClassConstructorOrEntryPoint),
			new EnumVM(BreakProcessType.EntryPoint, dnSpy_Debugger_Resources.DbgBreak_EntryPoint),
		};
		public EnumListVM BreakProcessTypeVM {
			get { return breakProcessTypeVM; }
		}
		readonly EnumListVM breakProcessTypeVM = new EnumListVM(breakProcessTypeList);

		public BreakProcessType BreakProcessType {
			get { return (BreakProcessType)BreakProcessTypeVM.SelectedItem; }
			set { BreakProcessTypeVM.SelectedItem = value; }
		}

		public string Filename {
			get { return filename; }
			set {
				if (filename != value) {
					filename = value;
					OnPropertyChanged("Filename");
					HasErrorUpdated();
					var path = GetPath(filename);
					if (path != null)
						CurrentDirectory = path;
				}
			}
		}
		string filename;

		public string CommandLine {
			get { return commandLine; }
			set {
				if (commandLine != value) {
					commandLine = value;
					OnPropertyChanged("CommandLine");
				}
			}
		}
		string commandLine;

		public string CurrentDirectory {
			get { return currentDirectory; }
			set {
				if (currentDirectory != value) {
					currentDirectory = value;
					OnPropertyChanged("CurrentDirectory");
				}
			}
		}
		string currentDirectory;

		public DebugProcessVM() {
		}

		static string GetPath(string file) {
			try {
				return Path.GetDirectoryName(file);
			}
			catch {
			}
			return null;
		}

		void PickNewFilename() {
			if (pickFilename == null)
				throw new InvalidOperationException();

			var newFilename = pickFilename.GetFilename(Filename, "exe", PickFilenameConstants.DotNetExecutableFilter);
			if (newFilename == null)
				return;

			Filename = newFilename;
		}

		void PickNewCurrentDirectory() {
			if (pickDirectory == null)
				throw new InvalidOperationException();

			var newDir = pickDirectory.GetDirectory(currentDirectory);
			if (newDir == null)
				return;

			CurrentDirectory = newDir;
		}

		public DebugProcessVM Clone() {
			return CopyTo(new DebugProcessVM());
		}

		public DebugProcessVM CopyTo(DebugProcessVM other) {
			other.Filename = this.Filename;
			other.CommandLine = this.CommandLine;
			other.CurrentDirectory = this.CurrentDirectory;
			other.BreakProcessType = this.BreakProcessType;
			return other;
		}

		protected override string Verify(string columnName) {
			if (columnName == "Filename") {
				if (!File.Exists(filename)) {
					if (string.IsNullOrWhiteSpace(filename))
						return dnSpy_Debugger_Resources.Error_MissingFilename;
					return dnSpy_Debugger_Resources.Error_FileDoesNotExist;
				}
				return string.Empty;
			}

			return string.Empty;
		}

		public override bool HasError {
			get {
				if (!string.IsNullOrEmpty(Verify("Filename")))
					return true;

				return false;
			}
		}
	}
}
