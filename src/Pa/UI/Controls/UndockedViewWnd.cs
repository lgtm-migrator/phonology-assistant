// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2005-2015, SIL International.
// <copyright from='2005' to='2015' company='SIL International'>
//		Copyright (c) 2005-2015, SIL International.
//    
//		This software is distributed under the MIT License, as specified in the LICENSE.txt file.
// </copyright> 
#endregion
// 
using System;
using System.Drawing;
using System.Windows.Forms;
using SIL.FieldWorks.Common.UIAdapters;
using SIL.Pa.Filters;
using SilTools;

namespace SIL.Pa.UI.Controls
{
	/// ----------------------------------------------------------------------------------------
	public partial class UndockedViewWnd : Form, IUndockedViewWnd, IxCoreColleague
	{
		private readonly Control m_view;
		private ITMAdapter m_mainMenuAdapter;
		private bool m_checkForModifiedDataSources = true;
		
		/// ------------------------------------------------------------------------------------
		public UndockedViewWnd()
		{
			InitializeComponent();
		}

		/// ------------------------------------------------------------------------------------
		public UndockedViewWnd(Control view) : this()
		{
			if (view != null)
				Name = view.GetType().Name;

			try
			{
				Properties.Settings.Default[Name] = App.InitializeForm(this, Properties.Settings.Default[Name] as FormSettings);
			}
			catch
			{
				StartPosition = FormStartPosition.CenterScreen;
			}

			m_mainMenuAdapter = App.LoadDefaultMenu(this);
			m_mainMenuAdapter.AllowUpdates = false;
			Controls.Add(view);
			view.BringToFront();
			m_view = view;
			Opacity = 0;

			sblblMain.Text = sblblProgress.Text = string.Empty;
			sblblProgress.Font = FontHelper.MakeFont(FontHelper.UIFont, 9, FontStyle.Bold);
			sblblProgress.Visible = false;
			sbProgress.Visible = false;
			sblblPercent.Visible = false;
			MinimumSize = App.MinimumViewWindowSize;

			sblblFilter.Paint += HandleFilterStatusStripLabelPaint;
			
			if (App.Project != null)
				OnFilterChanged(App.Project.CurrentFilter);

			App.MsgMediator.AddColleague(this);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		/// ------------------------------------------------------------------------------------
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}

			base.Dispose(disposing);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			// Fade-in the undocked form because it looks cool.
			while (Opacity < 1.0)
			{
				try
				{
					System.Threading.Thread.Sleep(10);
					Opacity += 0.05f;
				}
				catch
				{
					try
					{
						Opacity = 1;
					}
					catch { }
				}
				finally
				{
					Utils.UpdateWindow(Handle);
				}
			}

			m_checkForModifiedDataSources = false;
			Activate();
			m_checkForModifiedDataSources = true;
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);

			if (m_mainMenuAdapter != null)
				m_mainMenuAdapter.AllowUpdates = true;

			Invalidate();  // Used to be: Utils.UpdateWindow(Handle); but I'm not sure why. I suspect there was a good reason though.

			if (App.Project != null && m_checkForModifiedDataSources &&
				Properties.Settings.Default.ReloadProjectsWhenAppBecomesActivate)
			{
				App.Project.CheckForModifiedDataSources();
			}
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnDeactivate(EventArgs e)
		{
			base.OnDeactivate(e);

			if (m_mainMenuAdapter != null)
				m_mainMenuAdapter.AllowUpdates = false;
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			App.MsgMediator.RemoveColleague(this);
			Visible = false;
			App.UnloadDefaultMenu(m_mainMenuAdapter);
			m_mainMenuAdapter.Dispose();
			m_mainMenuAdapter = null;
			Controls.Remove(m_view);
			base.OnFormClosing(e);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// When there is no project open, this forces the gradient background to be repainted
		/// on the application workspace.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			if (App.Project == null)
				Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		private void HandleProgressLabelVisibleChanged(object sender, EventArgs e)
		{
			sblblMain.BorderSides = (sblblProgress.Visible ?
				ToolStripStatusLabelBorderSides.Right : ToolStripStatusLabelBorderSides.None);
		}

		/// ------------------------------------------------------------------------------------
		private void HandleFilterStatusStripLabelPaint(object sender, PaintEventArgs e)
		{
			if (App.Project != null && App.Project.CurrentFilter != null)
			{
				PaMainWnd.PaintFilterStatusStripLabel(sender as ToolStripStatusLabel,
					App.Project.CurrentFilter.Name, e);
			}
		}
	
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the status bar.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public StatusStrip StatusBar
		{
			get { return statusStrip; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the status bar label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ToolStripStatusLabel StatusBarLabel
		{
			get { return sblblMain; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the progress bar.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ToolStripProgressBar ProgressBar
		{
			get { return sbProgress; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the progress bar's label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ToolStripStatusLabel ProgressBarLabel
		{
			get { return sblblProgress; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the status bar label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ToolStripStatusLabel ProgressPercentLabel
		{
			get { return sblblPercent; }
		}

		/// ------------------------------------------------------------------------------------
		protected bool OnFilterChanged(object args)
		{
			var filter = args as Filter;
			sblblFilter.Visible = (filter != null);
			if (filter != null)
			{
				sblblFilter.Text = filter.Name;
				var constraint = new Size(statusStrip.Width / 3, 0);
				sblblFilter.Width = sblblFilter.GetPreferredSize(constraint).Width + 20;
			}

			return false;
		}

		/// ------------------------------------------------------------------------------------
		protected bool OnFilterTurnedOff(object args)
		{
			sblblFilter.Visible = false;
			sblblFilter.Text = string.Empty;
			return false;
		}

		#region IxCoreColleague Members
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
	}
}