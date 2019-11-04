namespace CacheTraceDecoder
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.cachePathTextBox = new System.Windows.Forms.TextBox();
			this.browseButton = new System.Windows.Forms.Button();
			this.inputTextBox = new System.Windows.Forms.TextBox();
			this.inputLabel = new System.Windows.Forms.Label();
			this.outputLabel = new System.Windows.Forms.Label();
			this.outputTextBox = new System.Windows.Forms.TextBox();
			this.cachePathLabel = new System.Windows.Forms.Label();
			this.cachePathDialog = new System.Windows.Forms.OpenFileDialog();
			this.SuspendLayout();
			// 
			// cachePathTextBox
			// 
			this.cachePathTextBox.Location = new System.Drawing.Point(12, 25);
			this.cachePathTextBox.Name = "cachePathTextBox";
			this.cachePathTextBox.Size = new System.Drawing.Size(654, 20);
			this.cachePathTextBox.TabIndex = 0;
			this.cachePathTextBox.TextChanged += new System.EventHandler(this.cachePathTextBox_TextChanged);
			// 
			// browseButton
			// 
			this.browseButton.Location = new System.Drawing.Point(672, 24);
			this.browseButton.Name = "browseButton";
			this.browseButton.Size = new System.Drawing.Size(116, 22);
			this.browseButton.TabIndex = 1;
			this.browseButton.Text = "Browse";
			this.browseButton.UseVisualStyleBackColor = true;
			this.browseButton.Click += new System.EventHandler(this.browseButton_Click);
			// 
			// inputTextBox
			// 
			this.inputTextBox.Location = new System.Drawing.Point(12, 77);
			this.inputTextBox.MaxLength = 999999999;
			this.inputTextBox.Multiline = true;
			this.inputTextBox.Name = "inputTextBox";
			this.inputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.inputTextBox.Size = new System.Drawing.Size(776, 333);
			this.inputTextBox.TabIndex = 2;
			this.inputTextBox.TextChanged += new System.EventHandler(this.inputTextBox_TextChanged);
			// 
			// inputLabel
			// 
			this.inputLabel.AutoSize = true;
			this.inputLabel.Location = new System.Drawing.Point(12, 61);
			this.inputLabel.Name = "inputLabel";
			this.inputLabel.Size = new System.Drawing.Size(34, 13);
			this.inputLabel.TabIndex = 3;
			this.inputLabel.Text = "Input:";
			// 
			// outputLabel
			// 
			this.outputLabel.AutoSize = true;
			this.outputLabel.Location = new System.Drawing.Point(12, 413);
			this.outputLabel.Name = "outputLabel";
			this.outputLabel.Size = new System.Drawing.Size(42, 13);
			this.outputLabel.TabIndex = 5;
			this.outputLabel.Text = "Output:";
			// 
			// outputTextBox
			// 
			this.outputTextBox.Location = new System.Drawing.Point(12, 429);
			this.outputTextBox.MaxLength = 999999999;
			this.outputTextBox.Multiline = true;
			this.outputTextBox.Name = "outputTextBox";
			this.outputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.outputTextBox.Size = new System.Drawing.Size(776, 333);
			this.outputTextBox.TabIndex = 4;
			// 
			// cachePathLabel
			// 
			this.cachePathLabel.AutoSize = true;
			this.cachePathLabel.Location = new System.Drawing.Point(12, 9);
			this.cachePathLabel.Name = "cachePathLabel";
			this.cachePathLabel.Size = new System.Drawing.Size(93, 13);
			this.cachePathLabel.TabIndex = 6;
			this.cachePathLabel.Text = "Path to cache file:";
			// 
			// cachePathDialog
			// 
			this.cachePathDialog.Filter = "osu!patch dictionary (*.dic)|*.dic|All files (*.*)|*.*";
			this.cachePathDialog.Title = "Select osu!patch *.dic dictionary file";
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 772);
			this.Controls.Add(this.cachePathLabel);
			this.Controls.Add(this.outputLabel);
			this.Controls.Add(this.outputTextBox);
			this.Controls.Add(this.inputLabel);
			this.Controls.Add(this.inputTextBox);
			this.Controls.Add(this.browseButton);
			this.Controls.Add(this.cachePathTextBox);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "MainForm";
			this.Text = "CacheTraceDecoder";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TextBox cachePathTextBox;
		private System.Windows.Forms.Button browseButton;
		private System.Windows.Forms.TextBox inputTextBox;
		private System.Windows.Forms.Label inputLabel;
		private System.Windows.Forms.Label outputLabel;
		private System.Windows.Forms.TextBox outputTextBox;
		private System.Windows.Forms.Label cachePathLabel;
		private System.Windows.Forms.OpenFileDialog cachePathDialog;
	}
}