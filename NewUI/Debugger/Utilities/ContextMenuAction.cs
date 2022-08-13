﻿using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mesen.Config;
using Mesen.Config.Shortcuts;
using Mesen.Controls;
using Mesen.Interop;
using Mesen.Localization;
using Mesen.Utilities;
using Mesen.ViewModels;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;

namespace Mesen.Debugger.Utilities
{
	public abstract class BaseMenuAction : ViewModelBase, IDisposable
	{
		public ActionType ActionType;
		public string? CustomText { get; set; }
		public Func<string>? DynamicText { get; set; }
		public Func<string>? DynamicIcon { get; set; }

		public virtual string Name
		{
			get
			{
				string label;
				if(DynamicText != null) {
					label = DynamicText();
				} else if(ActionType == ActionType.Custom) {
					label = CustomText ?? "";
				} else {
					label = ResourceHelper.GetEnumText(ActionType);
				}
				
				if(HintText != null) {
					string hint = HintText();
					if(!string.IsNullOrWhiteSpace(hint)) {
						label += " (" + hint + ")";
					}
				}
				return label;
			}
		}

		private string? _currentIcon = null;

		private string? GetIconFile()
		{
			IconFileAttribute? attr = ActionType.GetAttribute<IconFileAttribute>();
			if(!string.IsNullOrEmpty(attr?.Icon)) {
				return  attr.Icon;
			} else if(DynamicIcon != null) {
				return "Assets/" + DynamicIcon() + ".png";
			} else if(IsSelected?.Invoke() == true) {
				return ConfigManager.ActiveTheme == MesenTheme.Light ? "Assets/MenuItemChecked.png" : "Assets/MenuItemCheckedDark.png";
			}
			return null;
		}

		List<object>? _subActions;
		public List<object>? SubActions
		{
			get => _subActions;
			set
			{
				_subActions = value;

				if(_subActions != null) {
					Func<bool>? isEnabled = IsEnabled;

					IsEnabled = () => {
						if(isEnabled != null && !isEnabled()) {
							return false;
						}

						foreach(object subAction in _subActions) {
							if(subAction is BaseMenuAction act) {
								if(act.IsEnabled == null || act.IsEnabled()) {
									return true;
								}
							}
						}
						return false;
					};
				}
				this.RaiseAndSetIfChanged(ref _subActions, value);
			}
		}

		public Func<string>? HintText { get; set; }
		public Func<bool>? IsEnabled { get; set; }
		public Func<bool>? IsSelected { get; set; }
		public Func<bool>? IsVisible { get; set; }
		
		public bool AllowedWhenHidden { get; set; }
		public bool AlwaysShowLabel { get; set; }
		public RoutingStrategies RoutingStrategy { get; set; } = RoutingStrategies.Bubble;

		public abstract string ShortcutText { get; }

		[Reactive] public string ActionName { get; set; } = "";
		[Reactive] public Image? ActionIcon { get; set; }
		[Reactive] public bool Enabled { get; set; }
		[Reactive] public bool Visible { get; set; }

		private static ReactiveCommand<Unit, Unit> _emptyCommand = ReactiveCommand.Create(() => { });

		private ReactiveCommand<Unit, Unit>? _clickCommand;
		public ReactiveCommand<Unit, Unit>? ClickCommand
		{
			get
			{
				Update();
				return _clickCommand ?? ContextMenuAction._emptyCommand;
			}
		}

		private Action _onClick = () => { };
		public Action OnClick
		{
			get => _onClick;
			set
			{
				_onClick = () => {
					if((IsVisible == null || AllowedWhenHidden || IsVisible()) && (IsEnabled == null || IsEnabled())) {
						if(ActionType == ActionType.Exit) {
							//When using exit, the command is disposed while the command is running, which causes a crash
							//Run the code in a posted action to prevent the crash
							Dispatcher.UIThread.Post(() => { value(); });
						} else {
							try {
								value();
							} catch(Exception ex) {
								Dispatcher.UIThread.Post(() => MesenMsgBox.ShowException(ex));
							}
						}
					}
				};
				_clickCommand = ReactiveCommand.Create(_onClick);
			}
		}

		public void Update()
		{
			ActionName = Name;

			string? iconFile = GetIconFile();
			if(_currentIcon != iconFile) {
				if(iconFile != null) {
					ActionIcon = ImageUtilities.FromAsset(iconFile);
				} else {
					ActionIcon = null;
				}
				_currentIcon = iconFile;
			}

			Enabled = IsEnabled?.Invoke() ?? true;
			Visible = IsVisible?.Invoke() ?? true;
		}

		public virtual void Dispose()
		{
			_onClick = () => { };
			_clickCommand?.Dispose();
			_clickCommand = null;
			IsSelected = null;
			IsEnabled = null;
			IsVisible = null;
			if(_subActions != null) {
				foreach(object subAction in _subActions) {
					if(subAction is BaseMenuAction action) {
						action.Dispose();
					}
				}
			}
		}
	}

	public class MainMenuAction : BaseMenuAction
	{
		public EmulatorShortcut? Shortcut { get; set; }
		public uint ShortcutParam { get; set; }

		public Func<string>? CustomShortcutText { get; set; }

		public MainMenuAction()
		{
		}

		public MainMenuAction(EmulatorShortcut? shortcut)
		{
			if(shortcut.HasValue) {
				Shortcut = shortcut.Value;

				IsEnabled = () => EmuApi.IsShortcutAllowed(shortcut.Value, ShortcutParam);

				OnClick = () => {
					//Run outside the UI thread to avoid deadlocks, etc.
					Task.Run(() => {
						EmuApi.ExecuteShortcut(new ExecuteShortcutParams() { Shortcut = shortcut.Value, Param = ShortcutParam });
					});
				};
			}
		}

		public override string ShortcutText
		{
			get
			{
				if(CustomShortcutText != null) {
					return CustomShortcutText();
				} else {
					return Shortcut.HasValue ? Shortcut.Value.GetShortcutKeys()?.ToString() ?? "" : "";
				}
			}
		}

	}

	public class ContextMenuAction : BaseMenuAction
	{
		public Func<DbgShortKeys>? Shortcut { get; set; }
		public override string ShortcutText => Shortcut?.Invoke().ToString() ?? "";

		public override void Dispose()
		{
			base.Dispose();
			Shortcut = null;
		}
	}

	public class ContextMenuSeparator : ContextMenuAction
	{
		public override string Name => "-";
	}

	public enum ActionType
	{
		Custom,

		[IconFile("Copy")]
		Copy,

		[IconFile("Paste")]
		Paste,

		[IconFile("SelectAll")]
		SelectAll,

		[IconFile("EditLabel")]
		EditLabel,

		[IconFile("Add")]
		AddWatch,

		[IconFile("BreakpointEnableDisable")]
		EditBreakpoint,

		MarkSelectionAs,

		[IconFile("Accept")]
		MarkAsCode,

		[IconFile("CheatCode")]
		MarkAsData,
		
		[IconFile("Help")]
		MarkAsUnidentified,

		[IconFile("Add")]
		Add,

		[IconFile("Edit")]
		Edit,

		[IconFile("Close")]
		Delete,

		[IconFile("Breakpoint")]
		AddBreakpoint,
		[IconFile("Breakpoint")]
		EditTilemapBreakpoint,
		[IconFile("Breakpoint")]
		EditAttributeBreakpoint,

		[IconFile("MoveUp")]
		MoveUp,

		[IconFile("MoveDown")]
		MoveDown,

		WatchDecimalDisplay,
		WatchHexDisplay,
		WatchBinaryDisplay,
		
		RowDisplayFormat,
		RowFormatBinary,
		RowFormatHex8Bits,
		RowFormatHex16Bits,
		RowFormatHex24Bits,
		RowFormatSigned8Bits,
		RowFormatSigned16Bits,
		RowFormatSigned24Bits,
		RowFormatUnsigned,

		[IconFile("Close")]
		ClearFormat,

		[IconFile("Import")]
		Import,

		[IconFile("Export")]
		Export,

		ShowConsoleStatus,
		ShowBreakpointList,
		ShowWatchList,
		ShowFunctionList,
		ShowLabelList,
		ShowCallStack,
		ShowControllers,

		ShowSettingsPanel,
		ShowMemoryMappings,

		[IconFile("Settings")]
		Preferences,

		[IconFile("MediaPlay")]
		Continue,

		[IconFile("MediaPause")]
		Break,

		[IconFile("StepInto")]
		StepInto,
		
		[IconFile("StepOver")]
		StepOver,
		
		[IconFile("StepOut")]
		StepOut,

		[IconFile("StepBack")]
		StepBack,

		[IconFile("RunCpuCycle")]
		RunCpuCycle,

		[IconFile("RunPpuCycle")]
		RunPpuCycle,

		[IconFile("RunPpuScanline")]
		RunPpuScanline,

		[IconFile("RunPpuFrame")]
		RunPpuFrame,

		[IconFile("StepNmi")]
		RunToNmi,

		[IconFile("StepIrq")]
		RunToIrq,

		BreakIn,
		BreakOn,

		[IconFile("Refresh")]
		Reset,

		[IconFile("PowerCycle")]
		PowerCycle,

		[IconFile("Script")]
		NewScript,

		[IconFile("MediaPlay")]
		RunScript,

		[IconFile("MediaStop")]
		StopScript,

		[IconFile("LogWindow")]
		BuiltInScripts,

		[IconFile("Folder")]
		Open,

		[IconFile("SaveFloppy")]
		Save,
		
		SaveAs,

		[IconFile("Exit")]
		Exit,

		[IconFile("Help")]
		HelpApiReference,

		RecentScripts,

		[IconFile("Refresh")]
		Refresh,
		EnableAutoRefresh,
		RefreshOnBreakPause,
		
		ZoomIn,
		ZoomOut,

		[IconFile("Expand")]
		GoToLocation,

		[IconFile("Export")]
		ExportToPng,

		[IconFile("VerticalLayout")]
		ViewInTileViewer,

		[IconFile("CheatCode")]
		ViewInMemoryViewer,
		
		LoadTblFile,
		ResetTblMappings,

		GoTo,
		GoToAddress,
		GoToProgramCounter,
		GoToAll,

		[IconFile("Debugger")]
		OpenDebugger,

		[IconFile("EventViewer")]
		OpenEventViewer,

		[IconFile("CheatCode")]
		OpenMemoryTools,

		[IconFile("RegisterIcon")]
		OpenRegisterViewer,

		[IconFile("LogWindow")]
		OpenTraceLogger,
		
		[IconFile("Find")]
		OpenMemorySearch,

		[IconFile("VideoOptions")]
		OpenTilemapViewer,

		[IconFile("VerticalLayout")]
		OpenTileViewer,

		[IconFile("PerfTracker")]
		OpenSpriteViewer,

		[IconFile("VideoFilter")]
		OpenPaletteViewer,

		[IconFile("Chip")]
		OpenAssembler,
		
		[IconFile("LogWindow")]
		OpenDebugLog,

		[IconFile("Speed")]
		OpenProfiler,

		[IconFile("Script")]
		OpenScriptWindow,

		[IconFile("Find")]
		OpenWatchWindow,

		[IconFile("Edit")]
		OpenNesHeaderEditor,

		[IconFile("Settings")]
		OpenDebugSettings,

		[IconFile("SpcDebugger")]
		OpenSpcDebugger,
		[IconFile("Cx4Debugger")]
		OpenCx4Debugger,
		[IconFile("NecDspDebugger")]
		OpenNecDspDebugger,
		[IconFile("GsuDebugger")]
		OpenGsuDebugger,
		[IconFile("Sa1Debugger")]
		OpenSa1Debugger,
		[IconFile("GameboyDebugger")]
		OpenGameboyDebugger,

		[IconFile("CheatCode")]
		Cheats,
		[IconFile("HistoryViewer")]
		HistoryViewer,
		[IconFile("Movie")]
		Movies,
		[IconFile("MediaPlay")]
		Play,
		[IconFile("Record")]
		Record,
		[IconFile("MediaStop")]
		Stop,
		
		[IconFile("Network")]
		NetPlay,
		Connect,
		Disconnect,
		StartServer,
		StopServer,
		SelectController,

		[IconFile("Microphone")]
		SoundRecorder,
		[IconFile("VideoRecorder")]
		VideoRecorder,

		[IconFile("HdPack")]
		HdPacks,
		[IconFile("Import")]
		InstallHdPack,
		[IconFile("HdPack")]
		HdPackBuilder,

		[IconFile("LogWindow")]
		LogWindow,

		[IconFile("Camera")]
		TakeScreenshot,

		[IconFile("Help")]
		OnlineHelp,
		[IconFile("Update")]
		CheckForUpdates,
		[IconFile("Exclamation")]
		About,
		[IconFile("Comment")]
		ReportBug,

		[IconFile("Speed")]
		Speed,
		NormalSpeed,
		DoubleSpeed,
		TripleSpeed,
		MaximumSpeed,
		HalfSpeed,
		QuarterSpeed,
		IncreaseSpeed,
		DecreaseSpeed,
		ShowFps,

		[IconFile("Fullscreen")]
		VideoScale,
		Fullscreen,
		
		[IconFile("VideoFilter")]
		VideoFilter,
		[IconFile("WebBrowser")]
		Region,

		[IconFile("Audio")]
		Audio,
		[IconFile("HammerScrewdriver")]
		Emulation,
		[IconFile("Controller")]
		Input,
		[IconFile("VideoOptions")]
		Video,
		[IconFile("NesIcon")]
		Nes,
		[IconFile("SnesIcon")]
		Snes,
		[IconFile("GameboyIcon")]
		Gameboy,
		[IconFile("PceIcon")]
		PcEngine,
		[IconFile("MediaPause")]
		Pause,
		[IconFile("MediaStop")]
		PowerOff,
		[IconFile("MediaPlay")]
		Resume,

		[IconFile("SaveFloppy")]
		SelectDisk,
		[IconFile("MediaEject")]
		EjectDisk,

		[IconFile("Coins")]
		InsertCoin1,
		[IconFile("Coins")]
		InsertCoin2,
		[IconFile("Coins")]
		InsertCoin3,
		[IconFile("Coins")]
		InsertCoin4,
		
		SaveState,
		LoadState,
		[IconFile("SplitView")]
		SaveStateDialog,
		[IconFile("SaveFloppy")]
		SaveStateToFile,
		[IconFile("SplitView")]
		LoadStateDialog,
		[IconFile("Folder")]
		LoadStateFromFile,
		
		RecentFiles,
		LoadLastSession,

		[IconFile("SaveFloppy")]
		SaveRomAs,
		[IconFile("CheatCode")]
		SaveEditsAsIps,

		[IconFile("Breakpoint")]
		ToggleBreakpoint,

		[IconFile("Edit")]
		EditSelectedCode,

		[IconFile("Debugger")]
		ViewInDebugger,

		[IconFile("Breakpoint")]
		SetBreakpoint,
		
		[IconFile("Close")]
		RemoveBreakpoint,
		
		[IconFile("BreakpointEnableDisable")]
		EnableBreakpoint,

		[IconFile("BreakpointEnableDisable")]
		DisableBreakpoint,

		[IconFile("Edit")]
		CodeWindowEditBreakpoint,
		
		CodeDataLogger,
		[IconFile("Refresh")]
		ResetCdl,
		[IconFile("Folder")]
		LoadCdl,
		[IconFile("SaveFloppy")]
		SaveCdl,

		MoveProgramCounter,
		ShowToolbar,

		[IconFile("Refresh")]
		ResetWorkspace,
		[IconFile("TabContent")]
		Workspace,
		
		[IconFile("Import")]
		ImportLabels,
		[IconFile("Export")]
		ExportLabels,
		
		[IconFile("Import")]
		ImportWatchEntries,
		[IconFile("Export")]
		ExportWatchEntries,

		[IconFile("Barcode")]
		InputBarcode,

		[IconFile("Tape")]
		TapeRecorder,

		GenerateRom,
		CdlRomStripUnused,
		CdlRomStripUsed,

		[IconFile("Find")]
		Find,
		[IconFile("NextArrow")]
		FindNext,
		[IconFile("PreviousArrow")]
		FindPrev,
		[IconFile("Find")]
		FindOccurrences,

		[IconFile("Edit")]
		EditTile,
		[IconFile("Edit")]
		EditTiles,
		[IconFile("Edit")]
		EditSprite,
		[IconFile("Edit")]
		EditColor,

		NavigateTo,

		[IconFile("PrevCode")]
		GoToPrevCode,
		[IconFile("NextCode")]
		GoToNextCode,

		[IconFile("PrevData")]
		GoToPrevData,
		[IconFile("NextData")]
		GoToNextData,

		[IconFile("PrevUnknown")]
		GoToPrevUnknown,
		[IconFile("NextUnknown")]
		GoToNextUnknown,

		[IconFile("PrevRead")]
		GoToPrevRead,
		[IconFile("NextRead")]
		GoToNextRead,

		[IconFile("PrevWrite")]
		GoToPrevWrite,
		[IconFile("NextWrite")]
		GoToNextWrite,

		[IconFile("PrevExec")]
		GoToPrevExec,
		[IconFile("NextExec")]
		GoToNextExec,

		[IconFile("Export")]
		ExportMovie,
		[IconFile("SaveFloppy")]
		CreateSaveState,
		[IconFile("MediaPlay")]
		ResumeGameplay,
	}
}
