using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SIL.Pa.Model;
using SilTools;

namespace SIL.Pa.UI.Controls
{
	public partial class ChartOptionsDropDown : UserControl
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public ChartOptionsDropDown(string ignoreList)
		{
			InitializeComponent();
			lblSSegsToIgnore.Font = FontHelper.UIFont;
			lnkRefresh.Font = FontHelper.UIFont;
			lnkHelp.Font = FontHelper.UIFont;

			float fontSize = Math.Min(17, SystemInformation.MenuFont.SizeInPoints * 2);

			pickerIgnore.Font =	FontHelper.MakeRegularFontDerivative(App.PhoneticFont, fontSize);
			
			pickerIgnore.ItemSize = new Size(pickerIgnore.PreferredItemHeight,
				pickerIgnore.PreferredItemHeight);
			
			pickerIgnore.LoadCharacters(ci => !ci.IsBase && ci.SubType != IPASymbolSubType.notApplicable);

			SetIgnoredChars(ignoreList);

			// Adjust the size of the drop-down to fit 5 columns.
			Width = pickerIgnore.GetPreferredWidth(5) + pnlPicker.Padding.Left + pnlPicker.Padding.Right;
			Height = pickerIgnore.PreferredHeight + pnlPicker.Top + Padding.Bottom;

			// Center the refresh and help labels vertically between the bottom of the
			// drop-down and the bottom of the picker.
			lnkRefresh.Top = ClientSize.Height -
				((ClientSize.Height - pnlPicker.Bottom) / 2) - (lnkRefresh.Height / 2);

			lnkHelp.Top = lnkRefresh.Top;
			lnkHelp.Left = ClientRectangle.Right - lnkHelp.Width - 10;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a string containing all the characters of the checked buttons in the
		/// specified chooser.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string GetIgnoredChars()
		{
			StringBuilder ignoreList = new StringBuilder();
			foreach (ToolStripButton item in pickerIgnore.Items)
			{
				if (item.Checked)
					ignoreList.Append(item.Text.Replace(App.kDottedCircle, string.Empty));
			}

			return (ignoreList.ToString());
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void SetIgnoredChars(string ignoreList)
		{
			foreach (ToolStripButton item in pickerIgnore.Items)
			{
				// Remove the dotted circle (if there is one) from the button's text, then
				// check the button if its text is found in the ignore list.
				string chr = item.Text.Replace(App.kDottedCircle, string.Empty);
				item.Checked = (ignoreList != null && ignoreList.Contains(chr));
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			App.ShowHelpTopic("hidIgnoredSuprasegmentalsPopup");
		}
	}
}
