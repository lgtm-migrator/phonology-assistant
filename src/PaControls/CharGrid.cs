using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using SIL.SpeechTools.Utils;
using SIL.Pa.Data;
using SIL.FieldWorks.Common.UIAdapters;
using XCore;

namespace SIL.Pa.Controls
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Encapsulates a character grid (for vowel and consonants) with special headers (for
	/// columns and rows) that can span multiple columns in the DataGridView.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public partial class CharGrid : UserControl, IxCoreColleague
	{
		private const string kDropTargetCell = "dtc";

		internal static Color kGridColor = 
				ColorHelper.CalculateColor(SystemColors.Window, SystemColors.GrayText, 70);

		public event ItemDragEventHandler ItemDrag;

		private const int kCellHeight = 34;
		private const int kMinHdrSize = 22;

		private int m_cellWidth = 38;
		private List<CharGridHeader> m_colHdrs;
		private List<CharGridHeader> m_rowHdrs;
		private CharGridHeader m_currentHeader = null;
		private bool m_searchWhenPhoneDoubleClicked = true;
		private Point m_mouseDownGridLocation = Point.Empty;
		private CharGridHeaderCollectionPanel m_pnlColHeaders;
		private CharGridHeaderCollectionPanel m_pnlRowHeaders;
		private DataGridViewCell m_cellDraggedOver;
		private string m_phoneBeingDragged = null;
		private Font m_chartFont;
		private ITMAdapter m_tmAdapter;
		private bool m_showUncertainPhones = false;
		private string m_supraSegsToIgnore = PhoneCache.kDefaultChartSupraSegsToIgnore;
		private CharGridHeader m_currentRowHeader = null;
		private CharGridHeader m_currentColHeader = null;
		private Type m_owningViewType = null;
		private PhoneInfoPopup m_phoneInfoPopup;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGrid()
		{
			InitializeComponent();

			SuspendLayout();
			DoubleBuffered = true;
			m_rowHdrs = new List<CharGridHeader>();
			m_colHdrs = new List<CharGridHeader>();
			m_chartFont = new Font(FontHelper.PhoneticFont.FontFamily, 14, GraphicsUnit.Point);
			m_grid.Font = m_chartFont;
			m_grid.GridColor = kGridColor;

			m_pnlColHeaders = new CharGridHeaderCollectionPanel(true);
			pnlColHeaderOuter.Controls.Add(m_pnlColHeaders);

			m_pnlRowHeaders = new CharGridHeaderCollectionPanel(false);
			pnlRowHeaderOuter.Controls.Add(m_pnlRowHeaders);

			m_phoneInfoPopup = new PhoneInfoPopup(m_grid);
			ResumeLayout(false);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Clear's the grid and all the headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void Reset()
		{
			m_currentColHeader = null;
			m_currentRowHeader = null;
			m_grid.Rows.Clear();
			m_grid.Columns.Clear();
			m_pnlRowHeaders.Controls.Clear();
			m_rowHdrs.Clear();
			m_pnlColHeaders.Controls.Clear();
			m_colHdrs.Clear();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Calls the grid's CurrentCellChanged event even if the current cell hasn't changed.
		/// This is useful to force a selection painting of the current cell's row and column
		/// heading.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void ForceCurrentCellUpdate()
		{
			m_grid_CurrentCellChanged(null, null);
		}

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the font used in the chart.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public Font ChartFont
		{
			get { return m_chartFont; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets a value indicating whether or not the column and row headers are
		/// visible.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool HeadersVisible
		{
			get { return pnlColHeaderOuter.Visible; }
			set
			{
				pnlColHeaderOuter.Visible = value;
				pnlRowHeaderOuter.Visible = value;
				m_vsplitter.Visible = value;
				m_hsplitter.Visible = value;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the width of each cell in the grid. This should be set before the
		/// chart is loaded with phones.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public int CellWidth
		{
			get { return m_cellWidth; }
			set { m_cellWidth = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets a value indicating whether or not a default search is performed for
		/// a phone when the cell it's in is double-clicked.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public bool SearchWhenPhoneDoubleClicked
		{
			get { return m_searchWhenPhoneDoubleClicked; }
			set { m_searchWhenPhoneDoubleClicked = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the toolbar/menu adapter for the chart.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ITMAdapter TMAdapter
		{
			get { return m_tmAdapter; }
			set {m_tmAdapter = value;}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the owning view type.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Type OwningViewType
		{
			get { return m_owningViewType; }
			set { m_owningViewType = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the chart's grid control.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public DataGridView Grid
		{
			get { return m_grid; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the current phone in the chart.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string CurrentPhone
		{
			get 
			{
				CharGridCell cell = (m_grid.CurrentCell == null ?
					null : m_grid.CurrentCell.Value as CharGridCell);

				return (cell == null || string.IsNullOrEmpty(cell.Phone) ? null : cell.Phone);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the collection of selected phones phone in the chart.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string[] SelectedPhones
		{
			get
			{
				List<string> phones = new List<string>();

				if (m_grid.SelectedCells == null || m_grid.SelectedCells.Count == 0)
				{
					string currPhone = CurrentPhone;
					if (!string.IsNullOrEmpty(currPhone))
						phones.Add(CurrentPhone);
				}
				else
				{
					foreach (DataGridViewCell dgvCell in m_grid.SelectedCells)
					{
						CharGridCell cell = dgvCell.Value as CharGridCell;
						if (cell != null && !string.IsNullOrEmpty(cell.Phone))
							phones.Add(cell.Phone);
					}
				}

				return (phones.Count == 0 ? null : phones.ToArray());
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the panel that owns the collection of column header controls.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public CharGridHeaderCollectionPanel ColumnHeadersCollectionPanel
		{
			get { return m_pnlColHeaders; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the panel that owns the collection of row header controls.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public CharGridHeaderCollectionPanel RowHeadersCollectionPanel
		{
			get { return m_pnlRowHeaders; }
		}
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the collection of row headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public List<CharGridHeader> RowHeaders
		{
			get { return m_rowHdrs; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the collection of column headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public List<CharGridHeader> ColumnHeaders
		{
			get { return m_colHdrs; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the row header width.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int RowHeaderWidth
		{
			get { return pnlRowHeaderOuter.Width; }
			set
			{
				pnlRowHeaderOuter.Width = (value < kMinHdrSize ? kMinHdrSize : value);
				m_pnlRowHeaders.Width = pnlRowHeaderOuter.Width;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the column header height.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int ColumnHeaderHeight
		{
			get { return pnlColHeaderOuter.Height; }
			set
			{
				pnlColHeaderOuter.Height = (value < kMinHdrSize ? kMinHdrSize : value);
				m_pnlColHeaders.Height = pnlColHeaderOuter.Height;
			}
		}

		/// --------------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets a value indicating whether or not to show uncertain phones in the chart.
		/// </summary>
		/// --------------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool ShowUncertainPhones
		{
			get { return m_showUncertainPhones; }
			set { m_showUncertainPhones = value; }
		}

		/// --------------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the list of suprasegmentals to ignore.
		/// </summary>
		/// --------------------------------------------------------------------------------------------
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string SupraSegsToIgnore
		{
			get { return m_supraSegsToIgnore; }
			set { m_supraSegsToIgnore = value; }
		}

		#endregion

		#region Save/Restore settings
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			if (!PaApp.DesignMode)
			{
				if (m_tmAdapter != null)
					m_tmAdapter.SetContextMenuForControl(m_grid, "cmnuCharChartGrid");

				PaApp.AddMediatorColleague(this);
				m_pnlRowHeaders.Width = pnlRowHeaderOuter.Width;
				m_pnlColHeaders.Height = pnlColHeaderOuter.Height;
				Adjust();

				m_grid_CurrentCellChanged(null, null);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Save the header sizes.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnHandleDestroyed(EventArgs e)
		{
			PaApp.RemoveMediatorColleague(this);
			base.OnHandleDestroyed(e);
		}

		#endregion

		#region Misc. Methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the group the specified row is contained in.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public int GetRowsGroup(int rowIndex)
		{
			CharGridHeader hdr = GetRowsHeader(rowIndex);
			return (hdr == null ? -1 : hdr.Group);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets header that owns the row specified by the supplied row index.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader GetRowsHeader(int rowIndex)
		{
			if (rowIndex >= 0 && rowIndex < m_grid.Rows.Count)
			{
				foreach (CharGridHeader hdr in m_rowHdrs)
				{
					foreach (DataGridViewRow row in hdr.OwnedRows)
					{
						if (row.Index == rowIndex)
							return hdr;
					}
				}
			}

			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets header that owns the column specified by the supplied column index.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader GetColumnsHeader(int colIndex)
		{
			if (colIndex >= 0 && colIndex < m_grid.Columns.Count)
			{
				foreach (CharGridHeader hdr in m_colHdrs)
				{
					foreach (DataGridViewColumn col in hdr.OwnedColumns)
					{
						if (col.Index == colIndex)
							return hdr;
					}
				}
			}

			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes a single row from the CharGrid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void RemoveRow(int rowIndex)
		{
			if (rowIndex < 0 || rowIndex >= m_grid.Rows.Count)
				return;

			CharGridHeader hdr = m_grid.Rows[rowIndex].Tag as CharGridHeader;
			if (hdr == null)
				return;

			// If there's only one row left then remove the entire heading too.
			if (hdr.OwnedRows.Count == 1)
				RemoveRowHeader(hdr);
			else
			{
				hdr.RemoveRow(m_grid.Rows[rowIndex]);
				m_grid.Rows.RemoveAt(rowIndex);
				m_grid.Refresh();
				CalcHeights();
				AdjustRowHeadingLocation();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes a single column from the CharGrid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void RemoveColumn(int colIndex)
		{
			if (colIndex < 0 || colIndex >= m_grid.Columns.Count)
				return;

			CharGridHeader hdr = m_grid.Columns[colIndex].Tag as CharGridHeader;
			if (hdr == null)
				return;

			// If there's only one column left then remove the entire heading too.
			if (hdr.OwnedColumns.Count == 1)
				RemoveColumnHeader(hdr);
			else
			{
				hdr.RemoveColumn(m_grid.Columns[colIndex]);
				m_grid.Columns.RemoveAt(colIndex);
				m_grid.Refresh();
				CalcWidths();
				AdjustColumnHeadingLocation();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes the specified row header from the CharGrid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void RemoveRowHeader(CharGridHeader hdr)
		{
			if (hdr != null)
			{
				m_currentRowHeader = null;
				hdr.RemoveOwnedRows();
				m_rowHdrs.Remove(hdr);
				m_pnlRowHeaders.Controls.Remove(hdr);
				m_grid.Refresh();
				CalcHeights();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes the specified column header from the CharGrid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void RemoveColumnHeader(CharGridHeader hdr)
		{
			if (hdr != null)
			{
				m_currentColHeader = null;
				hdr.RemoveOwnedColumns();
				m_colHdrs.Remove(hdr);
				m_pnlColHeaders.Controls.Remove(hdr);
				m_grid.Refresh();
				CalcWidths();
			}
		}

		#endregion

		#region Adding/Creating Rows
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new row without a label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddRowHeader()
		{
			return AddRowHeader(string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new row with a label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddRowHeader(string text)
		{
			return AddRowHeader(text, string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new row with a label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddRowHeader(string text, string subheadtext)
		{
			CharGridHeader hdr = CreateRowHeader(text, -1);
			AddRowToHeading(hdr, subheadtext);
			return hdr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new row to the grid under the specified heading.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataGridViewRow AddRowToHeading(CharGridHeader hdr)
		{
			return AddRowToHeading(hdr, string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new row to the grid under the specified heading.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataGridViewRow AddRowToHeading(CharGridHeader hdr, string subheadtext)
		{
			if (hdr == null)
				return null;

			int insertRowIndex = (hdr.LastRow == null ? m_grid.Rows.Count : hdr.LastRow.Index + 1);
			m_grid.Rows.Insert(insertRowIndex, new DataGridViewRow());
			DataGridViewRow newRow = m_grid.Rows[insertRowIndex];
			newRow.Height = kCellHeight;
			hdr.AddRow(newRow, subheadtext);
			CalcHeights();
			return newRow;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new row header.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private CharGridHeader CreateRowHeader(string text, int insertIndex)
		{
			m_pnlRowHeaders.SuspendLayout();

			CharGridHeader newHdr = new CharGridHeader(text, false);
			newHdr.Height = kCellHeight;

			if (insertIndex == -1)
			{
				m_rowHdrs.Add(newHdr);
				m_pnlRowHeaders.Controls.Add(newHdr);
				newHdr.BringToFront();
			}
			else
			{
				m_rowHdrs.Insert(insertIndex, newHdr);
				m_pnlRowHeaders.Controls.Clear();
				foreach (CharGridHeader hdr in m_rowHdrs)
				{
					m_pnlRowHeaders.Controls.Add(hdr);
					hdr.BringToFront();
				}
			}

			CalcHeights();
			m_pnlRowHeaders.ResumeLayout();
			return newHdr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Calculate what the height should be of the panel that owns all the row headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void CalcHeights()
		{
			int height = 0;
			foreach (Control ctrl in m_pnlRowHeaders.Controls)
				height += ctrl.Height;

			m_pnlRowHeaders.Height = height;
			m_grid.Height = height + 1;
		}

		#endregion

		#region Adding/Creating Columns
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new column header without a label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddColumnHeader()
		{
			return AddColumnHeader(string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new column header without a label.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddColumnHeader(string text)
		{
			return AddColumnHeader(text, string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new column header label and a single grid column it owns.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridHeader AddColumnHeader(string text, string subheadtext)
		{
			CharGridHeader hdr = CreateColumnHeader(text, -1);
			AddColumnToHeading(hdr, subheadtext);
			return hdr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new column to the grid under the specified heading.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataGridViewColumn AddColumnToHeading(CharGridHeader hdr)
		{
			return AddColumnToHeading(hdr, string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds a new column to the grid under the specified heading.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataGridViewColumn AddColumnToHeading(CharGridHeader hdr, string subheadtext)
		{
			if (hdr == null)
				return null;

			int insertColIndex = (hdr.LastColumn == null ?
				m_grid.Columns.Count : hdr.LastColumn.Index + 1);
			
			DataGridViewColumn newCol = CreateColumn();
			hdr.AddColumn(newCol, subheadtext);
			CalcWidths();
			m_grid.Columns.Insert(insertColIndex, newCol);
			return m_grid.Columns[insertColIndex];
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates a new header.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private CharGridHeader CreateColumnHeader(string text, int insertIndex)
		{
			m_pnlColHeaders.SuspendLayout();
			CharGridHeader newHdr = new CharGridHeader(text, true);
			newHdr.Width = m_cellWidth;

			if (insertIndex == -1)
			{
				m_colHdrs.Add(newHdr);
				m_pnlColHeaders.Controls.Add(newHdr);
				newHdr.BringToFront();
			}
			else
			{
				m_colHdrs.Insert(insertIndex, newHdr);
				m_pnlColHeaders.Controls.Clear();
				foreach (CharGridHeader hdr in m_colHdrs)
				{
					m_pnlColHeaders.Controls.Add(hdr);
					hdr.BringToFront();
				}
			}

			CalcWidths();
			m_pnlColHeaders.ResumeLayout(); ;
			return newHdr;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Create a new grid column.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataGridViewColumn CreateColumn()
		{
			string colName = string.Format("col{0}", m_grid.Columns.Count);
			DataGridViewColumn col = SilGrid.CreateTextBoxColumn(colName);
			col.CellTemplate.Style.Font = m_chartFont;
			col.Width = m_cellWidth;
			return col;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Calculate what the width should be of the panel that owns all the column headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void CalcWidths()
		{
			int width = 0;
			foreach (Control ctrl in m_pnlColHeaders.Controls)
				width += ctrl.Width;

			m_pnlColHeaders.Width = width;
			m_grid.Width = width + 1;

			if (m_grid.Rows.Count > 0)
				m_grid.Height = (m_grid.Rows.Count * kCellHeight) + 1;
		}

		#endregion

		#region Methods for adjusting the size and location of the heading panels
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Moves the grid to the proper location relative to the splitters.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void Adjust()
		{
			if (pnlCorner.Width != pnlRowHeaderOuter.Width)
			{
				pnlCorner.Width = pnlRowHeaderOuter.Width + m_vsplitter.Width;
				pnlCorner.Invalidate();
			}

			m_pnlRowHeaders.Width = pnlRowHeaderOuter.Width;
			m_pnlColHeaders.Height = pnlColHeaderOuter.Height;
			
			Point ptv = pnlWrapper.PointToScreen(new Point(m_vsplitter.SplitPosition, 0));
			ptv = pnlGrid.PointToClient(ptv);

			Point pth = pnlWrapper.PointToScreen(new Point(0, m_hsplitter.SplitPosition));
			pth = pnlGrid.PointToClient(pth);

			m_grid.Location = new Point(ptv.X + m_vsplitter.Width - 1,
				pth.Y + m_hsplitter.Height - 1);

			AdjustColumnHeadingLocation();
			AdjustRowHeadingLocation();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void AdjustColumnHeadingLocation()
		{
			Point pt = pnlGrid.PointToScreen(m_grid.Location);
			pt = pnlColHeaderOuter.PointToClient(pt);
			m_pnlColHeaders.Left = pt.X + 1;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void AdjustRowHeadingLocation()
		{
			Point pt = pnlGrid.PointToScreen(m_grid.Location);
			pt = pnlRowHeaderOuter.PointToClient(pt);
			m_pnlRowHeaders.Top = pt.Y + 1;
		}

		#endregion

		#region Misc. event methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure the height of the panel that owns the column header is adjust as the
		/// splitter below it moves.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandleSplitterMoved(object sender, SplitterEventArgs e)
		{
			Adjust();
			m_hsplitter.Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure new rows have their heights set properly.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
		{
			foreach (DataGridViewRow row in m_grid.Rows)
			{
				if (row.Height != kCellHeight)
					row.Height = kCellHeight;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure the proper row and column header is selected as the current cell changes.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CurrentCellChanged(object sender, EventArgs e)
		{
			if (m_grid.CurrentCell == null)
				return;

			// This will need to be added back in when moving rows and columns is added.
			//bool allowed = (m_grid.CurrentCell != null &&
			//    m_grid.CurrentCell.RowIndex < m_grid.Rows.Count - 2);
			////PaApp.MsgMediator.SendMessage("UpdateMoveCharChartRowDown", allowed);

			//allowed = (m_grid.CurrentCell != null &&
			//    m_grid.CurrentCell.RowIndex > 0);
			////PaApp.MsgMediator.SendMessage("UpdateMoveCharChartRowUp", allowed);

			// Check if the current row header changed.
			CharGridHeader hdr = GetRowsHeader(m_grid.CurrentCell.RowIndex);
			if (hdr != m_currentRowHeader)
			{
				if (m_currentRowHeader != null)
					m_currentRowHeader.Selected = false;

				m_currentRowHeader = hdr;
				m_currentRowHeader.Selected = true;
			}

			// Check if the current column header changed.
			hdr = GetColumnsHeader(m_grid.CurrentCell.ColumnIndex);
			if (hdr != m_currentColHeader)
			{
				if (m_currentColHeader != null)
					m_currentColHeader.Selected = false;

				m_currentColHeader = hdr;
				m_currentColHeader.Selected = true;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Perform a search on the phone when it's double-clicked on.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (m_searchWhenPhoneDoubleClicked && !string.IsNullOrEmpty(CurrentPhone))
				PaApp.MsgMediator.SendMessage("ChartPhoneSearchAnywhere", null);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Removes all empty rows and columns.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void RemoveAllEmptyRowsAndColumns()
		{
			// Remove empty rows.
			for (int r = m_grid.Rows.Count - 1; r >= 0; r--)
			{
				if (IsRowEmtpy(r))
					RemoveRow(r);
			}

			// Remove empty columns.
			for (int c = m_grid.Columns.Count - 1; c >= 0; c--)
			{
				if (IsColumnEmtpy(c))
					RemoveColumn(c);
			}
		}

		#endregion

		#region Painting methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Custom paint cells being dragged over when dragging phones around.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			// Only paint the row headings and the first row.
			if (e.ColumnIndex >= 0 && e.RowIndex >= 0 &&
				(m_grid[e.ColumnIndex, e.RowIndex].Tag as string) == kDropTargetCell)
			{
				Color clr = ColorHelper.CalculateColor(SystemColors.WindowText,
					SystemColors.Window, 65);

				TextFormatFlags flags = TextFormatFlags.HorizontalCenter |
					TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
					TextFormatFlags.NoPrefix;

				TextRenderer.DrawText(e.Graphics, m_phoneBeingDragged,
					m_chartFont, e.CellBounds, clr, flags);

				e.Handled = true;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void pnlCorner_Paint(object sender, PaintEventArgs e)
		{
			Rectangle rc = pnlCorner.ClientRectangle;
			Point pt1 = new Point(rc.Width - 4, 0);
			Point pt2 = new Point(rc.Width - 4, rc.Bottom - 1);

			// Draw a double vertical line to the left of the first column heading
			// (which is the right edge of the top, left corner panel).
			using (LinearGradientBrush br =	new LinearGradientBrush(pt1, pt2,
				CharGrid.kGridColor, SystemColors.GrayText))
			{
				using (Pen pen = new Pen(br))
				{
					e.Graphics.DrawLine(pen, pt1, pt2);
					pt1.X += 3;
					pt2.X += 3;
					e.Graphics.DrawLine(pen, pt1, pt2);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Paint double lines on the horizontal splitter. From the left edge of the splitter
		/// to where the vertical splitter begins will only be a single line (i.e. the line
		/// the separates the row headers from the empty area (corner panel) above the row
		/// headers and to the left of the column headers).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_hsplitter_Paint(object sender, PaintEventArgs e)
		{
			Rectangle rc = m_hsplitter.ClientRectangle;
			int left = m_vsplitter.SplitPosition;

			// Draw the top line of the double dark line on the bottom edge.
			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(0, 0), new Point(left, 0));

			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(left + 3, 0), new Point(rc.Right - 1, 0));

			// Draw the bottom line of the double dark line on the bottom edge.
			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(0, rc.Bottom - 1), new Point(left, rc.Bottom - 1));

			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(left + 3, rc.Bottom - 1), new Point(rc.Right - 1, rc.Bottom - 1));
		}
		
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_vsplitter_Paint(object sender, PaintEventArgs e)
		{
			Rectangle rc = m_vsplitter.ClientRectangle;

			// Draw the top line of the double dark line on the bottom edge.
			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(0, 0), new Point(0, rc.Bottom - 1));

			// Draw the bottom line of the double dark line on the bottom edge.
			e.Graphics.DrawLine(SystemPens.GrayText,
				new Point(rc.Right - 1, 0), new Point(rc.Right - 1, rc.Bottom - 1));
		}

		#endregion

		#region Methods for grid's phone information popup
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
		{
			// This will not be empty when the mouse button is down.
			if (!m_mouseDownGridLocation.IsEmpty || e.ColumnIndex < 0 || e.RowIndex < 0 || 
				(m_grid[e.ColumnIndex, e.RowIndex].Value as CharGridCell) == null ||
				!PaApp.IsFormActive(FindForm()))
			{
				return;
			}

			Rectangle rc = m_grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
			CharGridCell cgc = m_grid[e.ColumnIndex, e.RowIndex].Value as CharGridCell;
			if (m_phoneInfoPopup.Initialize(cgc.Phone, m_grid[e.ColumnIndex, e.RowIndex]))
				m_phoneInfoPopup.Show(rc);
		}

		#endregion

		#region Grid Drag/Drop Methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
				m_mouseDownGridLocation = e.Location;
			else if (e.Button == MouseButtons.Right && e.ColumnIndex >= 0 && e.RowIndex >= 0)
			{
				m_grid.CurrentCell = m_grid[e.ColumnIndex, e.RowIndex];
				if (!m_grid.Focused)
					m_grid.Focus();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
		{
			m_mouseDownGridLocation = Point.Empty; 
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
		{
			// This will be empty when the mouse button is not down.
			if (m_mouseDownGridLocation.IsEmpty || e.ColumnIndex < 0 || e.RowIndex < 0 || 
				(m_grid[e.ColumnIndex, e.RowIndex].Value as CharGridCell) == null)
			{
				return;
			}

			// Begin draging a cell when the mouse is held down
			// and has moved 4 or more pixels in any direction.
			int dx = Math.Abs(m_mouseDownGridLocation.X - e.X);
			int dy = Math.Abs(m_mouseDownGridLocation.Y - e.Y);
			if (dx >= 4 || dy >= 4)
			{
				m_mouseDownGridLocation = Point.Empty;
				DataGridViewCell cell = m_grid[e.ColumnIndex, e.RowIndex];
			
				// When someone has subscribed to the drag event (which is really more like
				// a begin drag event) then call that. Otherwise, start a drag, drop event
				// within the grid.
				if (ItemDrag != null)
				{
					ItemDragEventArgs args = new ItemDragEventArgs(e.Button, cell.Value);
					ItemDrag(this, args);
				}
				else
				{
					m_phoneBeingDragged = ((CharGridCell)cell.Value).Phone;
					m_cellDraggedOver = cell;
					m_grid.DoDragDrop(m_grid[e.ColumnIndex, e.RowIndex], DragDropEffects.Move);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_DragOver(object sender, DragEventArgs e)
		{
			// Get the cell being dragged.
			DataGridViewTextBoxCell draggedCell =
				e.Data.GetData(typeof(DataGridViewTextBoxCell)) as DataGridViewTextBoxCell;

			Point pt = m_grid.PointToClient(new Point(e.X, e.Y));
			DataGridView.HitTestInfo hinfo = m_grid.HitTest(pt.X, pt.Y);

			// Drop is not allowed if we can't determine what cell we're over.
			if (hinfo.ColumnIndex < 0 || hinfo.ColumnIndex >= m_grid.Columns.Count ||
				hinfo.RowIndex < 0 || hinfo.RowIndex >= m_grid.Rows.Count || draggedCell == null)
			{
				e.Effect = DragDropEffects.None;
				return;
			}

			// Can't drop on a cell that already has query in it.
			DataGridViewTextBoxCell cellOver = m_grid[hinfo.ColumnIndex, hinfo.RowIndex] as
				DataGridViewTextBoxCell;

			// If we're dragging over a cell that hasn't been given a drag-over background,
			// then repaint the cell that used to have a drag-over background so it has a
			// regular background.
			if (m_cellDraggedOver != null && m_cellDraggedOver != cellOver)
			{
				m_cellDraggedOver.Tag = null;
				m_grid.InvalidateCell(m_cellDraggedOver);
			}

			if (cellOver == null || (cellOver.Value as CharGridCell) != null)
			{
				e.Effect = DragDropEffects.None;
				return;
			}

			e.Effect = e.AllowedEffect;

			// If we're dragging over a cell that hasn't been given a drag-over background,
			// then repaint the cell being dragged over so it has a drag-over background.
			if (m_cellDraggedOver != cellOver)
			{
				m_cellDraggedOver = cellOver;
				m_cellDraggedOver.Tag = kDropTargetCell;
				m_grid.InvalidateCell(m_cellDraggedOver);
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_DragDrop(object sender, DragEventArgs e)
		{
			if (m_cellDraggedOver != null)
			{
				m_cellDraggedOver.Tag = null;
				m_grid.InvalidateCell(m_cellDraggedOver);
			}

			m_cellDraggedOver = null;

			// Get the cell being dragged.
			DataGridViewTextBoxCell draggedCell =
				e.Data.GetData(typeof(DataGridViewTextBoxCell)) as DataGridViewTextBoxCell;

			Point pt = m_grid.PointToClient(new Point(e.X, e.Y));
			DataGridView.HitTestInfo hinfo = m_grid.HitTest(pt.X, pt.Y);

			// Get the cell we're dropping on.
			DataGridViewTextBoxCell droppedOnCell = null;
			if (hinfo.ColumnIndex >= 0 && hinfo.ColumnIndex < m_grid.Columns.Count &&
				hinfo.RowIndex >= 0 && hinfo.RowIndex < m_grid.Rows.Count && draggedCell != null)
			{
				droppedOnCell = m_grid[hinfo.ColumnIndex, hinfo.RowIndex] as
					DataGridViewTextBoxCell;
			}

			if (droppedOnCell != null)
			{
				droppedOnCell.Value = draggedCell.Value;
				draggedCell.Value = null;
				m_grid.CurrentCell = droppedOnCell;
			}
		}

		#endregion

		#region Methods for grid panel's scrolling and resizing
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure the headers scroll with the grid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void m_grid_LocationChanged(object sender, EventArgs e)
		{
			AdjustRowHeadingLocation();
			AdjustColumnHeadingLocation();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adjust the location of the panel that owns all the column headers.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void pnlGrid_Resize(object sender, EventArgs e)
		{
			AdjustRowHeadingLocation();
			AdjustColumnHeadingLocation();
		}

		#endregion
	}

	#region CharGridCell Class
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// 
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[XmlType("Phone")]
	public class CharGridCell
	{
		private string m_phone;
		private bool m_visible = true;
		private bool m_isUncertain = false;
		private bool m_isPlacedOnChart = false;
		private int m_defaultCol = -1;
		private int m_defaultGroup = -1;
		private int m_row = -1;
		private int m_col = -1;
		private int m_group = -1;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// This constructor is for serialization/deserialization.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridCell()
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridCell(string phone)
		{
			m_phone = phone;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public CharGridCell(string phone, bool visible) : this(phone)
		{
			m_visible = visible;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns the cell's phone.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override string ToString()
		{
			return (m_visible ? m_phone : null);
		}

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlAttribute("text")]
		public string Phone
		{
			get { return m_phone; }
			set { m_phone = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlAttribute]
		public bool Visible
		{
			get { return m_visible; }
			set { m_visible = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlAttribute]
		public int Row
		{
			get { return m_row; }
			set { m_row = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlAttribute]
		public int Column
		{
			get { return m_col; }
			set { m_col = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlAttribute]
		public int Group
		{
			get { return m_group; }
			set { m_group = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public int DefaultColumn
		{
			get { return m_defaultCol; }
			set { m_defaultCol = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public int DefaultGroup
		{
			get { return m_defaultGroup; }
			set { m_defaultGroup = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public bool IsPlacedOnChart
		{
			get { return m_isPlacedOnChart; }
			set { m_isPlacedOnChart = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public bool IsUncertain
		{
			get { return m_isUncertain; }
			set { m_isUncertain = value; }
		}

		#endregion
	}

	#endregion
}