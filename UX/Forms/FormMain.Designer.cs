using System;
using System.Windows.Forms;

namespace Bees.UX.Forms.MainUI;

partial class FormMain
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
            this.components = new System.ComponentModel.Container();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panelBeePlayGround = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // panelBeePlayGround
            // 
            this.panelBeePlayGround.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBeePlayGround.Location = new System.Drawing.Point(0, 0);
            this.panelBeePlayGround.Name = "panelBeePlayGround";
            this.panelBeePlayGround.Size = new System.Drawing.Size(900, 561);
            this.panelBeePlayGround.TabIndex = 0;
            this.panelBeePlayGround.Paint += new System.Windows.Forms.PaintEventHandler(this.PanelBeePlayGround_Paint);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.CausesValidation = false;
            this.ClientSize = new System.Drawing.Size(900, 561);
            this.Controls.Add(this.panelBeePlayGround);
            this.Font = new System.Drawing.Font("Segoe UI Variable Text", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(6);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Bees and flowers";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.ResumeLayout(false);

    }

    #endregion
    private ToolTip toolTip1;
    private Panel panelBeePlayGround;
}