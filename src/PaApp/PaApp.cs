// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2005, SIL International. All Rights Reserved.   
// <copyright from='2005' to='2005' company='SIL International'>
//		Copyright (c) 2005, SIL International. All Rights Reserved.   
//    
//		Distributable under the terms of either the Common Public License or the
//		GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright> 
#endregion
// 
// File: Pacs
// Responsibility: DavidO
// 
// <remarks>
// </remarks>
// ---------------------------------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Xml;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SIL.Pa.Data;
using SIL.SpeechTools.Utils;
using SIL.FieldWorks.Common.UIAdapters;
using SIL.Pa.Resources;
using SIL.Pa.FFSearchEngine;
using XCore;

namespace SIL.Pa
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// 
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public interface ITabView
	{
		Control DockableContainer { get;}
		void ViewDocked();
		void ViewUndocking();
		ToolStripProgressBar ProgressBar { get;}
		ToolStripStatusLabel ProgressBarLabel { get;}
		ToolStripStatusLabel StatusBarLabel { get;}
		StatusStrip StatusBar { get;}
		ToolTip ViewsToolTip { get;}
		ITMAdapter TMAdapter { get;}
	}

	/// ------------------------------------------------------------------------------------
	/// <summary>
	/// 
	/// </summary>
	/// ------------------------------------------------------------------------------------
	public static class PaApp
	{
		public enum FeatureType
		{
			Articulatory,
			Binary
		}
		
		public static string kOpenClassBracket = ResourceHelper.GetString("kstidOpenClassSymbol");
		public static string kCloseClassBracket = ResourceHelper.GetString("kstidCloseClassSymbol");
		public const string kOptionsSettingsKey = "globaloptions";
		public const string kHelpFileName = "Phonology_Assistant_Help.chm";
		public const string kHelpSubFolder = "Helps";

		private static string s_helpFilePath = null;
		private static string s_settingsFile;
		private static PaSettingsHandler s_settingsHndlr;
		private static Mediator s_msgMediator;
		private static ITMAdapter s_tmAdapter;
		private static ToolStripStatusLabel s_statusBarLabel;
		private static ToolStripProgressBar s_progressBar;
		private static ToolStripStatusLabel s_progressBarLabel;
		private static ToolStripProgressBar s_savProgressBar;
		private static ToolStripStatusLabel s_savProgressBarLabel;
		private static bool s_statusBarHasBeenInitialized = false;
		private static Form s_mainForm;
		private static Form s_currentView;
		private static Type s_currentViewType;
		private static bool s_projectLoadInProcess = false;
		private static PaProject s_project;
		private static RecordCache s_recCache;
		private static WordCache s_wordCache;
		private static PhoneCache s_phoneCache;
		private static bool	s_phoneCacheBuilt = false;
		private static int s_phoneCacheIndex = 0;
		private static PaFieldInfoList s_fieldInfo;
		private static ISplashScreen s_splashScreen;
		private static Dictionary<Type, Form> s_openForms = new Dictionary<Type, Form>();
		private static string s_defaultProjFolder;
		private static List<ITMAdapter> s_defaultMenuAdapters;
		private static UndefinedCodePointInfoList s_undefinedCodepoints;

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// --------------------------------------------------------------------------------
		static PaApp()
		{
			if (Application.ExecutablePath.ToLower().EndsWith("pa.exe"))
			{
				s_splashScreen = new SplashScreen(true, true);
				s_splashScreen.Show();
				s_splashScreen.Message = Properties.Resources.kstidSplashScreenLoadingMsg;
			}

			InitializePaRegKey();
			s_msgMediator = new Mediator();
			s_settingsFile = Path.Combine(s_defaultProjFolder, "pa.xml");
			s_settingsHndlr = new PaSettingsHandler(s_settingsFile);

			// Create the master set of PA fields. When a project is opened, any
			// custom fields belonging to the project will be added to this list.
			s_fieldInfo = PaFieldInfoList.DefaultFieldInfoList;

			// If there's a setting in the settings file for the time to wait for SQL server
			// to start, then use that value rather than the default of 15 seconds.
			FwDBUtils.SecondsToWaitForSQLToStart = s_settingsHndlr.GetIntSettingsValue(
				"sqlserverwaittime", "seconds", FwDBUtils.SecondsToWaitForSQLToStart);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void InitializePaRegKey()
		{
			// Construct the default project path.
			string projPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			projPath = Path.Combine(projPath, @"SIL Software\Phonology Assistant");

			// Check if an entry in the registry specifies the path. If not, create it.
			string keyName = @"Software\SIL\Phonology Assistant";
			RegistryKey key = Registry.CurrentUser.CreateSubKey(keyName);

			if (key != null)
			{
				string tmpProjPath = key.GetValue("DefaultProjectsLocation") as string;

				// If the registry value was not found, then create it.
				if (string.IsNullOrEmpty(tmpProjPath))
					key.SetValue("DefaultProjectsLocation", projPath);
				else
					projPath = tmpProjPath;

				key.Close();
			}

			s_defaultProjFolder = projPath;

			// Create the folder if it doesn't exist.
			if (!Directory.Exists(projPath))
				Directory.CreateDirectory(projPath);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// The normal DesignMode property doesn't work when derived classes are loaded in
		/// designer.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool DesignMode
		{
			get { return System.Diagnostics.Process.GetCurrentProcess().ProcessName == "devenv"; }
		}

		#region Cache related properties and methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static RecordCache RecordCache
		{
			get { return s_recCache; }
			set { s_recCache = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static WordCache WordCache
		{
			get { return s_wordCache; }
			set
			{
				if (value != null && s_wordCache != value)
				{
					s_phoneCacheBuilt = false;
					s_phoneCacheIndex = 0;
					s_wordCache = value;
					s_phoneCache = new PhoneCache();
					BuildPhoneCache(true);
					Application.Idle += new EventHandler(Application_Idle);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Progressively build the phone cache during idle cycles.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void Application_Idle(object sender, EventArgs e)
		{
			BuildPhoneCache(true);
			if (s_phoneCacheBuilt)
				Application.Idle -= Application_Idle;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Builds the cache of phones from the data corpus. When interuptable is true, it
		/// means the cache is being built when the program is idle. When the cache is
		/// accessed the first time and it hasn't been fully built, this method will be called
		/// and not interuptable in order to force completion of cache building.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void BuildPhoneCache(bool interuptable)
		{
			if (s_phoneCacheBuilt)
				return;

			int interuptCount = 0;

			while (s_phoneCacheIndex < s_wordCache.Count)
			{
				string[] phones = s_wordCache[s_phoneCacheIndex].Phones;
				if (phones != null)
				{
					for (int i = 0; i < phones.Length; i++)
					{
						// Don't bother adding break characters.
						if (IPACharCache.kBreakChars.Contains(phones[i]))
							continue;

						if (!s_phoneCache.ContainsKey(phones[i]))
							s_phoneCache.AddPhone(phones[i]);

						// Determine if the current phone is the primary
						// phone in an uncertain group.
						bool isPrimaryUncertainPhone =
							s_wordCache[s_phoneCacheIndex].ContiansUncertainties &&
							s_wordCache[s_phoneCacheIndex].UncertainPhones.ContainsKey(i);

						// When the phone is the primary phone in an uncertain group, we
						// don't add it to the total count but to the counter that keeps
						// track of the primary	uncertain phones. Then we also add to the
						// cache the non primary uncertain phones.
						if (!isPrimaryUncertainPhone)
							s_phoneCache[phones[i]].TotalCount++;
						else
						{
							s_phoneCache[phones[i]].CountAsPrimaryUncertainty++;

							// Go through the uncertain phones and add them to the cache.
							if (s_wordCache[s_phoneCacheIndex].ContiansUncertainties)
							{
								AddUncertainPhonesToCache(s_wordCache[s_phoneCacheIndex].UncertainPhones);
								UpdateSiblingUncertaintys(s_wordCache[s_phoneCacheIndex].UncertainPhones);
							}
						}
					}
				}

				s_phoneCacheIndex++;

				// Process 20 words, then leave if the process is
				// being performed during idle cycles.
				if (interuptable && ++interuptCount == 20)
					return;
			}

			s_phoneCacheBuilt = true;
			if (PhoneCache.FeatureOverrides != null)
				PhoneCache.FeatureOverrides.MergeWithPhoneCache(s_phoneCache);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds the specified list of uncertain phones to the phone cache. It is assumed the
		/// first (i.e. primary) phone in the list has already been added to the cache and,
		/// therefore, it will not be added nor its count incremented.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void AddUncertainPhonesToCache(Dictionary<int, string[]> uncertainPhones)
		{
			// Go through the uncertain phone groups, skipping the
			// primary one in each group since that was already added
			// to the cache above.
			foreach (string[] uPhones in uncertainPhones.Values)
			{
				for (int i = 1; i < uPhones.Length; i++)
				{
					// Don't bother adding break characters.
					if (!IPACharCache.kBreakChars.Contains(uPhones[i]))
					{
						if (!s_phoneCache.ContainsKey(uPhones[i]))
							s_phoneCache.AddPhone(uPhones[i]);

						s_phoneCache[uPhones[i]].CountAsNonPrimaryUncertainty++;
					}
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Updates a uncertain phone sibling lists for each phone in each uncertain group for
		/// the specified uncertain groups.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private static void UpdateSiblingUncertaintys(Dictionary<int, string[]> uncertainPhones)
		{
			// Go through the uncertain phone groups
			foreach (string[] uPhones in uncertainPhones.Values)
			{
				// Go through the uncertain phones in this group.
				for (int i = 0; i < uPhones.Length; i++)
				{
					IPhoneInfo phoneUpdating;
					
					// TODO: Log an error that the phone isn't found in the the cache
					// Get the cache entry for the phone whose sibling list will be updated.
					if (!s_phoneCache.TryGetValue(uPhones[i], out phoneUpdating))
						continue;

					// Go through the sibling phones, adding them to
					// the updated phones sibling list.
					for (int j = 0; j < uPhones.Length; j++)
					{
						// Add the phone pointed to by j if it's not the phone whose
						// cache entry we're updating and if it's not a phone already
						// in the sibling list of the cache entry we're updating.
						if (j != i && !phoneUpdating.SiblingUncertainties.Contains(uPhones[j]))
							phoneUpdating.SiblingUncertainties.Add(uPhones[j]);
					}
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the cache of phones in the current project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static PhoneCache PhoneCache
		{
			get
			{
				if (!s_phoneCacheBuilt)
					BuildPhoneCache(false);

				return s_phoneCache;
			}
		}

		#endregion

		private static List<IxCoreColleague> s_colleagueList = new List<IxCoreColleague>();

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void AddMediatorColleague(IxCoreColleague colleague)
		{
			if (colleague != null && !DesignMode)
			{
				s_msgMediator.AddColleague(colleague);

				if (!s_colleagueList.Contains(colleague))
					s_colleagueList.Add(colleague);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void RemoveMediatorColleague(IxCoreColleague colleague)
		{
			if (colleague != null && !DesignMode)
			{
				try
				{
					s_msgMediator.RemoveColleague(colleague);
				}
				catch { }

				if (s_colleagueList.Contains(colleague))
					s_colleagueList.Remove(colleague);
			}
		}

		#region Misc. Properties
		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Gets the XCore message mediator for the application.
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static Mediator MsgMediator
		{
			get { return s_msgMediator; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the list of code points found in the data that could not be found
		/// in the IPA character cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static UndefinedCodePointInfoList UndefinedCodepoints
		{
			get { return s_undefinedCodepoints; }
			set { s_undefinedCodepoints = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the default location for PA projects.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string DefaultProjectFolder
		{
			get { return s_defaultProjFolder; }
			set { s_defaultProjFolder = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the application's splash screen.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ISplashScreen SplashScreen
		{
			get { return PaApp.s_splashScreen; }
			set { PaApp.s_splashScreen = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the currently opened PA project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static PaProject Project
		{
			get { return s_project; }
			set { s_project = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets a value indicating whether or not a project is being loaded.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool ProjectLoadInProcess
		{
			get { return PaApp.s_projectLoadInProcess; }
			set { PaApp.s_projectLoadInProcess = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static PaFieldInfoList FieldInfo
		{
			get
			{
				if (s_fieldInfo == null)
					s_fieldInfo = PaFieldInfoList.DefaultFieldInfoList;

				return s_fieldInfo;
			}
			set { s_fieldInfo = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the toolbar menu adapter PaMainWnd. This value should only be set
		/// by the PaMainWnd class.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITMAdapter TMAdapter
		{
			get {return s_tmAdapter;}
			set {s_tmAdapter = value;}
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path and filename of the XML file that stores the application's
		/// settings.
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static string SettingsFile
		{
			get	{return s_settingsFile;}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static PaSettingsHandler SettingsHandler
		{
			get {return s_settingsHndlr;}
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path and filename of the application's help file.
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static string HelpFile
		{
			get {return null;}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the application's main form.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Form MainForm
		{
			get { return s_mainForm; }
			set { s_mainForm = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the application's current view form. When the view is docked, then
		/// this form will not be visible.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Form CurrentView
		{
			get { return s_currentView; }
			set { s_currentView = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the application's current view type.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Type CurrentViewType
		{
			get { return s_currentViewType; }
			set { s_currentViewType = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the main status bar label on PaMainWnd.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ToolStripStatusLabel StatusBarLabel
		{
			get
			{
				if (s_currentView != null && s_currentView.Visible && s_currentView is ITabView &&
					((ITabView)s_currentView).StatusBarLabel != null)
				{
					return ((ITabView)s_currentView).StatusBarLabel;
				}

				return s_statusBarLabel;
			}
			set { s_statusBarLabel = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the progress bar on the PaMainWnd.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ToolStripProgressBar ProgressBar
		{
			get
			{
				if (s_currentView != null && s_currentView.Visible && s_currentView is ITabView &&
					((ITabView)s_currentView).ProgressBar != null)
				{
					return ((ITabView)s_currentView).ProgressBar;
				}
			
				return s_progressBar;
			}
			set { s_progressBar = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the progress bar's label on the PaMainWnd.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ToolStripStatusLabel ProgressBarLabel
		{
			get
			{
				if (s_currentView != null && s_currentView.Visible && s_currentView is ITabView &&
					((ITabView)s_currentView).ProgressBarLabel != null)
				{
					return ((ITabView)s_currentView).ProgressBarLabel;
				}

				return s_progressBarLabel;
			}
			set	{s_progressBarLabel = value;}
		}

		#endregion

		#region Options Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets an Options value indicating whether or not class names are shown in
		/// search patterns and nested class definitions. If this value is false, then class
		/// members are shown instead.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool ShowClassNames
		{
			get
			{
				return PaApp.SettingsHandler.GetBoolSettingsValue(
					kOptionsSettingsKey, "showclassname", true);
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "showclassname", value);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets a value indicating whether or not to display the diamond
		/// pattern when the find phones search pattern text box is empty.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool ShowEmptyDiamondSearchPattern
		{
			get
			{
				return PaApp.SettingsHandler.GetBoolSettingsValue(
					kOptionsSettingsKey, "showemptydiamondpattern", true);
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "showemptydiamondpattern", value);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the number of queries to remember in the recently used queries list
		/// in the search window's side panel.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static int NumberOfRecentlyUsedQueries
		{
			get
			{
				return PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "numberofrecentlyusedqueries", 20);
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "numberofrecentlyusedqueries", value);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the color of the record view's field labels.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color RecordViewFieldLabelColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "recordviewfieldlabelcolor", -1);

				return (colorValue == -1 ? Color.DarkRed : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "recordviewfieldlabelcolor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color UncertainPhoneForeColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "uncertainphoneforecolor", -1);

				return (colorValue == -1 ? Color.RoyalBlue : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "uncertainphoneforecolor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color QuerySearchItemForeColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "querysearchitemforecolor", -1);

				return (colorValue == -1 ? Color.Black : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "querysearchitemforecolor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color QuerySearchItemBackColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "querysearchitembackcolor", -1);

				return (colorValue == -1 ? Color.Yellow : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "querysearchitembackcolor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the background color for result cells in an XY grid whose value is
		/// zero.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color XYChartZeroBackColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "xychartzerobackColor", -1);

				return (colorValue == -1 ? Color.PaleGoldenrod : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "xychartzerobackColor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the foreground color for result cells in an XY grid whose value is
		/// zero.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color XYChartZeroForeColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "xychartzeroforeColor", -1);

				return (colorValue == -1 ? Color.Black : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "xychartzeroforeColor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the background color for result cells in an XY grid whose value is
		/// greater than zero.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color XYChartNonZeroBackColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "xychartnonzerobackColor", -1);

				return (colorValue == -1 ? Color.LightSteelBlue : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "xychartnonzerobackColor", value.ToArgb());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the foreground color for result cells in an XY grid whose value is
		/// greater than zero.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static Color XYChartNonZeroForeColor
		{
			get
			{
				int colorValue = PaApp.SettingsHandler.GetIntSettingsValue(
					kOptionsSettingsKey, "xychartnonzeroforeColor", -1);

				return (colorValue == -1 ? Color.Black : Color.FromArgb(colorValue));
			}
			set
			{
				PaApp.SettingsHandler.SaveSettingsValue(
					kOptionsSettingsKey, "xychartnonzeroforeColor", value.ToArgb());
			}
		}
		
		#endregion

		#region Misc. methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Loads the default phonology assistant menu in the specified container control.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ITMAdapter LoadDefaultMenu(Control menuContainer)
		{
			ITMAdapter adapter = AdapterHelper.CreateTMAdapter();

			if (adapter != null)
			{
				string[] defs = new string[1];
				defs[0] = Path.Combine(Application.StartupPath, "PaTMDefinition.xml");
				adapter.Initialize(menuContainer,
					PaApp.MsgMediator, ApplicationRegKeyPath, defs);

				adapter.AllowUpdates = true;
			}

			adapter.RecentFilesList = RecentlyUsedProjectList;
			adapter.RecentlyUsedItemChosen += 
				new RecentlyUsedItemChosenHandler(adapter_RecentlyUsedItemChosen);

			if (s_defaultMenuAdapters == null)
				s_defaultMenuAdapters = new List<ITMAdapter>();

			s_defaultMenuAdapters.Add(adapter);
			return adapter;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Informs whoever cares that a recently used project has been chosen.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		static void adapter_RecentlyUsedItemChosen(string filename)
		{
			MsgMediator.SendMessage("RecentlyUsedProjectChosen", filename);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds the specified file to the recently used projects list.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void AddProjectToRecentlyUsedProjectsList(string filename)
		{
			List<string> rufList = new List<string>(RecentlyUsedProjectList);

			// First, remove the filename from the list if it's in there.
			if (rufList.Contains(filename))
				rufList.Remove(filename);

			rufList.Insert(0, filename);

			for (int i = 1; i < 10 && i <= rufList.Count; i++)
			{
				string entry = string.Format("ruf{0}", i);
				SettingsHandler.SaveSettingsValue(entry, "file", rufList[i - 1]);
			}

			if (s_defaultMenuAdapters != null)
			{
				foreach (ITMAdapter adapter in s_defaultMenuAdapters)
					adapter.RecentFilesList = rufList.ToArray();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the list of recently used projects from the PA settings file.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string[] RecentlyUsedProjectList
		{
			get
			{
				List<string> rufList = new List<string>();

				for (int i = 1; i < 10; i++)
				{
					string entry = string.Format("ruf{0}", i);
					string filename = SettingsHandler.GetStringSettingsValue(entry, "file", null);
					if (!string.IsNullOrEmpty(filename))
						rufList.Add(filename);
				}

				return rufList.ToArray();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the top-level registry key path to the application's registry entry.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string ApplicationRegKeyPath
		{
			get { return @"Software\SIL\Phonology Assistant"; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Enables or disables a TM item based on whether or not there is project loaded
		/// and there are records.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void EnableWhenProjectOpenHaveRecords(TMItemProperties itemProps)
		{
			bool enable = (PaApp.Project != null && PaApp.WordCache.Count != 0);
			if (itemProps != null && itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Enables or disables a TM item based on whether or not there is project loaded.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void EnableWhenProjectOpen(TMItemProperties itemProps)
		{
			bool enable = (PaApp.Project != null);

			if (itemProps != null && itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Enables or disables a TM item based on whether or not there is a project loaded
		/// and the specified view type is current.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool DetermineMenuStateBasedOnViewType(TMItemProperties itemProps, Type viewType)
		{
			bool enable = (PaApp.Project != null && PaApp.CurrentViewType != viewType);

			if (itemProps != null && itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}

			return (itemProps != null && PaApp.CurrentViewType == viewType);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes the progress bar, assuming the max. value will be the count of items
		/// in the current project's word cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ToolStripProgressBar InitializeProgressBar(string text)
		{
			return InitializeProgressBar(text, s_wordCache.Count);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static ToolStripProgressBar InitializeProgressBar(string text, int maxValue)
		{
			if (s_progressBar != null)
			{
				if (s_mainForm != null)
				{
					Application.UseWaitCursor = true;
					Application.DoEvents();
				}

				// Save the current progress bar and initialize s_progressBar with the one
				// returned by the property since it may not be the same. Normally, the one
				// stored in s_progressBar is the one on the main form but the one returned
				// from the property may be one from an undocked form. s_progressBar will
				// be restored to the one on the main form in UninitializeProgressBar.
				if (!s_statusBarHasBeenInitialized)
				{
					s_savProgressBar = s_progressBar;
					s_savProgressBarLabel = s_progressBarLabel;
					s_progressBar = ProgressBar;
					s_progressBarLabel = ProgressBarLabel;
					s_statusBarHasBeenInitialized = true;
				}

				s_progressBar.Maximum = maxValue;
				s_progressBar.Value = 0;
				s_progressBarLabel.Text = text;
				s_progressBarLabel.Visible = s_progressBar.Visible = true;
				Application.DoEvents();
			}

			return s_progressBar;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes the progress bar for the specified view.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void InitializeProgressBarForLoadingView(string viewName, int maxValue)
		{
			string text = string.Format(Properties.Resources.kstidViewInitProgBarTemplate, viewName);
			InitializeProgressBar(text, maxValue);

			if (s_splashScreen != null && s_splashScreen.StillAlive)
				s_splashScreen.Message = text;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void UninitializeProgressBar()
		{
			Application.UseWaitCursor = false;

			if (s_progressBar != null)
				s_progressBar.Visible = false;

			if (s_progressBarLabel != null)
				s_progressBarLabel.Visible = false;

			if (s_statusBarHasBeenInitialized)
			{
				if (s_savProgressBar != null)
					s_progressBar = s_savProgressBar;

				if (s_savProgressBarLabel != null)
					s_progressBarLabel = s_savProgressBarLabel;

				s_savProgressBar = null;
				s_savProgressBarLabel = null;
				s_statusBarHasBeenInitialized = false;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Increments the progress bar by one.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void IncProgressBar()
		{
			IncProgressBar(1);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Increments the progress bar by the specified amount. Passing -1 to this method
		/// will cause the progress bar to go to it's max. value.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void IncProgressBar(int amount)
		{
			if (s_progressBar != null)
			{
				if (amount != -1 && (s_progressBar.Value + amount) <= s_progressBar.Maximum)
					s_progressBar.Value += amount;
				else
					s_progressBar.Value = s_progressBar.Maximum;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Updates the progress bar label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void UpdateProgressBarLabel(string label)
		{
			if (s_progressBarLabel != null)
			{
				if (s_splashScreen != null && s_splashScreen.StillAlive)
					s_splashScreen.Message = label;
				
				s_progressBarLabel.Text = label;
				Application.DoEvents();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Closes the splash screen if it's showing.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void CloseSplashScreen()
		{
			if (s_splashScreen != null && s_splashScreen.StillAlive)
			{
				Application.DoEvents();
				if (PaApp.MainForm != null)
					PaApp.MainForm.Activate();

				s_splashScreen.Close();
			}

			s_splashScreen = null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string OpenFileDialog(string defaultFileType, string filter, string dlgTitle)
		{
			int filterIndex = 0;
			return OpenFileDialog(defaultFileType, filter, ref filterIndex,	dlgTitle);
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static string OpenFileDialog(string defaultFileType, string filter,
			ref int filterIndex, string dlgTitle)
		{
			string[] filenames = OpenFileDialog(defaultFileType, filter, ref filterIndex,
				dlgTitle, false, null);

			return (filenames == null || filenames.Length == 0 ? null : filenames[0]);
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static string[] OpenFileDialog(string defaultFileType, string filter,
			ref int filterIndex, string dlgTitle, bool multiSelect)
		{
			return OpenFileDialog(defaultFileType, filter, ref filterIndex,
				dlgTitle, multiSelect, null);
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// --------------------------------------------------------------------------------
		public static string[] OpenFileDialog(string defaultFileType, string filter,
			ref int filterIndex, string dlgTitle, bool multiSelect, string initialDirectory)
		{
			string[] filters = filter.Split("|".ToCharArray());
			
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.CheckFileExists = true;
			dlg.CheckPathExists = true;
			dlg.DefaultExt = defaultFileType;
			dlg.Filter = filter;
			dlg.Multiselect = multiSelect;
			dlg.ShowReadOnly = false;
			dlg.ShowHelp = false;
			dlg.Title = dlgTitle;
			dlg.RestoreDirectory = false;
			dlg.InitialDirectory = (initialDirectory == null ?
				Environment.CurrentDirectory : initialDirectory);

			if (filterIndex > 0)
				dlg.FilterIndex = filterIndex;

			dlg.ShowDialog();

			filterIndex = dlg.FilterIndex;
			return dlg.FileNames;
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Open the Save File dialog.
		/// </summary>
		/// <param name="defaultFileType">Default file type to save</param>
		/// <param name="filter">The parent's saved filter</param>
		/// <param name="filterIndex">The new filter index</param>
		/// <param name="dlgTitle">Title of the Save File dialog</param>
		/// <param name="initialDir">Directory where the dialog starts</param>
		/// --------------------------------------------------------------------------------
		public static string SaveFileDialog(string defaultFileType, string filter,
			ref int filterIndex, string dlgTitle, string initialDir)
		{
			return SaveFileDialog(defaultFileType, filter, ref filterIndex, dlgTitle,
				null, initialDir);
		}

		/// --------------------------------------------------------------------------------
		/// <summary>
		/// Open the Save File dialog.
		/// </summary>
		/// <param name="defaultFileType">Default file type to save</param>
		/// <param name="filter">The parent's saved filter</param>
		/// <param name="filterIndex">The new filter index</param>
		/// <param name="initialFileName">The default filename</param>
		/// <param name="initialDir">Directory where the dialog starts</param>
		/// <param name="dlgTitle">Title of the Save File dialog</param>
		/// --------------------------------------------------------------------------------
		public static string SaveFileDialog(string defaultFileType, string filter,
			ref int filterIndex, string dlgTitle, string initialFileName, string initialDir)
		{
			string[] filters = filter.Split("|".ToCharArray());

			SaveFileDialog dlg = new SaveFileDialog();
			dlg.AddExtension = true;
			dlg.DefaultExt = defaultFileType;
			dlg.OverwritePrompt = true;
			dlg.Filter = filter;
			dlg.Title = dlgTitle;
			dlg.RestoreDirectory = false;

			if (!string.IsNullOrEmpty(initialFileName))
				dlg.FileName = initialFileName;

			if (!string.IsNullOrEmpty(initialDir))
				dlg.InitialDirectory = initialDir;

			if (filterIndex > 0)
				dlg.FilterIndex = filterIndex;

			if (dlg.ShowDialog() == DialogResult.OK)
			{
				filterIndex = dlg.FilterIndex;
				return dlg.FileName;
			}

			return String.Empty;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not the specified view type or form is the active.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsViewOrFormActive(Type viewType, Form frm)
		{
			return (viewType == PaApp.CurrentViewType && frm != null && frm.ContainsFocus);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not the specified form is the active form.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool IsFormActive(Form frm)
		{
			if (frm == null)
				return false;

			if (frm.ContainsFocus || frm.GetType() == PaApp.CurrentViewType)
				return true;

			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Closes all MDI child forms.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void CloseAllForms()
		{
			s_openForms.Clear();

			if (s_mainForm == null)
				return;

			// There may be some child forms not in the s_openForms collection. If that's
			// the case, then close them this way.
			for (int i = s_mainForm.MdiChildren.Length - 1; i >= 0; i--)
				s_mainForm.MdiChildren[i].Close();
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates and loads a result cache for the specified search query.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static WordListCache Search(SearchQuery query)
		{
			int resultCount;
			return Search(query, true, false, 1, out resultCount);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates and loads a result cache for the specified search query.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static WordListCache Search(SearchQuery query, int incAmount)
		{
			int resultCount;
			return Search(query, true, false, incAmount, out resultCount);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates and loads a result cache for the specified search query.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static WordListCache Search(SearchQuery query, bool incProgressBar,
			bool returnCountOnly, int incAmount, out int resultCount)
		{
			resultCount = 0;
			bool patternContainsWordBoundaries = (query.Pattern.IndexOf('#') >= 0);

			int incCounter = 0;
			int[] result;
			WordListCache resultCache = (returnCountOnly ? null : new WordListCache());
			SearchQuery modifiedQuery = ConvertClassesToPatterns(query, true);
			
			if (modifiedQuery == null)
				return null;

			SearchEngine engine = new SearchEngine(modifiedQuery, PaApp.PhoneCache);
			foreach (WordCacheEntry wordEntry in PaApp.WordCache)
			{
				if (incProgressBar && (incCounter++) == incAmount)
				{
					IncProgressBar(incAmount);
					incCounter = 0;
				}

				string[][] eticWords = new string[1][];

				if (query.IncludeAllUncertainPossibilities && wordEntry.ContiansUncertainties)
				{
					// All uncertain possibilities should be included in the search, so load
					// up all the phones from each possible word the uncertain phones can make.
					eticWords = wordEntry.GetAllPossibleUncertainWords(false);
					if (eticWords == null)
						continue;
				}
				else
				{
					// Not all uncertain possibilities should be included in the search, so
					// just load up the phones that only include the primary uncertain Phone(s).
					eticWords[0] = wordEntry.Phones;
					if (eticWords[0] == null)
						continue;
				}

				for (int i = 0; i < eticWords.Length; i++)
				{
					// If the search pattern contains the word breaking character,
					// then add a space at the beginning and end of the array of
					// phones so the word breaking character has something to
					// match at the extremes of the phonetic values.
					if (patternContainsWordBoundaries)
					{
						List<string> tmpChars = new List<string>(eticWords[i]);
						tmpChars.Insert(0, " ");
						tmpChars.Add(" ");
						eticWords[i] = tmpChars.ToArray();
					}

					bool matchFound = engine.SearchWord(eticWords[i], out result);
					while (matchFound)
					{
						if (returnCountOnly)
							resultCount++;
						else
							resultCache.Add(wordEntry, eticWords[i], result[0], result[1]);
						
						matchFound = engine.SearchWord(out result);
					}
				}
			}

			if (incProgressBar)
				IncProgressBar(-1);
			
			return resultCache;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Searches the specified query's pattern for search class specifications and replaces
		/// them with the pattern the classes represent.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static SearchQuery ConvertClassesToPatterns(SearchQuery query, bool showMsgOnErr)
		{
			string msg;
			return ConvertClassesToPatterns(query, showMsgOnErr, out msg);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Searches the specified query's pattern for search class specifications and replaces
		/// them with the pattern the classes represent.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static SearchQuery ConvertClassesToPatterns(SearchQuery query, bool showMsgOnErr,
			out string errorMsg)
		{
			Debug.Assert(query != null);
			Debug.Assert(query.Pattern != null);
			errorMsg = null;

			// Get the index of the first opening bracket and check if we need go further.
			int i = query.Pattern.IndexOf(kOpenClassBracket);
			if (PaApp.Project == null || PaApp.Project.SearchClasses.Count == 0 || i < 0)
				return query;

			SearchQuery newQuery = query.Clone();

			while (i >= 0)
			{
				// Save the offset of the open bracket and find
				// its corresponding closing bracket.
				int start = i;
				i = newQuery.Pattern.IndexOf(kCloseClassBracket, i);

				if (i > start)
				{
					// Extract the class name from the query's pattern and
					// find the SearchClass object having that class name.
					string className = newQuery.Pattern.Substring(start, i - start + 1);
					SearchClass srchClass = PaApp.Project.SearchClasses[className];
					if (srchClass != null)
						newQuery.Pattern = newQuery.Pattern.Replace(className, srchClass.Pattern);
					else
					{
						errorMsg = Properties.Resources.kstidMissingClassMsg;
						errorMsg = string.Format(errorMsg, className);

						if (showMsgOnErr)
						{
							STUtils.STMsgBox(errorMsg, MessageBoxButtons.OK,
								   MessageBoxIcon.Exclamation);
						}

						return null;
					}
				}

				// Get the next open class bracket.
				i = newQuery.Pattern.IndexOf(kOpenClassBracket);
			}

			return newQuery;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void DrawWatermarkImage(string imageId, Graphics g, Rectangle clientRectangle)
		{
			Image watermark = Properties.Resources.ResourceManager.GetObject(imageId) as Image;
			if (watermark == null)
				return;

			Rectangle rc = new Rectangle();
			rc.Size = watermark.Size;
			rc.X = clientRectangle.Right - rc.Width - 10;
			rc.Y = clientRectangle.Bottom - rc.Height - 10;
			g.DrawImage(watermark, rc);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void ShowHelpTopic(Control ctrl)
		{
			ShowHelpTopic("hid" + ctrl.Name);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void ShowHelpTopic(string hid)
		{
			if (string.IsNullOrEmpty(s_helpFilePath))
			{
				s_helpFilePath = Path.GetDirectoryName(Application.ExecutablePath);
				s_helpFilePath = Path.Combine(s_helpFilePath, kHelpSubFolder);
				s_helpFilePath = Path.Combine(s_helpFilePath, kHelpFileName);
			}

			if (File.Exists(s_helpFilePath))
				Help.ShowHelp(new Label(), s_helpFilePath, ResourceHelper.GetHelpString(hid));
			else
			{
				string msg = string.Format(Properties.Resources.kstidHelpFileMissingMsg, s_helpFilePath);
				STUtils.STMsgBox(msg, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
	}
}