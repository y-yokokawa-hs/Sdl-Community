﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Sdl.Community.projectAnonymizer.Batch_Task;
using Sdl.Community.projectAnonymizer.Helpers;
using Sdl.Community.projectAnonymizer.Models;
using Sdl.Community.projectAnonymizer.Process_Xliff;
using Sdl.Desktop.IntegrationApi;

namespace Sdl.Community.projectAnonymizer.Ui
{
	public partial class AnonymizerSettingsControl : UserControl, ISettingsAware<AnonymizerSettings>
	{
		private BindingList<RegexPattern> regexPatterns;

		public AnonymizerSettingsControl()
		{
			InitializeComponent();
		}

		public string EncryptionKey
		{
			get => encryptionBox.Text;
			set => encryptionBox.Text = value;
		}

		public bool SelectAll
		{
			get => selectAll.Checked;
			set => selectAll.Checked = value;
		}

		public BindingList<RegexPattern> RegexPatterns
		{
			get
			{
				expressionsGrid.EndEdit();
				foreach (var pattern in regexPatterns)
				{
					if (string.IsNullOrEmpty(pattern.Id))
					{
						pattern.Id = Guid.NewGuid().ToString();
					}
				}

				return regexPatterns;
			}
			set
			{
				regexPatterns = value;
			}
		}

		public AnonymizerSettings Settings { get; set; }

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			//create tooltips for buttons
			var exportTooltip = new ToolTip();
			exportTooltip.SetToolTip(exportBtn, "Export selected expressions to disk");
			var importTooltip = new ToolTip();
			importTooltip.SetToolTip(importBtn, "Import regular expressions in to current project");

			//labels text
			descriptionLbl.Text = Constants.GetGridDescription();
			encryptionLbl.Text = Constants.GetKeyDescription();

			expressionsGrid.AutoGenerateColumns = false;
			var exportHeaderCell = new CustomColumnHeader
			{
				IsChecked = Settings.EnableAll
			};
			exportHeaderCell.OnCheckBoxHeaderClick += ExportHeaderCell_OnCheckBoxHeaderClick;
			var exportColumn = new DataGridViewCheckBoxColumn
			{
				Width = 100,
				HeaderText = @"Enable?",
				DataPropertyName = "ShouldEnable",
				Name = "Enable",
				HeaderCell = exportHeaderCell
			};

			var encryptHeaderCell = new CustomColumnHeader
			{
				IsChecked = Settings.EncryptAll
			};
			encryptHeaderCell.OnCheckBoxHeaderClick += EncryptHeaderCell_OnCheckBoxHeaderClick;
			var shouldEncryptColumn = new DataGridViewCheckBoxColumn
			{
				HeaderText = @"Encrypt?",
				Width = 110,
				DataPropertyName = "ShouldEncrypt",
				HeaderCell = encryptHeaderCell,
				Name = "Encrypt"
			};
			expressionsGrid.Columns.Add(exportColumn);
			var pattern = new DataGridViewTextBoxColumn
			{
				HeaderText = @"Regex Pattern",
				DataPropertyName = "Pattern",
				AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
			};
			expressionsGrid.Columns.Add(pattern);
			var description = new DataGridViewTextBoxColumn
			{
				HeaderText = @"Description",
				DataPropertyName = "Description",
				AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
			};
			expressionsGrid.Columns.Add(description);
			expressionsGrid.Columns.Add(shouldEncryptColumn);

			ReadExistingExpressions();
			SetSettings(Settings);

			if (Settings.EncryptionState == State.DefaultState)
			{
				Settings.EncryptionState = IsProjectAnonymized() ? State.DataEncrypted : State.Decrypted;
			}

			if ((Settings.EncryptionState & State.DataEncrypted) != 0)
			{
				//if (Settings.IsOldVersion ?? false)
				//{
				//	encryptedMessage.Text = "Old version of Anonymizer was used on this project. Unprotect Data before proceeding.";
				//}
				mainPanel.Visible = false;
				encryptedPanel.Visible = true;
			}
		}

		private void EncryptHeaderCell_OnCheckBoxHeaderClick(CheckBoxHeaderCellEventArgs e)
		{
			foreach (var pattern in RegexPatterns)
			{
				pattern.ShouldEncrypt = e.IsChecked;
			}
			Settings.EncryptAll = e.IsChecked;
			Settings.RegexPatterns = RegexPatterns;
		}

		private void ExportHeaderCell_OnCheckBoxHeaderClick(CheckBoxHeaderCellEventArgs e)
		{
			foreach (var pattern in RegexPatterns)
			{
				pattern.ShouldEnable = e.IsChecked;
			}
			Settings.EnableAll = e.IsChecked;
			Settings.RegexPatterns = RegexPatterns;
		}

		private void ReadExistingExpressions()
		{
			if (Settings.DefaultListAlreadyAdded || Settings.RegexPatterns.Count > 0)
				return;

			Settings.RegexPatterns.Clear();
			var patterns = Constants.GetDefaultRegexPatterns();
			foreach (var pattern in patterns)
			{
				Settings.AddPattern(pattern);
			}
			Settings.DefaultListAlreadyAdded = true;
		}

		private void SetSettings(AnonymizerSettings settings)
		{
			Settings = settings;
			RegexPatterns = Settings.RegexPatterns;
			var key = Settings.GetSetting<string>(nameof(Settings.EncryptionKey)).Value;
			key = key == "<dummy-encryption-key>" ? "" : key;
			if (!string.IsNullOrEmpty(key))
			{
				encryptionBox.Text = AnonymizeData.DecryptData(key, Constants.Key);
			}

			expressionsGrid.DataSource = RegexPatterns;
		}

		private void selectAll_CheckedChanged(object sender, EventArgs e)
		{
			var shouldSelect = ((CheckBox)sender).Checked;
			foreach (var pattern in RegexPatterns)
			{
				pattern.ShouldEnable = shouldSelect;
				pattern.ShouldEncrypt = shouldSelect;
			}
			Settings.RegexPatterns = RegexPatterns;
			Settings.EnableAll = shouldSelect;
			Settings.EncryptAll = shouldSelect;
		}

		private void exportBtn_Click(object sender, EventArgs e)
		{
			if (expressionsGrid.SelectedRows.Count > 0)
			{
				var fileDialog = new SaveFileDialog
				{
					Title = @"Export selected expressions",
					Filter = @"Excel |*.xlsx"
				};
				var result = fileDialog.ShowDialog();
				if (result == DialogResult.OK && fileDialog.FileName != string.Empty)
				{
					var selectedExpressions = new List<RegexPattern>();
					foreach (DataGridViewRow row in expressionsGrid.SelectedRows)
					{
						var regexPattern = row.DataBoundItem as RegexPattern;
						selectedExpressions.Add(regexPattern);
					}
					Expressions.ExportExporessions(fileDialog.FileName, selectedExpressions);
					MessageBox.Show(@"File was exported successfully to selected location", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			else
			{
				MessageBox.Show(@"Please select at least one row to export", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private void importBtn_Click(object sender, EventArgs e)
		{
			var fileDialog = new OpenFileDialog
			{
				Title = @"Please select the files you want to import",
				Filter = @"Excel |*.xlsx",
				CheckFileExists = true,
				CheckPathExists = true,
				DefaultExt = "xlsx",
				Multiselect = true
			};
			var result = fileDialog.ShowDialog();
			if (result == DialogResult.OK && fileDialog.FileNames.Length > 0)
			{
				var importedExpressions = Expressions.GetImportedExpressions(fileDialog.FileNames.ToList());
				ImportExpressionsInSettings(importedExpressions);
			}
		}

		private void ImportExpressionsInSettings(List<RegexPattern> expressions)
		{
			foreach (var expression in expressions)
			{
				var existScript = RegexPatterns.FirstOrDefault(s => s.Pattern.Equals(expression.Pattern));
				//add script to list
				if (existScript == null)
				{
					expression.ShouldEncrypt = true;
					expression.ShouldEnable = true;
					RegexPatterns.Add(expression);
				}
			}
			Settings.RegexPatterns = RegexPatterns;
		}

		private void expressionsGrid_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode.Equals(Keys.Delete))
			{
				var result = MessageBox.Show(@"Are you sure you want to delete the expressions?", @"Please confirm",
					MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

				if (result == DialogResult.OK)
				{
					RegexPatterns.AllowRemove = true;
					foreach (DataGridViewRow row in expressionsGrid.SelectedRows)
					{
						var regexPattern = row.DataBoundItem as RegexPattern;
						if (regexPattern != null)
						{
							var itemToRemove = RegexPatterns.FirstOrDefault(p => p.Id.Equals(regexPattern.Id));
							RegexPatterns.Remove(itemToRemove);
						}
					}
				}
				Settings.RegexPatterns = RegexPatterns;
			}
		}

		private bool IsProjectAnonymized()
		{
			return Settings.EncryptionKey != "<dummy-encryption-key>";
		}
	}
}