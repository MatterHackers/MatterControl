namespace MatterHackers.MatterControl
{
	partial class InspectForm
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
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.debugTextWidget = new System.Windows.Forms.CheckBox();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.aggTreeView = new System.Windows.Forms.TreeView();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.sceneTreeView = new System.Windows.Forms.TreeView();
			this.tabPage3 = new System.Windows.Forms.TabPage();
			this.btnApply = new System.Windows.Forms.Button();
			this.themeTreeView = new System.Windows.Forms.TreeView();
			this.btnSaveTheme = new System.Windows.Forms.Button();
			this.tabPage4 = new System.Windows.Forms.TabPage();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.pipelineTree = new System.Windows.Forms.TreeView();
			this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
			this.debugMenus = new System.Windows.Forms.CheckBox();
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.tabPage3.SuspendLayout();
			this.tabPage4.SuspendLayout();
			this.SuspendLayout();
			// 
			// splitContainer1
			// 
			this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.splitContainer1.Location = new System.Drawing.Point(0, 0);
			this.splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			this.splitContainer1.Panel1.Controls.Add(this.debugMenus);
			this.splitContainer1.Panel1.Controls.Add(this.debugTextWidget);
			this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
			// 
			// splitContainer1.Panel2
			// 
			this.splitContainer1.Panel2.Controls.Add(this.propertyGrid1);
			this.splitContainer1.Size = new System.Drawing.Size(1370, 972);
			this.splitContainer1.SplitterDistance = 912;
			this.splitContainer1.TabIndex = 0;
			// 
			// debugTextWidget
			// 
			this.debugTextWidget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.debugTextWidget.AutoSize = true;
			this.debugTextWidget.Location = new System.Drawing.Point(738, 5);
			this.debugTextWidget.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.debugTextWidget.Name = "debugTextWidget";
			this.debugTextWidget.Size = new System.Drawing.Size(167, 24);
			this.debugTextWidget.TabIndex = 2;
			this.debugTextWidget.Text = "Debug TextWidget";
			this.debugTextWidget.UseVisualStyleBackColor = true;
			this.debugTextWidget.CheckedChanged += new System.EventHandler(this.debugTextWidget_CheckedChanged);
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Controls.Add(this.tabPage3);
			this.tabControl1.Controls.Add(this.tabPage4);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl1.Location = new System.Drawing.Point(0, 0);
			this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(912, 972);
			this.tabControl1.TabIndex = 1;
			this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this.aggTreeView);
			this.tabPage1.Location = new System.Drawing.Point(4, 29);
			this.tabPage1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage1.Size = new System.Drawing.Size(904, 939);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "SystemWindow";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// aggTreeView
			// 
			this.aggTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.aggTreeView.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
			this.aggTreeView.FullRowSelect = true;
			this.aggTreeView.HideSelection = false;
			this.aggTreeView.Location = new System.Drawing.Point(4, 5);
			this.aggTreeView.Name = "aggTreeView";
			this.aggTreeView.Size = new System.Drawing.Size(896, 929);
			this.aggTreeView.TabIndex = 1;
			this.aggTreeView.DrawNode += new System.Windows.Forms.DrawTreeNodeEventHandler(this.AggTreeView_DrawNode);
			this.aggTreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.AggTreeView_AfterSelect);
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.sceneTreeView);
			this.tabPage2.Location = new System.Drawing.Point(4, 29);
			this.tabPage2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage2.Size = new System.Drawing.Size(904, 939);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Scene";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// sceneTreeView
			// 
			this.sceneTreeView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.sceneTreeView.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
			this.sceneTreeView.FullRowSelect = true;
			this.sceneTreeView.HideSelection = false;
			this.sceneTreeView.Location = new System.Drawing.Point(4, 5);
			this.sceneTreeView.Name = "sceneTreeView";
			this.sceneTreeView.Size = new System.Drawing.Size(896, 929);
			this.sceneTreeView.TabIndex = 2;
			this.sceneTreeView.DrawNode += new System.Windows.Forms.DrawTreeNodeEventHandler(this.SceneTreeView_DrawNode);
			this.sceneTreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.SceneTreeView_AfterSelect);
			// 
			// tabPage3
			// 
			this.tabPage3.Controls.Add(this.btnApply);
			this.tabPage3.Controls.Add(this.themeTreeView);
			this.tabPage3.Controls.Add(this.btnSaveTheme);
			this.tabPage3.Location = new System.Drawing.Point(4, 29);
			this.tabPage3.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage3.Name = "tabPage3";
			this.tabPage3.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage3.Size = new System.Drawing.Size(904, 939);
			this.tabPage3.TabIndex = 2;
			this.tabPage3.Text = "Theme";
			this.tabPage3.UseVisualStyleBackColor = true;
			// 
			// btnApply
			// 
			this.btnApply.Location = new System.Drawing.Point(657, 495);
			this.btnApply.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.btnApply.Name = "btnApply";
			this.btnApply.Size = new System.Drawing.Size(112, 35);
			this.btnApply.TabIndex = 3;
			this.btnApply.Text = "Apply";
			this.btnApply.UseVisualStyleBackColor = true;
			this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
			// 
			// themeTreeView
			// 
			this.themeTreeView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.themeTreeView.FullRowSelect = true;
			this.themeTreeView.HideSelection = false;
			this.themeTreeView.Location = new System.Drawing.Point(4, 5);
			this.themeTreeView.Name = "themeTreeView";
			this.themeTreeView.Size = new System.Drawing.Size(886, 387);
			this.themeTreeView.TabIndex = 2;
			// 
			// btnSaveTheme
			// 
			this.btnSaveTheme.Location = new System.Drawing.Point(778, 495);
			this.btnSaveTheme.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.btnSaveTheme.Name = "btnSaveTheme";
			this.btnSaveTheme.Size = new System.Drawing.Size(112, 35);
			this.btnSaveTheme.TabIndex = 0;
			this.btnSaveTheme.Text = "Save Theme";
			this.btnSaveTheme.UseVisualStyleBackColor = true;
			// 
			// tabPage4
			// 
			this.tabPage4.Controls.Add(this.textBox1);
			this.tabPage4.Controls.Add(this.button1);
			this.tabPage4.Controls.Add(this.pipelineTree);
			this.tabPage4.Location = new System.Drawing.Point(4, 29);
			this.tabPage4.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage4.Name = "tabPage4";
			this.tabPage4.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.tabPage4.Size = new System.Drawing.Size(904, 939);
			this.tabPage4.TabIndex = 3;
			this.tabPage4.Text = "tabPage4";
			this.tabPage4.UseVisualStyleBackColor = true;
			// 
			// textBox1
			// 
			this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBox1.Location = new System.Drawing.Point(26, 449);
			this.textBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(848, 406);
			this.textBox1.TabIndex = 5;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(764, 405);
			this.button1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(112, 35);
			this.button1.TabIndex = 4;
			this.button1.Text = "button1";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// pipelineTree
			// 
			this.pipelineTree.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pipelineTree.FullRowSelect = true;
			this.pipelineTree.HideSelection = false;
			this.pipelineTree.Location = new System.Drawing.Point(3, 8);
			this.pipelineTree.Name = "pipelineTree";
			this.pipelineTree.Size = new System.Drawing.Size(886, 387);
			this.pipelineTree.TabIndex = 3;
			// 
			// propertyGrid1
			// 
			this.propertyGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.propertyGrid1.LineColor = System.Drawing.SystemColors.ControlDark;
			this.propertyGrid1.Location = new System.Drawing.Point(0, 0);
			this.propertyGrid1.Name = "propertyGrid1";
			this.propertyGrid1.Size = new System.Drawing.Size(454, 972);
			this.propertyGrid1.TabIndex = 0;
			this.propertyGrid1.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid1_PropertyValueChanged);
			// 
			// debugMenus
			// 
			this.debugMenus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.debugMenus.AutoSize = true;
			this.debugMenus.Location = new System.Drawing.Point(585, 5);
			this.debugMenus.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
			this.debugMenus.Name = "debugMenus";
			this.debugMenus.Size = new System.Drawing.Size(135, 24);
			this.debugMenus.TabIndex = 3;
			this.debugMenus.Text = "Debug Menus";
			this.debugMenus.UseVisualStyleBackColor = true;
			this.debugMenus.CheckedChanged += new System.EventHandler(this.DebugMenus_CheckedChanged);
			// 
			// InspectForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1370, 972);
			this.Controls.Add(this.splitContainer1);
			this.Name = "InspectForm";
			this.Text = "InspectForm";
			this.Load += new System.EventHandler(this.InspectForm_Load);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel1.PerformLayout();
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
			this.splitContainer1.ResumeLayout(false);
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.tabPage2.ResumeLayout(false);
			this.tabPage3.ResumeLayout(false);
			this.tabPage4.ResumeLayout(false);
			this.tabPage4.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.PropertyGrid propertyGrid1;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.TreeView aggTreeView;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.TreeView sceneTreeView;
		private System.Windows.Forms.CheckBox debugTextWidget;
		private System.Windows.Forms.TabPage tabPage3;
		private System.Windows.Forms.Button btnSaveTheme;
		private System.Windows.Forms.TreeView themeTreeView;
		private System.Windows.Forms.Button btnApply;
		private System.Windows.Forms.TabPage tabPage4;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.TreeView pipelineTree;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.CheckBox debugMenus;
	}
}
