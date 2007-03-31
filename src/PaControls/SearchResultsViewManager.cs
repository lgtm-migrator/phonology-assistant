using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using SIL.Pa.Resources;
using SIL.Pa.FFSearchEngine;
using SIL.SpeechTools.Utils;
using SIL.FieldWorks.Common.UIAdapters;
using XCore;

namespace SIL.Pa.Controls
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// 
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public enum SearchResultLocation
	{
		/// <summary>Adds tab results to the current tab, even if the tab already contains results.</summary>
		CurrentTab,
		/// <summary>Adds tab results to a new tab created in the current tab group.</summary>
		CurrentTabGroup,
		/// <summary>Adds tab results to a new tab created in a new stacked tab group.</summary>
		NewStackedTabGroup,
		/// <summary>Adds tab results to a new tab created in a new side-by-side tab group.</summary>
		NewSideBySideTabGroup
	}

	#region ISearchResultsViewHost
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// 
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public interface ISearchResultsViewHost
	{
		void BeforeSearchPerformed(SearchQuery query, WordListCache resultCache);
		void AfterSearchPerformed(SearchQuery query, WordListCache resultCache);
		bool ShouldMenuBeEnabled(string menuName);
		SearchQuery GetQueryForMenu(string menuName);
		void NotifyAllTabsClosed();
		void NotifyCurrentTabChanged(SearchResultTab newTab);
		void ShowFindDlg(PaWordListGrid grid);
	}

	#endregion

	#region TabChangingArgs Class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Used to pass information to message handlers called when a tab changes.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class TabChangingArgs
	{
		public Form OwningForm;
		public SearchResultTabGroup Group;
		public SearchResultTab Tab;

		public TabChangingArgs(Form owningForm, SearchResultTabGroup group, SearchResultTab tab)
		{
			OwningForm = owningForm;
			Group = group;
			Tab = tab;
		}
	}

	#endregion

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Encapsulates methods, properties and message handlers for managing a split container
	/// control that contains search result view tab groups and a raw record view.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class SearchResultsViewManager : IxCoreColleague, IDisposable, IMessageFilter
	{
		private SortOptionsDropDown m_phoneticSortOptionsDropDown;
		private SearchResultTabPopup m_srchResultTabPopup;
		private PlaybackSpeedAdjuster m_playbackSpeedAdjuster;
		private float m_horzSplitterCount = 0;
		private float m_vertSplitterCount = 0;
		private bool m_ignoreTagGroupRemoval = false;
		private SearchResultTabGroup m_currTabGroup;
		private SplitterPanel m_resultsPanel;
		private bool m_rawRecViewOn = true;
		private Form m_form;
		private ISearchResultsViewHost m_srchRsltVwHost;
		private ITMAdapter m_tmAdapter;
		private SplitContainer m_splitResults;
		private RawRecordView m_rawRecView;
		private List<SearchResultTabGroup> m_tabGroups;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public SearchResultsViewManager(Form frm, ITMAdapter tmAdapter,
			SplitContainer splitResults, RawRecordView rawRecView)
		{
			m_form = frm;
			m_srchRsltVwHost = frm as ISearchResultsViewHost;
			System.Diagnostics.Debug.Assert(m_srchRsltVwHost != null);
			m_tmAdapter = tmAdapter;
			m_splitResults = splitResults;
			m_resultsPanel = splitResults.Panel1;
			m_rawRecView = rawRecView;
			m_tabGroups = new List<SearchResultTabGroup>();
			PaApp.AddMediatorColleague(this);

			m_playbackSpeedAdjuster = new PlaybackSpeedAdjuster();
			m_playbackSpeedAdjuster.lnkPlay.Click +=
				new EventHandler(HandlePlaybackSpeedAdjusterPlayClick);

			m_resultsPanel.ControlRemoved += new ControlEventHandler(this.HandleTabGroupRemoved);
			Application.AddMessageFilter(this);
		}

		#region IDisposable Members
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void Dispose()
		{
			Application.RemoveMessageFilter(this);
			m_form = null;
			m_tmAdapter = null;
			m_splitResults = null;
			m_resultsPanel = null;
			m_currTabGroup = null;
			m_tmAdapter = null;
			PaApp.RemoveMediatorColleague(this);

			if (m_srchResultTabPopup != null)
				m_srchResultTabPopup.Dispose();

			if (m_playbackSpeedAdjuster != null)
				m_playbackSpeedAdjuster.Dispose();
		}

		#endregion

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public List<SearchResultTabGroup> TabGroups
		{
			get { return m_tabGroups; }
			set { m_tabGroups = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the view tabs manager's record view.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public RawRecordView RawRecordView
		{
			get { return m_rawRecView; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool RawRecViewOn
		{
			get { return m_rawRecViewOn; }
			set
			{
				if (m_rawRecViewOn != value)
				{
					m_rawRecViewOn = value;
					m_splitResults.Panel2Collapsed = !m_splitResults.Panel2Collapsed;
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the current tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public SearchResultTabGroup CurrentTabGroup
		{
			get { return m_currTabGroup; }
			set { m_currTabGroup = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the grid of the current result view tab on the current result view tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public PaWordListGrid CurrentViewsGrid
		{
			get
			{
				return (m_currTabGroup == null ||
					m_currTabGroup.CurrentTab == null ||
					m_currTabGroup.CurrentTab.ResultView == null ? null :
					m_currTabGroup.CurrentTab.ResultView.Grid);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the view tabs manager's toolbar/menu adapter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ITMAdapter TMAdapter
		{
			get { return m_tmAdapter; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a value indicating whether or not the view manager contains any results and
		/// if so, does the current tab on the current tab group contain results.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool ContainsResults
		{
			get {return m_resultsPanel.Controls.Count > 0 && CurrentViewsGrid != null;}
		}

		#endregion

		#region Message mediator message handlers and update handlers
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnGroupBySortedField(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			if (CurrentViewsGrid != null)
				CurrentViewsGrid.GroupOnSortedField = !CurrentViewsGrid.GroupOnSortedField;

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateGroupBySortedField(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (!PaApp.IsFormActive(m_form) || itemProps == null)
				return false;

			bool enable = (CurrentViewsGrid != null && CurrentViewsGrid.Cache != null &&
				!CurrentViewsGrid.Cache.IsCIEList);
			
			bool check = (enable && CurrentViewsGrid.GroupOnSortedField);

			if (itemProps.Checked != check || itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Checked = check;
				itemProps.Update = true;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnExpandAllGroups(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			if (CurrentViewsGrid != null)
				CurrentViewsGrid.ToggleGroupExpansion(true);
	
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateExpandAllGroups(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (!PaApp.IsFormActive(m_form) || itemProps == null)
				return false;

			bool enable = (CurrentViewsGrid != null && (CurrentViewsGrid.GroupOnSortedField ||
				(CurrentViewsGrid.Cache != null && CurrentViewsGrid.Cache.IsCIEList)));

			if (itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnCollapseAllGroups(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			if (CurrentViewsGrid != null)
				CurrentViewsGrid.ToggleGroupExpansion(false);
	
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateCollapseAllGroups(object args)
		{
			return OnUpdateExpandAllGroups(args);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Toggles showing the record pane below the tab groups.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnShowRecordPane(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			RawRecViewOn = !m_rawRecViewOn;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowRecordPane(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			bool enable = true;

			// Check if the record view is in a split container (call it A) that is owned by
			// another split container (call it B). If so, then assume A is contained in
			// Panel2 of B and only enable the item when B is not collapsed.
			if (m_splitResults.Parent is SplitterPanel)
				enable = !((SplitContainer)m_splitResults.Parent.Parent).Panel2Collapsed;

			bool check = (m_rawRecViewOn && itemProps.Enabled);
			if (itemProps.Checked != check || itemProps.Enabled != enable || !itemProps.Visible)
			{
				itemProps.Enabled = enable;
				itemProps.Checked = check;
				itemProps.Visible = true;
				itemProps.Update = true;
			}
			
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This message is received when a tab should be moved to a new side-by-side tab
		/// group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnReflectMoveToNewSideBySideTabGroup(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			return MoveTabToNewTabGroup(args as SearchResultTab,
				SearchResultLocation.NewSideBySideTabGroup);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This message is received when a tab should be moved to a new stacked tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnReflectMoveToNewStackedTabGroup(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			return MoveTabToNewTabGroup(args as SearchResultTab,
				SearchResultLocation.NewStackedTabGroup);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private bool MoveTabToNewTabGroup(SearchResultTab tab, SearchResultLocation resultLocation)
		{
			if (tab == null)
				return false;

			SearchResultTabGroup tabGroup = tab.OwningTabGroup;

			if (tabGroup != null)
				tabGroup.RemoveTab(tab, false);

			m_resultsPanel.SuspendLayout();
			tabGroup = CreateNewTabGroup(resultLocation);
			tabGroup.AddTab(tab);
			tabGroup.SelectTab(tab, true);
			m_resultsPanel.ResumeLayout(true);
			
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnCloseAllTabGroups(object args)
		{
			if (PaApp.IsFormActive(m_form))
			{
				m_ignoreTagGroupRemoval = true;
				Control ctrl = m_resultsPanel.Controls[0];
				m_resultsPanel.Controls.RemoveAt(0);
				ctrl.Dispose();
				m_ignoreTagGroupRemoval = false;
				m_rawRecView.UpdateRecord(null);
				m_currTabGroup = null;
				m_horzSplitterCount = m_vertSplitterCount = 0;

				// For some reason, closing all tabs from the context menu causes the form
				// to loose focus. Grr!
				m_form.Activate();

				m_srchRsltVwHost.NotifyAllTabsClosed();
			}
			
			return false;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new tab in the current tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool OnNewTabInCurrentTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			CreateTab(SearchResultLocation.CurrentTabGroup);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new tab in a new stacked tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnNewTabInNewStackedTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			CreateTab(SearchResultLocation.NewStackedTabGroup);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not to enable the option to create a new stacked tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateNewTabInNewStackedTabGroup(object args)
		{
			return OnUpdateNewTabInNewSideBySideTabGroup(args);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new tab in a new side-by-side tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnNewTabInNewSideBySideTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			CreateTab(SearchResultLocation.NewSideBySideTabGroup);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Determines whether or not to enable the option to create a new side-by-side
		/// tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateNewTabInNewSideBySideTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			bool enable = (m_resultsPanel.Controls.Count > 0);
			if (itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}
			
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs a search and puts the results in the current tab group when the user
		/// presses enter when the focus is in the pattern text box.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool OnEnterPressedInSearchPatternTextBox(object args)
		{
			SearchQuery query = args as SearchQuery;
			if (query == null || !PaApp.IsFormActive(m_form))
				return false;

			PerformSearch(query, SearchResultLocation.CurrentTab);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs a search and puts the results in the current tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool OnShowResultsInCurrentTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			SearchQuery query = m_srchRsltVwHost.GetQueryForMenu(itemProps.Name);
			if (query != null)
				PerformSearch(query, SearchResultLocation.CurrentTabGroup);

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowResultsInCurrentTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			itemProps.Visible = true;
			itemProps.Enabled = m_srchRsltVwHost.ShouldMenuBeEnabled(itemProps.Name);
			itemProps.Update = true;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs a search and puts the results in a new stacked tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnShowResultsInNewStackedTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			SearchQuery query = m_srchRsltVwHost.GetQueryForMenu(itemProps.Name);
			if (query != null)
				PerformSearch(query, SearchResultLocation.NewStackedTabGroup);

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowResultsInNewStackedTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			itemProps.Enabled = m_srchRsltVwHost.ShouldMenuBeEnabled(itemProps.Name) &&
				m_resultsPanel.Controls.Count > 0;

			itemProps.Visible = true;
			itemProps.Update = true;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Performs a search and puts the results in a new side-by-side tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnShowResultsInNewSideBySideTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			SearchQuery query = m_srchRsltVwHost.GetQueryForMenu(itemProps.Name);
			if (query != null)
				PerformSearch(query, SearchResultLocation.NewSideBySideTabGroup);

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowResultsInNewSideBySideTabGroup(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			itemProps.Enabled = m_srchRsltVwHost.ShouldMenuBeEnabled(itemProps.Name) &&
				m_resultsPanel.Controls.Count > 0;

			itemProps.Visible = true;
			itemProps.Update = true;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdatePlayback(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form) || ContainsResults)
				return false;

			if (itemProps.Enabled)
			{
				itemProps.Visible = true;
				itemProps.Enabled = false;
				itemProps.Update = true;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdatePlaybackRepeatedly(object args)
		{
			return OnUpdatePlayback(args);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateAdjustPlaybackSpeed(object args)
		{
			return OnUpdatePlayback(args);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateStopPlayback(object args)
		{
			return OnUpdatePlayback(args);
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This message is received when the current tab group changes.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		internal void SearchResultTabGroupChanged(SearchResultTabGroup newGroup)
		{
			if (m_currTabGroup != newGroup)
				m_currTabGroup = newGroup;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Notifies those who care that the current tab has changed.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		internal void CurrentSearchResultTabChanged(SearchResultTab tab)
		{
			m_srchRsltVwHost.NotifyCurrentTabChanged(tab);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure that when tab groups are removed a couple of other clean-up procedures
		/// are done.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandleTabGroupRemoved(object sender, ControlEventArgs e)
		{
			if (m_ignoreTagGroupRemoval)
				return;

			SearchResultTabGroup tabGroup = e.Control as SearchResultTabGroup;
			SplitterPanel owningPanel = sender as SplitterPanel;
			if (tabGroup == null || owningPanel == null)
				return;

			m_splitResults.SuspendLayout();
			bool removedTabGroupWasCurrent = tabGroup.IsCurrent;
			Control siblingPaneToRelocate = null;

			// Get the splitter that owns the panel from which a tab group was just removed.
			SplitContainer owningSplitContainer = owningPanel.GetContainerControl() as SplitContainer;

			// Determine whether or not the tab group was removed from the first or second panel.
			int paneOfRemovedGroup = (int)owningPanel.Tag;

			// If the tab group was not removed from the top level panel that is the parent for
			// all tab groups and their split containers, then remove the tab group's sibling
			// from it's split container and place it on the split container that's one level
			// up the parent chain of split containers.
			if (paneOfRemovedGroup > 0 && owningSplitContainer != null)
			{
				// Get the sibling tab group.
				siblingPaneToRelocate = (paneOfRemovedGroup == 2 ?
					owningSplitContainer.Panel1.Controls[0] :
					owningSplitContainer.Panel2.Controls[0]);

				// Determine the new owning split panel and remove from that panel the
				// split container that used to own the removed tab group. Then add to that
				// panel the removed tab group's sibling.
				m_ignoreTagGroupRemoval = true;
				SplitterPanel newOwningPanel = owningSplitContainer.Parent as SplitterPanel;
				newOwningPanel.Controls.Remove(owningSplitContainer);
				newOwningPanel.Controls.Add(siblingPaneToRelocate);
				m_ignoreTagGroupRemoval = false;

				if (owningSplitContainer.Orientation == Orientation.Horizontal)
					m_horzSplitterCount--;
				else
					m_vertSplitterCount--;

				owningSplitContainer.Dispose();
			}
			else
				m_horzSplitterCount = m_vertSplitterCount = 0;

			// Hide the record view when there are no more tab groups.
			if (m_resultsPanel.Controls.Count == 0)
			{
				m_currTabGroup = null;
				m_rawRecView.UpdateRecord(null);
				m_srchRsltVwHost.NotifyAllTabsClosed();
			}
			else if (removedTabGroupWasCurrent)
			{
				// When the removed tag group is the current one,
				// make sure a remaining group is made current.
				SearchResultTabGroup newTabGroup = (siblingPaneToRelocate == null ?
					m_resultsPanel.Controls[0] : siblingPaneToRelocate) as SearchResultTabGroup;

				SearchResultTabGroupChanged(newTabGroup);
				PaApp.MsgMediator.SendMessage("SearchResultTabGroupChanged", newTabGroup);
			}

			tabGroup.Dispose();
			m_splitResults.ResumeLayout();
		}

		#region Methods for performing searches
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnShowResults(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			SearchQuery query = m_srchRsltVwHost.GetQueryForMenu(itemProps.Name);
			if (query != null)
				PerformSearch(query, SearchResultLocation.CurrentTab);

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowResults(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			bool enable = m_srchRsltVwHost.ShouldMenuBeEnabled(itemProps.Name);

			if (itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}
			
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public WordListCache PerformSearch(SearchQuery query, SearchResultLocation resultLocation)
		{
			if (query == null)
				return null;

			// TODO: check error conditions returned from creating an engine
			//if (engine.Error != null)
			//{
			//	Do something
			//}

			m_srchRsltVwHost.BeforeSearchPerformed(query, null);
			PaApp.InitializeProgressBar(ResourceHelper.GetString("kstidQuerySearchingMsg"));
			WordListCache resultCache = PaApp.Search(query, 5);

			if (resultCache != null)
			{
				resultCache.SearchQuery = query.Clone();
				m_srchRsltVwHost.AfterSearchPerformed(query, resultCache);
				ShowResults(resultCache, resultLocation);
			}
			
			PaApp.UninitializeProgressBar();
			return resultCache;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ShowResults(WordListCache resultCache, SearchResultLocation resultLocation)
		{
			// When this is true, it probably means there are no results showing.
			// But, since there is about to be, we'll need to show the record
			// view when m_rawRecViewOn is true.
			if (m_rawRecViewOn && m_splitResults.Panel2Collapsed)
				m_splitResults.Panel2Collapsed = false;

			SearchResultView resultView = new SearchResultView(m_form.GetType(), m_tmAdapter);
			resultView.Initialize(resultCache);

			// When the results should be shown in the current tab group, then check if the
			// current tab is empty. If so, then use that tab instead of creating a new tab
			// in which to display the results.
			if (resultLocation == SearchResultLocation.CurrentTabGroup && m_currTabGroup != null &&
				m_currTabGroup.CurrentTab != null && m_currTabGroup.CurrentTab.IsEmpty)
			{
				resultLocation = SearchResultLocation.CurrentTab;
			}

			CreateTab(resultLocation, resultView);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds an empty tab to the current tab group. If there is no tab group, then a new
		/// tab group is created first.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void CreateEmptyTab()
		{
			CreateTab(SearchResultLocation.CurrentTabGroup);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new tab in the specified tab group.
		/// Once the tab is created, it's selected.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private SearchResultTab CreateTab(SearchResultLocation resultLocation)
		{
			return CreateTab(resultLocation, null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new tab in the specified tab group.
		/// Once the tab is created, it's selected.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private SearchResultTab CreateTab(SearchResultLocation resultLocation,
			SearchResultView resultView)
		{
			SearchResultTab tab;
			m_resultsPanel.SuspendLayout();

			// Return the current tab in the current tab group if
			// it matches the specified location.
			if (m_currTabGroup != null && m_currTabGroup.CurrentTab != null &&
				resultLocation == SearchResultLocation.CurrentTab)
			{
				tab = m_currTabGroup.CurrentTab;
				if (tab != null && resultView != null)
				{
					tab.Text = null;
					m_currTabGroup.InitializeTab(tab, resultView, true);
				}

				m_resultsPanel.ResumeLayout(true);
				// Must Select the Tab, so the Current Playback Grid for the tab is set to true.
				// This will ensure the sound file Playback will always work.
				m_currTabGroup.SelectTab(tab, true);
				return tab;
			}

			SearchResultTabGroup tabGroup = m_currTabGroup;

			if (m_resultsPanel.Controls.Count == 0)
			{
				// No tab groups exist yet so just add one without any splitter.
				tabGroup = new SearchResultTabGroup(this);
				tabGroup.SuspendLayout();
				tabGroup.Size = new Size(m_resultsPanel.Width, m_resultsPanel.Height);
				tabGroup.Dock = DockStyle.Fill;
				m_resultsPanel.Controls.Add(tabGroup);
				TabGroups.Add(tabGroup);
				m_resultsPanel.Tag = 0;
			}
			else if (resultLocation != SearchResultLocation.CurrentTab &&
				resultLocation != SearchResultLocation.CurrentTabGroup)
			{
				tabGroup = CreateNewTabGroup(resultLocation);
			}

			tab = (resultView == null ? tabGroup.AddTab() :
				tabGroup.AddTab(resultView));

			tabGroup.SelectTab(tab, true);
			tab.MouseEnter += new EventHandler(tab_MouseEnter);
			tab.MouseLeave += new EventHandler(tab_MouseLeave);

			// If the tab just added is a new, empty tab, selecting it will cause a chain
			// reaction caused by hiding the former selected tab's grid. Doing that forces
			// the .Net framework to look for the next visible control in line that can
			// be focused. Sometimes that means a grid on another tab will get focus and
			// ultimately cause that grid's tab to become selected, thus negating the fact
			// that we just got through setting the tab we wanted to be current. Therefore,
			// try again, when we get back from selecting our new tab and it's still not
			// selected.
			if (tabGroup != m_currTabGroup || !tab.Selected)
				tabGroup.SelectTab(tab, true);

			tabGroup.ResumeLayout(false);
			m_resultsPanel.ResumeLayout();
			return tab;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new stacked or side-by-side tab group.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private SearchResultTabGroup CreateNewTabGroup(SearchResultLocation resultLocation)
		{
			// At this point, we know the user wants to add the new search results to a
			// new tab group. Therefore, a split container is created for the new tab.
			m_resultsPanel.SuspendLayout();

			SplitContainer split = new SplitContainer();
			split.SuspendLayout();
			split.SplitterWidth = 8;
			split.TabStop = false;
			split.Dock = DockStyle.Fill;
			split.Panel1.ControlRemoved += new ControlEventHandler(HandleTabGroupRemoved);
			split.Panel2.ControlRemoved += new ControlEventHandler(HandleTabGroupRemoved);
			split.Panel1.Tag = 1;
			split.Panel2.Tag = 2;

			// Determine whether the new tab group's split container
			// should be oriented horizontally or vertically.
			float newSplitDistance = 0;
			if (resultLocation == SearchResultLocation.NewSideBySideTabGroup)
			{
				split.Orientation = Orientation.Vertical;
				m_vertSplitterCount++;
				newSplitDistance = (float)m_resultsPanel.Width *
					(m_vertSplitterCount / (m_vertSplitterCount + 1f));
			}
			else
			{
				split.Orientation = Orientation.Horizontal;
				m_horzSplitterCount++;
				newSplitDistance = m_resultsPanel.Height *
					(m_horzSplitterCount / (m_horzSplitterCount + 1f));
			}

			// Create a new tab group and add it to the new split container.
			SearchResultTabGroup tabGroup = new SearchResultTabGroup(this);
			tabGroup.SuspendLayout();
			tabGroup.Dock = DockStyle.Fill;
			tabGroup.Size = new Size(split.Panel2.Width, split.Panel2.Height);
			split.Panel2.Controls.Add(tabGroup);
			TabGroups.Add(tabGroup);

			// Now, all tab groups that previously existed before creating the new one, need
			// to be removed from their container and placed in the left or top pane of the
			// new split container. To do this, just remove the only control (which may well
			// be a bunch of nested split containers) that exists in the top most panel for
			// all tab groups and place it in the left or top pane of the new split container.
			// The new split container is then added to the top most panel for all tab groups.
			// In other words, every time a new split container is added for a tab group, it
			// becomes the wrapper for all subsequent tab group split containers. Whew!
			m_ignoreTagGroupRemoval = true;
			Control ctrl = m_resultsPanel.Controls[0];
			m_resultsPanel.Controls.Remove(ctrl);

			// Set these values now, because, even though they're docked, we're still
			// in a suspended layout mode so setting the splitter before resuming layout
			// causes the splitter distance to be set on the undocked size of the control.
			// But, this problem can be remedied by first setting the sizes.
			split.Size = new Size(m_resultsPanel.Width, m_resultsPanel.Height);
			try { split.SplitterDistance = (int)newSplitDistance; }
			catch { }
			ctrl.Size = new Size(split.Panel1.Width, split.Panel1.Height);
			
			split.Panel1.Controls.Add(ctrl);
			m_resultsPanel.Controls.Add(split);
			
			tabGroup.ResumeLayout(false);
			split.ResumeLayout(false);
			m_resultsPanel.ResumeLayout();
			
			m_ignoreTagGroupRemoval = false;
			return tabGroup;
		}

		#endregion

		#region Methods for showing and hiding the tab information popup.
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Show information about the tab's search query.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		void tab_MouseEnter(object sender, EventArgs e)
		{
			if (m_srchResultTabPopup == null)
				m_srchResultTabPopup = new SearchResultTabPopup();

			SearchResultTab tab = sender as SearchResultTab;
			if (tab != null)
				m_srchResultTabPopup.Show(tab);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		void tab_MouseLeave(object sender, EventArgs e)
		{
			if (m_srchResultTabPopup != null)
				m_srchResultTabPopup.HidePopup();
		}

		#endregion

		#region Phonetic Sort methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnDropDownSearchResultPhoneticSort(object args)
		{
			ToolBarPopupInfo itemProps = args as ToolBarPopupInfo;
			if (!PaApp.IsFormActive(m_form) || CurrentViewsGrid == null || itemProps == null)
				return false;

			m_phoneticSortOptionsDropDown =
				new SortOptionsDropDown(CurrentViewsGrid.SortOptions, true, m_tmAdapter);

			m_phoneticSortOptionsDropDown.SortOptionsChanged +=
				new SortOptionsDropDown.SortOptionsChangedHandler(HandlePhoneticSortOptionsChanged);

			itemProps.Control = m_phoneticSortOptionsDropDown;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Called when sort options change.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandlePhoneticSortOptionsChanged(SortOptions sortOptions)
		{
			if (CurrentViewsGrid != null)
			{
				CurrentViewsGrid.SortOptions = sortOptions;
				m_rawRecView.UpdateRecord(CurrentViewsGrid.GetRecord());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnDropDownClosedSearchResultPhoneticSort(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			m_phoneticSortOptionsDropDown.Dispose();
			m_phoneticSortOptionsDropDown = null;
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateSearchResultPhoneticSort(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			bool enable = (CurrentViewsGrid != null);
			if (itemProps.Enabled != enable)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Update = true;
			}

			return true;
		}

		#endregion

		#region Playback related methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public PlaybackSpeedAdjuster PlaybackSpeedAdjuster
		{
			get { return m_playbackSpeedAdjuster; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandlePlaybackSpeedAdjusterPlayClick(object sender, EventArgs e)
		{
			m_tmAdapter.HideBarItemsPopup("tbbAdjustPlaybackSpeedParent");
			m_tmAdapter.HideBarItemsPopup("tbbPlayback");
			CurrentViewsGrid.OnPlayback(null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnDropDownAdjustPlaybackSpeed(object args)
		{
			if (!PaApp.IsFormActive(m_form) || CurrentViewsGrid == null ||
				CurrentViewsGrid.Cache == null)
			{
				return false;
			}

			m_playbackSpeedAdjuster.PlaybackSpeed =
				PaApp.SettingsHandler.GetIntSettingsValue(m_form.GetType().Name, "playbackspeed", 100);

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnDropDownClosedAdjustPlaybackSpeed(object args)
		{
			if (!PaApp.IsFormActive(m_form))
				return false;

			PaApp.SettingsHandler.SaveSettingsValue(m_form.GetType().Name,
				"playbackspeed", m_playbackSpeedAdjuster.PlaybackSpeed);
			
			return true;
		}

		#endregion

		#region CIE (i.e. minimal pair) methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnShowCIEResults(object args)
		{
			if (!PaApp.IsFormActive(m_form) || CurrentViewsGrid == null ||
				CurrentViewsGrid.Cache == null)
			{
				return false;
			}

			CurrentTabGroup.CurrentTab.ToggleCIEView();
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected bool OnUpdateShowCIEResults(object args)
		{
			TMItemProperties itemProps = args as TMItemProperties;
			if (itemProps == null || !PaApp.IsFormActive(m_form))
				return false;

			bool enable = (CurrentViewsGrid != null && CurrentViewsGrid.Cache != null &&
				CurrentViewsGrid.RowCount > 2);

			bool check = (CurrentViewsGrid != null && CurrentViewsGrid.Cache != null &&
				CurrentViewsGrid.Cache.IsCIEList);

			if (itemProps.Enabled != enable || itemProps.Checked != check)
			{
				itemProps.Visible = true;
				itemProps.Enabled = enable;
				itemProps.Checked = check;
				itemProps.Update = true;
			}

			return true;
		}

		#endregion

		#region HTML Export Methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Attempts to export the manager's current grid contents to HTML and returns the
		/// file the html file exported to.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string HTMLExport(Form frm)
		{
			PaWordListGrid grid = CurrentViewsGrid;

			if (!PaApp.IsFormActive(frm) || grid == null)
				return null;

			string queryName = (string.IsNullOrEmpty(grid.Cache.SearchQuery.Name) ?
				string.Empty : grid.Cache.SearchQuery.Name);

			// The query name may just be the pattern and in that case, we won't use it as
			// part of the default output file name. But if all characters in the name
			// are valid, then it will be used as part of the default file name.
			foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
			{
				if (queryName.Contains(invalidChar.ToString()))
				{
					queryName = string.Empty;
					break;
				}
			}

			string defaultHTMLFileName = string.Format(
				Properties.Resources.kstidSearchResultHTMLFileName,
				PaApp.Project.Language, queryName);

			return HTMLGridWriter.Export(grid, defaultHTMLFileName,
				new string[] { queryName, grid.Cache.SearchQuery.Pattern });
		}

		#endregion

		#region IxCoreColleague Members
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Never used in PA.
		/// </summary>
		/// <param name="mediator"></param>
		/// <param name="configurationParameters"></param>
		/// ------------------------------------------------------------------------------------
		public void Init(Mediator mediator, System.Xml.XmlNode configurationParameters)
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the message target.
		/// </summary>
		/// <returns></returns>
		/// ------------------------------------------------------------------------------------
		public IxCoreColleague[] GetMessageTargets()
		{
			return new IxCoreColleague[] { this };
		}

		#endregion

		#region IMessageFilter Members
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool PreFilterMessage(ref Message m)
		{
			// Check for WM_KEYDOWN
			if (m.Msg != 0x100)
				return false;

			if ((int)m.WParam == (int)Keys.M && (Control.ModifierKeys & Keys.Control) > 0 &&
				(Control.ModifierKeys & Keys.Alt) > 0 && m_currTabGroup != null &&
				m_currTabGroup.CurrentTab != null)
			{
				m_currTabGroup.CurrentTab.ShowCIEOptions();
				return true;
			}

			return false;
		}

		#endregion
	}
}